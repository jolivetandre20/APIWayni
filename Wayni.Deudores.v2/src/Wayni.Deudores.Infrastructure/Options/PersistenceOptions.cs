namespace Wayni.Deudores.Infrastructure.Options;

/// <summary>Section "Persistence".</summary>
public sealed class PersistenceOptions
{
    public const string Section = "Persistence";

    /// <summary>
    /// true (default): LOAD DATA LOCAL INFILE into a temporary table + one INSERT ... SELECT ... ON DUPLICATE KEY UPDATE
    /// per chunk - minutes for the ~20 million debtors of a full file. Needs local_infile=ON on the server and
    /// AllowLoadLocalInfile=true in the connection string (both set in docker-compose).
    /// false: parameterised multi-row INSERT ... ON DUPLICATE KEY UPDATE (portable, slower).
    /// </summary>
    public bool UseBulkLoad { get; set; } = true;

    /// <summary>Rows per bulk-load chunk.</summary>
    public int BulkChunkSize { get; set; } = 250_000;

    /// <summary>Rows per statement for the parameterised strategy.</summary>
    public int BatchSize { get; set; } = 1_000;
}
