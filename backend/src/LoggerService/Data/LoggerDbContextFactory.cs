using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LoggerService.Data;

public class LoggerDbContextFactory : IDesignTimeDbContextFactory<LoggerDbContext>
{
    public LoggerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LoggerDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost,1433;Database=LOG_SERVICE_DB;User Id=sa;Password=StrongP@ssword123!;TrustServerCertificate=True;"
        );

        return new LoggerDbContext(optionsBuilder.Options);
    }
}