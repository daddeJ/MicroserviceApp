#!/bin/bash

set -e

echo "🚀 Starting CI/CD Simulation Setup..."

COMPOSE_FILE="docker-compose.local.yml"

# Load environment variables
if [ -f .env.local ]; then
    echo "✅ Loading .env.local..."
    while IFS= read -r line; do
        [[ -z "$line" || "$line" =~ ^[[:space:]]*# ]] && continue
        if [[ "$line" =~ ^[a-zA-Z_][a-zA-Z0-9_]*= ]]; then
            export "$line" 2>/dev/null || echo "   Warning: Could not export: $line"
        fi
    done < .env.local
else
    echo "⚠️  No .env.local found, using default values"
fi

# Set defaults
: ${MSSQL_SA_PASSWORD:=StrongP@ssw0rd123!}

# Clean up any existing containers
echo "🧹 Cleaning up existing containers..."
docker compose -f "$COMPOSE_FILE" down -v --remove-orphans

# Build all services
echo "🔨 Building services..."
docker compose -f "$COMPOSE_FILE" build --no-cache

# Start infrastructure services only
echo "🔄 Starting infrastructure services..."
docker compose -f "$COMPOSE_FILE" up -d mssql redis rabbitmq

# Wait for infrastructure to be healthy
echo "⏳ Waiting for infrastructure services to be healthy..."
sleep 30

# Check infrastructure health
echo "📊 Infrastructure status:"
docker compose -f "$COMPOSE_FILE" ps mssql redis rabbitmq

# Initialize databases and tables
echo "🗃️ Initializing databases..."
docker compose -f "$COMPOSE_FILE" exec -T mssql /opt/mssql-tools18/bin/sqlcmd \
    -S localhost \
    -U sa \
    -P "$MSSQL_SA_PASSWORD" \
    -i /tmp/sql/init-database.sql \
    -C

# Start application services
echo "🚀 Starting application services..."
docker compose -f "$COMPOSE_FILE" up -d authservice userservice loggerservice nginx

# Wait for applications to start
echo "⏳ Waiting for applications to be healthy..."
sleep 30

# Final status check
echo "📊 Final service status:"
docker compose -f "$COMPOSE_FILE" ps

# Quick health check
echo "🏥 Quick health check..."
for service in 5002 5001 5003; do
    echo -n "   Port $service... "
    curl -f -s http://localhost:$service/health >/dev/null 2>&1 || \
    curl -f -s http://localhost:$service/api/health >/dev/null 2>&1 && \
    echo "✅" || echo "❌"
done

echo "🎉 CI/CD Simulation Setup Complete!"