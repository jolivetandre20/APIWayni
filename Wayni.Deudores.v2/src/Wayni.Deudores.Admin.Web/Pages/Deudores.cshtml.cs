using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Wayni.Deudores.Admin.Web.Services;

namespace Wayni.Deudores.Admin.Web.Pages;

public class DeudoresModel(QueryApiClient query) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Cuit { get; set; }
    [BindProperty(SupportsGet = true)] public int N { get; set; } = 10;
    [BindProperty(SupportsGet = true)] public int? Situacion { get; set; }

    public DeudorDto? Deudor { get; private set; }
    public IReadOnlyList<DeudorDto> Top { get; private set; } = [];
    public IReadOnlyList<SituacionDto> Situaciones { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            Situaciones = await query.GetSituacionesAsync(ct);
            if (!string.IsNullOrWhiteSpace(Cuit))
            {
                Cuit = Cuit.Trim().Replace("-", string.Empty);
                Deudor = await query.GetDeudorAsync(Cuit, ct);
            }
            Top = await query.GetTopAsync(Math.Clamp(N, 1, 1000), Situacion, ct);
        }
        catch (HttpRequestException ex)
        {
            Error = "No se pudo contactar al Query API: " + ex.Message;
        }
    }
}
