using System.IO.Compression;
using Wayni.Deudores.Infrastructure.Files;

namespace Wayni.Deudores.Tests.Files;

public class DeudoresFileOpenerTests
{
    [Theory]
    [InlineData("deudores.txt", true)]
    [InlineData("202311DEUDORES_20240122204012.7Z", true)]
    [InlineData("deudores.zip", true)]
    [InlineData("deudores.csv", false)]
    public void Knows_the_supported_types(string name, bool supported) =>
        Assert.Equal(supported, DeudoresFileOpener.IsSupported(name));

    [Fact]
    public void Opens_deudores_txt_inside_a_zip_like_the_bcra_package()
    {
        var zipPath = Path.Combine(Path.GetTempPath(), $"wayni-test-{Guid.NewGuid():N}.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zip.CreateEntry("Fecha_Proceso_20240118.txt");
            using var writer = new StreamWriter(zip.CreateEntry("DEUDORES.TXT").Open());
            writer.Write("0000720231111200058945820141 2406,0      ,0\r\n");
        }

        using (var stream = DeudoresFileOpener.OpenFromZip(zipPath))
        using (var reader = new StreamReader(stream))
        {
            Assert.StartsWith("00007202311", reader.ReadLine());
        }

        File.Delete(zipPath);
    }
}
