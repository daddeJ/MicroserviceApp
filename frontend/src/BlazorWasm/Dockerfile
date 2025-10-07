FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["BlazorWasm/BlazorWasm.csproj", "BlazorWasm/"]
RUN dotnet restore "BlazorWasm/BlazorWasm.csproj"
COPY . .
WORKDIR "/src/BlazorWasm"
RUN dotnet publish "BlazorWasm.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BlazorWasm.dll"]
