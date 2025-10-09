using Serilog.Context;

namespace Shared.Logging;

public static class LogContextHelper
{
    public static IDisposable WithCorrelationId(string correlationId)
    {
        return LogContext.PushProperty("CorrelationId", correlationId);
    }
}