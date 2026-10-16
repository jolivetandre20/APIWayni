using System.Text;
using Wayni.Deudores.Core.Parsing;
using Wayni.Deudores.Core.Processing;

namespace Wayni.Deudores.Tests.Processing;

public class DeudoresAggregatorTests
{
    private static DeudorRecord R(int entidad, long cuit, int situacion, decimal prestamos) =>
        new(entidad, 202311, 11, cuit, situacion, prestamos);

    [Fact]
    public void Deudores_take_the_maximum_situacion_and_the_sum_of_prestamos()
    {
        var aggregator = new DeudoresAggregator();
        aggregator.Add(R(7, 20111111112, 1, 100.5m));
        aggregator.Add(R(11, 20111111112, 4, 50.0m));
        aggregator.Add(R(14, 20111111112, 3, 0.5m));
        aggregator.Add(R(7, 27222222223, 1, 10.0m));

        var deudores = aggregator.EnumerateDeudores().ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal(2, deudores.Count);
        Assert.Equal(4, deudores["20111111112"].SituacionMaxima);
        Assert.Equal(151.0m, deudores["20111111112"].SumaTotalPrestamos);
        Assert.Equal(3, deudores["20111111112"].Registros);
        Assert.Equal(1, deudores["27222222223"].SituacionMaxima);
    }

    [Fact]
    public void Entidades_sum_prestamos_grouped_by_codigo()
    {
        var aggregator = new DeudoresAggregator();
        aggregator.Add(R(7, 20111111112, 1, 100.5m));
        aggregator.Add(R(7, 27222222223, 1, 10.0m));
        aggregator.Add(R(11, 20111111112, 4, 50.0m));

        Assert.Equal(110.5m, aggregator.Entidades[7].SumaTotalPrestamos);
        Assert.Equal(2, aggregator.Entidades[7].Registros);
        Assert.Equal(50.0m, aggregator.Entidades[11].SumaTotalPrestamos);
        Assert.Equal(160.5m, aggregator.TotalPrestamos);
    }

    [Fact]
    public void Alphanumeric_and_numeric_ids_are_kept_apart()
    {
        var aggregator = new DeudoresAggregator();
        aggregator.Add(new DeudorRecord(7, 202311, 98, 0, 1, 44m, "00007CN0001"));
        aggregator.Add(new DeudorRecord(7, 202311, 98, 0, 3, 1m, "00007CN0001"));
        aggregator.Add(R(7, 20111111112, 1, 5m));

        var deudores = aggregator.EnumerateDeudores().ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal(2, aggregator.CantidadDeudores);
        Assert.Equal(3, deudores["00007CN0001"].SituacionMaxima);
        Assert.Equal(45m, deudores["00007CN0001"].SumaTotalPrestamos);
    }

    [Fact]
    public void Invalid_lines_are_counted_and_sampled_not_thrown()
    {
        var aggregator = new DeudoresAggregator(maxInvalidSamples: 1);
        Assert.False(aggregator.Add("garbage"));
        Assert.False(aggregator.Add("more garbage"));
        Assert.Equal(2, aggregator.LineasInvalidas);
        Assert.Single(aggregator.InvalidSamples);
        Assert.Equal(1, aggregator.InvalidSamples[0].LineNumber);
    }

    [Fact]
    public async Task Reads_the_real_sample_file()
    {
        // First 2,000 lines of the November 2023 deudores.txt.
        await using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestData", "deudores_sample.txt"));
        var aggregator = new DeudoresAggregator();
        var summary = await aggregator.ReadAllAsync(file);

        Assert.Equal(2000, summary.LineasLeidas);
        Assert.Equal(0, summary.LineasInvalidas);
        Assert.Equal(202311, summary.Periodo);
        Assert.True(summary.Deudores is > 0 and <= 2000);
        Assert.Equal(summary.TotalPrestamos, aggregator.Entidades.Values.Sum(e => e.SumaTotalPrestamos));
        Assert.Equal(summary.TotalPrestamos, aggregator.EnumerateDeudores().Sum(d => d.Value.SumaTotalPrestamos));
    }

    [Fact]
    public async Task Reads_latin1_with_crlf()
    {
        var text = "0000720231111200058945820141 2406,0      ,0\r\n0001120231111200058945820143 1,5         ,0\r\n";
        await using var stream = new MemoryStream(Encoding.Latin1.GetBytes(text));
        var aggregator = new DeudoresAggregator();
        var summary = await aggregator.ReadAllAsync(stream);

        Assert.Equal(2, summary.LineasValidas);
        var deudor = Assert.Single(aggregator.EnumerateDeudores());
        Assert.Equal(3, deudor.Value.SituacionMaxima);
        Assert.Equal(2407.5m, deudor.Value.SumaTotalPrestamos);
    }
}
