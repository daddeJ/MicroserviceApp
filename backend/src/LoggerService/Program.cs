using System.Data;
using LoggerService.Consumers;
using LoggerService.Data;
using LoggerService.Enricher;
using LoggerService.Extensions;
using LoggerService.Factories;
using LoggerService.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Shared.Extensions;

// Enable Serilog self-log for internal errors
SelfLog.Enable(msg => Console.WriteLine($"SERILOG ERROR: {msg}"));

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ========================
    // Add shared factories and infrastructure
    // ========================
    builder.Services.AddSharedFactories(builder.Configuration);

    // ========================
    // Register DI services
    // ========================
    builder.Services.AddSingleton<ILoggerActionFactory, LoggerActionFactory>();
    builder.Services.AddSingleton<IActivityConsumer, ActivityConsumer>();

    // Hosted service to consume messages
    builder.Services.AddHostedService<UserActivityConsumerService>();

    // ========================
    // Configure Serilog
    // ========================
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        var connectionString = context.Configuration.GetConnectionString("LoggerServiceConnection");

        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithCorrelationId()
            .Enrich.WithClientIp()
            .Enrich.With(new ApplicationLogIdEnricher())
            .WriteTo.Console()
            .WriteTo.File(
                path: "logs/log-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                restrictedToMinimumLevel: LogEventLevel.Information);

        if (!string.IsNullOrEmpty(connectionString))
        {
            try
            {
                configuration.AddCustomSqlLogger(connectionString);
                Console.WriteLine("✅ SQL Logger configured successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to configure SQL Logger: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("⚠️ WARNING: LoggerServiceConnection is null or empty!");
        }
    });

    Log.Information("=== Starting Logger Service ===");

    // ========================
    // Add DbContext with retry logic
    // ========================
    builder.Services.AddDbContext<LoggerDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("LoggerServiceConnection"),
            sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null
            )
        ));

    var app = builder.Build();

    // ========================
    // Database migration (optional but recommended)
    // ========================
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoggerDbContext>();
        
        Log.Information("Checking database connection...");
        await dbContext.Database.CanConnectAsync();
        Log.Information("✅ Database connection successful");
        
        // Apply migrations if needed
        // await dbContext.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "⚠️ Database connection failed - service will continue without database");
    }

    // Middleware for Serilog request logging
    app.UseSerilogRequestLogging();

    // Health endpoint - should respond even if database is down
    app.MapGet("/api/logger/health", async (LoggerDbContext dbContext) =>
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync();
            return Results.Ok(new
            {
                status = canConnect ? "Healthy" : "Degraded",
                timestamp = DateTime.UtcNow,
                database = canConnect ? "Connected" : "Disconnected"
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Health check database connection failed");
            return Results.Ok(new
            {
                status = "Degraded",
                timestamp = DateTime.UtcNow,
                database = "Error",
                error = ex.Message
            });
        }
    });

    Log.Information("LoggerService is running...");
    Log.Warning("Test warning log");
    Log.Error("Test error log");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "LoggerService failed to start");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}