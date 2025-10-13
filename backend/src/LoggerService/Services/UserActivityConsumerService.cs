using LoggerService.Consumers;
using Serilog;

namespace LoggerService.Services;

public class UserActivityConsumerService : BackgroundService
{
    private readonly IActivityConsumer _consumer;

    public UserActivityConsumerService(IActivityConsumer consumer)
    {
        _consumer = consumer;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Starting UserActivityConsumer...");
        await _consumer.StartConsumingAsync();
    }
}