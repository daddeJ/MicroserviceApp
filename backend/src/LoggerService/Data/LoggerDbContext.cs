using LoggerService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoggerService.Data;

public class LoggerDbContext : DbContext
{
    public LoggerDbContext(DbContextOptions<LoggerDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<Applicationlog> Applicationlogs { get; set; }
}