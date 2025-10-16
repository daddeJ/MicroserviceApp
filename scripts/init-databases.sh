#!/bin/bash

set -e

echo "🗃️ Initializing databases and tables..."

# Wait for SQL Server to be ready
echo "⏳ Waiting for SQL Server to be ready..."
until docker compose exec -T mssql /opt/mssql-tools18/bin/sqlcmd \
    -S localhost \
    -U sa \
    -P "$MSSQL_SA_PASSWORD_LOCAL" \
    -Q "SELECT 1" -C > /dev/null 2>&1; do
    echo "📡 Waiting for SQL Server..."
    sleep 5
done

echo "✅ SQL Server is ready"

# Execute the SQL script to create databases and tables
echo "🏗️ Creating databases and tables..."
docker compose exec -T mssql /opt/mssql-tools18/bin/sqlcmd \
    -S localhost \
    -U sa \
    -P "$MSSQL_SA_PASSWORD_LOCAL" \
    -i /scripts/init-all-databases.sql \
    -C

echo "✅ Databases and tables created successfully"