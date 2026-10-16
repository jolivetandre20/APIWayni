using Microsoft.EntityFrameworkCore;
using Wayni.Deudores.Infrastructure.Persistence.Entities;

namespace Wayni.Deudores.Infrastructure.Persistence.Repositories;

/// <summary>Read side used by the Query API (GET /deudores, /entidades, /deudores/top).</summary>
public interface IDeudoresQueryRepository
{
    Task<Deudor?> GetDeudorAsync(string nroIdentificacion, CancellationToken ct);
    Task<Entidad?> GetEntidadAsync(int codigo, CancellationToken ct);
    Task<IReadOnlyList<Deudor>> GetTopDeudoresAsync(int n, int? situacion, CancellationToken ct);
    Task<IReadOnlyList<Entidad>> GetEntidadesAsync(int take, CancellationToken ct);
    Task<IReadOnlyList<Situacion>> GetSituacionesAsync(CancellationToken ct);
}

public sealed class DeudoresQueryRepository(WayniDbContext db) : IDeudoresQueryRepository
{
    public Task<Deudor?> GetDeudorAsync(string nroIdentificacion, CancellationToken ct) =>
        db.Deudores.AsNoTracking().FirstOrDefaultAsync(d => d.NroIdentificacion == nroIdentificacion, ct);

    public Task<Entidad?> GetEntidadAsync(int codigo, CancellationToken ct) =>
        db.Entidades.AsNoTracking().FirstOrDefaultAsync(e => e.CodigoEntidad == codigo, ct);

    /// <summary>
    /// Top N by suma_total_prestamos, optionally filtered by situacion_maxima. Served by the
    /// descending indexes ix_deudores_suma_total_prestamos / ix_deudores_situacion_maxima_suma_total_prestamos.
    /// </summary>
    public async Task<IReadOnlyList<Deudor>> GetTopDeudoresAsync(int n, int? situacion, CancellationToken ct)
    {
        var query = db.Deudores.AsNoTracking();
        if (situacion is not null)
            query = query.Where(d => d.SituacionMaxima == situacion);
        return await query
            .OrderByDescending(d => d.SumaTotalPrestamos)
            .ThenBy(d => d.NroIdentificacion)
            .Take(n)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Entidad>> GetEntidadesAsync(int take, CancellationToken ct) =>
        await db.Entidades.AsNoTracking()
            .OrderByDescending(e => e.SumaTotalPrestamos)
            .ThenBy(e => e.CodigoEntidad)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Situacion>> GetSituacionesAsync(CancellationToken ct) =>
        await db.Situaciones.AsNoTracking().OrderBy(s => s.Codigo).ToListAsync(ct);
}
