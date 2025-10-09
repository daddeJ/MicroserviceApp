namespace Shared.Messaging;

public class MessageEnvelope<T>
{
    public string EventType { get; set; } = string.Empty;
    public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
    public T Payload { get; set; } = default;
}