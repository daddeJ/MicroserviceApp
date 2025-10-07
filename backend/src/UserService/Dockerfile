# Dockerfile (UserService)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# COPY csproj files relative to build context
COPY UserService/UserService.csproj UserService/
COPY Shared/Shared.csproj Shared/

# Restore only the project
RUN dotnet restore UserService/UserService.csproj

# Copy all source files
COPY . ./

# Build & publish
WORKDIR /src/UserService
RUN dotnet build UserService.csproj -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish UserService.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UserService.dll"]
