#!/bin/bash
# ============================================================
# MicroserviceApp: Full Development Environment Setup Script
# ============================================================

set -e  # Exit on error

# ---------- CONFIGURATION ----------
ROOT_DIR="$(pwd)"
SOLUTION_NAME="MicroserviceApp.sln"
SRC_DIR="${ROOT_DIR}/backend/src"
KEYS_DIR="${ROOT_DIR}/backend/keys"
PRIVATE_KEY_FILE="${KEYS_DIR}/jwt_private.pem"
PUBLIC_KEY_FILE="${KEYS_DIR}/jwt_public.pem"
DOCKER_COMPOSE_FILE="${ROOT_DIR}/docker-compose.yml"

MSSQL_SA_PASSWORD="StrongP@ssword123!"
REDIS_PORT=6379
RABBITMQ_PORT=5672
MSSQL_PORT=1433

SERVICES=("UserService" "LoggerService" "AuthService" "OrderService" "CustomerService")

# ---------- STEP 1: CHECK DEPENDENCIES ----------
echo "🔍 Checking dependencies..."
for dep in docker dotnet openssl; do
    if ! command -v $dep &>/dev/null; then
        echo "❌ Missing dependency: $dep. Please install it first."
        exit 1
    fi
done
echo "✅ Dependencies OK."

# ---------- STEP 2: CREATE PROJECT STRUCTURE ----------
echo "📁 Creating folder structure..."
mkdir -p "${SRC_DIR}"
mkdir -p "${KEYS_DIR}"

# ---------- STEP 3: CREATE SOLUTION FILE ----------
if [ ! -f "${ROOT_DIR}/${SOLUTION_NAME}" ]; then
    echo "🧩 Creating new solution: ${SOLUTION_NAME}"
    dotnet new sln -n MicroserviceApp
else
    echo "✅ Solution file already exists."
fi

# ---------- STEP 4: GENERATE RSA KEY PAIR ----------
echo "🔐 Generating RSA 4096-bit key pair..."
if [ ! -f "$PRIVATE_KEY_FILE" ] || [ ! -f "$PUBLIC_KEY_FILE" ]; then
    openssl genpkey -algorithm RSA -out "$PRIVATE_KEY_FILE" -pkeyopt rsa_keygen_bits:4096
    openssl rsa -pubout -in "$PRIVATE_KEY_FILE" -out "$PUBLIC_KEY_FILE"
    echo "✅ Keys generated:"
    echo "   - Private: $PRIVATE_KEY_FILE"
    echo "   - Public:  $PUBLIC_KEY_FILE"
else
    echo "✅ RSA keys already exist."
fi

# ---------- STEP 5: CREATE MICROSERVICE PROJECTS ----------
for service in "${SERVICES[@]}"; do
    SERVICE_PATH="${SRC_DIR}/${service}"
    if [ ! -d "$SERVICE_PATH" ]; then
        echo "⚙️ Creating project: $service"
        dotnet new webapi -n "$service" -o "$SERVICE_PATH" --no-https
    else
        echo "✅ $service already exists."
    fi
done

# ---------- STEP 6: ADD PROJECTS TO SOLUTION ----------
echo "📦 Adding projects to solution..."
dotnet sln "${ROOT_DIR}/${SOLUTION_NAME}" add $(find "${SRC_DIR}" -name "*.csproj")

# ---------- STEP 7: EXPORT ENVIRONMENT VARIABLES ----------
echo "🌍 Setting up environment variables..."
export ConnectionStrings__MSSQL="Server=localhost,${MSSQL_PORT};Database=MicroserviceDB;User=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;"
export ConnectionStrings__Redis="localhost:${REDIS_PORT}"
export ConnectionStrings__RabbitMQ="amqp://guest:guest@localhost:${RABBITMQ_PORT}/"
export JWT__PrivateKeyPath="${PRIVATE_KEY_FILE}"
export JWT__PublicKeyPath="${PUBLIC_KEY_FILE}"
export ASPNETCORE_ENVIRONMENT="Development"

echo "✅ Environment variables exported."

# ---------- STEP 8: RESTORE & BUILD SOLUTION ----------
echo "⚙️ Restoring and building solution..."
dotnet restore "${ROOT_DIR}/${SOLUTION_NAME}"
dotnet build "${ROOT_DIR}/${SOLUTION_NAME}" -c Debug
echo "✅ Build complete."

# ---------- STEP 9: DOCKER COMPOSE ----------
if [ ! -f "$DOCKER_COMPOSE_FILE" ]; then
    echo "🐳 Creating docker-compose.yml..."
    cat <<EOF > "$DOCKER_COMPOSE_FILE"
version: '3.9'

services:
  mssql:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: mssql
    ports:
      - "1433:1433"
    environment:
      SA_PASSWORD: "${MSSQL_SA_PASSWORD}"
      ACCEPT_EULA: "Y"
    volumes:
      - mssql_data:/var/opt/mssql

  redis:
    image: redis:7
    container_name: redis
    ports:
      - "6379:6379"

  rabbitmq:
    image: rabbitmq:3-management
    container_name: rabbitmq
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest

volumes:
  mssql_data:
EOF
else
    echo "✅ docker-compose.yml already exists."
fi

# ---------- STEP 10: START CONTAINERS ----------
echo "🚀 Starting infrastructure containers..."
docker compose -f "$DOCKER_COMPOSE_FILE" up -d
echo "✅ Containers running."

# ---------- STEP 11: SUMMARY ----------
echo ""
echo "============================================================"
echo "✅ MICROservices Development Environment Ready"
echo "------------------------------------------------------------"
echo " MSSQL:     localhost:${MSSQL_PORT}"
echo " Redis:     localhost:${REDIS_PORT}"
echo " RabbitMQ:  localhost:${RABBITMQ_PORT} (UI: http://localhost:15672)"
echo "------------------------------------------------------------"
echo " JWT Private Key: ${PRIVATE_KEY_FILE}"
echo " JWT Public Key:  ${PUBLIC_KEY_FILE}"
echo "------------------------------------------------------------"
echo " Run a service manually:"
echo "   cd backend/src/AuthService && dotnet run"
echo "============================================================"

