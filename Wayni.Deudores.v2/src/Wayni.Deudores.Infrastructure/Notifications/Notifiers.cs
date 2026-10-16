using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wayni.Deudores.Core.Messaging;
using Wayni.Deudores.Infrastructure.Options;

namespace Wayni.Deudores.Infrastructure.Notifications;

/// <summary>"Notificación de Finalización": e-mail, webhook POST and/or structured log.</summary>
public interface IImportNotifier
{
    Task NotifyAsync(ImportCompletedNotification notification, CancellationToken ct);
}

/// <summary>Sends the notification through every enabled channel; one failing channel never hides the others.</summary>
public sealed class CompositeImportNotifier(IEnumerable<IImportNotificationChannel> channels, ILogger<CompositeImportNotifier> logger)
    : IImportNotifier
{
    public async Task NotifyAsync(ImportCompletedNotification notification, CancellationToken ct)
    {
        foreach (var channel in channels.Where(c => c.IsEnabled))
        {
            try
            {
                await channel.SendAsync(notification, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Notification channel {Channel} failed for job {JobId}", channel.Name, notification.JobId);
            }
        }
    }
}

public interface IImportNotificationChannel
{
    string Name { get; }
    bool IsEnabled { get; }
    Task SendAsync(ImportCompletedNotification notification, CancellationToken ct);
}

/// <summary>Structured log with total time and load summary (JSON console formatter makes every field a property).</summary>
public sealed class LogNotificationChannel(IOptions<NotificationOptions> options, ILogger<LogNotificationChannel> logger)
    : IImportNotificationChannel
{
    public string Name => "log";
    public bool IsEnabled => options.Value.Log;

    public Task SendAsync(ImportCompletedNotification n, CancellationToken ct)
    {
        logger.LogInformation(
            "ImportFinished {JobId} {Status} file={FileName} periodo={Periodo} durationMs={DuracionMs} " +
            "lineas={LineasLeidas} invalidas={LineasInvalidas} deudores={Deudores} entidades={Entidades} totalPrestamos={TotalPrestamos} error={Error}",
            n.JobId, n.Status, n.FileName, n.Periodo, n.DuracionMs, n.LineasLeidas, n.LineasInvalidas, n.Deudores, n.Entidades,
            n.TotalPrestamos, n.Error);
        return Task.CompletedTask;
    }
}

/// <summary>POSTs the notification as JSON to Notifications:Webhook:Url.</summary>
public sealed class WebhookNotificationChannel(HttpClient http, IOptions<NotificationOptions> options, ILogger<WebhookNotificationChannel> logger)
    : IImportNotificationChannel
{
    public const string SecretHeader = "X-Webhook-Secret";

    public string Name => "webhook";
    public bool IsEnabled => !string.IsNullOrWhiteSpace(options.Value.Webhook.Url);

    public async Task SendAsync(ImportCompletedNotification notification, CancellationToken ct)
    {
        var settings = options.Value.Webhook;
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.Url) { Content = JsonContent.Create(notification) };
        if (!string.IsNullOrEmpty(settings.Secret))
            request.Headers.Add(SecretHeader, settings.Secret);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        logger.LogInformation("Webhook {Url} answered {StatusCode} for job {JobId}", settings.Url, (int)response.StatusCode, notification.JobId);
    }
}

/// <summary>SMTP e-mail (Mailpit in docker-compose: UI on http://localhost:8025).</summary>
public sealed class EmailNotificationChannel(IOptions<NotificationOptions> options, ILogger<EmailNotificationChannel> logger)
    : IImportNotificationChannel
{
    public string Name => "email";
    public bool IsEnabled => options.Value.Email.Enabled;

    public async Task SendAsync(ImportCompletedNotification n, CancellationToken ct)
    {
        var settings = options.Value.Email;
        var inv = CultureInfo.InvariantCulture;
        using var message = new MailMessage(settings.From, settings.To)
        {
            Subject = $"[Wayni] Importación BCRA {n.Status}: {n.FileName}",
            Body = string.Join(Environment.NewLine,
                $"Job: {n.JobId}",
                $"Estado: {n.Status}",
                $"Archivo: {n.FileName}",
                $"Período: {n.Periodo}",
                $"Duración: {TimeSpan.FromMilliseconds(n.DuracionMs):hh\\:mm\\:ss}",
                $"Líneas leídas: {n.LineasLeidas.ToString("N0", inv)} (inválidas: {n.LineasInvalidas.ToString("N0", inv)})",
                $"Deudores: {n.Deudores.ToString("N0", inv)}",
                $"Entidades: {n.Entidades.ToString("N0", inv)}",
                $"Total préstamos (miles de $): {n.TotalPrestamos.ToString("N1", inv)}",
                n.Error is null ? string.Empty : $"Error: {n.Error}"),
        };

        using var client = new SmtpClient(settings.Host, settings.Port) { EnableSsl = settings.EnableSsl };
        if (!string.IsNullOrEmpty(settings.Username))
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);

        await client.SendMailAsync(message, ct);
        logger.LogInformation("E-mail sent to {To} for job {JobId}", settings.To, n.JobId);
    }
}
