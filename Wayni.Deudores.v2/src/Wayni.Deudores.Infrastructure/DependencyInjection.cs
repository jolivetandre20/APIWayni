using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wayni.Deudores.Infrastructure.Files;
using Wayni.Deudores.Infrastructure.Messaging;
using Wayni.Deudores.Infrastructure.Notifications;
using Wayni.Deudores.Infrastructure.Options;
using Wayni.Deudores.Infrastructure.Persistence;
using Wayni.Deudores.Infrastructure.Persistence.Repositories;
using Wayni.Deudores.Infrastructure.Storage;

namespace Wayni.Deudores.Infrastructure;

/// <summary>Registration helpers; each microservice adds only what it uses.</summary>
public static class DependencyInjection
{
    /// <summary>EF Core (MySQL / Pomelo) + repositories. Connection string: ConnectionStrings:Wayni.</summary>
    public static IServiceCollection AddWayniPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Wayni")
            ?? throw new InvalidOperationException("ConnectionStrings:Wayni is not configured (see .env.example).");

        services.Configure<PersistenceOptions>(configuration.GetSection(PersistenceOptions.Section));
        services.AddDbContext<WayniDbContext>(options => options.UseMySql(
            connectionString,
            PersistenceDefaults.ServerVersion,
            mySql => mySql.EnableRetryOnFailure(maxRetryCount: 5).CommandTimeout(600)));

        services.AddScoped<IImportJobRepository, ImportJobRepository>();
        services.AddScoped<IDeudoresQueryRepository, DeudoresQueryRepository>();
        services.AddScoped<IImportWriter, MySqlImportWriter>();
        return services;
    }

    /// <summary>S3 + SQS clients pointed at real AWS or at a local emulator (Aws:S3:ServiceUrl / Aws:Sqs:ServiceUrl).</summary>
    public static IServiceCollection AddWayniAws(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AwsOptions>(configuration.GetSection(AwsOptions.Section));

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AwsOptions>>().Value;
            var config = new AmazonS3Config
            {
                ForcePathStyle = options.S3.ForcePathStyle,
                // S3-compatible emulators do not implement the default CRC trailing checksums of recent SDKs.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            };
            Point(config, options.S3.ServiceUrl, options.Region);
            return Credentials(options) is { } credentials ? new AmazonS3Client(credentials, config) : new AmazonS3Client(config);
        });

        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AwsOptions>>().Value;
            var config = new AmazonSQSConfig();
            Point(config, options.Sqs.ServiceUrl, options.Region);
            return Credentials(options) is { } credentials ? new AmazonSQSClient(credentials, config) : new AmazonSQSClient(config);
        });

        services.AddSingleton<IFileStorage, S3FileStorage>();
        services.AddSingleton<IImportQueue, SqsImportQueue>();
        services.Configure<ArchiveOptions>(configuration.GetSection(ArchiveOptions.Section));
        services.AddSingleton<DeudoresFileOpener>();
        return services;
    }

    /// <summary>Completion notifications: structured log, webhook POST and SMTP e-mail.</summary>
    public static IServiceCollection AddWayniNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.Section));
        services.AddHttpClient<WebhookNotificationChannel>(client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddTransient<IImportNotificationChannel, LogNotificationChannel>();
        services.AddTransient<IImportNotificationChannel>(sp => sp.GetRequiredService<WebhookNotificationChannel>());
        services.AddTransient<IImportNotificationChannel, EmailNotificationChannel>();
        services.AddTransient<IImportNotifier, CompositeImportNotifier>();
        return services;
    }

    private static void Point(ClientConfig config, string? serviceUrl, string region)
    {
        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
        }
        else
        {
            config.ServiceURL = serviceUrl;
            config.AuthenticationRegion = region;
        }
    }

    private static AWSCredentials? Credentials(AwsOptions options) =>
        string.IsNullOrWhiteSpace(options.AccessKey)
            ? null
            : new BasicAWSCredentials(options.AccessKey, options.SecretKey);
}
