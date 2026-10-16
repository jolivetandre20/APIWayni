<span style="color:#E65100">**Uso interno**</span>

# Usar el MySQL local (MySQL80) con docker compose

En lugar del contenedor `mysql`, las APIs, el Processor y el Migrator se conectan al MySQL instalado en
Windows a través de `host.docker.internal:3306`. S3Mock, ElasticMQ y Mailpit siguen en Docker.

## Una sola vez

1. **MySQL Workbench**, conectado como `root` a *Local instance MySQL80*:
   * Abrir `db/scripts/local-mysql80-setup.sql`, reemplazar `<MYSQL_PASSWORD>` por el `MYSQL_PASSWORD` del `.env`
     y ejecutarlo (⚡). Debe mostrar `local_infile = 1`.
   * Si todavía no restauraste la muestra: **Server → Data Import → Import from Self-Contained File** →
     `db/backup/wayni_deudores_sample_backup.sql` → **Start Import**.
2. **`.env`**: verificar `MYSQL_DATABASE=wayni_deudores` y `MYSQL_USER=wayni`. Opcional, si tu MySQL no está en
   el puerto 3306:
   ```
   LOCAL_MYSQL_HOST=host.docker.internal
   LOCAL_MYSQL_PORT=3306
   ```

## Levantar

```powershell
docker compose -f docker-compose.yml -f docker-compose.localmysql.yml up --build
```

* Frontend: http://localhost:5100 · Query API: http://localhost:5102/scalar/v1
* El Migrator ve la migración ya registrada (si restauraste el backup) y termina sin cambios; con una base
  vacía crea las tablas y la semilla.
* El contenedor `mysql` no se inicia.

## Problemas típicos

| Mensaje | Causa / solución |
|---|---|
| `Access denied for user 'wayni'@'172.x.x.x'` | Falta el paso 1 o la contraseña no coincide con `.env`. |
| `Unable to connect to any of the specified MySQL hosts` | MySQL80 no está corriendo, o el firewall de Windows bloquea el 3306 para la red de Docker. |
| `Loading local data is disabled` | Falta `SET PERSIST local_infile = 1` (paso 1). |
| `service "migrator" depends on undefined service "mysql"` / `unknown tag !reset` | Docker Compose anterior a 2.24: actualizar Docker Desktop. |

<span style="color:#E65100">**Uso interno**</span>
