using LoggerService.Consumers;
using Serilog;
using Shared.Helpers;

namespace LoggerService.Startup;

public static class LoggerStartupHelper
{
    public static async Task InitializeAndStartConsumersAsync()
    {
        var rabbitHelper = new RabbitMqConnectionHelper();

        var userConsumer = new UserActivityConsumer(rabbitHelper);
        var authConsumer = new AuthActivityConsumer(rabbitHelper);

        await Task.WhenAll(
            userConsumer.StartConsumingAsync(),
            authConsumer.StartConsumingAsync()
        );

        Log.Information("🚀 LoggerService is running...");
    }
}