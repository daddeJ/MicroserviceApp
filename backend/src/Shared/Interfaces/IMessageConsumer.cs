namespace Shared.Interfaces;

public interface IMessageConsumer
{
    void Consume(string queueName);
}