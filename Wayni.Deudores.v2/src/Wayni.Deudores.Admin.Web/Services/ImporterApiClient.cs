using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Wayni.Deudores.Admin.Web.Services;

public sealed class ImporterApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<JobDto>> GetJobsAsync(int take, CancellationToken ct) =>
        await http.GetFromJsonAsync<List<JobDto>>($"jobs?take={take}", ct) ?? [];

    public async Task<IReadOnlyList<InboxFileDto>> GetInboxAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<List<InboxFileDto>>("import/local", ct) ?? [];

    public async Task<(JobDto? Job, string? Error)> UploadAsync(string fileName, Stream content, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(content, 1 << 20);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        using var response = await http.PostAsync("upload", form, ct);
        return await ReadJobAsync(response, ct);
    }

    public async Task<(JobDto? Job, string? Error)> ImportLocalAsync(string path, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("import/local", new { path }, ct);
        return await ReadJobAsync(response, ct);
    }

    public async Task<(JobDto? Job, string? Error)> ReprocessAsync(Guid id, CancellationToken ct)
    {
        using var response = await http.PostAsync($"jobs/{id}/reprocess", null, ct);
        return await ReadJobAsync(response, ct);
    }

    private static async Task<(JobDto?, string?)> ReadJobAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<JobDto>(ct), null);
        var body = await response.Content.ReadAsStringAsync(ct);
        return (null, $"{(int)response.StatusCode} {response.ReasonPhrase}: {(response.StatusCode == HttpStatusCode.NotFound ? "not found" : body)}");
    }
}
