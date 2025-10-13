using LoggerService.Consumers;
using LoggerService.Data;
using LoggerService.Factories;
using LoggerService.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;
using Shared.Extensions;

try
{
    var builder = WebApplication.CreateBuilder(args);
    
    builder.Services.AddSharedFactories(builder.Configuration);
    
    builder.Services.AddSingleton<ILoggerActionFactory, LoggerActionFactory>();
    builder.Services.AddSingleton<IActivityConsumer, ActivityConsumer>();
    builder.Services.AddHostedService<UserActivityConsumerService>();

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithCorrelationId()
            .Enrich.WithClientIp()
            .WriteTo.Console()
            .WriteTo.File(
                path: "logs/log-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.Async(a => a.MSSqlServer(
                connectionString: context.Configuration.GetConnectionString("LoggerServiceConnection"),
                sinkOptions: new MSSqlServerSinkOptions
                {
                    TableName = "ApplicationLogs",
                    AutoCreateSqlTable = true
                },
                restrictedToMinimumLevel: LogEventLevel.Information
            ));
    });

    Log.Information("=== Starting Logger Service ===");

    builder.Services.AddDbContext<LoggerDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("LoggerServiceConnection")));

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    Log.Information("LoggerService is running...");
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