namespace Shared.Events;

public sealed class AuthActivityEvent
{
    public Guid UserId { get; set; }
    public string Action { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Metadata { get; set; }

    public AuthActivityEvent()
    {
    }
    
    public AuthActivityEvent(Guid userId, string action, DateTime timestamp, string? metadata = null)
    {
        UserId = userId;
        Action = action ?? throw new ArgumentNullException(nameof(action));
        Timestamp = timestamp;
        Metadata = metadata;
    }
}