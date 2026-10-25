using System.Diagnostics;
using System.Globalization;
using ChessInfrastructure.Models.Analysis;

namespace ChessTrainer;

/// <summary>Последовательно обменивается UCI-командами с одним процессом Stockfish</summary>
public sealed class StockfishEngine : IAsyncDisposable
{
    /// <summary>Параметры запуска с перенаправленными stdin и stdout</summary>
    private readonly ProcessStartInfo _processStartInfo;

    /// <summary>Число вариантов, вычисляемых движком</summary>
    private readonly int _countOfBestMoves;

    /// <summary>Предельная длительность операции после получения блокировки</summary>
    private readonly TimeSpan _commandTimeout;

    /// <summary>Блокировка обмена, исключающая чтение ответов чужой команды</summary>
    private readonly SemaphoreSlim _commandLock = new(1, 1);

    /// <summary>Процесс движка или null до запуска и после остановки</summary>
    private Process? _process;

    /// <summary>Признак окончательного освобождения экземпляра</summary>
    private bool _disposed;

    /// <summary>Проверяет настройки и сохраняет их снимок</summary>
    /// <param name="options">Путь к движку, число вариантов и таймаут операций</param>
    public StockfishEngine(StockfishOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.PathToStockFish);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.CountOfBestMoves, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.CountOfBestMoves, 500);
        if (options.CommandTimeout <= TimeSpan.Zero ||
            options.CommandTimeout.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options.CommandTimeout));
        }

        _processStartInfo = new ProcessStartInfo(options.PathToStockFish)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardInput = true
        };
        _countOfBestMoves = options.CountOfBestMoves;
        _commandTimeout = options.CommandTimeout;
    }

    /// <summary>Запускает процесс и ожидает завершения UCI-инициализации</summary>
    /// <param name="cancellationToken">Токен отмены запуска и инициализации</param>
    public async Task StartEngineAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async token =>
        {
            _process = Process.Start(_processStartInfo)
                ?? throw new InvalidOperationException("Процесс с движком не удалось запустить");
            await InitializeAsync(token);
            return true;
        }, cancellationToken, starting: true);
    }

    /// <summary>Согласует UCI-протокол и применяет начальные настройки</summary>
    /// <param name="cancellationToken">Токен отмены обмена с движком</param>
    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await ExecuteCoreAsync(new UciOkCommand(), cancellationToken);
        await SendAsync($"setoption name {StockfishOptions.MultiPV} value " +
            _countOfBestMoves.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await ExecuteCoreAsync(new IsReadyCommand(), cancellationToken);
    }

    /// <summary>Сообщает о новой партии, очищает данные предыдущего поиска и ожидает готовности движка</summary>
    /// <param name="cancellationToken">Токен отмены переключения партии</param>
    /// <returns>Задача завершения переключения партии</returns>
    public async Task NewGameAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async token =>
        {
            await SendAsync("ucinewgame", token);
            return await ExecuteCoreAsync(new IsReadyCommand(), token);
        }, cancellationToken);
    }

    /// <summary>Устанавливает UCI-опцию и ожидает готовности движка</summary>
    /// <param name="optionCommand">Имя опции и её значение без переводов строки</param>
    /// <param name="cancellationToken">Токен отмены изменения опции</param>
    public async Task SetOptionAsync(OptionCommand optionCommand, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(optionCommand);
        ValidateLine(optionCommand.Name, nameof(optionCommand.Name));
        ValidateLine(optionCommand.Value, nameof(optionCommand.Value));
        await RunAsync(async token =>
        {
            await SendAsync($"setoption name {optionCommand.Name} value {optionCommand.Value}", token);
            return await ExecuteCoreAsync(new IsReadyCommand(), token);
        }, cancellationToken);
    }

    /// <summary>Передаёт полную UCI-команду position без проверки шахматной корректности</summary>
    /// <param name="position">Команда вида position startpos moves e2e4 e7e5 или position fen с FEN</param>
    /// <param name="cancellationToken">Токен отмены установки позиции</param>
    public async Task SetPositionAsync(string position, CancellationToken cancellationToken = default)
    {
        ValidateLine(position, nameof(position));
        if (position != "position startpos" &&
            !position.StartsWith("position startpos moves ", StringComparison.Ordinal) &&
            !position.StartsWith("position fen ", StringComparison.Ordinal))
        {
            throw new ArgumentException("Ожидается UCI-команда position startpos или position fen", nameof(position));
        }

        await RunAsync(async token =>
        {
            await SendAsync(position, token);
            return await ExecuteCoreAsync(new IsReadyCommand(), token);
        }, cancellationToken);
    }

    /// <summary>Возвращает лучший ход и последнюю оценку основной линии multipv 1</summary>
    /// <param name="depth">Положительная глубина поиска в полуходах</param>
    /// <param name="cancellationToken">Токен отмены поиска, при отмене процесс останавливается</param>
    /// <returns>Результат поиска или null, если в позиции нет допустимых ходов</returns>
    public Task<EngineMoveInfo?> GetBestMoveAsync(int depth, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        return RunAsync<EngineMoveInfo?>(async token =>
        {
            // Полный ответ поиска до завершающей строки bestmove включительно
            var response = await ExecuteCoreAsync(new GoCommand(depth), token);
            // Последняя оценка основной линии, а не последнего варианта MultiPV
            EngineMoveInfo? latestInfo = null;
            // Каждая строка разбирается независимо существующим парсером
            foreach (var line in response.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("bestmove ", StringComparison.Ordinal))
                {
                    // Второе слово содержит фактический ход, далее может идти ponder
                    var bestMove = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
                    if (bestMove is "(none)" or "0000")
                        return null;

                    return (latestInfo ?? new EngineMoveInfo()) with { BestMove = bestMove };
                }

                // Структурированные поля одной строки info
                var info = EngineInfoMapper.Parse(line);
                if (info is not null && (info.MultiPv is null or 1) &&
                    (info.ScoreCp.HasValue || info.MateIn.HasValue))
                {
                    latestInfo = info;
                }
            }

            throw new InvalidOperationException("Движок не вернул лучший ход");
        }, cancellationToken);
    }

    /// <summary>Выполняет одну UCI-команду и читает ответ до ожидаемого маркера</summary>
    /// <param name="command">Команда с непустым маркером завершения ответа</param>
    /// <param name="cancellationToken">Токен отмены обмена, при отмене процесс останавливается</param>
    /// <returns>Все строки ответа, включая маркер завершения</returns>
    public Task<string> ExecuteCommandAsync(Command command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateLine(command.ExecuteCommand, nameof(command.ExecuteCommand));
        ValidateLine(command.ExpectedResult, nameof(command.ExpectedResult));
        return RunAsync(token => ExecuteCoreAsync(command, token), cancellationToken);
    }

    /// <summary>Пишет команду и принудительно отправляет её в stdin движка</summary>
    /// <param name="command">Одна строка UCI-команды</param>
    /// <param name="cancellationToken">Токен отмены записи</param>
    private async Task SendAsync(string command, CancellationToken cancellationToken)
    {
        if (_process is null || _process.HasExited)
            throw new InvalidOperationException("Движок не запущен или уже завершился");

        await _process.StandardInput.WriteLineAsync(command.AsMemory(), cancellationToken);
        await _process.StandardInput.FlushAsync(cancellationToken);
    }

    /// <summary>Читает ответ команды под уже полученной блокировкой</summary>
    /// <param name="command">Команда и маркер завершения ответа</param>
    /// <param name="cancellationToken">Токен отмены записи и чтения</param>
    /// <returns>Строки ответа, разделённые символом перевода строки</returns>
    private async Task<string> ExecuteCoreAsync(Command command, CancellationToken cancellationToken)
    {
        await SendAsync(command.ExecuteCommand, cancellationToken);
        // Накопленные строки ответа текущей команды
        var lines = new List<string>();
        while (true)
        {
            // Следующая строка stdout, null означает закрытие потока движком
            var line = await _process!.StandardOutput.ReadLineAsync(cancellationToken);
            if (line is null)
                throw new InvalidOperationException("Движок закрыл stdout до завершения ответа");

            lines.Add(line);
            if (line == command.ExpectedResult ||
                line.StartsWith(command.ExpectedResult + " ", StringComparison.Ordinal))
            {
                return string.Join('\n', lines);
            }
        }
    }

    /// <summary>Сериализует операцию и закрывает процесс при нарушении обмена</summary>
    /// <typeparam name="T">Тип результата операции</typeparam>
    /// <param name="operation">Операция под блокировкой с общим таймаутом</param>
    /// <param name="cancellationToken">Токен отмены ожидания блокировки и выполнения операции</param>
    /// <param name="starting">Признак операции запуска нового процесса</param>
    /// <returns>Результат операции</returns>
    private async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken,
        bool starting = false)
    {
        await _commandLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (starting && _process is not null)
                throw new InvalidOperationException("Движок уже запущен");
            if (!starting && (_process is null || _process.HasExited))
                throw new InvalidOperationException("Движок не запущен или уже завершился");

            // Таймер ограничивает весь обмен после получения блокировки
            using var timeout = new CancellationTokenSource(_commandTimeout);
            // Объединённый токен отменяется пользователем или таймером
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                return await operation(linked.Token);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                StopProcess();
                throw new TimeoutException($"Движок не завершил операцию за {_commandTimeout}", exception);
            }
            catch
            {
                // После ошибки нельзя использовать поток с недочитанным ответом
                StopProcess();
                throw;
            }
        }
        finally
        {
            _commandLock.Release();
        }
    }

    /// <summary>Проверяет, что значение содержит ровно одну непустую строку</summary>
    /// <param name="value">Проверяемое значение команды или её аргумента</param>
    /// <param name="parameterName">Имя параметра для сообщения об ошибке</param>
    private static void ValidateLine(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Contains('\r') || value.Contains('\n'))
            throw new ArgumentException("Переводы строки в UCI-команде недопустимы", parameterName);
    }

    /// <summary>Принудительно завершает процесс и освобождает его потоки</summary>
    private void StopProcess()
    {
        if (_process is null)
            return;

        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    /// <summary>Отправляет quit и закрывает процесс, если он не вышел за две секунды</summary>
    public async ValueTask DisposeAsync()
    {
        await _commandLock.WaitAsync();
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_process is not null && !_process.HasExited)
            {
                // Отдельный таймаут на штатное завершение процесса
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    await SendAsync("quit", timeout.Token);
                    await _process.WaitForExitAsync(timeout.Token);
                }
                catch (Exception exception) when (exception is OperationCanceledException or IOException or InvalidOperationException)
                {
                    // StopProcess в finally освобождает зависший или завершившийся процесс
                }
            }
        }
        finally
        {
            try
            {
                StopProcess();
            }
            finally
            {
                _commandLock.Release();
            }
        }
    }
}
