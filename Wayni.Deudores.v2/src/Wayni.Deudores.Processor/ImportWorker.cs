using Wayni.Deudores.Infrastructure.Messaging;

namespace Wayni.Deudores.Processor;

/// <summary>
/// Long-polls SQS and processes one import at a time. While a file is being processed the message
/// visibility is extended every minute, so a 10-minute import is not delivered twice. The message is
/// deleted when the job ends (Completed or Failed); failed jobs can be re-queued with POST /jobs/{id}/reprocess.
/// </summary>
public sealed class ImportWorker(IImportQueue queue, IServiceScopeFactory scopes, ILogger<ImportWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Heartbeat = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Visibility = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Import worker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            ReceivedImport? received;
            try
            {
                received = await queue.ReceiveAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("SQS receive failed: {Message}. Retrying in 5 s", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            if (received is null)
                continue;

            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var heartbeat = KeepInvisibleAsync(received, heartbeatCts.Token);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ImportProcessor>().ProcessAsync(received.Message, stoppingToken);
                await queue.DeleteAsync(received, CancellationToken.None);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Shutting down during job {JobId}; the message will be delivered again", received.Message.JobId);
            }
            finally
            {
                await heartbeatCts.CancelAsync();
                await heartbeat;
            }
        }
    }

    private async Task KeepInvisibleAsync(ReceivedImport received, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(Heartbeat, ct);
                await queue.ExtendVisibilityAsync(received, Visibility, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning("Could not extend visibility of job {JobId}: {Message}", received.Message.JobId, ex.Message);
        }
    }
}
