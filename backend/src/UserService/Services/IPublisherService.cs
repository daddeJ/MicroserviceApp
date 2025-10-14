namespace UserService.Services;

public interface IPublisherService
{
    Task PublishLogAsync(Guid userId, string action, string? metadata = null);
    Task PublishTokenAndActivityEvents(Guid userId, string userAction);
}