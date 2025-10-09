using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using RabbitMQ.Client;
using Shared.Helpers;
using Shared.Interfaces;

namespace Shared.Messaging;

public class EventBus : IMessagePublisher
{
    private readonly RabbitMqConnectionHelper _settings;

    public EventBus(RabbitMqConnectionHelper settings)
    {
        _settings = settings;
    }

    public async Task PublishAsync<T>(string queueName, T @event)
    {
        using var channel = await _settings.GetChannelAsync();
        
        channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false);
        
        var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@event));
        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: queueName,
            mandatory: false,
            body: body
        );
    }

}