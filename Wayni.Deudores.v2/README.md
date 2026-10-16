<span style="color:#E65100">**Uso interno**</span>

# Wayni · Central de Deudores BCRA (.NET 10, microservicios)

Solución al *Desafío Técnico - Wayni Móvil*: importa el padrón de deudores del BCRA (`deudores.txt`),
aplica las reglas de negocio, guarda el resultado en **MySQL** (EF Core + migraciones), guarda el
archivo original en **S3**, procesa de forma asíncrona con **SQS**, notifica el fin del proceso
(**webhook + correo + log estructurado**) y expone una **API de consulta** y un **frontend de administración**.
Todo se levanta con `docker compose up` y **no requiere cuentas ni licencias de AWS**.

## Arquitectura

```
                 ┌──────────────┐  POST /upload  ┌────────────────┐  original   ┌───────────────┐
 navegador ────▶ │  Admin.Web   │ ─────────────▶ │  Importer.Api  │ ──────────▶ │ S3 (S3Mock)   │
 (Razor Pages)   │  :5100       │ ◀── webhook ── │  :5101         │             └───────▲───────┘
                 └──────┬───────┘        │       └───────┬────────┘                     │ lee
                        │ GET            │               │ mensaje                      │
                        ▼                │               ▼                              │
                 ┌──────────────┐        │       ┌───────────────┐   recibe    ┌────────┴───────┐
                 │  Query.Api   │        └────── │ SQS (ElasticMQ)│ ─────────▶ │   Processor    │──▶ correo (Mailpit)
                 │  :5102       │                └───────────────┘             │   :5103        │──▶ log JSON
                 └──────┬───────┘                                              └────────┬───────┘
                        │ EF Core                                                       │ LOAD DATA + upsert
                        └──────────────────────────▶  MySQL 8.4  ◀──────────────────────┘
                                                      ▲
                                        Migrator (aplica migraciones + seed y termina)
```

| Proyecto | Rol |
|---|---|
| `Wayni.Deudores.Core` | Parser de ancho fijo, reglas de negocio (agregación), contratos de mensajes. Sin dependencias. |
| `Wayni.Deudores.Infrastructure` | EF Core/Pomelo (MySQL), migraciones y seed, upsert masivo, S3, SQS, notificaciones, lectura .txt/.zip/.7z (7-Zip CLI). |
| `Wayni.Deudores.Migrator` | Contenedor *one-shot*: `Database.MigrateAsync()` (esquema + semilla). Proyecto de inicio para `dotnet ef`. |
| `Wayni.Deudores.Importer.Api` | Microservicio importador: `POST /upload`, `POST /import/local`, jobs. |
| `Wayni.Deudores.Processor` | Microservicio worker: consume SQS, procesa, graba en MySQL, notifica. |
| `Wayni.Deudores.Query.Api` | Microservicio de consulta: `GET /deudores/{cuit}`, `/entidades/{codigo}`, `/deudores/top/{n}`. |
| `Wayni.Deudores.Admin.Web` | Bonus: interfaz de administración ASP.NET Core Razor Pages (misma solución). |
| `tests/Wayni.Deudores.Tests` | xUnit: parser, reglas, archivos comprimidos y Query API end-to-end (EF InMemory). |

## Cómo ejecutar

Requisitos: Docker Desktop con **al menos 4 GB de memoria** asignada (el procesamiento de un mes completo
mantiene ~20 millones de CUIT en memoria, ~1 GB en el Processor).

```bash
cp .env.example .env          # y reemplazar los CHANGE_ME
docker compose up --build
```

| URL | Qué es |
|---|---|
| http://localhost:5100 | Frontend de administración (panel, importar, deudores, entidades) |
| http://localhost:5101/scalar/v1 | Importer API (documentación interactiva OpenAPI) |
| http://localhost:5102/scalar/v1 | Query API (documentación interactiva OpenAPI) |
| http://localhost:8025 | Mailpit: correos de fin de proceso |
| http://localhost:9325 | ElasticMQ: estado de la cola SQS |

### Importar el archivo del BCRA

