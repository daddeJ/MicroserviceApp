using LoggerService.Consumers;
using LoggerService.Factories;
using LoggerService.Services;
using Serilog;
using Serilog.Events;
using Shared.Extensions;

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
                restrictedToMinimumLevel: LogEventLevel.Information);

        // Uncomment this when DB is ready
        // .WriteTo.Async(a => a.MSSqlServer(
        //     connectionString: context.Configuration.GetConnectionString("LoggerServiceConnection"),
        //     sinkOptions: new MSSqlServerSinkOptions
        //     {
        //         TableName = "ApplicationLogs",
        //         AutoCreateSqlTable = true
        //     },
        //     restrictedToMinimumLevel: LogEventLevel.Information
        // ));
    });

    Log.Information("=== Starting Logger Service ===");

    // ========================
    // Add DbContext (commented out for now)
    // ========================
    // builder.Services.AddDbContext<LoggerDbContext>(options =>
    //     options.UseSqlServer(builder.Configuration.GetConnectionString("LoggerServiceConnection")));

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
