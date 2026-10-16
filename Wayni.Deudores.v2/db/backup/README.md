<span style="color:#E65100">**Uso interno**</span>

# Backup de MySQL (muestra)

`wayni_deudores_sample_backup.sql` (117 KB) es un `mysqldump` de la base `wayni_deudores` para una prueba rápida:

| Tabla | Filas | Contenido |
|---|---|---|
| deudores | 1.000 | Los 1.000 CUIT con mayor `suma_total_prestamos` de noviembre 2023 |
| entidades | 485 | Nombre (Maeent.txt) y totales reales del archivo completo |
| situaciones | 8 | Catálogo del campo 6 |
| import_jobs | 1 | La importación de referencia (métricas del archivo completo) |
| __EFMigrationsHistory | 1 | `20261016120000_InitialCreate`: el Migrator no vuelve a aplicar la migración |

Los totales de `entidades` y del job son los del archivo completo; para tener los 19,9 millones de deudores,
importar el `.7z` del BCRA desde el frontend (unos minutos).

```bash
docker compose up -d mysql
./scripts/restore-backup.sh            # Windows: .\scripts\restore-backup.ps1
```

Manualmente: `docker compose exec -T mysql mysql -uroot -p<ROOT_PASSWORD> < db/backup/wayni_deudores_sample_backup.sql`

El script incluye `DROP DATABASE IF EXISTS` + `CREATE DATABASE`: **reemplaza la base existente**.
Probado: restaurado en MySQL 8.0 → 1.000 deudores, 485 entidades, 8 situaciones, índices y migración registrados.

<span style="color:#E65100">**Uso interno**</span>
