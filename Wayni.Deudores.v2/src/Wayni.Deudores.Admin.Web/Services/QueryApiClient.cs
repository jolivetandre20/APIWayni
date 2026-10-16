using System.Net;
using System.Net.Http.Json;

namespace Wayni.Deudores.Admin.Web.Services;

public sealed class QueryApiClient(HttpClient http)
{
    public async Task<DeudorDto?> GetDeudorAsync(string cuit, CancellationToken ct)
    {
        using var response = await http.GetAsync($"deudores/{Uri.EscapeDataString(cuit)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DeudorDto>(ct);
    }

    public async Task<IReadOnlyList<DeudorDto>> GetTopAsync(int n, int? situacion, CancellationToken ct) =>
        await http.GetFromJsonAsync<List<DeudorDto>>(
            situacion is null ? $"deudores/top/{n}" : $"deudores/top/{n}?situacion={situacion}", ct) ?? [];

    public async Task<EntidadDto?> GetEntidadAsync(int codigo, CancellationToken ct)
    {
        using var response = await http.GetAsync($"entidades/{codigo}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EntidadDto>(ct);
    }

    public async Task<IReadOnlyList<EntidadDto>> GetEntidadesAsync(int take, CancellationToken ct) =>
        await http.GetFromJsonAsync<List<EntidadDto>>($"entidades?take={take}", ct) ?? [];

    public async Task<IReadOnlyList<SituacionDto>> GetSituacionesAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<List<SituacionDto>>("situaciones", ct) ?? [];
}
