namespace LoggerService.Consumers;

public interface IActivityConsumer
{
    Task StartConsumingAsync(CancellationToken cancellationToken);
}