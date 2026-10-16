using Wayni.Deudores.Core.Parsing;

namespace Wayni.Deudores.Tests.Parsing;

public class DeudoresLineParserTests
{
    // Real lines of deudores.txt (November 2023, BCRA).
    private const string Galicia = "0000720231111200058945820141 2406,0      ,0          ,0          ,0          ,0          ,0          2406,0      ,0          ,0          ,0          0           0000000   ";
    private const string Foreign = "000072023119800007CN00016411 44,0        ,0          108992,0    ,0          ,0          ,0          ,0          ,0          ,0          ,0          0           0000000   ";

    [Fact]
    public void Parses_the_fields_used_by_the_business_rules()
    {
        Assert.True(DeudoresLineParser.TryParse(Galicia, out var record, out var error));
        Assert.Null(error);
        Assert.Equal(7, record.CodigoEntidad);
        Assert.Equal(202311, record.Periodo);
        Assert.Equal(11, record.TipoIdentificacion);
        Assert.Equal(20005894582L, record.NroIdentificacion);
        Assert.Equal("20005894582", record.Identificacion);
        Assert.Equal(1, record.Situacion);       // "1 " - left-aligned, blank padded
        Assert.Equal(2406.0m, record.Prestamos); // "2406,0" - comma decimal separator
    }

    [Fact]
    public void Accepts_alphanumeric_ids_of_foreign_debtors()
    {
        Assert.True(DeudoresLineParser.TryParse(Foreign, out var record, out _));
        Assert.Equal(98, record.TipoIdentificacion);
        Assert.Equal("00007CN0001", record.Identificacion);
        Assert.Equal(1, record.Situacion);
        Assert.Equal(44.0m, record.Prestamos);
    }

    [Theory]
    [InlineData(",0", 0)]
    [InlineData("1,0", 1)]
    [InlineData("117,5", 117.5)]
    [InlineData("10317199,0  ", 10317199)]
    [InlineData("", 0)]
    [InlineData("12.5", 12.5)]
    public void Parses_amounts(string text, double expected)
    {
        Assert.True(DeudoresLineParser.TryParseAmount(text, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("00007202311")]
    [InlineData("XXXXX20231111200058945820141 2406,0      ")]
    [InlineData("0000720231311200058945820141 2406,0      ")] // month 13
    [InlineData("000072023111120005894-820141 2406,0      ")]
    [InlineData("0000720231111200058945820141 24x6,0      ")]
    public void Rejects_invalid_lines(string line)
    {
        Assert.False(DeudoresLineParser.TryParse(line, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Ignores_trailing_crlf()
    {
        Assert.True(DeudoresLineParser.TryParse(Galicia + "\r\n", out var record, out _));
        Assert.Equal(2406.0m, record.Prestamos);
    }

    [Theory]
    [InlineData("20005894582", true)]
    [InlineData("00007CN0001", true)]
    [InlineData("2000589458", false)]
    [InlineData("20-00589458", false)]
    [InlineData(null, false)]
    public void Validates_identifications(string? text, bool valid) =>
        Assert.Equal(valid, DeudoresLineParser.IsValidIdentificacion(text));
}
