using AuthService.Data.Entities;

namespace AuthService.Helpers;

public static class StatisticHelper
{
    public static object GetSessionStatistic(List<Session> sessions)
    {
        var now = DateTime.UtcNow;
        var activeCount = sessions.Count(s => s.Status == "Active");
        var expiredCount = sessions.Count(s => s.ExpiresAt < now);
        var validCount = sessions.Count(s => s.ExpiresAt > now);
        var avgLifetime = sessions.Any()
            ? sessions.Average(s => (s.ExpiresAt - s.IssueAt).TotalHours)
            : 0;
        var groupedByDay = sessions
            .GroupBy(s => s.IssueAt.Date.Day)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Date)
            .ToList();

        var deviceDistribution = sessions
            .GroupBy(s => s.DeviceInfo ?? "Unknown")
            .Select(g => new { Device = g.Key, Count = g.Count() })
            .ToList();

        var ipDistribution = sessions
            .GroupBy(s => s.IP ?? "Unknown")
            .Select(g => new { IP = g.Key, Count = g.Count() })
            .ToList();
        
        return new
        {
            ActiveSessions = activeCount,
            ExpiredSessions = expiredCount,
            ValidSessions = validCount,
            AvgLifetimeHours = avgLifetime,
            LoginsPerDay = groupedByDay,
            DeviceDistribution = deviceDistribution,
            IPDistribution = ipDistribution
        };
    }
}