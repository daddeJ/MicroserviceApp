using Serilog;
using Serilog.Events;
using Shared.Events;
using Shared.Factories;

namespace LoggerService.Factories;

public class LoggerActionFactory : ILoggerActionFactory
{
    private readonly IUserActionFactory _userActionFactory;

    public LoggerActionFactory(IUserActionFactory userActionFactory)
    {
        _userActionFactory = userActionFactory;
    }
    
    public LogEventLevel ResolveLogLevel(UserActionMetadata metadata)
    {
        return metadata.DefaultLogLevel.ToLower() switch
        {
            "debug" => LogEventLevel.Debug,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }

    public void LogActivity(UserActivityEvent activity)
    {
        var metadata = _userActionFactory.GetMetadata(activity.Action);
        var logLevel = ResolveLogLevel(metadata);

        switch (logLevel)
        {
            case LogEventLevel.Error:
                Log.Error("[{Category}] UserId: {UserId}, Action: {Action}, Time: {Time}, Desc: {Desc}",
                    metadata.Category, activity.UserId, activity.Action, activity.Timestamp, metadata.Description);
                break;

            case LogEventLevel.Warning:
                Log.Warning("[{Category}] UserId: {UserId}, Action: {Action}, Time: {Time}, Desc: {Desc}",
                    metadata.Category, activity.UserId, activity.Action, activity.Timestamp, metadata.Description);
                break;

            case LogEventLevel.Debug:
                Log.Debug("[{Category}] UserId: {UserId}, Action: {Action}, Time: {Time}, Desc: {Desc}",
                    metadata.Category, activity.UserId, activity.Action, activity.Timestamp, metadata.Description);
                break;

            default:
                Log.Information("[{Category}] UserId: {UserId}, Action: {Action}, Time: {Time}, Desc: {Desc}",
                    metadata.Category, activity.UserId, activity.Action, activity.Timestamp, metadata.Description);
                break;
        }
    }
}