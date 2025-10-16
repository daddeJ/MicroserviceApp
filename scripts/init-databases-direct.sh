#!/bin/bash

set -e

echo "🗃️ Initializing databases directly..."

# Wait for SQL Server to be ready
until docker compose exec -T mssql /opt/mssql-tools18/bin/sqlcmd \
    -S localhost \
    -U sa \
    -P "$MSSQL_SA_PASSWORD_LOCAL" \
    -Q "SELECT 1" -C > /dev/null 2>&1; do
    echo "📡 Waiting for SQL Server..."
    sleep 5
done

echo "✅ SQL Server is ready"

# Read the SQL file and execute it directly
SQL_CONTENT=$(cat scripts/sql/init-database.sql)

# Execute the SQL content directly
docker compose exec -T mssql /opt/mssql-tools18/bin/sqlcmd \
    -S localhost \
    -U sa \
    -P "$MSSQL_SA_PASSWORD_LOCAL" \
    -Q "$SQL_CONTENT" \
    -C

echo "✅ Databases initialized successfully"