using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Wayni.Deudores.Admin.Web.Services;

namespace Wayni.Deudores.Admin.Web.Pages;

public class IndexModel(ImporterApiClient importer, NotificationStore notifications, ILogger<IndexModel> logger) : PageModel
{
    public IReadOnlyList<JobDto> Jobs { get; private set; } = [];
    public IReadOnlyList<ReceivedNotification> Notifications { get; private set; } = [];
    public JobDto? LastCompleted { get; private set; }
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Notifications = notifications.Latest();
        try
        {
            Jobs = await importer.GetJobsAsync(25, ct);
            LastCompleted = Jobs.FirstOrDefault(j => j.Status == "Completed");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Importer API not reachable: {Message}", ex.Message);
            Error = "No se pudo contactar al Importer API: " + ex.Message;
        }
    }

    public async Task<IActionResult> OnPostReprocessAsync(Guid id, CancellationToken ct)
    {
        await importer.ReprocessAsync(id, ct);
        return RedirectToPage();
    }

    public static string FormatDuration(long? ms) =>
        ms is null ? "—" : TimeSpan.FromMilliseconds(ms.Value).ToString(ms.Value >= 3_600_000 ? @"h\:mm\:ss" : @"m\:ss");
}
