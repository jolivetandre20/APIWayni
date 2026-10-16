namespace Wayni.Deudores.Core.Parsing;

/// <summary>
/// The fields of one deudores.txt line that the business rules need
/// (BCRA "Central de deudores del sistema financiero", LEAME DEUDORES.pdf).
/// </summary>
/// <param name="CodigoEntidad">Campo 1 - código de la entidad (5).</param>
/// <param name="Periodo">Campo 2 - fecha de información AAAAMM (6).</param>
/// <param name="TipoIdentificacion">Campo 3 - 11 = CUIT/CUIL/CDI (2).</param>
/// <param name="NroIdentificacion">Campo 4 - CUIT/CUIL (11) as a number when it is all digits (99.99% of the file).</param>
/// <param name="Situacion">Campo 6 - situación (2): 1, 21, 23, 3, 4, 5, 11.</param>
/// <param name="Prestamos">Campo 7 - préstamos / total de garantías afrontadas, in thousands of pesos with one decimal (12).</param>
/// <param name="NroIdentificacionTexto">
/// Campo 4 when it is not numeric. Campo 4 is "Carácter": foreign debtors (tipo 98) carry
/// alphanumeric ids such as "00007CN0001" (285 lines in the November 2023 file). Null otherwise.
/// </param>
public readonly record struct DeudorRecord(
    int CodigoEntidad,
    int Periodo,
    int TipoIdentificacion,
    long NroIdentificacion,
    int Situacion,
    decimal Prestamos,
    string? NroIdentificacionTexto = null)
{
    /// <summary>Campo 4 as text (11 characters), whatever its kind.</summary>
    public string Identificacion => NroIdentificacionTexto ?? DeudoresLineParser.FormatIdentificacion(NroIdentificacion);
}
