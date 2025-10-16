#!/bin/bash

set -euo pipefail

# ============================================================================
# Unified Microservices Setup Script (Enhanced)
# ============================================================================

# Configuration
COMPOSE_FILE="docker-compose.local.yml"
SQL_MOUNT_PATH="/tmp/sql"
LOG_FILE="setup_$(date +%Y%m%d_%H%M%S).log"
VERBOSE=${VERBOSE:-false}

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# ============================================================================
# Logging Setup
# ============================================================================
exec > >(tee -a "$LOG_FILE") 2>&1

log_info() {
    echo -e "${GREEN}✅${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}⚠️${NC}  $1"
}

log_error() {
    echo -e "${RED}❌${NC} $1"
}

# ============================================================================
# Error Handling
# ============================================================================
cleanup_on_error() {
    log_error "Setup failed! Check logs: $LOG_FILE"
    echo ""
    read -p "Would you like to rollback and cleanup? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        log_warn "Rolling back..."
        docker compose -f "$COMPOSE_FILE" down -v 2>/dev/null || true
    fi
    exit 1
}

trap cleanup_on_error ERR

# ============================================================================
# Dependency Checks
# ============================================================================
check_dependencies() {
    echo "🔍 Checking dependencies..."
    
    local missing=0
    
    if ! command -v docker &> /dev/null; then
        log_error "docker is not installed"
        ((missing++))
    else
        echo "   docker: $(docker --version | head -n1)"
    fi
    
    if ! command -v docker compose &> /dev/null; then
        log_error "docker compose is not installed"
        ((missing++))
    else
        echo "   docker compose: $(docker compose version | head -n1)"
    fi
    
    if ! command -v curl &> /dev/null; then
        log_error "curl is not installed"
        ((missing++))
    else
        echo "   curl: $(curl --version | head -n1)"
    fi
    
    if [ $missing -gt 0 ]; then
        log_error "$missing required dependencies missing"
        exit 1
    fi
    
    log_info "All dependencies installed"
}

# Diagnostic function for debugging health status
diagnose_service() {
    local service=$1
    
    local container_id=$(docker compose -f "$COMPOSE_FILE" ps -q "$service" 2>/dev/null)
    
    if [ -z "$container_id" ]; then
        echo "   ❌ Container not found"
        return
    fi
    
    echo "🔍 $service (ID: ${container_id:0:12})"
    echo "   └─ Status: $(docker inspect "$container_id" --format='{{.State.Status}}')"
    echo "   └─ Health: $(docker inspect "$container_id" --format='{{if .State.Health}}{{.State.Health.Status}}{{else}}no healthcheck{{end}}')"
    
    if docker inspect "$container_id" --format='{{.State.Health}}' 2>/dev/null | grep -q "Status"; then
        local health_log=$(docker inspect "$container_id" --format='{{range .State.Health.Log}}{{.ExitCode}}|{{.Output}}{{"\n"}}{{end}}' 2>/dev/null | tail -n 1)
        if [ -n "$health_log" ]; then
            local exit_code=$(echo "$health_log" | cut -d'|' -f1)
            local output=$(echo "$health_log" | cut -d'|' -f2 | head -c 100)
            if [ "$exit_code" != "0" ]; then
                echo "   └─ Last Check: EXIT $exit_code - $output"
            fi
        fi
    fi
}

