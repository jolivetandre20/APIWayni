using System.Data.Common;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Wayni.Deudores.Core.Processing;
using Wayni.Deudores.Infrastructure.Options;

namespace Wayni.Deudores.Infrastructure.Persistence.Repositories;

/// <summary>Write side used by the Processor: "Inserta o actualiza los datos en la base seleccionada".</summary>
public interface IImportWriter
{
    /// <summary>Upserts every debtor and entity of the aggregation. Returns the rows sent per table.</summary>
    Task<(long Deudores, int Entidades)> WriteAsync(DeudoresAggregator aggregation, CancellationToken ct);
}

/// <summary>
/// Upsert semantics: a re-imported CUIT/entity takes the values of the new file (snapshot of the period);
/// rows not present in the new file are kept. Entity names (seeded from Maeent.txt) are never overwritten.
/// </summary>
public sealed class MySqlImportWriter(WayniDbContext db, IOptions<PersistenceOptions> options, ILogger<MySqlImportWriter> logger)
    : IImportWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly PersistenceOptions _options = options.Value;

    public async Task<(long Deudores, int Entidades)> WriteAsync(DeudoresAggregator aggregation, CancellationToken ct)
    {
        var periodo = aggregation.Periodo ?? 0;
        var now = DateTime.UtcNow;

        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (MySqlConnection)db.Database.GetDbConnection();

            var deudores = _options.UseBulkLoad
                ? await BulkUpsertDeudoresAsync(connection, aggregation, periodo, now, ct)
                : await BatchUpsertDeudoresAsync(connection, aggregation, periodo, now, ct);

            var entidades = await UpsertEntidadesAsync(connection, aggregation, periodo, now, ct);
            return (deudores, entidades);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<long> BulkUpsertDeudoresAsync(MySqlConnection connection, DeudoresAggregator aggregation,
        int periodo, DateTime now, CancellationToken ct)
    {
        await ExecuteAsync(connection, """
            CREATE TEMPORARY TABLE IF NOT EXISTS tmp_deudores (
                nro_identificacion varchar(11) NOT NULL PRIMARY KEY,
                situacion_maxima int NOT NULL,
                suma_total_prestamos decimal(18,1) NOT NULL,
                cantidad_registros int NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, ct);

        long total = 0;
        var chunk = new StringBuilder(capacity: _options.BulkChunkSize * 32);
        var rows = 0;

        foreach (var (nro, deudor) in aggregation.EnumerateDeudores())
        {
            chunk.Append(nro).Append('\t')
                .Append(deudor.SituacionMaxima.ToString(Inv)).Append('\t')
                .Append(deudor.SumaTotalPrestamos.ToString("0.0", Inv)).Append('\t')
                .Append(deudor.Registros.ToString(Inv)).Append('\n');

            if (++rows == _options.BulkChunkSize)
            {
                total += await FlushChunkAsync(connection, chunk, rows, periodo, now, ct);
                chunk.Clear();
                rows = 0;
            }
        }

        if (rows > 0)
            total += await FlushChunkAsync(connection, chunk, rows, periodo, now, ct);

        await ExecuteAsync(connection, "DROP TEMPORARY TABLE IF EXISTS tmp_deudores", ct);
        return total;
    }

    private async Task<long> FlushChunkAsync(MySqlConnection connection, StringBuilder chunk, int rows,
        int periodo, DateTime now, CancellationToken ct)
    {
        await ExecuteAsync(connection, "TRUNCATE TABLE tmp_deudores", ct);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(chunk.ToString()));
        var loader = new MySqlBulkLoader(connection)
        {
            TableName = "tmp_deudores",
            Local = true,
            SourceStream = stream,
            FieldTerminator = "\t",
            LineTerminator = "\n",
            CharacterSet = "utf8mb4",
        };
        loader.Columns.AddRange(["nro_identificacion", "situacion_maxima", "suma_total_prestamos", "cantidad_registros"]);
        var loaded = await loader.LoadAsync(ct);

        await using var upsert = connection.CreateCommand();
        upsert.CommandTimeout = 600;
        upsert.CommandText = """
            INSERT INTO deudores (nro_identificacion, situacion_maxima, suma_total_prestamos, cantidad_registros, periodo, actualizado_en)
            SELECT t.nro_identificacion, t.situacion_maxima, t.suma_total_prestamos, t.cantidad_registros, @periodo, @now
            FROM tmp_deudores AS t
            ON DUPLICATE KEY UPDATE
                situacion_maxima = t.situacion_maxima,
                suma_total_prestamos = t.suma_total_prestamos,
                cantidad_registros = t.cantidad_registros,
                periodo = @periodo,
                actualizado_en = @now
            """;
        upsert.Parameters.AddWithValue("@periodo", periodo);
        upsert.Parameters.AddWithValue("@now", now);
        await upsert.ExecuteNonQueryAsync(ct);

        logger.LogInformation("Bulk-loaded {Rows} deudores ({Loaded} rows read by LOAD DATA)", rows, loaded);
        return rows;
    }

    private async Task<long> BatchUpsertDeudoresAsync(MySqlConnection connection, DeudoresAggregator aggregation,
        int periodo, DateTime now, CancellationToken ct)
    {
        long total = 0;
        foreach (var batch in aggregation.EnumerateDeudores().Chunk(_options.BatchSize))
        {
            await using var command = connection.CreateCommand();
            var sql = new StringBuilder(
                "INSERT INTO deudores (nro_identificacion, situacion_maxima, suma_total_prestamos, cantidad_registros, periodo, actualizado_en) VALUES ");
            for (var i = 0; i < batch.Length; i++)
            {
                if (i > 0) sql.Append(',');
                sql.Append(CultureInfo.InvariantCulture, $"(@n{i},@s{i},@p{i},@c{i},@periodo,@now)");
                command.Parameters.AddWithValue($"@n{i}", batch[i].Key);
                command.Parameters.AddWithValue($"@s{i}", batch[i].Value.SituacionMaxima);
                command.Parameters.AddWithValue($"@p{i}", batch[i].Value.SumaTotalPrestamos);
                command.Parameters.AddWithValue($"@c{i}", batch[i].Value.Registros);
            }
            sql.Append("""
                 AS new ON DUPLICATE KEY UPDATE
                    situacion_maxima = new.situacion_maxima,
                    suma_total_prestamos = new.suma_total_prestamos,
                    cantidad_registros = new.cantidad_registros,
                    periodo = new.periodo,
                    actualizado_en = new.actualizado_en
                """);
            command.Parameters.AddWithValue("@periodo", periodo);
            command.Parameters.AddWithValue("@now", now);
            command.CommandText = sql.ToString();
            await command.ExecuteNonQueryAsync(ct);
            total += batch.Length;
        }

        logger.LogInformation("Upserted {Rows} deudores with batched INSERT", total);
        return total;
    }

    private static async Task<int> UpsertEntidadesAsync(MySqlConnection connection, DeudoresAggregator aggregation,
        int periodo, DateTime now, CancellationToken ct)
    {
        var entidades = aggregation.Entidades.OrderBy(e => e.Key).ToArray();
        if (entidades.Length == 0)
            return 0;

        await using var command = connection.CreateCommand();
        var sql = new StringBuilder(
            "INSERT INTO entidades (codigo_entidad, suma_total_prestamos, cantidad_registros, periodo, actualizado_en) VALUES ");
        for (var i = 0; i < entidades.Length; i++)
        {
            if (i > 0) sql.Append(',');
            sql.Append(CultureInfo.InvariantCulture, $"(@e{i},@p{i},@c{i},@periodo,@now)");
            command.Parameters.AddWithValue($"@e{i}", entidades[i].Key);
            command.Parameters.AddWithValue($"@p{i}", entidades[i].Value.SumaTotalPrestamos);
            command.Parameters.AddWithValue($"@c{i}", entidades[i].Value.Registros);
        }
        sql.Append("""
             AS new ON DUPLICATE KEY UPDATE
                suma_total_prestamos = new.suma_total_prestamos,
                cantidad_registros = new.cantidad_registros,
                periodo = new.periodo,
                actualizado_en = new.actualizado_en
            """);
        command.Parameters.AddWithValue("@periodo", periodo);
        command.Parameters.AddWithValue("@now", now);
        command.CommandText = sql.ToString();
        await command.ExecuteNonQueryAsync(ct);
        return entidades.Length;
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 600;
        await command.ExecuteNonQueryAsync(ct);
    }
}
