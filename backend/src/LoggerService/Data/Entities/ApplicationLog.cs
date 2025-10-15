namespace LoggerService.Data.Entities;

public class ApplicationLog
{
    public int Id { get; set; }
    public Guid ApplicationLogId { get; set; } = Guid.NewGuid();
    public string Message { get; set; }
    public string MessageTemplate { get; set; }
    public string Level { get; set; }
    public DateTime Timestamp { get; set; }
    public string Exception { get; set; }
    public string Properties { get; set; }
}