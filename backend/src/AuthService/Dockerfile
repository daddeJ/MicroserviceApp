# AuthService/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5002

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files relative to build context
COPY AuthService/AuthService.csproj AuthService/
COPY Shared/Shared.csproj Shared/

# Restore only the project
RUN dotnet restore AuthService/AuthService.csproj

# Copy all source files
COPY . ./

# Build & publish
WORKDIR /src/AuthService
RUN dotnet build AuthService.csproj -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish AuthService.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AuthService.dll"]
