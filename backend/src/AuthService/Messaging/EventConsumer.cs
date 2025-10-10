using System.Text;
using System.Text.Json;
using AuthService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Constants;
using Shared.Events;
using Shared.Helpers;

namespace AuthService.Messaging;

public class EventConsumer
{
    private readonly RabbitMqConnectionHelper _rabbitHelper;
    private readonly IAuthService _authService;

    public EventConsumer(RabbitMqConnectionHelper rabbitHelper, IAuthService authService)
    {
        _rabbitHelper = rabbitHelper;
        _authService = authService;
    }

    public async Task StartConsuming()
    {
        var channel = await _rabbitHelper.GetChannelAsync();

        await channel.QueueDeclareAsync(
            queue: QueueNames.GenerateTokenActivity,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                var userEvent = JsonSerializer.Deserialize<UserActivityEvent>(message);
                if (userEvent != null)
                {
                    await _authService.HandleUserAuthenticationTokenAsync(userEvent.UserId);
                }

                channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex}");
            }
        };

        await channel.BasicConsumeAsync(
            queue: QueueNames.GenerateTokenActivity,
            autoAck: false,
            consumer: consumer
        );
    }
}
