namespace Wayni.Deudores.Core.Parsing;

/// <summary>
/// Fixed-width layout of deudores.txt (LEAME DEUDORES.pdf, "Diseño de registro").
/// Offsets are zero-based; only the fields used by the business rules are listed.
/// A complete record is 171 characters (+ CRLF).
/// </summary>
public static class DeudoresLayout
{
    public const int CodigoEntidadStart = 0, CodigoEntidadLength = 5;            // Campo 1
    public const int PeriodoStart = 5, PeriodoLength = 6;                        // Campo 2
    public const int TipoIdentificacionStart = 11, TipoIdentificacionLength = 2; // Campo 3
    public const int NroIdentificacionStart = 13, NroIdentificacionLength = 11;  // Campo 4
    public const int ActividadStart = 24, ActividadLength = 3;                   // Campo 5 (not used)
    public const int SituacionStart = 27, SituacionLength = 2;                   // Campo 6
    public const int PrestamosStart = 29, PrestamosLength = 12;                  // Campo 7

    /// <summary>Minimum length that still contains fields 1 to 7.</summary>
    public const int MinimumLength = PrestamosStart + PrestamosLength;           // 41

    /// <summary>Length of a complete record (fields 1 to 24).</summary>
    public const int RecordLength = 171;
}
