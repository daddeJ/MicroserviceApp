using LoggerService.Data.Entities;
using Microsoft.EntityFrameworkCore;

public class LoggerDbContext : DbContext
{
    public LoggerDbContext(DbContextOptions<LoggerDbContext> options) 
        : base(options) { }
    
    public DbSet<ApplicationLog> ApplicationLogs { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("ApplicationLogs");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.ApplicationLogId)
                .HasDefaultValueSql("NEWID()") 
                .IsRequired();
        });

    }
}