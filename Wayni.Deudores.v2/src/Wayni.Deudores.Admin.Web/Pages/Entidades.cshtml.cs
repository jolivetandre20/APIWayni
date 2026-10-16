using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Wayni.Deudores.Admin.Web.Services;

namespace Wayni.Deudores.Admin.Web.Pages;

public class EntidadesModel(QueryApiClient query) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Codigo { get; set; }

    public EntidadDto? Entidad { get; private set; }
    public IReadOnlyList<EntidadDto> Ranking { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            if (Codigo is not null)
                Entidad = await query.GetEntidadAsync(Codigo.Value, ct);
            Ranking = await query.GetEntidadesAsync(30, ct);
        }
        catch (HttpRequestException ex)
        {
            Error = "No se pudo contactar al Query API: " + ex.Message;
        }
    }
}
