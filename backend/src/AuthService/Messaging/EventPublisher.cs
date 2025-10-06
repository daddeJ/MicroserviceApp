using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Shared.Helpers;

namespace AuthService.Messaging;

public class EventPublisher : IEventPublisher
{
    private readonly RabbitMqConnectionHelper _rabbitHelper;

    public EventPublisher(RabbitMqConnectionHelper rabbitHelper)
    {
        _rabbitHelper = rabbitHelper;
    }
    
    public async Task PublishAsync<T>(string routingKey, T @event)
    {
        var channel = await _rabbitHelper.GetChannelAsync();
        
        // Declare queue before publishing (idempotent - safe to call every time)
        await channel.QueueDeclareAsync(
            queue: routingKey, // routing key is the queue name when using default exchange
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );
        
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
    
        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: routingKey,
            mandatory: false,
            body: body
        );
    }
}