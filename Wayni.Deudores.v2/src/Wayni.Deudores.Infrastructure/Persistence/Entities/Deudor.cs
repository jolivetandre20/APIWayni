namespace Wayni.Deudores.Infrastructure.Persistence.Entities;

/// <summary>Table <c>deudores</c>: one row per CUIT/CUIL (campo 4).</summary>
public class Deudor
{
    /// <summary>nro_identificacion - campo 4 (11 characters).</summary>
    public string NroIdentificacion { get; set; } = string.Empty;

    /// <summary>situacion_maxima - máximo valor del campo 6 entre los registros del CUIT.</summary>
    public int SituacionMaxima { get; set; }

    /// <summary>suma_total_prestamos - suma del campo 7 (miles de pesos, un decimal).</summary>
    public decimal SumaTotalPrestamos { get; set; }

    /// <summary>Lines of the file (one per reporting entity) that make up the totals.</summary>
    public int CantidadRegistros { get; set; }

    /// <summary>Campo 2 (AAAAMM) of the file the row comes from.</summary>
    public int Periodo { get; set; }

    public DateTime ActualizadoEn { get; set; }
}
