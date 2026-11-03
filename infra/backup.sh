#!/usr/bin/env sh
set -eu

BACKUP_ROOT="${BACKUP_ROOT:-/backups}"
KEEP="${BACKUP_KEEP:-14}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
DEST="${BACKUP_ROOT}/${STAMP}"
POSTGRES_HOST="${POSTGRES_HOST:-postgres}"
POSTGRES_USER="${POSTGRES_USER:-saekg}"
POSTGRES_DB="${POSTGRES_DB:-saekg}"
DUCKDB_PATH="${DUCKDB_PATH:-/data/ecg.duckdb}"

mkdir -p "${DEST}"
echo "Backing up Postgres to ${DEST}/postgres.dump"
PGPASSWORD="${POSTGRES_PASSWORD:-saekg}" pg_dump -h "${POSTGRES_HOST}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -Fc -f "${DEST}/postgres.dump"

if [ -f "${DUCKDB_PATH}" ]; then
  echo "Copying DuckDB ${DUCKDB_PATH}"
  cp "${DUCKDB_PATH}" "${DEST}/ecg.duckdb"
else
  echo "DuckDB file not found, skipping"
fi

echo "${STAMP}" > "${DEST}/ok"

echo "Pruning backups older than last ${KEEP}"
ls -1dt "${BACKUP_ROOT}"/*/ 2>/dev/null | tail -n +"$((KEEP + 1))" | while read -r old; do
  rm -rf "${old}"
done

echo "Backup complete: ${DEST}"
