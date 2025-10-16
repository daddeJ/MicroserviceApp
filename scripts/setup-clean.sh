#!/bin/bash

set -e

echo "🧹 Starting clean database setup..."

COMPOSE_FILE="docker-compose.local.yml"

# Debug: Show current environment
echo "🔍 Environment Debug Info:"
echo "   Current directory: $(pwd)"
echo "   Script location: $(dirname "$0")"
echo "   Compose file: $COMPOSE_FILE"

# Load environment variables with better debugging
if [ -f .env.local ]; then
    echo "✅ Found .env.local file"
    # Show what's in the .env.local file (without sensitive data)
    grep -E "^(MSSQL_|#)" .env.local || echo "   No MSSQL related variables found"
    
    # Load environment variables - fix for quotes
    set -a  # Automatically export all variables
    source .env.local
    set +a
    echo "✅ Loaded .env.local"
else
    echo "⚠️  No .env.local found, using default values"
fi

# Debug: Show MSSQL password status - FIXED SYNTAX
echo "🔍 MSSQL Password Debug:"
if [ -z "$MSSQL_SA_PASSWORD" ]; then
    echo "❌ MSSQL_SA_PASSWORD is EMPTY or NOT SET"
    echo "   Using default password: StrongP@ssw0rd123!"
    MSSQL_SA_PASSWORD="StrongP@ssw0rd123!"
else
    echo "✅ MSSQL_SA_PASSWORD is SET"
    echo "   Password length: ${#MSSQL_SA_PASSWORD} characters"  # FIXED: removed extra $
    echo "   First 2 chars: ${MSSQL_SA_PASSWORD:0:2}***"  # FIXED: removed extra $
fi

# Show all MSSQL related environment variables
echo "🔍 MSSQL Environment Variables:"
env | grep -i mssql || echo "   No MSSQL environment variables found"

# Start only MSSQL for database operations
echo "🚀 Starting MSSQL using $COMPOSE_FILE..."
docker compose -f "$COMPOSE_FILE" up -d mssql

# Wait for MSSQL to be ready with better debugging
echo "⏳ Waiting for MSSQL to be ready..."

# Test different connection approaches
MAX_RETRIES=10
RETRY_COUNT=0

while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    echo "   Attempt $((RETRY_COUNT + 1))/$MAX_RETRIES..."
    
    # Method 1: Try with the variable
    if docker compose -f "$COMPOSE_FILE" exec -T mssql /opt/mssql-tools18/bin/sqlcmd \
        -S localhost \
        -U sa \
        -P "$MSSQL_SA_PASSWORD" \
        -Q "SELECT 1" -C > /dev/null 2>&1; then
        echo "✅ Connected to MSSQL successfully!"
        break
    else
        echo "   ❌ Connection failed with MSSQL_SA_PASSWORD"
        
        # Method 2: Try with default password
        if docker compose -f "$COMPOSE_FILE" exec -T mssql /opt/mssql-tools18/bin/sqlcmd \
            -S localhost \
            -U sa \
            -P "StrongP@ssw0rd123!" \
            -Q "SELECT 1" -C > /dev/null 2>&1; then
            echo "✅ Connected to MSSQL with default password!"
            MSSQL_SA_PASSWORD="StrongP@ssw0rd123!"
            break
        else
            echo "   ❌ Connection failed with default password too"
        fi
    fi
    
    RETRY_COUNT=$((RETRY_COUNT + 1))
    sleep 5
done

if [ $RETRY_COUNT -eq $MAX_RETRIES ]; then
    echo "❌ Failed to connect to MSSQL after $MAX_RETRIES attempts"
    echo "🔍 Debugging container status:"
    docker compose -f "$COMPOSE_FILE" ps mssql
    echo "🔍 Checking container logs:"
    docker compose -f "$COMPOSE_FILE" logs mssql --tail=20
    exit 1
fi

echo "✅ SQL Server is ready and responsive"

# Execute the clean database setup
echo "🗃️ Dropping and recreating databases and tables..."
if docker compose -f "$COMPOSE_FILE" exec -T mssql /opt/mssql-tools18/bin/sqlcmd \
    -S localhost \
    -U sa \
    -P "$MSSQL_SA_PASSWORD" \
    -i /tmp/sql/init-database.sql \
    -C; then
    echo "✅ Database setup completed successfully"
else
    echo "❌ Database setup failed"
    echo "🔍 Checking if SQL file exists in container:"
    docker compose -f "$COMPOSE_FILE" exec mssql ls -la /tmp/sql/ || echo "SQL directory not found"
    exit 1
fi

echo "🎉 Clean setup complete! All databases and tables were dropped and recreated."