El archivo puede subirse **tal cual lo publica el BCRA** (`202311DEUDORES_20240122204012.7Z`, 240 MB): el
Processor extrae `deudores.txt` (5,9 GB) en streaming con la línea de comandos de 7-Zip (`7z e -so`, incluida en
su imagen Docker), sin descomprimirlo a disco. También acepta `.txt` y `.zip`. Fuera de Docker, en Windows,
instalar 7-Zip y configurar `Archives__SevenZipPath=C:\Program Files\7-Zip\7z.exe`.

* **Recomendado (archivo grande):** copiarlo a `./data/inbox/` y en el frontend → *Importar* → *Importar*, o:
  ```bash
  curl -X POST http://localhost:5101/import/local -H "Content-Type: application/json" \
       -d '{"path":"202311DEUDORES_20240122204012.7Z"}'
  ```
* **Endpoint /upload:**
  ```bash
  curl -F "file=@202311DEUDORES_20240122204012.7Z" http://localhost:5101/upload
  ```

Ambos responden `202 Accepted` con el `id` del job; el estado se consulta en `GET /jobs/{id}` o en el panel.
Al terminar llegan: el webhook al panel, el correo a Mailpit y una línea de log `ImportFinished` en
`docker compose logs processor` con duración total y resumen de carga.

### Cargar la base de muestra (backup)

`db/backup/wayni_deudores_sample_backup.sql` (117 KB) trae la base lista para probar la Query API sin
importar nada: esquema, migración registrada, 485 entidades con sus totales reales y los **1.000 deudores con
mayor suma de préstamos**. Ver [`db/backup/README.md`](db/backup/README.md):

```bash
docker compose up -d mysql
./scripts/restore-backup.sh          # Windows: .\scripts\restore-backup.ps1
docker compose up --build            # el Migrator detecta la migración ya aplicada y no hace nada
curl http://localhost:5102/deudores/top/5
```

## Endpoints

### Importer API (`:5101`)

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/upload` | `multipart/form-data`, campo `file` (.txt/.zip/.7z). Guarda en S3, encola en SQS. `202` + job. |
| POST | `/import/local` | `{"path":"archivo"}` relativo a `./data/inbox` (se rechaza cualquier ruta fuera). |
| GET | `/import/local` | Archivos disponibles en la carpeta inbox. |
| GET | `/jobs?take=20` | Últimos jobs. |
| GET | `/jobs/{id}` | Estado: `Queued`, `Processing`, `Completed`, `Failed` + métricas. |
| POST | `/jobs/{id}/reprocess` | Vuelve a encolar el mismo objeto de S3. |

### Query API (`:5102`)

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/deudores/{cuit}` | `nroIdentificacion`, `situacionMaxima` (+ descripción), `sumaTotalPrestamos`, registros, período. `400` si el CUIT no tiene 11 caracteres, `404` si no existe. |
| GET | `/deudores/top/{n}?situacion=5` | N deudores (1 a 1000) con mayor suma de préstamos; filtro opcional por situación. |
| GET | `/entidades/{codigo}` | `codigoEntidad`, `nombre` (Maeent.txt), `sumaTotalPrestamos`. |
| GET | `/entidades?take=50` | Ranking de entidades por suma de préstamos. |
| GET | `/situaciones` | Catálogo del campo 6. |

Los montos están en **miles de pesos con un decimal**, como los publica el BCRA.

## Reglas de negocio

Del diseño de registro (`LEAME DEUDORES.pdf`), ancho fijo de 171 caracteres, ISO-8859-1:

| Campo | Posición | Uso |
|---|---|---|
| 1 Código de entidad | 1-5 | `entidades.codigo_entidad` |
| 2 Fecha de información | 6-11 | `periodo` (AAAAMM) |
| 4 N° de identificación | 14-24 | `deudores.nro_identificacion` |
| 6 Situación | 28-29 | `deudores.situacion_maxima` = **máximo** entre registros del mismo CUIT |
| 7 Préstamos | 30-41 | `suma_total_prestamos` = **suma** por CUIT y por entidad |