# ============================================================================
# Pre-flight Validation
# ============================================================================
validate_setup() {
    echo ""
    echo "🔍 Pre-flight validation..."
    echo "   Compose file: $COMPOSE_FILE"
    echo "   SQL path: $SQL_MOUNT_PATH"
    echo "   Log file: $LOG_FILE"
    
    if [ ! -f "$COMPOSE_FILE" ]; then
        log_error "Compose file $COMPOSE_FILE not found!"
        echo "   Available: $(ls -1 docker-compose*.yml 2>/dev/null || echo 'None')"
        exit 1
    fi
    
    if [ ! -f "scripts/sql/init-database.sql" ]; then
        log_error "SQL init file not found: scripts/sql/init-database.sql"
        exit 1
    fi
    
    # Check Docker daemon
    if ! docker info > /dev/null 2>&1; then
        log_error "Docker daemon is not running"
        exit 1
    fi
    
    # Check for Docker warnings
    if docker info 2>&1 | grep -q "WARNING"; then
        log_warn "Docker has warnings:"
        docker info 2>&1 | grep WARNING | sed 's/^/   /'
    fi
    
    # Check NGINX requirements
    echo ""
    echo "🔍 Checking NGINX configuration..."
    
    if [ ! -f "nginx/nginx.conf" ]; then
        log_error "NGINX config not found: nginx/nginx.conf"
        exit 1
    else
        echo "   ✓ nginx.conf found"
    fi
    
    # Check for SSL certificates (required for HTTPS)
    if [ ! -d "nginx/certs" ]; then
        log_warn "NGINX certs directory not found: nginx/certs"
        echo "   Creating directory..."
        mkdir -p nginx/certs
    fi
    
    if [ ! -f "nginx/certs/server.crt" ] || [ ! -f "nginx/certs/server.key" ]; then
        log_warn "SSL certificates not found in nginx/certs/"
        echo "   NGINX will fail to start HTTPS (443)"
        echo ""
        echo "   To generate self-signed certificates:"
        echo "   openssl req -x509 -nodes -days 365 -newkey rsa:2048 \\"
        echo "     -keyout nginx/certs/server.key \\"
        echo "     -out nginx/certs/server.crt \\"
        echo "     -subj '/CN=localhost'"
        echo ""
        read -p "   Generate certificates now? (Y/n): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Nn]$ ]]; then
            mkdir -p nginx/certs
            openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
                -keyout nginx/certs/server.key \
                -out nginx/certs/server.crt \
                -subj '/CN=localhost' 2>/dev/null
            if [ $? -eq 0 ]; then
                log_info "SSL certificates generated"
            else
                log_error "Failed to generate certificates"
                exit 1
            fi
        else
            log_warn "Continuing without SSL certificates - HTTPS will fail"
        fi
    else
        echo "   ✓ SSL certificates found"
    fi
    
    log_info "Validation passed"
}

