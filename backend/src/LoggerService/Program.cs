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

// ----------------------
// Enable Serilog internal logging
// ----------------------
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
    builder.Services.AddHostedService<UserActivityConsumerService>(); // background consumer

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
    // Database migration & initial check
    // ========================
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoggerDbContext>();

        var connected = false;
        var retries = 5;
        while (!connected && retries-- > 0)
        {
            try
            {
                Log.Information("Checking database connection...");
                connected = await dbContext.Database.CanConnectAsync();
                if (connected)
                {
                    Log.Information("✅ Database connection successful");
                    await dbContext.Database.MigrateAsync(); // ensure schema is applied
                }
                else
                {
                    Log.Warning("Database not ready, retrying...");
                    await Task.Delay(5000); // wait 5 sec before retry
                }
            }
            catch
            {
                Log.Warning("Database not ready, retrying...");
                await Task.Delay(5000);
            }
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "⚠️ Database connection failed - service will continue without database");
    }

    // ========================
    // Middleware
    // ========================
    app.UseSerilogRequestLogging();

    // ========================
    // Health endpoint
    // ========================
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
