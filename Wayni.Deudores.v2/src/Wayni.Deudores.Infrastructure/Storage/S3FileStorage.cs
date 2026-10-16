using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.S3.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wayni.Deudores.Infrastructure.Options;

namespace Wayni.Deudores.Infrastructure.Storage;

/// <summary>Object storage for the original files (optional point of the challenge: "guarda el archivo original en S3").</summary>
public interface IFileStorage
{
    string Bucket { get; }
    Task UploadAsync(string key, Stream content, CancellationToken ct);
    Task UploadFileAsync(string key, string filePath, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    Task DownloadToFileAsync(string key, string filePath, CancellationToken ct);
}

public sealed class S3FileStorage(IAmazonS3 s3, IOptions<AwsOptions> options, ILogger<S3FileStorage> logger) : IFileStorage
{
    private readonly AwsOptions.S3Settings _settings = options.Value.S3;
    private int _bucketReady;

    public string Bucket => _settings.Bucket;

    public async Task UploadAsync(string key, Stream content, CancellationToken ct)
    {
        await EnsureBucketAsync(ct);
        using var transfer = new TransferUtility(s3);
        await transfer.UploadAsync(new TransferUtilityUploadRequest
        {
            BucketName = Bucket,
            Key = key,
            InputStream = content,
            AutoCloseStream = false,
            PartSize = 16 * 1024 * 1024,
        }, ct);
        logger.LogInformation("Stored s3://{Bucket}/{Key}", Bucket, key);
    }

    public async Task UploadFileAsync(string key, string filePath, CancellationToken ct)
    {
        await EnsureBucketAsync(ct);
        using var transfer = new TransferUtility(s3);
        await transfer.UploadAsync(new TransferUtilityUploadRequest
        {
            BucketName = Bucket,
            Key = key,
            FilePath = filePath,
            PartSize = 16 * 1024 * 1024,
        }, ct);
        logger.LogInformation("Stored {Path} as s3://{Bucket}/{Key}", filePath, Bucket, key);
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        var response = await s3.GetObjectAsync(Bucket, key, ct);
        return response.ResponseStream;
    }

    public async Task DownloadToFileAsync(string key, string filePath, CancellationToken ct)
    {
        using var response = await s3.GetObjectAsync(Bucket, key, ct);
        await using var target = File.Create(filePath);
        await response.ResponseStream.CopyToAsync(target, 1 << 20, ct);
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (!_settings.CreateIfMissing || Volatile.Read(ref _bucketReady) == 1)
            return;

        if (!await AmazonS3Util.DoesS3BucketExistV2Async(s3, Bucket))
        {
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = Bucket, UseClientRegion = true }, ct);
            logger.LogInformation("Created bucket {Bucket}", Bucket);
        }
        Volatile.Write(ref _bucketReady, 1);
    }
}