# ============================================================================
# Environment Configuration
# ============================================================================
load_environment() {
    echo ""
    echo "🔧 Loading environment configuration..."
    
    if [ -f .env.local ]; then
        log_info "Loading .env.local..."
        while IFS= read -r line; do
            [[ -z "$line" || "$line" =~ ^[[:space:]]*# ]] && continue
            if [[ "$line" =~ ^[a-zA-Z_][a-zA-Z0-9_]*= ]]; then
                export "$line" 2>/dev/null || log_warn "Could not export: ${line%%=*}"
            fi
        done < .env.local
    else
        log_warn "No .env.local found, using defaults"
    fi
    
    # Set defaults
    export MSSQL_SA_PASSWORD="${MSSQL_SA_PASSWORD:-StrongP@ssw0rd123!}"
    export MSSQL_SA_PASSWORD_LOCAL="${MSSQL_SA_PASSWORD}"  # Fix for healthcheck
    export MSSQL_ACCEPT_EULA="${MSSQL_ACCEPT_EULA:-Y}"
    export MSSQL_PID="${MSSQL_PID:-Developer}"
    
    # Validate password
    if [ ${#MSSQL_SA_PASSWORD} -lt 8 ]; then
        log_error "MSSQL password must be at least 8 characters"
        exit 1
    fi
    
    if [[ "$MSSQL_SA_PASSWORD" =~ [\'\"\`] ]]; then
        log_error "MSSQL password contains unsafe characters (quotes, backticks)"
        exit 1
    fi
    
    if [ "$VERBOSE" = true ]; then
        echo "   MSSQL Password: ${MSSQL_SA_PASSWORD}"
    else
        echo "   MSSQL Password: ****** (${#MSSQL_SA_PASSWORD} chars)"
    fi
    
    # Validate connection strings
    echo ""
    echo "🔍 Validating connection strings..."
    
    local has_errors=0
    
    # Check AUTH_SERVICE_DBCONNECTION_LOCAL
    if [ -n "$AUTH_SERVICE_DBCONNECTION_LOCAL" ]; then
        if [[ "$AUTH_SERVICE_DBCONNECTION_LOCAL" =~ ^[\'\"] ]]; then
            log_error "AUTH_SERVICE_DBCONNECTION_LOCAL starts with quotes - remove them from .env.local"
            echo "   Current: ${AUTH_SERVICE_DBCONNECTION_LOCAL:0:50}..."
            echo "   Should be: AUTH_SERVICE_DBCONNECTION_LOCAL=Server=mssql;Database=..."
            echo "   NOT: AUTH_SERVICE_DBCONNECTION_LOCAL='Server=mssql;Database=...'"
            has_errors=1
        else
            echo "   ✓ AUTH_SERVICE_DBCONNECTION_LOCAL format looks valid"
        fi
    else
        log_warn "AUTH_SERVICE_DBCONNECTION_LOCAL not set"
    fi
    
    # Check USER_SERVICE_DBCONNECTION_LOCAL
    if [ -n "$USER_SERVICE_DBCONNECTION_LOCAL" ]; then
        if [[ "$USER_SERVICE_DBCONNECTION_LOCAL" =~ ^[\'\"] ]]; then
            log_error "USER_SERVICE_DBCONNECTION_LOCAL starts with quotes - remove them from .env.local"
            has_errors=1
        else
            echo "   ✓ USER_SERVICE_DBCONNECTION_LOCAL format looks valid"
        fi
    else
        log_warn "USER_SERVICE_DBCONNECTION_LOCAL not set"
    fi
    
    # Check LOG_SERVICE_DBCONNECTION_LOCAL
    if [ -n "$LOG_SERVICE_DBCONNECTION_LOCAL" ]; then
        if [[ "$LOG_SERVICE_DBCONNECTION_LOCAL" =~ ^[\'\"] ]]; then
            log_error "LOG_SERVICE_DBCONNECTION_LOCAL starts with quotes - remove them from .env.local"
            has_errors=1
        else
            echo "   ✓ LOG_SERVICE_DBCONNECTION_LOCAL format looks valid"
        fi
    else
        log_warn "LOG_SERVICE_DBCONNECTION_LOCAL not set"
    fi
    
    if [ $has_errors -eq 1 ]; then
        echo ""
        log_error "Connection string validation failed"
        echo ""
        echo "📝 How to fix your .env.local file:"
        echo "   Remove single or double quotes around connection strings"
        echo ""
        echo "   ❌ WRONG:"
        echo "   AUTH_SERVICE_DBCONNECTION_LOCAL='Server=mssql;...'"
        echo ""
        echo "   ✅ CORRECT:"
        echo "   AUTH_SERVICE_DBCONNECTION_LOCAL=Server=mssql;Database=AUTH_SERVICE_DB;User Id=sa;Password=StrongP@ssw0rd123!;TrustServerCertificate=true"
        echo ""
        exit 1
    fi
    
    log_info "Environment configured"
}

# ============================================================================
# Cleanup
# ============================================================================
cleanup_previous() {
    echo ""
    echo "🧹 Cleaning previous deployment..."
    
    if docker compose -f "$COMPOSE_FILE" ps --quiet 2>/dev/null | grep -q .; then
        log_warn "Found running services"
        docker compose -f "$COMPOSE_FILE" ps
        echo ""
        read -p "Stop and remove all containers and volumes? (Y/n): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Nn]$ ]]; then
            docker compose -f "$COMPOSE_FILE" down -v --remove-orphans
            log_info "Cleanup complete"
        else
            log_error "Cannot proceed with existing services running"
            exit 1
        fi
    else
        docker compose -f "$COMPOSE_FILE" down -v --remove-orphans 2>/dev/null || true
        log_info "No previous deployment found"
    fi
}

# ============================================================================
# Build Services
# ============================================================================
build_services() {
    echo ""
    echo "🔨 Building services..."
    
    if docker compose -f "$COMPOSE_FILE" build --no-cache; then
        log_info "Build successful"
    else
        log_error "Build failed"
        exit 1
    fi
}

# ============================================================================
# Infrastructure Management
# ============================================================================
start_infrastructure() {
    echo ""
    echo "🔄 Starting infrastructure services..."
    
    if docker compose -f "$COMPOSE_FILE" up -d mssql redis rabbitmq; then
        log_info "Infrastructure services started"
    else
        log_error "Failed to start infrastructure"
        exit 1
    fi
}

get_service_health() {
    local service=$1
    local health=""
    
    # Get container ID first
    local container_id=$(docker compose -f "$COMPOSE_FILE" ps -q "$service" 2>/dev/null)
    
    if [ -z "$container_id" ]; then
        echo "not_found"
        return
    fi
    
    # Method 1: Direct docker inspect (most reliable)
    health=$(docker inspect "$container_id" --format='{{if .State.Health}}{{.State.Health.Status}}{{else}}no_healthcheck{{end}}' 2>/dev/null)
    
    if [ "$health" = "no_healthcheck" ]; then
        # No healthcheck defined, check if running
        local state=$(docker inspect "$container_id" --format='{{.State.Status}}' 2>/dev/null)
        if [ "$state" = "running" ]; then
            echo "healthy"
        else
            echo "$state"
        fi
        return
    fi
    
    # Return the health status
    echo "$health"
}

wait_for_infrastructure() {
    echo ""
    echo "⏳ Waiting for infrastructure to be healthy..."
    
    local max_retries=40
    local retry_count=0
    local check_interval=10
    
    # Debug: Show diagnostic info on first run
    if [ "$VERBOSE" = true ]; then
        echo ""
        echo "DEBUG: Initial service diagnostics:"
        diagnose_service "mssql"
        diagnose_service "redis"
        diagnose_service "rabbitmq"
        echo ""
        echo "Starting health check loop..."
        echo "DEBUG: Testing get_service_health function..."
        echo "   mssql: $(get_service_health "mssql")"
        echo "   redis: $(get_service_health "redis")"
        echo "   rabbitmq: $(get_service_health "rabbitmq")"
        echo ""
    fi
    
    while [ $retry_count -lt $max_retries ]; do
        local mssql_status="unknown"
        local redis_status="unknown"
        local rabbitmq_status="unknown"
        
        mssql_status=$(get_service_health "mssql") || mssql_status="error"
        redis_status=$(get_service_health "redis") || redis_status="error"
        rabbitmq_status=$(get_service_health "rabbitmq") || rabbitmq_status="error"
        
        local healthy_count=0
        [ "$mssql_status" = "healthy" ] && healthy_count=$((healthy_count + 1))
        [ "$redis_status" = "healthy" ] && healthy_count=$((healthy_count + 1))
        [ "$rabbitmq_status" = "healthy" ] && healthy_count=$((healthy_count + 1))
        
        local progress=$((retry_count * 100 / max_retries))
        local elapsed=$((retry_count * check_interval))
        local max_time=$((max_retries * check_interval))
        
        # Simple status line that's compatible with all shells
        echo -ne "\r   ⏱️  ${progress}% [${elapsed}s/${max_time}s] - MSSQL: ${mssql_status} | Redis: ${redis_status} | RabbitMQ: ${rabbitmq_status}          "
        
        if [ "$healthy_count" -eq 3 ]; then
            echo ""
            log_info "All infrastructure services healthy"
            return 0
        fi
        
        # Check for failed containers
        local has_exited=0
        [ "$mssql_status" = "exited" ] && has_exited=1
        [ "$redis_status" = "exited" ] && has_exited=1
        [ "$rabbitmq_status" = "exited" ] && has_exited=1
        
        if [ $has_exited -eq 1 ]; then
            echo ""
            log_error "One or more services exited unexpectedly"
            docker compose -f "$COMPOSE_FILE" ps
            exit 1
        fi
        
        # Show detailed diagnostics every 5 retries in verbose mode
        if [ "$VERBOSE" = true ] && [ $((retry_count % 5)) -eq 4 ] && [ $retry_count -gt 0 ]; then
            echo ""
            echo "DEBUG: Current diagnostics (retry $((retry_count + 1))):"
            [ "$mssql_status" != "healthy" ] && diagnose_service "mssql"
            [ "$redis_status" != "healthy" ] && diagnose_service "redis"
            [ "$rabbitmq_status" != "healthy" ] && diagnose_service "rabbitmq"
            echo ""
        fi
        
        retry_count=$((retry_count + 1))
        sleep $check_interval
    done
    
    echo ""
    log_error "Infrastructure not healthy after $((max_retries * check_interval)) seconds"
    echo ""
    echo "📊 Final diagnostics:"
    diagnose_service "mssql"
    diagnose_service "redis"
    diagnose_service "rabbitmq"
    echo ""
    echo "📊 Docker Compose Status:"
    docker compose -f "$COMPOSE_FILE" ps mssql redis rabbitmq
    echo ""
    echo "🔍 Recent logs:"
    for service in mssql redis rabbitmq; do
        echo ""
        echo "=== $service (last 20 lines) ==="
        docker compose -f "$COMPOSE_FILE" logs "$service" --tail=20
    done
    exit 1
}

# ============================================================================
# Database Initialization
# ============================================================================
wait_for_sql_ready() {
    echo "   Verifying SQL Server is ready for queries..."
    
    local max_attempts=15
    for i in $(seq 1 $max_attempts); do
        if docker compose -f "$COMPOSE_FILE" exec -T mssql \
            /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
            -P "$MSSQL_SA_PASSWORD" -Q "SELECT 1" -C &>/dev/null; then
            log_info "SQL Server is ready"
            return 0
        fi
        echo -ne "   Attempt $i/$max_attempts...\r"
        sleep 3
    done
    
    echo ""
    log_error "SQL Server not ready after $max_attempts attempts"
    return 1
}

initialize_databases() {
    echo ""
    echo "🗃️  Initializing databases..."
    
    # Wait for SQL to be actually ready
    if ! wait_for_sql_ready; then
        exit 1
    fi
    
    # Verify SQL file accessibility
    echo "   Verifying SQL file accessibility..."
    if ! docker compose -f "$COMPOSE_FILE" exec -T mssql test -f "$SQL_MOUNT_PATH/init-database.sql"; then
        log_error "SQL file not found in container at $SQL_MOUNT_PATH/init-database.sql"
        echo ""
        echo "   Checking mounted files:"
        docker compose -f "$COMPOSE_FILE" exec -T mssql ls -la "$SQL_MOUNT_PATH/" || true
        exit 1
    fi
    
    # Execute SQL initialization
    echo "   Executing initialization script..."
    if docker compose -f "$COMPOSE_FILE" exec -T mssql \
        /opt/mssql-tools18/bin/sqlcmd \
        -S localhost \
        -U sa \
        -P "$MSSQL_SA_PASSWORD" \
        -i "$SQL_MOUNT_PATH/init-database.sql" \
        -C 2>&1 | tee /tmp/sql_init.log; then
        
        if grep -qi "error" /tmp/sql_init.log; then
            log_error "SQL initialization completed with errors"
            cat /tmp/sql_init.log
            exit 1
        fi
        
        log_info "Databases initialized successfully"
    else
        log_error "Database initialization failed"
        exit 1
    fi
}

# ============================================================================
# Application Services
# ============================================================================
start_applications() {
    echo ""
    echo "🚀 Starting application services..."
    
    if docker compose -f "$COMPOSE_FILE" up -d authservice userservice loggerservice nginx 2>&1 | tee /tmp/app_start.log; then
        # Check if there were any errors in the output
        if grep -qi "error\|unhealthy\|failed" /tmp/app_start.log; then
            log_warn "Issues detected during startup"
            
            # Show which services failed
            echo ""
            echo "📊 Current service status:"
            docker compose -f "$COMPOSE_FILE" ps
            
            # Diagnose unhealthy services
            echo ""
            echo "🔍 Diagnosing unhealthy services..."
            for service in authservice userservice loggerservice; do
                local health=$(get_service_health "$service")
                if [ "$health" != "healthy" ] && [ "$health" != "starting" ]; then
                    echo ""
                    diagnose_service "$service"
                    echo ""
                    echo "Recent logs for $service:"
                    docker compose -f "$COMPOSE_FILE" logs "$service" --tail=30
                fi
            done
            
            log_error "Application startup encountered issues"
            return 1
        fi
        log_info "Application services started"
    else
        log_error "Failed to start applications"
        echo ""
        echo "📊 Service status:"
        docker compose -f "$COMPOSE_FILE" ps
        return 1
    fi
}

wait_for_applications() {
    echo ""
    echo "⏳ Waiting for applications to be ready..."
    
    local max_wait=90
    local check_interval=5
    local elapsed=0
    
    echo "   Initial wait period (up to ${max_wait}s)..."
    
    while [ $elapsed -lt $max_wait ]; do
        local auth_health=$(get_service_health "authservice")
        local user_health=$(get_service_health "userservice")
        local logger_health=$(get_service_health "loggerservice")
        local nginx_health=$(get_service_health "nginx")
        
        echo -ne "\r   ⏱️  ${elapsed}s/${max_wait}s - Auth: ${auth_health} | User: ${user_health} | Logger: ${logger_health} | NGINX: ${nginx_health}          "
        
        # Check if any service has failed/exited
        if [ "$auth_health" = "exited" ] || [ "$auth_health" = "unhealthy" ]; then
            echo ""
            log_error "AuthService is $auth_health"
            echo ""
            diagnose_service "authservice"
            echo ""
            echo "📋 AuthService logs (last 50 lines):"
            docker compose -f "$COMPOSE_FILE" logs "authservice" --tail=50
            return 1
        fi
        
        if [ "$user_health" = "exited" ] || [ "$user_health" = "unhealthy" ]; then
            echo ""
            log_error "UserService is $user_health"
            echo ""
            diagnose_service "userservice"
            echo ""
            echo "📋 UserService logs (last 50 lines):"
            docker compose -f "$COMPOSE_FILE" logs "userservice" --tail=50
            return 1
        fi
        
        if [ "$logger_health" = "exited" ] || [ "$logger_health" = "unhealthy" ]; then
            echo ""
            log_error "LoggerService is $logger_health"
            echo ""
            diagnose_service "loggerservice"
            echo ""
            echo "📋 LoggerService logs (last 50 lines):"
            docker compose -f "$COMPOSE_FILE" logs "loggerservice" --tail=50
            return 1
        fi
        
        # For nginx, check if it's just starting or actually unhealthy
        if [ "$nginx_health" = "exited" ]; then
            echo ""
            log_error "NGINX exited unexpectedly"
            echo ""
            diagnose_service "nginx"
            echo ""
            echo "📋 NGINX logs (last 50 lines):"
            docker compose -f "$COMPOSE_FILE" logs "nginx" --tail=50
            return 1
        fi
        
        # Check if all backend services are healthy first (nginx depends on them)
        local backend_healthy=0
        [ "$auth_health" = "healthy" ] && backend_healthy=$((backend_healthy + 1))
        [ "$user_health" = "healthy" ] && backend_healthy=$((backend_healthy + 1))
        [ "$logger_health" = "healthy" ] && backend_healthy=$((backend_healthy + 1))
        
        # Check if all are healthy (including nginx)
        if [ $backend_healthy -eq 3 ] && [ "$nginx_health" = "healthy" ]; then
            echo ""
            log_info "All application services are healthy"
            return 0
        fi
        
        # If backends are healthy but nginx isn't yet, that's okay - keep waiting
        if [ $backend_healthy -eq 3 ] && [ "$nginx_health" = "starting" ]; then
            # Give nginx extra time since backends just became healthy
            if [ $elapsed -gt 60 ]; then
                echo ""
                log_warn "NGINX still starting after backends are healthy"
                echo ""
                echo "🔍 Testing backend services directly:"
                for service in authservice:5002 userservice:5001 loggerservice:5003; do
                    local name="${service%:*}"
                    local port="${service#*:}"
                    echo -n "   $name (:$port)... "
                    if curl -f -s -m 5 "http://localhost:$port/health" >/dev/null 2>&1 || \
                       curl -f -s -m 5 "http://localhost:$port/api/health" >/dev/null 2>&1; then
                        echo "✅ Reachable"
                    else
                        echo "❌ Unreachable"
                    fi
                done
                echo ""
                echo "🔍 Testing NGINX directly:"
                echo -n "   HTTP (:80) /health... "
                if curl -f -s -m 5 "http://localhost:80/health" >/dev/null 2>&1; then
                    echo "✅ Reachable"
                else
                    echo "❌ Unreachable"
                fi
                echo -n "   HTTPS (:443) /health... "
                if curl -f -s -k -m 5 "https://localhost:443/health" >/dev/null 2>&1; then
                    echo "✅ Reachable"
                else
                    echo "❌ Unreachable (SSL cert issue?)"
                fi
                echo ""
                echo "🔍 NGINX container status:"
                docker compose -f "$COMPOSE_FILE" exec nginx nginx -t 2>&1 | sed 's/^/   /' || echo "   Failed to test config"
                echo ""
                echo "📋 NGINX error logs:"
                docker compose -f "$COMPOSE_FILE" exec nginx cat /var/log/nginx/error.log 2>/dev/null | tail -20 | sed 's/^/   /' || \
                    echo "   (No error logs available)"
                echo ""
                echo "📋 NGINX access logs:"
                docker compose -f "$COMPOSE_FILE" exec nginx cat /var/log/nginx/access.log 2>/dev/null | tail -10 | sed 's/^/   /' || \
                    echo "   (No access logs available)"
            fi
        fi
        
        sleep $check_interval
        elapsed=$((elapsed + check_interval))
    done
    
    echo ""
    
    # Check final state
    local auth_health=$(get_service_health "authservice")
    local user_health=$(get_service_health "userservice")
    local logger_health=$(get_service_health "loggerservice")
    local nginx_health=$(get_service_health "nginx")
    
    local backend_healthy=0
    [ "$auth_health" = "healthy" ] && backend_healthy=$((backend_healthy + 1))
    [ "$user_health" = "healthy" ] && backend_healthy=$((backend_healthy + 1))
    [ "$logger_health" = "healthy" ] && backend_healthy=$((backend_healthy + 1))
    
    if [ $backend_healthy -eq 3 ]; then
        if [ "$nginx_health" != "healthy" ]; then
            log_warn "Backend services healthy, but NGINX is $nginx_health (continuing anyway)"
            echo ""
            echo "💡 NGINX may need more time. You can check manually:"
            echo "   curl http://localhost:80"
            echo "   docker compose -f $COMPOSE_FILE logs nginx"
        else
            log_info "All services including NGINX are healthy"
            return 0
        fi
    else
        log_warn "Some services not healthy yet after ${max_wait}s"
    fi
    
    echo ""
    echo "📊 Final service status:"
    docker compose -f "$COMPOSE_FILE" ps
    
    # Show logs for any unhealthy services
    for service in authservice userservice loggerservice nginx; do
        local health=$(get_service_health "$service")
        if [ "$health" != "healthy" ]; then
            echo ""
            echo "⚠️  $service is $health - recent logs:"
            docker compose -f "$COMPOSE_FILE" logs "$service" --tail=20
        fi
    done
    
    return 0
}

# ============================================================================
# Health Checks
# ============================================================================
run_health_checks() {
    echo ""
    echo "🏥 Running health checks..."
    
    local services=(
        "authservice:5002:/api/auth/health"
        "userservice:5001:/api/user/health"
        "loggerservice:5003:/api/logger/health"
        "nginx:80:/"
    )
    
    local failed=0
    local max_retries=3
    
    for service_info in "${services[@]}"; do
        IFS=':' read -r name port path <<< "$service_info"
        
        echo -n "   $name (http://localhost:$port$path)... "
        
        local success=false
        for attempt in $(seq 1 $max_retries); do
            if curl -f -s -m 5 "http://localhost:$port$path" >/dev/null 2>&1; then
                success=true
                break
            fi
            [ $attempt -lt $max_retries ] && sleep 2
        done
        
        if $success; then
            echo -e "${GREEN}✅ Healthy${NC}"
        else
            echo -e "${RED}❌ Failed${NC}"
            ((failed++))
        fi
    done
    
    echo ""
    if [ $failed -eq 0 ]; then
        log_info "All health checks passed"
        return 0
    else
        log_warn "$failed service(s) failed health checks (may need more time)"
        return 1
    fi
}

# ============================================================================
# Summary Report
# ============================================================================
print_summary() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "🎉 Unified Setup Complete!"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""
    echo "📡 Service Endpoints:"
    echo "   • AuthService:     http://localhost:5002/health"
    echo "   • UserService:     http://localhost:5001/health"
    echo "   • LoggerService:   http://localhost:5003/health"
    echo "   • NGINX (HTTP):    http://localhost:80/health"
    echo "   • NGINX (HTTPS):   https://localhost:443/health (self-signed cert)"
    echo "   • RabbitMQ Admin:  http://localhost:15672 (guest/guest)"
    echo ""
    echo "🌐 API Access (via NGINX):"
    echo "   • Auth API:        https://localhost/api/auth/"
    echo "   • User API:        https://localhost/api/user/"
    echo "   • Logger API:      https://localhost/api/logger/"
    echo ""
    echo "📋 Useful Commands:"
    echo "   • View logs:       docker compose -f $COMPOSE_FILE logs -f [service]"
    echo "   • Check status:    docker compose -f $COMPOSE_FILE ps"
    echo "   • Restart service: docker compose -f $COMPOSE_FILE restart [service]"
    echo "   • Stop services:   docker compose -f $COMPOSE_FILE down"
    echo "   • Full cleanup:    docker compose -f $COMPOSE_FILE down -v"
    echo ""
    echo "🔧 Testing Endpoints:"
    echo "   curl http://localhost:5002/health      # Auth direct"
    echo "   curl http://localhost:80/health        # NGINX health"
    echo "   curl -k https://localhost/api/auth/health  # Via NGINX"
    echo ""
    
    # Check if nginx is actually healthy
    local nginx_health=$(get_service_health "nginx")
    if [ "$nginx_health" != "healthy" ]; then
        echo "⚠️  NGINX Healthcheck Recommendation:"
        echo "   Your NGINX healthcheck uses TCP connection test only."
        echo "   Consider updating docker-compose.local.yml:"
        echo ""
        echo "   healthcheck:"
        echo "     test: [\"CMD-SHELL\", \"curl -f http://localhost:80/health || exit 1\"]"
        echo "     interval: 10s"
        echo "     timeout: 5s"
        echo "     retries: 3"
        echo "     start_period: 10s"
        echo ""
    fi
    
    echo "📝 Setup log saved to: $LOG_FILE"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
}

# ============================================================================
# Main Execution
# ============================================================================
main() {
    echo "🚀 Unified Microservices Setup"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    
    check_dependencies
    validate_setup
    load_environment
    cleanup_previous
    build_services
    start_infrastructure
    wait_for_infrastructure
    initialize_databases
    
    # Start applications (may fail if unhealthy)
    if ! start_applications; then
        echo ""
        log_error "Application startup failed. Check logs above for details."
        echo ""
        echo "💡 Common issues:"
        echo "   • Database connection strings incorrect"
        echo "   • JWT keys missing or invalid"
        echo "   • Port conflicts"
        echo "   • Missing environment variables"
        echo ""
        echo "🔧 To debug:"
        echo "   docker compose -f $COMPOSE_FILE logs authservice --tail=50"
        echo "   docker compose -f $COMPOSE_FILE ps"
        echo ""
        exit 1
    fi
    
    # Wait for applications (will show diagnostics if issues found)
    if ! wait_for_applications; then
        echo ""
        log_error "Application health checks failed. See diagnostics above."
        echo ""
        echo "🛑 Services are running but unhealthy. You can:"
        echo "   • Check logs: docker compose -f $COMPOSE_FILE logs [service]"
        echo "   • Stop all: docker compose -f $COMPOSE_FILE down"
        echo ""
        exit 1
    fi
    
    run_health_checks || true  # Don't fail on health check warnings
    print_summary
}

# Run main function
main "$@"