namespace Wayni.Deudores.Infrastructure.Options;

/// <summary>
/// Section "Aws". ServiceUrl points the SDK at a local emulator (S3Mock / ElasticMQ / LocalStack);
/// leave it empty to use real AWS with the default credential chain.
/// </summary>
public sealed class AwsOptions
{
    public const string Section = "Aws";

    public string Region { get; set; } = "us-east-1";

    /// <summary>Emulators ignore credentials but the SDK requires some; real AWS uses the default chain when empty.</summary>
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }

    public S3Settings S3 { get; set; } = new();
    public SqsSettings Sqs { get; set; } = new();

    public sealed class S3Settings
    {
        /// <summary>e.g. http://s3:9090 (S3Mock) or http://localstack:4566. Empty = AWS.</summary>
        public string? ServiceUrl { get; set; }
        public string Bucket { get; set; } = "wayni-deudores";

        /// <summary>Emulators need path-style URLs (http://host/bucket/key).</summary>
        public bool ForcePathStyle { get; set; } = true;

        /// <summary>Create the bucket on first use when it does not exist (local emulators).</summary>
        public bool CreateIfMissing { get; set; } = true;
    }

    public sealed class SqsSettings
    {
        /// <summary>e.g. http://sqs:9324 (ElasticMQ) or http://localstack:4566. Empty = AWS.</summary>
        public string? ServiceUrl { get; set; }
        public string QueueName { get; set; } = "deudores-import";

        /// <summary>Create the queue on first use when it does not exist (local emulators).</summary>
        public bool CreateIfMissing { get; set; } = true;

        /// <summary>Seconds a received message stays invisible; the worker extends it while processing.</summary>
        public int VisibilityTimeoutSeconds { get; set; } = 300;
    }
}
