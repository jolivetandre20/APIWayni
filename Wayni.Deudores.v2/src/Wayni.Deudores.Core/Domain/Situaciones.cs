namespace Wayni.Deudores.Core.Domain;

/// <summary>Situación codes of campo 6 (LEAME DEUDORES.pdf, note (*)).</summary>
public static class Situaciones
{
    public static readonly IReadOnlyDictionary<int, string> Descripciones = new Dictionary<int, string>
    {
        [1] = "Situación Normal",
        // The November 2023 file reports "2" (587,960 debtors) rather than 21/23: it is the
        // historical code for "Con seguimiento especial / Riesgo bajo" (BCRA T.O., apartado B, punto 1).
        [2] = "Con seguimiento especial / Riesgo Bajo",
        [21] = "Riesgo Bajo",
        [23] = "En tratamiento especial",
        [3] = "Riesgo Medio",
        [4] = "Riesgo Alto",
        [5] = "Irrecuperable",
        [11] = "Con asistencias cubiertas en su totalidad con garantías preferidas \"A\"",
    };

    public static string Describe(int codigo) =>
        Descripciones.TryGetValue(codigo, out var text) ? text : "Desconocida";
}
