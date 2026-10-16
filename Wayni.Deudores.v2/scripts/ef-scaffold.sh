#!/usr/bin/env bash
# Database-first view of the same schema: scaffolds entity classes + DbContext from the running MySQL
# (requires `dotnet tool install --global dotnet-ef --version 9.0.10`). Output goes to a separate folder
# so it never overwrites the code-first model used by the migrations.
set -euo pipefail
cd "$(dirname "$0")/.."
set -a; source .env; set +a
dotnet ef dbcontext scaffold \
  "Server=localhost;Port=${MYSQL_PORT:-3306};Database=${MYSQL_DATABASE};User=${MYSQL_USER};Password=${MYSQL_PASSWORD}" \
  Pomelo.EntityFrameworkCore.MySql \
  --project src/Wayni.Deudores.Infrastructure --startup-project src/Wayni.Deudores.Migrator \
  --output-dir Persistence/Scaffolded --context ScaffoldedWayniDbContext --context-dir Persistence/Scaffolded \
  --table deudores --table entidades --table situaciones --table import_jobs --no-onconfiguring --force
