using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Wayni.Deudores.Importer.Api;
using Wayni.Deudores.Infrastructure;
using Wayni.Deudores.Infrastructure.Persistence.Entities;
using Wayni.Deudores.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddJsonConsole();

builder.Services.Configure<ImporterOptions>(builder.Configuration.GetSection("Importer"));
var maxUpload = builder.Configuration.GetValue<long?>("Importer:MaxUploadBytes") ?? new ImporterOptions().MaxUploadBytes;
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = maxUpload);
builder.Services.Configure<FormOptions>(f => f.MultipartBodyLengthLimit = maxUpload);

builder.Services.AddWayniPersistence(builder.Configuration);
builder.Services.AddWayniAws(builder.Configuration);
builder.Services.AddScoped<ImportService>();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.MapOpenApi();
app.MapScalarApiReference();
app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Health");

// POST /upload - multipart/form-data, field "file": deudores.txt or the BCRA .7z/.zip.
app.MapPost("/upload", async (IFormFile file, ImportService service, CancellationToken ct) =>
    {
        if (ImportService.Validate(file.FileName) is { } error)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [error] });

        await using var content = file.OpenReadStream();
        var job = await service.ImportStreamAsync(file.FileName, content, ct);
        return Results.Accepted($"/jobs/{job.Id}", JobResponse.From(job));
    })
    .DisableAntiforgery()
    .WithTags("Importador")
    .WithSummary("Sube el archivo del BCRA (.txt, .zip o .7z), lo guarda en S3 y encola el procesamiento en SQS.")
    .Produces<JobResponse>(StatusCodes.Status202Accepted);

// POST /import/local - { "path": "202311DEUDORES_20240122204012.7Z" } relative to Importer:InboxPath.
app.MapPost("/import/local", async (LocalImportRequest request, ImportService service, IOptions<ImporterOptions> options,
        CancellationToken ct) =>
    {
        var inbox = Path.GetFullPath(options.Value.InboxPath);
        var fullPath = Path.GetFullPath(Path.Combine(inbox, request.Path ?? string.Empty));
        if (!fullPath.StartsWith(inbox + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["path"] = [$"The path must be inside {inbox}."] });
        if (ImportService.Validate(fullPath) is { } error)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["path"] = [error] });
        if (!File.Exists(fullPath))
            return Results.NotFound(new { message = $"File not found: {request.Path}" });

        var job = await service.ImportLocalFileAsync(fullPath, ct);
        return Results.Accepted($"/jobs/{job.Id}", JobResponse.From(job));
    })
    .WithTags("Importador")
    .WithSummary("Importa un archivo que ya está en la carpeta inbox del servidor (ruta local).")
    .Produces<JobResponse>(StatusCodes.Status202Accepted);

app.MapGet("/import/local", (IOptions<ImporterOptions> options) =>
    {
        var inbox = options.Value.InboxPath;
        var files = new List<InboxFile>();
        if (Directory.Exists(inbox))
            files.AddRange(Directory.EnumerateFiles(inbox).Select(f => new FileInfo(f)).Select(f => new InboxFile(f.Name, f.Length)));
        return Results.Ok(files);
    })
    .WithTags("Importador")
    .WithSummary("Lista los archivos disponibles en la carpeta inbox.");

app.MapGet("/jobs", async (IImportJobRepository jobs, int? take, CancellationToken ct) =>
        Results.Ok((await jobs.ListAsync(Math.Clamp(take ?? 20, 1, 200), ct)).Select(JobResponse.From)))
    .WithTags("Jobs");

app.MapGet("/jobs/{id:guid}", async (Guid id, IImportJobRepository jobs, CancellationToken ct) =>
        await jobs.GetAsync(id, ct) is { } job ? Results.Ok(JobResponse.From(job)) : Results.NotFound())
    .WithTags("Jobs");

app.MapPost("/jobs/{id:guid}/reprocess", async (Guid id, ImportService service, CancellationToken ct) =>
        await service.ReprocessAsync(id, ct) is { } job ? Results.Accepted($"/jobs/{id}", JobResponse.From(job)) : Results.NotFound())
    .WithTags("Jobs")
    .WithSummary("Vuelve a encolar un job existente (mismo objeto en S3).");

app.Run();

public sealed record LocalImportRequest(string? Path);

public sealed record InboxFile(string Name, long SizeBytes);

public sealed record JobResponse(
    Guid Id, string FileName, string Status, string ObjectKey, DateTime CreatedAt, DateTime? StartedAt, DateTime? FinishedAt,
    int? Periodo, long LineasLeidas, long LineasInvalidas, int Deudores, int Entidades, decimal TotalPrestamos, long? DuracionMs,
    string? Error)
{
    public static JobResponse From(ImportJob j) => new(j.Id, j.FileName, j.Status, j.ObjectKey, j.CreatedAt, j.StartedAt,
        j.FinishedAt, j.Periodo, j.LineasLeidas, j.LineasInvalidas, j.Deudores, j.Entidades, j.TotalPrestamos, j.DuracionMs, j.Error);
}

public partial class Program;
