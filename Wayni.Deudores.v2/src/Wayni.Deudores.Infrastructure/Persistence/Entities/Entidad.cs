namespace Wayni.Deudores.Infrastructure.Persistence.Entities;

/// <summary>Table <c>entidades</c>: one row per reporting entity (campo 1), seeded from Maeent.txt.</summary>
public class Entidad
{
    /// <summary>codigo_entidad - campo 1.</summary>
    public int CodigoEntidad { get; set; }

    /// <summary>Nombre from the BCRA entity master (Maeent.txt); null for codes missing there.</summary>
    public string? Nombre { get; set; }

    /// <summary>suma_total_prestamos - suma del campo 7 agrupado por entidad (miles de pesos, un decimal).</summary>
    public decimal SumaTotalPrestamos { get; set; }

    /// <summary>Lines reported by the entity in the last imported file.</summary>
    public int CantidadRegistros { get; set; }

    /// <summary>Campo 2 (AAAAMM) of the last imported file; null until the first import.</summary>
    public int? Periodo { get; set; }

    public DateTime? ActualizadoEn { get; set; }
}
