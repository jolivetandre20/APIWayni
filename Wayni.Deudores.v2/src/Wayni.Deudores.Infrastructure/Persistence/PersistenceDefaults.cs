using Microsoft.EntityFrameworkCore;

namespace Wayni.Deudores.Infrastructure.Persistence;

public static class PersistenceDefaults
{
    /// <summary>
    /// The docker-compose image is mysql:8.4. Fixed instead of ServerVersion.AutoDetect so that
    /// services and the EF CLI never need a live connection just to build the model.
    /// </summary>
    public static readonly ServerVersion ServerVersion = new MySqlServerVersion(new Version(8, 4, 0));
}
