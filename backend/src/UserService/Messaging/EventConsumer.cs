using Shared.Helpers;
using Shared.Events;
using System.Text;
using System.Text.Json;
using UserService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace UserService.Messaging
{
    public class EventConsumer
    {
        private readonly RabbitMqConnectionHelper _rabbitHelper;
        private readonly IUserService _userService;

        public EventConsumer(RabbitMqConnectionHelper rabbitHelper, IUserService userService)
        {
            _rabbitHelper = rabbitHelper;
            _userService = userService;
        }

        public async void StartConsuming()
        {
            using var channel = await _rabbitHelper.GetChannelAsync();
            
            channel.QueueDeclareAsync(
                queue: "auth.token.generated",
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                await Task.Run(async () =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        var authEvent = JsonSerializer.Deserialize<AuthTokenGeneratedEvent>(message);

                        if (authEvent != null)
                        {
                            await _userService.HandleAuthTokenEventAsync(authEvent.UserId, authEvent.Token);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing message: {ex}");
                    }
                });
            };
            
            channel.BasicConsumeAsync(
                queue: "auth.token.generated",
                autoAck: true,
                consumer: consumer
            );
        }
    }
}
