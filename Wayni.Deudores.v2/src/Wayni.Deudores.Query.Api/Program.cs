using Scalar.AspNetCore;
using Wayni.Deudores.Core.Parsing;
using Wayni.Deudores.Infrastructure;
using Wayni.Deudores.Infrastructure.Persistence.Repositories;
using Wayni.Deudores.Query.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddJsonConsole();
builder.Services.AddWayniPersistence(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.MapOpenApi();
app.MapScalarApiReference();
app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Health");

const int MaxTop = 1000;

app.MapGet("/deudores/{cuit}", async (string cuit, IDeudoresQueryRepository repository, CancellationToken ct) =>
    {
        if (!DeudoresLineParser.IsValidIdentificacion(cuit))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["cuit"] = ["Must be 11 characters (CUIT/CUIL digits)."] });
        return await repository.GetDeudorAsync(cuit, ct) is { } deudor ? Results.Ok(DeudorResponse.From(deudor)) : Results.NotFound();
    })
    .WithTags("Deudores")
    .WithSummary("Deudor por CUIT/CUIL: situación máxima y suma total de préstamos.")
    .Produces<DeudorResponse>()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status404NotFound);

// Declared with a constraint so "top" never collides with a CUIT.
app.MapGet("/deudores/top/{n:int}", async (int n, int? situacion, IDeudoresQueryRepository repository, CancellationToken ct) =>
    {
        if (n is < 1 or > MaxTop)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["n"] = [$"Must be between 1 and {MaxTop}."] });
        var top = await repository.GetTopDeudoresAsync(n, situacion, ct);
        return Results.Ok(top.Select(DeudorResponse.From));
    })
    .WithTags("Deudores")
    .WithSummary("Los N deudores con mayor suma de préstamos. Filtro opcional ?situacion=5.")
    .Produces<IEnumerable<DeudorResponse>>();

app.MapGet("/entidades/{codigo:int}", async (int codigo, IDeudoresQueryRepository repository, CancellationToken ct) =>
        await repository.GetEntidadAsync(codigo, ct) is { } entidad ? Results.Ok(EntidadResponse.From(entidad)) : Results.NotFound())
    .WithTags("Entidades")
    .WithSummary("Entidad por código: suma total de préstamos agrupada.")
    .Produces<EntidadResponse>()
    .Produces(StatusCodes.Status404NotFound);

app.MapGet("/entidades", async (int? take, IDeudoresQueryRepository repository, CancellationToken ct) =>
        Results.Ok((await repository.GetEntidadesAsync(Math.Clamp(take ?? 50, 1, 1000), ct)).Select(EntidadResponse.From)))
    .WithTags("Entidades")
    .WithSummary("Entidades ordenadas por suma de préstamos (default 50).");

app.MapGet("/situaciones", async (IDeudoresQueryRepository repository, CancellationToken ct) =>
        Results.Ok((await repository.GetSituacionesAsync(ct)).Select(s => new SituacionResponse(s.Codigo, s.Descripcion))))
    .WithTags("Situaciones")
    .WithSummary("Catálogo de situaciones (campo 6).");

app.Run();

public partial class Program;
