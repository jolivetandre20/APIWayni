using System.Diagnostics;
using Wayni.Deudores.Core.Messaging;
using Wayni.Deudores.Core.Processing;
using Wayni.Deudores.Infrastructure.Files;
using Wayni.Deudores.Infrastructure.Messaging;
using Wayni.Deudores.Infrastructure.Notifications;
using Wayni.Deudores.Infrastructure.Persistence.Repositories;

namespace Wayni.Deudores.Processor;

/// <summary>Section "Processing".</summary>
public sealed class ProcessingOptions
{
    /// <summary>Pre-sizes the debtor dictionary (Nov 2023: 19.9 million) so it never re-hashes; 0 = grow on demand.</summary>
    public int ExpectedDeudores { get; set; } = 25_000_000;
}

/// <summary>One import, end to end: S3 -> parse/aggregate -> MySQL upsert -> job status -> notification.</summary>
public sealed class ImportProcessor(
    DeudoresFileOpener opener,
    IImportWriter writer,
    IImportJobRepository jobs,
    IImportNotifier notifier,
    Microsoft.Extensions.Options.IOptions<ProcessingOptions> options,
    ILogger<ImportProcessor> logger)
{
    public async Task ProcessAsync(ImportRequestedMessage message, CancellationToken ct)
    {
        var job = await jobs.GetAsync(message.JobId, ct);
        if (job is null)
        {
            logger.LogWarning("Job {JobId} does not exist; message ignored", message.JobId);
            return;
        }
        if (job.Status == ImportStatus.Completed)
        {
            logger.LogInformation("Job {JobId} already completed; duplicate message ignored", message.JobId);
            return;
        }

        var total = Stopwatch.StartNew();
        await jobs.MarkProcessingAsync(message.JobId, ct);
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["JobId"] = message.JobId });
        logger.LogInformation("Processing {FileName} from s3://{Bucket}/{Key}", message.FileName, message.Bucket, message.Key);

        try
        {
            var aggregator = new DeudoresAggregator(options.Value.ExpectedDeudores);
            AggregationSummary summary;
            await using (var stream = await opener.OpenAsync(message.Key, message.FileName, ct))
            {
                summary = await aggregator.ReadAllAsync(stream,
                    new Progress<long>(lines => logger.LogInformation("Read {Lines} lines", lines)), ct);
            }
            if (summary.LineasValidas == 0)
                throw new InvalidDataException(
                    $"No valid deudores.txt records found in '{message.FileName}' ({summary.LineasLeidas} lines read).");

            logger.LogInformation(
                "Parsed {Lineas} lines ({Invalidas} invalid) into {Deudores} deudores and {Entidades} entidades in {ParseMs} ms",
                summary.LineasLeidas, summary.LineasInvalidas, summary.Deudores, summary.Entidades, (long)summary.Duracion.TotalMilliseconds);
            foreach (var invalid in aggregator.InvalidSamples)
                logger.LogWarning("Invalid line {LineNumber}: {Error} [{Excerpt}]", invalid.LineNumber, invalid.Error, invalid.Excerpt);

            var write = Stopwatch.StartNew();
            var (deudores, entidades) = await writer.WriteAsync(aggregator, ct);
            logger.LogInformation("Upserted {Deudores} deudores and {Entidades} entidades in {WriteMs} ms",
                deudores, entidades, write.ElapsedMilliseconds);

            await jobs.MarkCompletedAsync(message.JobId, summary, total.Elapsed, ct);
            await notifier.NotifyAsync(new ImportCompletedNotification(
                message.JobId, message.FileName, ImportStatus.Completed, summary.Periodo, summary.LineasLeidas,
                summary.LineasInvalidas, summary.Deudores, summary.Entidades, summary.TotalPrestamos,
                total.ElapsedMilliseconds, DateTimeOffset.UtcNow, null), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Import {JobId} failed", message.JobId);
            await jobs.MarkFailedAsync(message.JobId, ex.Message, total.Elapsed, CancellationToken.None);
            await notifier.NotifyAsync(new ImportCompletedNotification(
                message.JobId, message.FileName, ImportStatus.Failed, null, 0, 0, 0, 0, 0m,
                total.ElapsedMilliseconds, DateTimeOffset.UtcNow, ex.Message), CancellationToken.None);
        }
    }
}
