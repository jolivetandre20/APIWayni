#!/usr/bin/env bash
# Restores a mysqldump (.sql or .sql.gz) into the compose MySQL. Default: the 1,000-debtor sample.
# Usage: ./scripts/restore-backup.sh            (from the repository root, with `docker compose up -d mysql`)
set -euo pipefail
cd "$(dirname "$0")/.."
set -a; source .env; set +a
BACKUP="${1:-db/backup/wayni_deudores_sample_backup.sql}"
echo "Restoring $BACKUP ..."
if [[ "$BACKUP" == *.gz ]]; then READ=(gunzip -c "$BACKUP"); else READ=(cat "$BACKUP"); fi
"${READ[@]}" | docker compose exec -T mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" --default-character-set=utf8mb4
docker compose exec -T mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" -e \
  "GRANT ALL PRIVILEGES ON ${MYSQL_DATABASE}.* TO '${MYSQL_USER}'@'%'; SELECT COUNT(*) AS deudores FROM ${MYSQL_DATABASE}.deudores;"
