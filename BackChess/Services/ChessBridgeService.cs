using System.Collections.Concurrent;
using System.Threading.Channels;
using BackChess.Models.Events;

namespace BackChess.Services;

/// <summary>
/// Доставляет игровые события в отдельные каналы активных SSE-подписок
/// </summary>
public class ChessBridgeService
{
    /// <summary>
    /// Согласует регистрацию и удаление подключений с очисткой пустых групп
    /// </summary>
    private readonly object _subscriptionsLock = new();

    /// <summary>
    /// Каналы подключений, сгруппированные по партии и игроку
    /// Внутренний ключ — connectionId конкретной SSE-подписки
    /// </summary>
    private readonly ConcurrentDictionary<
        (Guid gameId, Guid playerId),
        ConcurrentDictionary<Guid, Channel<GameEvent>>> _channels = new();
    
    /// <summary>
    /// Создает отдельный канал подключения или возвращает уже зарегистрированный
    /// </summary>
    /// <param name="gameId">Идентификатор доступной игроку партии</param>
    /// <param name="playerId">Идентификатор авторизованного получателя</param>
    /// <param name="connectionId">Идентификатор конкретной SSE-подписки</param>
    /// <returns>Читатель канала игровых событий данного подключения</returns>
    public ChannelReader<GameEvent> RegisterClient(Guid gameId, Guid playerId, Guid connectionId)
    {
        lock (_subscriptionsLock)
        {
            // Одна группа получателя может содержать несколько независимых вкладок
            var innerDict = _channels.GetOrAdd((gameId, playerId),
                _ => new ConcurrentDictionary<Guid, Channel<GameEvent>>());
            var channel = innerDict.GetOrAdd(connectionId, _ =>
                Channel.CreateUnbounded<GameEvent>(new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                }));

            return channel.Reader;
        }
    }

    /// <summary>
    /// Пытается отправить событие во все активные подключения получателя данной партии
    /// </summary>
    /// <param name="gameId">Идентификатор партии</param>
    /// <param name="playerId">Идентификатор игрока</param>
    /// <param name="message">Уведомление клиенту</param>
    /// <returns>True, если событие записано хотя бы в один канал, иначе false</returns>
    public bool TryWriteToClient(Guid gameId, Guid playerId, GameEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.GameId != gameId)
            throw new ArgumentException("Партия события должна совпадать с партией получателя", nameof(message));

        if (!_channels.TryGetValue((gameId, playerId), out var innerDict))
        {
            return false;
        }

        // TryWrite не выбрасывает исключение при конкурентном закрытии канала
        var delivered = false;
        foreach (var channel in innerDict.Values)
        {
            delivered |= channel.Writer.TryWrite(message);
        }

        return delivered;
    }

    /// <summary>
    /// Закрывает конкретное подключение, сохраняя остальные вкладки получателя
    /// </summary>
    /// <param name="gameId">Идентификатор партии</param>
    /// <param name="playerId">Идентификатор получателя</param>
    /// <param name="connectionId">Идентификатор отключившейся SSE-подписки</param>
    public void UnregisterClient(Guid gameId, Guid playerId, Guid connectionId)
    {
        lock (_subscriptionsLock)
        {
            if (!_channels.TryGetValue((gameId, playerId), out var innerDict))
                return;

            if (innerDict.TryRemove(connectionId, out var channel))
                channel.Writer.TryComplete();

            if (innerDict.IsEmpty)
                _channels.TryRemove((gameId, playerId), out _);
        }
    }
    
    /// <summary>
    /// Закрывает все подключения получателя к данной партии
    /// </summary>
    /// <param name="gameId">Идентификатор партии</param>
    /// <param name="playerId">Идентификатор получателя</param>
    public void UnregisterClient(Guid gameId, Guid playerId)
    {
        lock (_subscriptionsLock)
        {
            if (!_channels.TryRemove((gameId, playerId), out var innerDict))
                return;

            foreach (var channel in innerDict.Values)
                channel.Writer.TryComplete();
        }
    }
}