* **Deudores**: una fila por CUIT/CUIL con `situacion_maxima` y `suma_total_prestamos`.
* **Entidades**: una fila por código con `suma_total_prestamos`; el nombre viene sembrado desde `Maeent.txt`.
* **Inserta o actualiza**: re-importar un período reemplaza los valores de cada CUIT/entidad presente en el
  archivo (`INSERT … ON DUPLICATE KEY UPDATE`); los nombres de entidades nunca se pisan.

### Supuestos (validados contra el archivo real)

1. **Formato real**: los numéricos vienen alineados a izquierda con blancos (`"1 "`) y los montos con coma
   decimal (`"2406,0"`, `",0"`).
2. **Identificaciones alfanuméricas**: el campo 4 es "Carácter"; 285 líneas de deudores del exterior
   (tipo 98) traen ids como `00007CN0001`. Se aceptan y se guardan tal cual.
3. **Situación 2**: el archivo de noviembre 2023 informa el código histórico `2` (587.962 deudores) en lugar de
   `21`/`23`; se agregó al catálogo como "Con seguimiento especial / Riesgo Bajo".
4. **"Máximo valor" es numérico**, como pide el enunciado. Ojo: con esa regla `11` (cubierto con garantías "A")
   queda por encima de `5` (irrecuperable). Si negocio quisiera "peor situación", es un cambio de una línea en
   `DeudoresAggregator`.
5. Una re-importación no borra CUITs ausentes del archivo nuevo (no se pidió).

## Base de datos (MySQL + EF Core)

* **EF Core 9 + Pomelo 9 sobre .NET 10.** Pomelo todavía no publicó build para EF Core 10 y soporta
  oficialmente 9.x en .NET 10; todos los paquetes EF quedan en 9.0.x para evitar conflictos de versión.
* **Migraciones**: `src/Wayni.Deudores.Infrastructure/Persistence/Migrations/` → `InitialCreate` crea
  `deudores`, `entidades`, `situaciones`, `import_jobs`, los índices descendentes para el top N y la **semilla**
  (8 situaciones + 485 entidades de `Maeent.txt`). El contenedor `migrator` la aplica al iniciar.
* Script SQL equivalente (para un DBA): `db/scripts/01_initial_create.sql`.
* Comandos (`dotnet tool install --global dotnet-ef --version 9.0.10`):
  ```bash
  export WAYNI_DB_CONNECTION="Server=localhost;Port=3306;Database=wayni_deudores;User=wayni;Password=<DB_PASSWORD>"
  dotnet ef migrations add <Nombre> --project src/Wayni.Deudores.Infrastructure --startup-project src/Wayni.Deudores.Migrator
  dotnet ef migrations script     --project src/Wayni.Deudores.Infrastructure --startup-project src/Wayni.Deudores.Migrator
  dotnet ef database update       --project src/Wayni.Deudores.Infrastructure --startup-project src/Wayni.Deudores.Migrator
  ```
* **Scaffolding (database-first)**: `scripts/ef-scaffold.sh` genera entidades y `DbContext` desde la base en
  `Persistence/Scaffolded/`, sin pisar el modelo code-first.
* **Carga masiva**: el Processor no inserta fila por fila: carga bloques de 250.000 CUIT con `LOAD DATA LOCAL
  INFILE` a una tabla temporal y hace un `INSERT … SELECT … ON DUPLICATE KEY UPDATE` por bloque (requiere
  `local_infile=ON` y `AllowLoadLocalInfile=true`, ya configurados). `Persistence__UseBulkLoad=false` usa
  INSERT parametrizados por lotes.

## S3 y SQS sin licencia de AWS

Desde marzo de 2026 la imagen actual de LocalStack exige cuenta y token. Por eso el `docker-compose.yml` usa
emuladores libres compatibles con el SDK oficial de AWS (el código es el mismo que contra AWS real):

| Servicio | Emulador | Licencia |
|---|---|---|
| S3 | `adobe/s3mock` | Apache-2.0 |
| SQS | `softwaremill/elasticmq-native` (cola + DLQ con 3 reintentos) | Apache-2.0 |
| SMTP | `axllent/mailpit` | MIT |

Para usar **LocalStack** (criterio de evaluación del desafío):

