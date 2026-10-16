namespace Wayni.Deudores.Importer.Api;

/// <summary>Section "Importer".</summary>
public sealed class ImporterOptions
{
    /// <summary>Folder that POST /import/local may read from (mounted from ./data/inbox in docker-compose).</summary>
    public string InboxPath { get; set; } = "/data/inbox";

    /// <summary>Upload limit for POST /upload. Default 10 GB: the uncompressed deudores.txt is ~6 GB.</summary>
    public long MaxUploadBytes { get; set; } = 10L * 1024 * 1024 * 1024;
}
