namespace Wayni.Deudores.Infrastructure.Persistence.Entities;

/// <summary>Table <c>import_jobs</c>: one row per uploaded file, shared by Importer, Processor and Admin.</summary>
public class ImportJob
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>Queued, Processing, Completed or Failed (see <c>ImportStatus</c>).</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int? Periodo { get; set; }
    public long LineasLeidas { get; set; }
    public long LineasInvalidas { get; set; }
    public int Deudores { get; set; }
    public int Entidades { get; set; }
    public decimal TotalPrestamos { get; set; }
    public long? DuracionMs { get; set; }
    public string? Error { get; set; }
}
