using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Wayni.Deudores.Admin.Web.Services;
using Wayni.Deudores.Core.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddJsonConsole();

builder.Services.AddRazorPages();
builder.Services.AddSingleton<NotificationStore>();
builder.Services.AddHttpClient<ImporterApiClient>(c =>
{
    c.BaseAddress = new Uri(EnsureSlash(builder.Configuration["Services:ImporterApi"]!));
    c.Timeout = TimeSpan.FromHours(1); // uploads of several GB go through this client
});
builder.Services.AddHttpClient<QueryApiClient>(c => c.BaseAddress = new Uri(EnsureSlash(builder.Configuration["Services:QueryApi"]!)));

const long MaxUpload = 10L * 1024 * 1024 * 1024;
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = MaxUpload);
builder.Services.Configure<FormOptions>(f => f.MultipartBodyLengthLimit = MaxUpload);

var app = builder.Build();
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Completion webhook sent by the Processor (Notifications:Webhook:Url). Protected by a shared secret.
var webhookSecret = builder.Configuration["Webhook:Secret"];
app.MapPost("/webhooks/import-completed", (HttpRequest request, ImportCompletedNotification notification, NotificationStore store,
        ILogger<Program> logger) =>
    {
        if (!string.IsNullOrEmpty(webhookSecret))
        {
            var received = request.Headers["X-Webhook-Secret"].ToString();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(received), Encoding.UTF8.GetBytes(webhookSecret)))
                return Results.Unauthorized();
        }
        store.Add(notification);
        logger.LogInformation("Webhook received for job {JobId}: {Status}", notification.JobId, notification.Status);
        return Results.Ok();
    })
    .DisableAntiforgery();

app.Run();

static string EnsureSlash(string url) => url.EndsWith('/') ? url : url + "/";

public partial class Program;
