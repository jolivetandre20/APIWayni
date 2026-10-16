using Wayni.Deudores.Core.Messaging;
using Wayni.Deudores.Infrastructure.Files;
using Wayni.Deudores.Infrastructure.Messaging;
using Wayni.Deudores.Infrastructure.Persistence.Entities;
using Wayni.Deudores.Infrastructure.Persistence.Repositories;
using Wayni.Deudores.Infrastructure.Storage;

namespace Wayni.Deudores.Importer.Api;

/// <summary>Store original in S3 -> create the job row -> queue the SQS message.</summary>
public sealed class ImportService(IFileStorage storage, IImportQueue queue, IImportJobRepository jobs, ILogger<ImportService> logger)
{
    public async Task<ImportJob> ImportStreamAsync(string fileName, Stream content, CancellationToken ct)
    {
        var (jobId, key) = NewKey(fileName);
        await storage.UploadAsync(key, content, ct);
        return await QueueAsync(jobId, fileName, key, ct);
    }

    public async Task<ImportJob> ImportLocalFileAsync(string fullPath, CancellationToken ct)
    {
        var fileName = Path.GetFileName(fullPath);
        var (jobId, key) = NewKey(fileName);
        await storage.UploadFileAsync(key, fullPath, ct);
        return await QueueAsync(jobId, fileName, key, ct);
    }

    /// <summary>Queues an existing job again (same S3 object), e.g. after a failure.</summary>
    public async Task<ImportJob?> ReprocessAsync(Guid jobId, CancellationToken ct)
    {
        var job = await jobs.GetAsync(jobId, ct);
        if (job is null)
            return null;

        await jobs.MarkQueuedAsync(jobId, ct);
        await queue.EnqueueAsync(new ImportRequestedMessage(job.Id, job.Bucket, job.ObjectKey, job.FileName, DateTimeOffset.UtcNow), ct);
        return await jobs.GetAsync(jobId, ct);
    }

    public static string? Validate(string? fileName) => fileName switch
    {
        null or "" => "A file name is required.",
        _ when !DeudoresFileOpener.IsSupported(fileName) => "Only .txt, .zip or .7z files are accepted.",
        _ => null,
    };

    private async Task<ImportJob> QueueAsync(Guid jobId, string fileName, string key, CancellationToken ct)
    {
        var job = await jobs.CreateAsync(jobId, fileName, storage.Bucket, key, ct);
        await queue.EnqueueAsync(new ImportRequestedMessage(jobId, storage.Bucket, key, fileName, DateTimeOffset.UtcNow), ct);
        logger.LogInformation("Import {JobId} accepted for {FileName}", jobId, fileName);
        return job;
    }

    private static (Guid JobId, string Key) NewKey(string fileName)
    {
        var jobId = Guid.NewGuid();
        var safeName = string.Concat(Path.GetFileName(fileName).Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));
        return (jobId, $"uploads/{DateTime.UtcNow:yyyy/MM/dd}/{jobId}/{safeName}");
    }
}
