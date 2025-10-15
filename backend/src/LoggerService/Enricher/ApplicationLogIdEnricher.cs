using Serilog.Core;
using Serilog.Events;

namespace LoggerService.Enricher;

public class ApplicationLogIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Try to get correlation ID from log context (Serilog enrichers)
        var correlationId = logEvent.Properties.ContainsKey("CorrelationId")
            ? logEvent.Properties["CorrelationId"].ToString().Trim('"')
            : Guid.NewGuid().ToString();

        logEvent.AddOrUpdateProperty(
            propertyFactory.CreateProperty("ApplicationLogId", correlationId));
    }
}