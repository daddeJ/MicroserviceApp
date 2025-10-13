using System.Text;
using System.Text.Json;
using LoggerService.Factories;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using Shared.Constants;
using Shared.Events;
using Shared.Helpers;

namespace LoggerService.Consumers;

public class ActivityConsumer : IActivityConsumer
{
    private readonly RabbitMqConnectionHelper _rabbitHelper;
    private readonly ILoggerActionFactory _loggerActionFactory;
    public ActivityConsumer(RabbitMqConnectionHelper rabbitHelper, ILoggerActionFactory loggerActionFactory)
    {
        _rabbitHelper = rabbitHelper;
        _loggerActionFactory = loggerActionFactory;
    }

    public async Task StartConsumingAsync()
    {
        var channel = await _rabbitHelper.GetChannelAsync();

        await channel.QueueDeclareAsync(
            queue: QueueNames.LoggerActivity,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            
            var activity = JsonSerializer.Deserialize<UserActivityEvent>(message);

            if (activity != null)
            {
                _loggerActionFactory.LogActivity(activity);
            }
            
            await Task.CompletedTask;
        };
        
        await channel.BasicConsumeAsync(
            queue: QueueNames.AuthActivity,
            autoAck: false,
            consumer: consumer
        );
        Log.Information("Listening to 'auth.activity' queue...");
    }
}