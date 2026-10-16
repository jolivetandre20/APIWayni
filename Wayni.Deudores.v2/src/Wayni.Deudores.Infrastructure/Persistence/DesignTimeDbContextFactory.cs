using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wayni.Deudores.Infrastructure.Persistence;

/// <summary>
/// Used by the dotnet-ef CLI (migrations add / script / database update). The connection string
/// comes from WAYNI_DB_CONNECTION; the default targets the docker-compose MySQL published on localhost.
/// No connection is opened to create a migration: the server version is fixed.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<WayniDbContext>
{
    public WayniDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("WAYNI_DB_CONNECTION")
            ?? "Server=localhost;Port=3306;Database=wayni_deudores;User=wayni;Password=<DB_PASSWORD>";
        var options = new DbContextOptionsBuilder<WayniDbContext>()
            .UseMySql(connectionString, PersistenceDefaults.ServerVersion)
            .Options;
        return new WayniDbContext(options);
    }
}
