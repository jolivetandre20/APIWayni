namespace Wayni.Deudores.Infrastructure.Options;

/// <summary>Section "Notifications": how the end of an import is announced.</summary>
public sealed class NotificationOptions
{
    public const string Section = "Notifications";

    /// <summary>Structured log line with total time and load summary (always cheap, on by default).</summary>
    public bool Log { get; set; } = true;

    public WebhookSettings Webhook { get; set; } = new();
    public EmailSettings Email { get; set; } = new();

    public sealed class WebhookSettings
    {
        /// <summary>POST target, e.g. http://admin-web:8080/webhooks/import-completed. Empty = disabled.</summary>
        public string? Url { get; set; }

        /// <summary>Sent as header X-Webhook-Secret so the receiver can reject forged calls.</summary>
        public string? Secret { get; set; }
    }

    public sealed class EmailSettings
    {
        public bool Enabled { get; set; }
        public string Host { get; set; } = "mailpit";
        public int Port { get; set; } = 1025;
        public bool EnableSsl { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string From { get; set; } = "deudores@wayni.local";
        public string To { get; set; } = "equipo@wayni.local";
    }
}
