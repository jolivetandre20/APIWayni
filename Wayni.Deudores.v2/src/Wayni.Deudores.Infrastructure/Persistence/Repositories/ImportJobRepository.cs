using Microsoft.EntityFrameworkCore;
using Wayni.Deudores.Core.Messaging;
using Wayni.Deudores.Core.Processing;
using Wayni.Deudores.Infrastructure.Persistence.Entities;

namespace Wayni.Deudores.Infrastructure.Persistence.Repositories;

public interface IImportJobRepository
{
    Task<ImportJob> CreateAsync(Guid id, string fileName, string bucket, string objectKey, CancellationToken ct);
    Task<ImportJob?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ImportJob>> ListAsync(int take, CancellationToken ct);
    Task MarkQueuedAsync(Guid id, CancellationToken ct);
    Task MarkProcessingAsync(Guid id, CancellationToken ct);
    Task MarkCompletedAsync(Guid id, AggregationSummary summary, TimeSpan totalDuration, CancellationToken ct);
    Task MarkFailedAsync(Guid id, string error, TimeSpan totalDuration, CancellationToken ct);
}

public sealed class ImportJobRepository(WayniDbContext db) : IImportJobRepository
{
    public async Task<ImportJob> CreateAsync(Guid id, string fileName, string bucket, string objectKey, CancellationToken ct)
    {
        var job = new ImportJob
        {
            Id = id,
            FileName = Truncate(fileName, 255),
            Bucket = bucket,
            ObjectKey = objectKey,
            Status = ImportStatus.Queued,
            CreatedAt = DateTime.UtcNow,
        };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync(ct);
        return job;
    }

    public Task<ImportJob?> GetAsync(Guid id, CancellationToken ct) =>
        db.ImportJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task<IReadOnlyList<ImportJob>> ListAsync(int take, CancellationToken ct) =>
        await db.ImportJobs.AsNoTracking().OrderByDescending(j => j.CreatedAt).Take(take).ToListAsync(ct);

    public Task MarkQueuedAsync(Guid id, CancellationToken ct) =>
        db.ImportJobs.Where(j => j.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(j => j.Status, ImportStatus.Queued)
            .SetProperty(j => j.StartedAt, (DateTime?)null)
            .SetProperty(j => j.FinishedAt, (DateTime?)null)
            .SetProperty(j => j.Error, (string?)null), ct);

    public Task MarkProcessingAsync(Guid id, CancellationToken ct) =>
        db.ImportJobs.Where(j => j.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(j => j.Status, ImportStatus.Processing)
            .SetProperty(j => j.StartedAt, DateTime.UtcNow)
            .SetProperty(j => j.Error, (string?)null), ct);

    public Task MarkCompletedAsync(Guid id, AggregationSummary summary, TimeSpan totalDuration, CancellationToken ct) =>
        db.ImportJobs.Where(j => j.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(j => j.Status, ImportStatus.Completed)
            .SetProperty(j => j.FinishedAt, DateTime.UtcNow)
            .SetProperty(j => j.Periodo, summary.Periodo)
            .SetProperty(j => j.LineasLeidas, summary.LineasLeidas)
            .SetProperty(j => j.LineasInvalidas, summary.LineasInvalidas)
            .SetProperty(j => j.Deudores, summary.Deudores)
            .SetProperty(j => j.Entidades, summary.Entidades)
            .SetProperty(j => j.TotalPrestamos, summary.TotalPrestamos)
            .SetProperty(j => j.DuracionMs, (long?)totalDuration.TotalMilliseconds), ct);

    public Task MarkFailedAsync(Guid id, string error, TimeSpan totalDuration, CancellationToken ct) =>
        db.ImportJobs.Where(j => j.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(j => j.Status, ImportStatus.Failed)
            .SetProperty(j => j.FinishedAt, DateTime.UtcNow)
            .SetProperty(j => j.DuracionMs, (long?)totalDuration.TotalMilliseconds)
            .SetProperty(j => j.Error, Truncate(error, 2000)), ct);

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
