using Microsoft.EntityFrameworkCore;
using Wayni.Deudores.Infrastructure.Persistence.Entities;
using Wayni.Deudores.Infrastructure.Persistence.Seed;
// Alias: inside this class "Situaciones" is the DbSet property, which hides the Core catalogue class (CS1061).
using SituacionesCatalogo = Wayni.Deudores.Core.Domain.Situaciones;

namespace Wayni.Deudores.Infrastructure.Persistence;

/// <summary>
/// EF Core model of the MySQL database <c>wayni_deudores</c>. Table and column names follow the
/// challenge (snake_case: nro_identificacion, situacion_maxima, suma_total_prestamos, codigo_entidad).
/// Schema and seed data live in <c>Persistence/Migrations</c>.
/// </summary>
public class WayniDbContext(DbContextOptions<WayniDbContext> options) : DbContext(options)
{
    public DbSet<Deudor> Deudores => Set<Deudor>();
    public DbSet<Entidad> Entidades => Set<Entidad>();
    public DbSet<Situacion> Situaciones => Set<Situacion>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasCharSet("utf8mb4");

        modelBuilder.Entity<Deudor>(e =>
        {
            e.ToTable("deudores");
            e.HasKey(x => x.NroIdentificacion);
            e.Property(x => x.NroIdentificacion).HasColumnName("nro_identificacion").HasColumnType("varchar(11)").HasMaxLength(11);
            e.Property(x => x.SituacionMaxima).HasColumnName("situacion_maxima");
            e.Property(x => x.SumaTotalPrestamos).HasColumnName("suma_total_prestamos").HasPrecision(18, 1);
            e.Property(x => x.CantidadRegistros).HasColumnName("cantidad_registros");
            e.Property(x => x.Periodo).HasColumnName("periodo");
            e.Property(x => x.ActualizadoEn).HasColumnName("actualizado_en").HasColumnType("datetime(6)");

            // GET /deudores/top/{n} and its situación filter.
            e.HasIndex(x => x.SumaTotalPrestamos).IsDescending().HasDatabaseName("ix_deudores_suma_total_prestamos");
            e.HasIndex(x => new { x.SituacionMaxima, x.SumaTotalPrestamos })
                .IsDescending(false, true)
                .HasDatabaseName("ix_deudores_situacion_maxima_suma_total_prestamos");
        });

        modelBuilder.Entity<Entidad>(e =>
        {
            e.ToTable("entidades");
            e.HasKey(x => x.CodigoEntidad);
            e.Property(x => x.CodigoEntidad).HasColumnName("codigo_entidad").ValueGeneratedNever();
            e.Property(x => x.Nombre).HasColumnName("nombre").HasColumnType("varchar(70)").HasMaxLength(70);
            e.Property(x => x.SumaTotalPrestamos).HasColumnName("suma_total_prestamos").HasPrecision(18, 1);
            e.Property(x => x.CantidadRegistros).HasColumnName("cantidad_registros");
            e.Property(x => x.Periodo).HasColumnName("periodo");
            e.Property(x => x.ActualizadoEn).HasColumnName("actualizado_en").HasColumnType("datetime(6)");

            e.HasData(EntidadesSeed.Items.Select(i => new Entidad
            {
                CodigoEntidad = i.Codigo,
                Nombre = i.Nombre,
                SumaTotalPrestamos = 0m,
                CantidadRegistros = 0,
            }));
        });

        modelBuilder.Entity<Situacion>(e =>
        {
            e.ToTable("situaciones");
            e.HasKey(x => x.Codigo);
            e.Property(x => x.Codigo).HasColumnName("codigo").ValueGeneratedNever();
            e.Property(x => x.Descripcion).HasColumnName("descripcion").HasColumnType("varchar(100)").HasMaxLength(100);
            e.HasData(SituacionesCatalogo.Descripciones
                .OrderBy(s => s.Key)
                .Select(s => new Situacion { Codigo = s.Key, Descripcion = s.Value }));
        });

        modelBuilder.Entity<ImportJob>(e =>
        {
            e.ToTable("import_jobs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.FileName).HasColumnName("file_name").HasColumnType("varchar(255)").HasMaxLength(255);
            e.Property(x => x.Bucket).HasColumnName("bucket").HasColumnType("varchar(63)").HasMaxLength(63);
            e.Property(x => x.ObjectKey).HasColumnName("object_key").HasColumnType("varchar(512)").HasMaxLength(512);
            e.Property(x => x.Status).HasColumnName("status").HasColumnType("varchar(20)").HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
            e.Property(x => x.StartedAt).HasColumnName("started_at").HasColumnType("datetime(6)");
            e.Property(x => x.FinishedAt).HasColumnName("finished_at").HasColumnType("datetime(6)");
            e.Property(x => x.Periodo).HasColumnName("periodo");
            e.Property(x => x.LineasLeidas).HasColumnName("lineas_leidas");
            e.Property(x => x.LineasInvalidas).HasColumnName("lineas_invalidas");
            e.Property(x => x.Deudores).HasColumnName("deudores");
            e.Property(x => x.Entidades).HasColumnName("entidades");
            e.Property(x => x.TotalPrestamos).HasColumnName("total_prestamos").HasPrecision(20, 1);
            e.Property(x => x.DuracionMs).HasColumnName("duracion_ms");
            e.Property(x => x.Error).HasColumnName("error").HasColumnType("varchar(2000)").HasMaxLength(2000);
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_import_jobs_created_at");
        });
    }
}
