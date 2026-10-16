using System.Globalization;

namespace Wayni.Deudores.Core.Parsing;

/// <summary>
/// Parses one fixed-width line of deudores.txt. Works on spans (no allocations per line)
/// because the real file has ~34 million lines.
/// </summary>
/// <remarks>
/// Observed in the November 2023 file: numeric fields are left-aligned and padded with blanks
/// ("1 " for situación 1) and amounts use a comma as decimal separator ("2406,0", ",0").
/// </remarks>
public static class DeudoresLineParser
{
    public static bool TryParse(ReadOnlySpan<char> line, out DeudorRecord record, out string? error)
    {
        record = default;
        line = line.TrimEnd("\r\n");

        if (line.IsWhiteSpace())
        {
            error = "empty line";
            return false;
        }

        if (line.Length < DeudoresLayout.MinimumLength)
        {
            error = $"line too short ({line.Length} < {DeudoresLayout.MinimumLength})";
            return false;
        }

        if (!TryParseInt(Field(line, DeudoresLayout.CodigoEntidadStart, DeudoresLayout.CodigoEntidadLength), out var entidad))
        {
            error = "campo 1 (código de entidad) is not numeric";
            return false;
        }

        if (!TryParseInt(Field(line, DeudoresLayout.PeriodoStart, DeudoresLayout.PeriodoLength), out var periodo)
            || periodo < 190001 || periodo % 100 is < 1 or > 12)
        {
            error = "campo 2 (fecha de información AAAAMM) is invalid";
            return false;
        }

        if (!TryParseInt(Field(line, DeudoresLayout.TipoIdentificacionStart, DeudoresLayout.TipoIdentificacionLength), out var tipo))
        {
            error = "campo 3 (tipo de identificación) is not numeric";
            return false;
        }

        // Campo 4 is "Carácter": numeric CUIT/CUIL for tipo 11, alphanumeric for foreign ids (tipo 98).
        var id = Field(line, DeudoresLayout.NroIdentificacionStart, DeudoresLayout.NroIdentificacionLength);
        long nro = 0;
        string? texto = null;
        if (id.Length != DeudoresLayout.NroIdentificacionLength || !IsAlphanumeric(id))
        {
            error = "campo 4 (número de identificación) must be 11 letters or digits";
            return false;
        }
        if (!IsDigits(id) || !long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out nro))
            texto = id.ToString();

        if (!TryParseInt(Field(line, DeudoresLayout.SituacionStart, DeudoresLayout.SituacionLength), out var situacion))
        {
            error = "campo 6 (situación) is not numeric";
            return false;
        }

        if (!TryParseAmount(Field(line, DeudoresLayout.PrestamosStart, DeudoresLayout.PrestamosLength), out var prestamos))
        {
            error = "campo 7 (préstamos) is not a valid amount";
            return false;
        }

        record = new DeudorRecord(entidad, periodo, tipo, nro, situacion, prestamos, texto);
        error = null;
        return true;
    }

    /// <summary>Formats a CUIT/CUIL back to its 11-digit text form ("20003905528").</summary>
    public static string FormatIdentificacion(long nro) => nro.ToString("D11", CultureInfo.InvariantCulture);

    /// <summary>True for an 11-character identification (digits for CUIT/CUIL, letters allowed for foreign ids).</summary>
    public static bool IsValidIdentificacion(string? text) =>
        text is { Length: DeudoresLayout.NroIdentificacionLength } && IsAlphanumeric(text);

    /// <summary>Parses "2406,0", ",0", "1,0" or "2406.0" (thousands of pesos, one decimal). Blank = 0.</summary>
    public static bool TryParseAmount(ReadOnlySpan<char> text, out decimal value)
    {
        text = text.Trim();
        if (text.IsEmpty)
        {
            value = 0m;
            return true;
        }

        if (text.Length > 32)
        {
            value = 0m;
            return false;
        }

        Span<char> buffer = stackalloc char[text.Length];
        text.CopyTo(buffer);
        var comma = buffer.IndexOf(',');
        if (comma >= 0)
            buffer[comma] = '.';

        return decimal.TryParse(buffer, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out value);
    }

    private static ReadOnlySpan<char> Field(ReadOnlySpan<char> line, int start, int length) =>
        line.Slice(start, Math.Min(length, line.Length - start)).Trim();

    private static bool TryParseInt(ReadOnlySpan<char> text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    private static bool IsAlphanumeric(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (c is not ((>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')))
                return false;
        }
        return !text.IsEmpty;
    }

    private static bool IsDigits(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (c is < '0' or > '9')
                return false;
        }
        return !text.IsEmpty;
    }
}
