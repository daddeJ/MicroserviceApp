#!/bin/bash

echo "🏥 Quick Health Check"

COMPOSE_FILE="docker-compose.local.yml"

echo "📊 Container Status:"
docker compose -f "$COMPOSE_FILE" ps

echo ""
echo "🔌 Service Connectivity:"
services=("5002:AuthService" "5001:UserService" "5003:LoggerService" "80:NGINX")

for service in "${services[@]}"; do
    port="${service%:*}"
    name="${service#*:}"
    
    echo -n "   $name (localhost:$port)... "
    if curl -f -s http://localhost:$port/health >/dev/null 2>&1 || \
       curl -f -s http://localhost:$port/api/health >/dev/null 2>&1 || \
       curl -f -s http://localhost:$port/ >/dev/null 2>&1; then
        echo "✅ Reachable"
    else
        echo "❌ Unreachable"
    fi
done