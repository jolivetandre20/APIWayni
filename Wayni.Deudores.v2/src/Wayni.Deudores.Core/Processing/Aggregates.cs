namespace Wayni.Deudores.Core.Processing;

/// <summary>Running totals for one CUIT/CUIL (structure "Deudores" of the challenge).</summary>
/// <remarks>
/// Amounts are accumulated as an integer number of tenths (campo 7 has exactly one decimal),
/// which keeps the struct at 16 bytes: the November 2023 file has ~20 million distinct CUITs.
/// </remarks>
public struct DeudorAggregate
{
    /// <summary>Máximo valor del campo 6 entre los registros del mismo CUIT.</summary>
    public int SituacionMaxima;

    /// <summary>Records (entity lines) found for this CUIT.</summary>
    public int Registros;

    /// <summary>Suma del campo 7, in tenths (2406,0 is stored as 24060).</summary>
    public long SumaDecimos;

    /// <summary>Suma del campo 7 de todos los registros del mismo CUIT (miles de pesos, un decimal).</summary>
    public readonly decimal SumaTotalPrestamos => SumaDecimos / 10m;
}

/// <summary>Running totals for one entity (structure "Entidades" of the challenge).</summary>
public struct EntidadAggregate
{
    /// <summary>Records reported by this entity.</summary>
    public int Registros;

    /// <summary>Suma del campo 7 agrupado por entidad, in tenths.</summary>
    public long SumaDecimos;

    /// <summary>Suma del campo 7 agrupado por código de entidad (miles de pesos, un decimal).</summary>
    public readonly decimal SumaTotalPrestamos => SumaDecimos / 10m;
}
