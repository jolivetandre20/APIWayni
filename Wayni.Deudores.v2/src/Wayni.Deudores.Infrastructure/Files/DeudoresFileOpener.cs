using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wayni.Deudores.Infrastructure.Options;
using Wayni.Deudores.Infrastructure.Storage;

namespace Wayni.Deudores.Infrastructure.Files;

/// <summary>
/// Opens the deudores.txt stream of an uploaded object. Accepted uploads:
/// <list type="bullet">
///   <item><c>.txt</c> - streamed straight from S3 (5.9 GB for a full month, never fully in memory).</item>
///   <item><c>.7z</c> - the BCRA distribution (e.g. 202311DEUDORES_20240122204012.7Z, 240 MB); downloaded to a temp file and
///   decompressed on the fly by the 7-Zip command line (<c>7z e -so</c>). v2: replaces SharpCompress, whose versions up to 0.47.4
///   carry GHSA-6c8g-7p36-r338 and whose newer releases changed the public API; native 7-Zip is also several times faster.</item>
///   <item><c>.zip</c> - same as .7z.</item>
/// </list>
/// The entry used is <c>deudores.txt</c> (case-insensitive); in a .zip without it, the largest .txt.
/// </summary>
public sealed class DeudoresFileOpener(IFileStorage storage, IOptions<ArchiveOptions> archiveOptions, ILogger<DeudoresFileOpener> logger)
{
    public const string EntryName = "deudores.txt";

    public static bool IsSupported(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() is ".txt" or ".zip" or ".7z";

    public async Task<Stream> OpenAsync(string objectKey, string fileName, CancellationToken ct)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".txt")
            return await storage.OpenReadAsync(objectKey, ct);

        if (extension is not (".zip" or ".7z"))
            throw new NotSupportedException($"Unsupported file type '{extension}'. Use .txt, .zip or .7z.");

        var tempFile = Path.Combine(Path.GetTempPath(), $"wayni-{Guid.NewGuid():N}{extension}");
        logger.LogInformation("Downloading s3://{Bucket}/{Key} to {TempFile}", storage.Bucket, objectKey, tempFile);
        await storage.DownloadToFileAsync(objectKey, tempFile, ct);

        try
        {
            return extension == ".zip" ? OpenFromZip(tempFile) : OpenFrom7z(tempFile, archiveOptions.Value.SevenZipPath);
        }
        catch
        {
            TryDelete(tempFile);
            throw;
        }
    }

    /// <summary>Opens deudores.txt inside a local archive (also used by tests).</summary>
    public static Stream OpenFromZip(string zipPath)
    {
        var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries.FirstOrDefault(e => string.Equals(e.Name, EntryName, StringComparison.OrdinalIgnoreCase))
            ?? archive.Entries.Where(e => e.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.Length)
                .FirstOrDefault();
        if (entry is null)
        {
            archive.Dispose();
            throw new InvalidDataException($"No {EntryName} found in the .zip file.");
        }
        return new OwningStream(entry.Open(), archive, zipPath);
    }

    /// <summary>
    /// Streams deudores.txt out of a local .7z with the 7-Zip CLI (<c>7z e -so archive deudores.txt -r</c>).
    /// The archive is never fully extracted to disk: the 5.9 GB entry flows through the process stdout.
    /// </summary>
    public static Stream OpenFrom7z(string sevenZipPath, string sevenZipExecutable)
    {
        var start = new ProcessStartInfo(sevenZipExecutable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "e", "-so", "-y", sevenZipPath, EntryName, "-r" })
            start.ArgumentList.Add(argument);

        Process process;
        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start '{sevenZipExecutable}'.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"7-Zip was not found ('{sevenZipExecutable}'). Install it or set Archives:SevenZipPath. {ex.Message}", ex);
        }

        return new ProcessOutputStream(process, sevenZipPath);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Read-only stream that disposes its archive and deletes the temp file when closed.</summary>
    private sealed class OwningStream(Stream inner, IDisposable owner, string? tempFile) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                owner.Dispose();
                if (tempFile is not null && tempFile.StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
                    TryDelete(tempFile);
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>stdout of the 7-Zip process; fails at the end of the stream if 7-Zip reported an error.</summary>
    private sealed class ProcessOutputStream : Stream
    {
        private readonly Process _process;
        private readonly string _archivePath;
        private readonly StringBuilder _stderr = new();
        private readonly Stream _stdout;
        private bool _finished;

        public ProcessOutputStream(Process process, string archivePath)
        {
            _process = process;
            _archivePath = archivePath;
            _stdout = process.StandardOutput.BaseStream;
            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    lock (_stderr) _stderr.AppendLine(e.Data);
            };
            _process.BeginErrorReadLine();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => Check(_stdout.Read(buffer, offset, count));
        public override int Read(Span<byte> buffer) => Check(_stdout.Read(buffer));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Check(await _stdout.ReadAsync(buffer, cancellationToken).ConfigureAwait(false));

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Check(await _stdout.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false));

        private int Check(int read)
        {
            if (read > 0 || _finished)
                return read;

            _finished = true;
            _process.WaitForExit();
            if (_process.ExitCode != 0)
            {
                string errors;
                lock (_stderr) errors = _stderr.ToString().Trim();
                throw new IOException($"7-Zip failed with exit code {_process.ExitCode} on '{Path.GetFileName(_archivePath)}': {errors}");
            }
            return 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (!_process.HasExited)
                        _process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }
                _process.Dispose();
                if (_archivePath.StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
                    TryDelete(_archivePath);
            }
            base.Dispose(disposing);
        }
    }
}
