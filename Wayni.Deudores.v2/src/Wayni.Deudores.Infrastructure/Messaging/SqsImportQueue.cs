using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wayni.Deudores.Core.Messaging;
using Wayni.Deudores.Infrastructure.Options;

namespace Wayni.Deudores.Infrastructure.Messaging;

/// <summary>A message taken from the queue, with the handle needed to delete it or extend its visibility.</summary>
public sealed record ReceivedImport(ImportRequestedMessage Message, string ReceiptHandle);

/// <summary>Asynchronous hand-off between Importer and Processor (bonus: "SQS para procesamiento asíncrono").</summary>
public interface IImportQueue
{
    Task EnqueueAsync(ImportRequestedMessage message, CancellationToken ct);
    Task<ReceivedImport?> ReceiveAsync(CancellationToken ct);
    Task DeleteAsync(ReceivedImport received, CancellationToken ct);
    Task ExtendVisibilityAsync(ReceivedImport received, TimeSpan duration, CancellationToken ct);
}

public sealed class SqsImportQueue(IAmazonSQS sqs, IOptions<AwsOptions> options, ILogger<SqsImportQueue> logger) : IImportQueue
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly AwsOptions.SqsSettings _settings = options.Value.Sqs;
    private readonly SemaphoreSlim _urlLock = new(1, 1);
    private string? _queueUrl;

    public async Task EnqueueAsync(ImportRequestedMessage message, CancellationToken ct)
    {
        var url = await GetQueueUrlAsync(ct);
        await sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = url,
            MessageBody = JsonSerializer.Serialize(message, Json),
        }, ct);
        logger.LogInformation("Queued import {JobId} ({Key}) on {Queue}", message.JobId, message.Key, _settings.QueueName);
    }

    public async Task<ReceivedImport?> ReceiveAsync(CancellationToken ct)
    {
        var url = await GetQueueUrlAsync(ct);
        var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = url,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 20, // long polling
            VisibilityTimeout = _settings.VisibilityTimeoutSeconds,
        }, ct);

        var sqsMessage = response.Messages?.FirstOrDefault();
        if (sqsMessage is null)
            return null;

        var message = JsonSerializer.Deserialize<ImportRequestedMessage>(sqsMessage.Body, Json);
        if (message is null)
        {
            logger.LogWarning("Discarding unreadable message {MessageId}", sqsMessage.MessageId);
            await sqs.DeleteMessageAsync(url, sqsMessage.ReceiptHandle, ct);
            return null;
        }

        return new ReceivedImport(message, sqsMessage.ReceiptHandle);
    }

    public async Task DeleteAsync(ReceivedImport received, CancellationToken ct) =>
        await sqs.DeleteMessageAsync(await GetQueueUrlAsync(ct), received.ReceiptHandle, ct);

    public async Task ExtendVisibilityAsync(ReceivedImport received, TimeSpan duration, CancellationToken ct) =>
        await sqs.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
        {
            QueueUrl = await GetQueueUrlAsync(ct),
            ReceiptHandle = received.ReceiptHandle,
            VisibilityTimeout = (int)duration.TotalSeconds,
        }, ct);

    private async Task<string> GetQueueUrlAsync(CancellationToken ct)
    {
        if (_queueUrl is not null)
            return _queueUrl;

        await _urlLock.WaitAsync(ct);
        try
        {
            if (_queueUrl is not null)
                return _queueUrl;

            try
            {
                _queueUrl = (await sqs.GetQueueUrlAsync(_settings.QueueName, ct)).QueueUrl;
            }
            catch (QueueDoesNotExistException) when (_settings.CreateIfMissing)
            {
                _queueUrl = (await sqs.CreateQueueAsync(new CreateQueueRequest
                {
                    QueueName = _settings.QueueName,
                    Attributes = new Dictionary<string, string>
                    {
                        ["VisibilityTimeout"] = _settings.VisibilityTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                }, ct)).QueueUrl;
                logger.LogInformation("Created queue {Queue}", _settings.QueueName);
            }
            return _queueUrl;
        }
        finally
        {
            _urlLock.Release();
        }
    }
}
