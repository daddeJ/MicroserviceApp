using AuthService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Data.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("Sessions");
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.SessionId)
            .IsUnique();
        builder.Property(s => s.UserId)
            .IsRequired();
        builder.Property(s => s.AccessToken)
            .IsRequired()
            .HasMaxLength(512);
        builder.Property(s => s.RefreshToken)
            .HasMaxLength(512);
        builder.Property(s => s.DeviceInfo)
            .HasMaxLength(256);
        builder.Property(s => s.IP)
            .HasMaxLength(64);
        builder.Property(s => s.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Active");
        builder.Property(s => s.IssueAt)
            .HasDefaultValueSql("GETUTCDATE()");
        builder.Property(s => s.ExpiresAt)
            .IsRequired();

        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.Status);
        builder.HasIndex(s => s.ExpiresAt);

    }
}