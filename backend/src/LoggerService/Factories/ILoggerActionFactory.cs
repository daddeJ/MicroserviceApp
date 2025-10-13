using Serilog.Events;
using Shared.Events;
using Shared.Factories;

namespace LoggerService.Factories;

public interface ILoggerActionFactory
{
    LogEventLevel ResolveLogLevel(UserActionMetadata metadata);
    void LogActivity(UserActivityEvent activity);
}