```bash
docker compose -f docker-compose.yml -f docker-compose.localstack.yml up --build
```

Usa la imagen 4.x anterior al cambio de licencia; con un token gratuito en `LOCALSTACK_AUTH_TOKEN` se puede
usar una versión nueva. Para AWS real basta con dejar vacíos `S3_SERVICE_URL` / `SQS_SERVICE_URL`.

## Configuración

Todo se externaliza en `.env` (ver `.env.example`, sin credenciales reales) y llega a los servicios como
variables de entorno de ASP.NET Core (`Seccion__Clave`). Claves principales: `ConnectionStrings__Wayni`,
`Aws__S3__*`, `Aws__Sqs__*`, `Notifications__Webhook__Url/Secret`, `Notifications__Email__*`,
`Persistence__UseBulkLoad`, `Processing__ExpectedDeudores`.

## Tests

```bash
dotnet test
```

* Parser con líneas reales (incluye ids alfanuméricos, montos con coma, líneas inválidas).
* Reglas de agregación (máximo de situación, sumas por CUIT y entidad) y lectura de una muestra real de 2.000 líneas.
* Apertura de `deudores.txt` dentro de un `.zip`.
* Query API end-to-end con `WebApplicationFactory` + EF Core InMemory (incluye la semilla de entidades).

## Resultado de referencia (noviembre 2023)

| Métrica | Valor |
|---|---|
| Líneas leídas | 34.385.567 (0 inválidas) |
| Deudores distintos | 19.926.484 |
| Entidades | 485 |
| Total préstamos | 23.001.588.914,0 miles de $ |
| Parseo + agregación | ~55 s (1 vCPU) |

Distribución de `situacion_maxima`: 1 → 15.394.172 · 2 → 587.962 · 3 → 430.413 · 4 → 740.540 ·
5 → 2.730.885 · 11 → 42.512.

## Cambios v2

* **Fix de compilación CS1061** en `WayniDbContext`: dentro del contexto, `Situaciones` es la propiedad `DbSet`
  y ocultaba la clase de catálogo `Core.Domain.Situaciones`; se usa un alias (`SituacionesCatalogo`).
  Los demás errores (CS0006 "metadata file could not be found") eran consecuencia de ese.
* **Sin SharpCompress (aviso NU1902, GHSA-6c8g-7p36-r338)**: las versiones ≤ 0.47.4 tienen la vulnerabilidad y
  las nuevas cambiaron la API. El `.7z` ahora se lee con el 7-Zip nativo (`p7zip-full` en la imagen del
  Processor): sin dependencia vulnerable y más rápido. Probado sobre el `.7z` real: 34.385.567 líneas,
  mismos totales, ~1 minuto.
* Un archivo sin registros válidos (por ejemplo un `.7z` sin `deudores.txt`) ahora termina en `Failed` en vez
  de `Completed` con cero filas.

## Estado de verificación

Lo que se ejecutó al construir la solución:

* `Core` compila con .NET 10 y se corrió sobre el archivo real completo; los totales por entidad y el total
  general coinciden exactamente con un cálculo independiente (awk) sobre el TXT, y dos CUIT se verificaron a mano.
* Los tests de `Core` (26 casos) pasan.
* `Admin.Web` compila y se probó contra APIs simuladas (páginas, webhook con y sin secreto).
* El script de migración, el upsert masivo (alta y actualización) y la restauración del backup de muestra se
  probaron en MySQL 8.0 (la carga completa de los 19,9 millones de deudores también: totales idénticos al TXT).
* **No compilado en el entorno de construcción** (sin acceso a NuGet): `Infrastructure`, `Migrator`, `Importer.Api`,
  `Processor`, `Query.Api` y los tests que usan EF/AWS/WebApplicationFactory. Solo se verificó su sintaxis.
  El primer `dotnet build` / `docker compose up --build` es la verificación real de esas piezas; las
  migraciones se escribieron a mano siguiendo la salida de `dotnet ef` para EF Core 9 + Pomelo 9
  (`dotnet ef migrations add Check` debería generar una migración vacía).

<span style="color:#E65100">**Uso interno**</span>
