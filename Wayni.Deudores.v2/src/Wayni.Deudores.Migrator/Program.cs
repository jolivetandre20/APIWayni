using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wayni.Deudores.Infrastructure;
using Wayni.Deudores.Infrastructure.Persistence;

// Applies Persistence/Migrations (InitialCreate: tables, indexes, situaciones + 485 entidades seed).
// docker-compose starts the APIs only after this container exits with code 0.
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddJsonConsole();
builder.Services.AddWayniPersistence(builder.Configuration);

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Migrator");

for (var attempt = 1; ; attempt++)
{
    try
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WayniDbContext>();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        logger.LogInformation("Pending migrations: {Count} {Migrations}", pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync();
        logger.LogInformation("Database is up to date ({Entidades} entidades, {Situaciones} situaciones)",
            await db.Entidades.CountAsync(), await db.Situaciones.CountAsync());
        return 0;
    }
    catch (Exception ex) when (attempt < 30)
    {
        logger.LogWarning("MySQL not ready (attempt {Attempt}/30): {Message}", attempt, ex.Message);
        await Task.Delay(TimeSpan.FromSeconds(3));
    }
}
