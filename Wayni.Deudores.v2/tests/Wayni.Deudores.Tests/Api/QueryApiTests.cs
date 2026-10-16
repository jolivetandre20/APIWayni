using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayni.Deudores.Infrastructure.Persistence;
using Wayni.Deudores.Infrastructure.Persistence.Entities;
using Wayni.Deudores.Query.Api;

namespace Wayni.Deudores.Tests.Api;

/// <summary>Query API end to end (HTTP -> minimal API -> EF Core), with the in-memory provider instead of MySQL.</summary>
public class QueryApiTests : IClassFixture<QueryApiTests.Factory>
{
    private readonly HttpClient _client;

    public QueryApiTests(Factory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Get_deudor_by_cuit()
    {
        var deudor = await _client.GetFromJsonAsync<DeudorResponse>("/deudores/30546689979");
        Assert.NotNull(deudor);
        Assert.Equal(5, deudor.SituacionMaxima);
        Assert.Equal("Irrecuperable", deudor.SituacionDescripcion);
        Assert.Equal(193659024.0m, deudor.SumaTotalPrestamos);
    }

    [Fact]
    public async Task Unknown_cuit_is_404_and_malformed_is_400()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/deudores/20000000001")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/deudores/123")).StatusCode);
    }

    [Fact]
    public async Task Top_n_is_ordered_by_suma_and_can_filter_by_situacion()
    {
        var top = await _client.GetFromJsonAsync<List<DeudorResponse>>("/deudores/top/2");
        Assert.Equal(new[] { "30546689979", "30685376349" }, top!.Select(d => d.NroIdentificacion));

        var irrecuperables = await _client.GetFromJsonAsync<List<DeudorResponse>>("/deudores/top/10?situacion=5");
        Assert.Equal(new[] { "30546689979", "20111111112" }, irrecuperables!.Select(d => d.NroIdentificacion));

        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/deudores/top/0")).StatusCode);
    }

    [Fact]
    public async Task Get_entidad_by_codigo()
    {
        var entidad = await _client.GetFromJsonAsync<EntidadResponse>("/entidades/7");
        Assert.Equal("BANCO DE GALICIA Y BUENOS AIRES S.A.U.", entidad!.Nombre);
        Assert.Equal(2167457645.0m, entidad.SumaTotalPrestamos);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/entidades/99999")).StatusCode);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _database = $"query-tests-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Wayni", "Server=unused;Database=unused");
            builder.ConfigureServices(services =>
            {
                // Replace the MySQL registration (EF Core 9 also registers IDbContextOptionsConfiguration<T>).
                services.RemoveAll<DbContextOptions<WayniDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<WayniDbContext>>();
                services.AddDbContext<WayniDbContext>(o => o.UseInMemoryDatabase(_database));

                using var scope = services.BuildServiceProvider().CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WayniDbContext>();
                db.Database.EnsureCreated(); // applies HasData seed: 485 entidades + situaciones
                var now = DateTime.UtcNow;
                db.Deudores.AddRange(
                    new Deudor { NroIdentificacion = "30546689979", SituacionMaxima = 5, SumaTotalPrestamos = 193659024.0m, CantidadRegistros = 31, Periodo = 202311, ActualizadoEn = now },
                    new Deudor { NroIdentificacion = "30685376349", SituacionMaxima = 1, SumaTotalPrestamos = 167862552.0m, CantidadRegistros = 23, Periodo = 202311, ActualizadoEn = now },
                    new Deudor { NroIdentificacion = "20111111112", SituacionMaxima = 5, SumaTotalPrestamos = 10.0m, CantidadRegistros = 1, Periodo = 202311, ActualizadoEn = now });
                var galicia = db.Entidades.Single(e => e.CodigoEntidad == 7);
                galicia.SumaTotalPrestamos = 2167457645.0m;
                db.SaveChanges();
            });
        }
    }
}
