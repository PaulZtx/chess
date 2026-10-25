using BackChess.Models.Events;
using ChessInfrastructure;
using ChessInfrastructure.Entities;
using ChessInfrastructure.Entities.Enums;
using ChessTrainer;
using Microsoft.EntityFrameworkCore;

namespace BackChess.Services;

/// <summary>
/// Последовательно обрабатывает задания анализа и сохраняет результаты обеих позиций
/// </summary>
/// <param name="logger">Логгер обработки заданий</param>
/// <param name="scopeFactory">Фабрика контекстов обработки заданий</param>
/// <param name="chessBridgeService">Доставка игровых событий активным SSE-подписчикам</param>
public class EngineJobWorker(ILogger<EngineJobWorker> logger, IServiceScopeFactory scopeFactory,
    ChessBridgeService chessBridgeService) : BackgroundService
{
    /// <summary>
    /// Переиспользуемый сервис движка, пересоздаваемый после сбоя анализа
    /// </summary>
    private ChessEngineService _chessEngineService = new(Config.GetSection<StockfishOptions>());

    /// <summary>
    /// Сохраняет результат и завершение каждого задания вместе без промежуточного Running
    /// </summary>
    /// <param name="stoppingToken">Токен остановки приложения</param>
    /// <returns>Задача обработки очереди до остановки приложения</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Отдельный контекст для текущей итерации обработки
                    using var scope = scopeFactory.CreateScope();

                    // Контекст сохраняет каждое задание независимо от остальных
                    var storage = scope.ServiceProvider.GetRequiredService<Storage>();

                    // Задания уже содержат обе позиции, навигации загружать не требуется
                    var pendingJobs = await storage.MoveAnalysisJobs
                        .Where(j => j.Status == StatusJob.Pending)
                        .OrderBy(job => job.CreatedAtUtc)
                        .ToListAsync(stoppingToken);

                    // Идентификаторы позиций, чей готовый анализ можно переиспользовать
                    var parentMoveIds = pendingJobs.Where(job => job.ParentMoveId.HasValue)
                        .Select(job => job.ParentMoveId!.Value).Distinct().ToList();

                    // Проекция получает ключ из задания в SQL без загрузки навигационного свойства
                    var savedAnalyses = await storage.MoveAnalyses.AsNoTracking()
                        .Where(analysis => parentMoveIds.Contains(analysis.MoveAnalysisJob.MoveId))
                        .OrderByDescending(analysis => analysis.MoveAnalysisJob.CreatedAtUtc)
                        .Select(analysis => new { analysis.MoveAnalysisJob.MoveId, analysis.AfterAnalysis })
                        .ToListAsync(stoppingToken);

                    // Повторные разборы одного хода не создают дублирующихся ключей
                    var previousAnalyses = savedAnalyses.GroupBy(analysis => analysis.MoveId)
                        .ToDictionary(group => group.Key, group => group.First().AfterAnalysis);

                    foreach (var job in pendingJobs)
                    {
                        // Готовый анализ родителя, если он уже сохранен
                        var beforeAnalysis = job.ParentMoveId is { } parentMoveId &&
                            previousAnalyses.TryGetValue(parentMoveId, out var previous)
                                ? previous : null;

                        // Результат доступен в catch для отмены его отслеживания после неудачной записи
                        MoveAnalysis? resultAnalysisMove = null;

                        // Завершенный анализ не требует перезапуска движка при последующей ошибке БД
                        var analysisCompleted = false;

                        try
                        {
                            // При отсутствии сохраненного анализа сервис сам рассчитывает позицию до хода
                            var info = await _chessEngineService.AnalyzeMoveAsync(job.GameId,
                                job.FenBefore, job.FenAfter, job.SearchDepth, beforeAnalysis, stoppingToken);
                            analysisCompleted = true;

                            job.Status = StatusJob.Completed;
                            job.ErrorMessage = null;

                            // Оба обязательных анализа берутся из готового результата, а не из кэша
                            resultAnalysisMove = new MoveAnalysis
                            {
                                MoveAnalysisJobId = job.Id,
                                BeforeAnalysis = info.Before,
                                AfterAnalysis = info.After
                            };

                            storage.MoveAnalyses.Add(resultAnalysisMove);
                            await storage.SaveChangesAsync(stoppingToken);

                            // Следующий ход в этой же пачке уже может использовать сохраненную оценку
                            previousAnalyses[job.MoveId] = info.After;
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            // При остановке незавершенный анализ остается Pending для следующего запуска
                            throw;
                        }
                        catch (Exception exception)
                        {
                            logger.LogError(exception, "Ошибка анализа задания {JobId} партии {GameId}",
                                job.Id, job.GameId);

                            // Отсоединяем только неудачный результат и его задание, остальные продолжают отслеживаться
                            if (resultAnalysisMove is not null)
                                storage.Entry(resultAnalysisMove).State = EntityState.Detached;
                            storage.Entry(job).State = EntityState.Detached;

                            // Ошибки сохранения и проверки входных данных не повреждают процесс движка
                            if (!analysisCompleted && exception is not ArgumentException)
                            {
                                await _chessEngineService.DisposeAsync();
                                _chessEngineService = new ChessEngineService(Config.GetSection<StockfishOptions>());
                            }

                            await storage.MoveAnalysisJobs.Where(_ => _.Id == job.Id).ExecuteUpdateAsync(_ =>
                                    _.SetProperty(p => p.Status, StatusJob.Failed)
                                        .SetProperty(p => p.ErrorMessage, exception.Message),
                                cancellationToken: stoppingToken);

                            continue;
                        }

                        // Ошибка доставки не должна менять уже сохраненный Completed на Failed
                        try
                        {
                            chessBridgeService.TryWriteToClient(job.GameId, job.RecipientPlayerId,
                                new MoveAnalyzedEvent
                                {
                                    GameId = job.GameId,
                                    JobId = job.Id,
                                    MoveId = job.MoveId,
                                    AnalysisId = resultAnalysisMove!.Id
                                });
                        }
                        catch (Exception exception)
                        {
                            logger.LogWarning(exception,
                                "Не удалось отправить событие задания {JobId} получателю {PlayerId}",
                                job.Id, job.RecipientPlayerId);
                        }
                    }

                    // TODO: отдельно обработать BotMoveJobs с проверкой позиции и версии перед применением хода

                    await Task.Delay(100, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Ошибка обработки очереди заданий движка");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        finally
        {
            await _chessEngineService.DisposeAsync();
        }
    }
}
