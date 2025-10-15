using System.Collections.ObjectModel;
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
using Serilog.Sinks.MSSqlServer;
using Shared.Extensions;

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

    // HostedService
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
            configuration.AddCustomSqlLogger(connectionString);
        }
        else
        {
            Console.WriteLine("⚠️ WARNING: LoggerServiceConnection is null or empty!");
        }
    });

    Log.Information("=== Starting Logger Service ===");

    // ========================
    // Add DbContext
    // ========================
    builder.Services.AddDbContext<LoggerDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("LoggerServiceConnection")));

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    Log.Information("LoggerService is running...");
    Log.Warning("Test warning log");
    Log.Error("Test error log");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "LoggerService failed to start");
}
finally
{
    Log.CloseAndFlush();
}
