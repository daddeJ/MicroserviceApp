using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using Shared.Events;
using Shared.Helpers;

namespace LoggerService.Consumers;

public class AuthActivityConsumer
{
    private readonly RabbitMqConnectionHelper _rabbitHelper;

    public AuthActivityConsumer(RabbitMqConnectionHelper rabbitHelper)
    {
        _rabbitHelper = rabbitHelper;
    }

    public async Task StartConsumingAsync()
    {
        var channel = await _rabbitHelper.GetChannelAsync();

        await channel.QueueDeclareAsync(
            queue: "auth.activity",
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
                Log.Information("[Auth Activity] UserId: {UserId}, Type: {ActivityType}, Time: {Timestamp}",
                    activity.UserId, activity.Action, activity.Timestamp);
            }
            
            await Task.CompletedTask;
        };
        
        await channel.BasicConsumeAsync(
            queue: "auth.activity",
            autoAck: false,
            consumer: consumer
        );
        Log.Information("Listening to 'auth.activity' queue...");
    }
}