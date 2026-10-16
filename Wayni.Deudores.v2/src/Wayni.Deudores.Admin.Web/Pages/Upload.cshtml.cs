using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Wayni.Deudores.Admin.Web.Services;

namespace Wayni.Deudores.Admin.Web.Pages;

[DisableRequestSizeLimit]
[RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
public class UploadModel(ImporterApiClient importer) : PageModel
{
    public IReadOnlyList<InboxFileDto> Inbox { get; private set; } = [];
    public string? Message { get; private set; }
    public bool Success { get; private set; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadInboxAsync(ct);

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            Message = "Seleccione un archivo.";
        }
        else
        {
            await using var content = file.OpenReadStream();
            var (job, error) = await importer.UploadAsync(file.FileName, content, ct);
            SetResult(job, error);
        }
        await LoadInboxAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLocalAsync(string path, CancellationToken ct)
    {
        var (job, error) = await importer.ImportLocalAsync(path, ct);
        SetResult(job, error);
        await LoadInboxAsync(ct);
        return Page();
    }

    private void SetResult(JobDto? job, string? error)
    {
        Success = job is not null;
        Message = job is not null
            ? $"Importación encolada: job {job.Id} ({job.FileName}). Siga el estado en el Panel."
            : $"No se pudo importar: {error}";
    }

    private async Task LoadInboxAsync(CancellationToken ct)
    {
        try
        {
            Inbox = await importer.GetInboxAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            Message ??= "No se pudo contactar al Importer API: " + ex.Message;
        }
    }
}
