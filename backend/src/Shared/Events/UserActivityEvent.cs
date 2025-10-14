namespace Shared.Events;

public sealed class UserActivityEvent
{
    public Guid UserId { get; set; }
    public string Action { get; set; }
    public string Category { get; set; }
    public string Description { get; set; }
    public string DefaultLogLevel { get; set; }
    public DateTime Timestamp { get; set; }

    public string? Metadata { get; set; }

    public UserActivityEvent() { }

    public UserActivityEvent(
        Guid userId,
        string action,
        string category,
        string description,
        string defaultLogLevel,
        DateTime timestamp,
        string? metadata = null)
    {
        UserId = userId;
        Action = action ?? throw new ArgumentNullException(nameof(action));
        Category = category ?? throw new ArgumentNullException(nameof(category));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        DefaultLogLevel = defaultLogLevel ?? "Information";
        Timestamp = timestamp;
        Metadata = metadata;
    }
}