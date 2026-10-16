namespace Wayni.Deudores.Infrastructure.Options;

/// <summary>Section "Archives".</summary>
public sealed class ArchiveOptions
{
    public const string Section = "Archives";

    /// <summary>
    /// 7-Zip command line used to stream deudores.txt out of the BCRA .7z. "7z" is on the PATH of the
    /// Processor image (p7zip-full). On Windows, outside Docker: "C:\\Program Files\\7-Zip\\7z.exe".
    /// </summary>
    public string SevenZipPath { get; set; } = "7z";
}
