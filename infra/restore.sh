#!/usr/bin/env sh
set -eu

BACKUP_ROOT="${BACKUP_ROOT:-/backups}"
BACKUP_ID="${1:-}"
POSTGRES_HOST="${POSTGRES_HOST:-postgres}"
POSTGRES_USER="${POSTGRES_USER:-saekg}"
POSTGRES_DB="${POSTGRES_DB:-saekg}"
DUCKDB_PATH="${DUCKDB_PATH:-/data/ecg.duckdb}"

if [ -z "${BACKUP_ID}" ]; then
  echo "Usage: restore.sh <backup-id>"
  echo "Available:"
  ls -1 "${BACKUP_ROOT}" 2>/dev/null || true
  exit 1
fi

SRC="${BACKUP_ROOT}/${BACKUP_ID}"
if [ ! -f "${SRC}/ok" ] || [ ! -f "${SRC}/postgres.dump" ]; then
  echo "Backup ${BACKUP_ID} is incomplete or missing"
  exit 1
fi

echo "Restoring Postgres from ${SRC}/postgres.dump"
PGPASSWORD="${POSTGRES_PASSWORD:-saekg}" pg_restore -h "${POSTGRES_HOST}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" --clean --if-exists "${SRC}/postgres.dump"

if [ -f "${SRC}/ecg.duckdb" ]; then
  echo "Restoring DuckDB to ${DUCKDB_PATH}"
  cp "${SRC}/ecg.duckdb" "${DUCKDB_PATH}"
fi

echo "Restore complete: ${BACKUP_ID}"
