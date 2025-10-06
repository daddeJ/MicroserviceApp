namespace Shared.Events;

public sealed class UserActivityEvent
{
    public Guid UserId { get; set; }
    public string ActivityType { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Metadata { get; set; }

    public UserActivityEvent()
    {
    }
    
    public UserActivityEvent(Guid userId, string activityType, DateTime timestamp, string? metadata = null)
    {
        UserId = userId;
        ActivityType = activityType ?? throw new ArgumentNullException(nameof(activityType));
        Timestamp = timestamp;
        Metadata = metadata;
    }
}