using System.Text.Json;
using Shared.Constants;
using Shared.Events;
using Shared.Factories;
using Shared.Helpers;
using Shared.Interfaces;

namespace UserService.Services;

public class PublishedService : IPublisherService
{
    private readonly IUserActionFactory _userActionFactory;
    private readonly IMessagePublisher _messagePublisher;

    public PublishedService(IUserActionFactory userActionFactory, IMessagePublisher messagePublisher)
    {
        _userActionFactory = userActionFactory;
        _messagePublisher = messagePublisher;
    }
    public async Task PublishLogAsync(Guid userId, string action, string? metadata = null)
    {
        var meta = _userActionFactory.GetMetadata(action);

        var logEvent = new UserActivityEvent(
            userId: userId,
            action: meta.Action,
            category: meta.Category,
            description: meta.Description,
            defaultLogLevel: meta.DefaultLogLevel,
            timestamp: DateTime.UtcNow,
            metadata: metadata);
        
        await _messagePublisher.PublishAsync(QueueNames.LoggerActivity, logEvent);
    }
    
    public async Task PublishTokenAndActivityEvents(Guid userId, string userAction)
    {
        var authActivity = new AuthActivityEvent()
        {
            UserId = userId,
            Action = "generate_token",
            Timestamp = DateTime.UtcNow
        };
        await _messagePublisher.PublishAsync(QueueNames.GenerateTokenActivity, authActivity);

        var userActivity = new UserActivityEvent
        {
            UserId = userId,
            Action = userAction,
            Timestamp = DateTime.UtcNow
        };
        await _messagePublisher.PublishAsync(QueueNames.UserActivity, userActivity);
    }
}