namespace Shared.Events;

public sealed class ErrorActivityEvent
{
    public string Source { get; set; }           // Where the error occurred (Controller, Service, etc.)
    public string ErrorType { get; set; }        // Type of error (Validation, Business, System)
    public string Message { get; set; }          // The error message
    public string? StackTrace { get; set; }      // Optional stack trace
    public DateTime Timestamp { get; set; }      // When the error occurred
    public Guid? UserId { get; set; }          // Optional user context
    public string? Endpoint { get; set; }        // Optional API endpoint or action
    public string? Metadata { get; set; }  
    public string Category { get; set; }
    public string Description { get; set; }
    public string DefaultLogLevel { get; set; }// Optional additional data

    public ErrorActivityEvent() { }

    public ErrorActivityEvent(
        string source,
        string errorType,
        string message,
        DateTime timestamp,
        string category,
        string description,
        string defaultLogLevel,
        string? stackTrace = null,
        Guid? userId = null,
        string? endpoint = null,
        string? metadata = null)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        ErrorType = errorType ?? throw new ArgumentNullException(nameof(errorType));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Timestamp = timestamp;
        StackTrace = stackTrace;
        UserId = userId;
        Endpoint = endpoint;
        Metadata = metadata;
        Category = category ?? throw new ArgumentNullException(nameof(category));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        DefaultLogLevel = defaultLogLevel ?? "Error";
    }
}