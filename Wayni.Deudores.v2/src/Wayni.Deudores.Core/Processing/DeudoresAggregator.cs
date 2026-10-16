using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Wayni.Deudores.Core.Parsing;

namespace Wayni.Deudores.Core.Processing;

/// <summary>
/// Applies the business rules of the challenge while streaming the file once:
/// <list type="bullet">
///   <item>Deudores: nro_identificacion (campo 4), situacion_maxima = MAX(campo 6), suma_total_prestamos = SUM(campo 7).</item>
///   <item>Entidades: codigo_entidad (campo 1), suma_total_prestamos = SUM(campo 7) grouped by entity.</item>
/// </list>
/// Memory grows with distinct CUITs (32 bytes each in the dictionary), not with the file size;
/// pass <c>expectedDeudores</c> to pre-size it and avoid re-hash peaks (~25 million for a full file).
/// </summary>
public sealed class DeudoresAggregator
{
    private readonly Dictionary<long, DeudorAggregate> _deudores;
    private readonly Dictionary<string, DeudorAggregate> _deudoresAlfanumericos = new(StringComparer.Ordinal);
    private readonly Dictionary<int, EntidadAggregate> _entidades = new();
    private readonly List<InvalidLine> _invalidSamples = new();
    private readonly int _maxInvalidSamples;

    public DeudoresAggregator(int expectedDeudores = 0, int maxInvalidSamples = 20)
    {
        _deudores = new Dictionary<long, DeudorAggregate>(expectedDeudores);
        _maxInvalidSamples = maxInvalidSamples;
    }

    public long LineasLeidas { get; private set; }
    public long LineasValidas { get; private set; }
    public long LineasInvalidas { get; private set; }
    private long _totalDecimos;

    public decimal TotalPrestamos => _totalDecimos / 10m;
    public int? Periodo { get; private set; }
    public IReadOnlyList<InvalidLine> InvalidSamples => _invalidSamples;
    /// <summary>Distinct identifications (numeric and alphanumeric).</summary>
    public int CantidadDeudores => _deudores.Count + _deudoresAlfanumericos.Count;
    public IReadOnlyDictionary<int, EntidadAggregate> Entidades => _entidades;

    /// <summary>Adds one raw line. Returns false (and counts it) when the line is invalid.</summary>
    public bool Add(ReadOnlySpan<char> line)
    {
        LineasLeidas++;
        if (!DeudoresLineParser.TryParse(line, out var record, out var error))
        {
            LineasInvalidas++;
            if (_invalidSamples.Count < _maxInvalidSamples)
                _invalidSamples.Add(new InvalidLine(LineasLeidas, error ?? "invalid",
                    line.Length > 60 ? line[..60].ToString() : line.ToString()));
            return false;
        }

        Add(record);
        return true;
    }

    /// <summary>Adds one already-parsed record.</summary>
    public void Add(in DeudorRecord record)
    {
        LineasValidas++;
        var decimos = ToDecimos(record.Prestamos);
        _totalDecimos += decimos;
        Periodo ??= record.Periodo;

        ref var deudor = ref record.NroIdentificacionTexto is null
            ? ref CollectionsMarshal.GetValueRefOrAddDefault(_deudores, record.NroIdentificacion, out var existed)
            : ref CollectionsMarshal.GetValueRefOrAddDefault(_deudoresAlfanumericos, record.NroIdentificacionTexto, out existed);
        deudor.SituacionMaxima = existed ? Math.Max(deudor.SituacionMaxima, record.Situacion) : record.Situacion;
        deudor.SumaDecimos += decimos;
        deudor.Registros++;

        ref var entidad = ref CollectionsMarshal.GetValueRefOrAddDefault(_entidades, record.CodigoEntidad, out _);
        entidad.SumaDecimos += decimos;
        entidad.Registros++;
    }

    /// <summary>All debtors with their 11-character identification (campo 4).</summary>
    public IEnumerable<KeyValuePair<string, DeudorAggregate>> EnumerateDeudores()
    {
        foreach (var (nro, aggregate) in _deudores)
            yield return new(DeudoresLineParser.FormatIdentificacion(nro), aggregate);
        foreach (var pair in _deudoresAlfanumericos)
            yield return pair;
    }

    /// <summary>Streams a whole file (deudores.txt is ISO-8859-1 with CRLF line ends).</summary>
    public async Task<AggregationSummary> ReadAllAsync(Stream stream, IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        using var reader = new StreamReader(stream, Encoding.Latin1, detectEncodingFromByteOrderMarks: false, bufferSize: 1 << 16);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            Add(line);
            if (progress is not null && LineasLeidas % 1_000_000 == 0)
                progress.Report(LineasLeidas);
        }

        return Summary(watch.Elapsed);
    }

    /// <summary>Campo 7 has one decimal ("Once enteros y un decimal"); store it as tenths.</summary>
    public static long ToDecimos(decimal amount) => (long)decimal.Round(amount * 10m, MidpointRounding.AwayFromZero);

    public AggregationSummary Summary(TimeSpan elapsed) => new(
        LineasLeidas, LineasValidas, LineasInvalidas, CantidadDeudores, _entidades.Count, TotalPrestamos, Periodo, elapsed);
}

public sealed record InvalidLine(long LineNumber, string Error, string Excerpt);

public sealed record AggregationSummary(
    long LineasLeidas,
    long LineasValidas,
    long LineasInvalidas,
    int Deudores,
    int Entidades,
    decimal TotalPrestamos,
    int? Periodo,
    TimeSpan Duracion);
