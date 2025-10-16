#!/bin/bash

echo "🔄 Resetting databases only..."

COMPOSE_FILE="docker-compose.local.yml"

# Load environment
if [ -f .env.local ]; then
    source .env.local
fi

: ${MSSQL_SA_PASSWORD:=StrongP@ssw0rd123!}

docker compose -f "$COMPOSE_FILE" exec -T mssql /opt/mssql-tools18/bin/sqlcmd \
    -S localhost \
    -U sa \
    -P "$MSSQL_SA_PASSWORD" \
    -i /tmp/sql/init-database.sql \
    -C

echo "✅ Databases reset complete"