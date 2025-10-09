namespace Shared.Interfaces;

public interface IMessagePublisher
{
    Task PublishAsync<T>(string queueName, T @event);
}