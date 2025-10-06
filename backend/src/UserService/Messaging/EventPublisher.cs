using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;
using Shared.Helpers;

namespace UserService.Messaging;

public class EventPublisher : IEventPublisher
{
    private readonly RabbitMqConnectionHelper _rabbitHelper;

    public EventPublisher(RabbitMqConnectionHelper rabbitHelper)
    {
        _rabbitHelper = rabbitHelper;
    }
    public async Task PublishAsync<T>(string routingKey, T @event)
    {
        using var channel = await _rabbitHelper.GetChannelAsync();
        
        var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@event));
    
        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: routingKey,
            mandatory: false,
            body: body
        );
    }
}