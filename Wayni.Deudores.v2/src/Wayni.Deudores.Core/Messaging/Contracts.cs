namespace Wayni.Deudores.Core.Messaging;

/// <summary>SQS message sent by the Importer API and consumed by the Processor worker.</summary>
public sealed record ImportRequestedMessage(Guid JobId, string Bucket, string Key, string FileName, DateTimeOffset RequestedAt);

/// <summary>Body of the completion webhook (also used for the e-mail and the structured log).</summary>
public sealed record ImportCompletedNotification(
    Guid JobId,
    string FileName,
    string Status,
    int? Periodo,
    long LineasLeidas,
    long LineasInvalidas,
    int Deudores,
    int Entidades,
    decimal TotalPrestamos,
    long DuracionMs,
    DateTimeOffset FinishedAt,
    string? Error);

public static class ImportStatus
{
    public const string Queued = "Queued";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
