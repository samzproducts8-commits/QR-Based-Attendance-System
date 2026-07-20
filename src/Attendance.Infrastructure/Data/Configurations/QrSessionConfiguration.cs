using Attendance.Infrastructure.Enums;
using Attendance.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Data.Configurations;

public class QrSessionConfiguration : IEntityTypeConfiguration<QrSession>
{
    public void Configure(EntityTypeBuilder<QrSession> builder)
    {
        builder.ToTable("QrSession");

        builder.HasKey(e => e.QrSessionId);

        builder.Property(e => e.QrSessionId)
            .UseIdentityColumn();

        builder.Property(e => e.TokenValue)
            .IsRequired()
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()");

        builder.Property(e => e.GeneratedAt)
            .IsRequired()
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(e => e.ExpiresAt)
            .IsRequired()
            .HasColumnType("datetime2");

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<byte>();

        builder.Property(e => e.UsedByStaffId);

        builder.Property(e => e.UsedAt)
            .HasColumnType("datetime2");

        // Unique index on TokenValue
        builder.HasIndex(e => e.TokenValue)
            .IsUnique()
            .HasDatabaseName("UX_QrSession_Token");

        // Filtered index on Status for active tokens only (WHERE Status = 0)
        // Square-bracket quoting is required by SQL Server for column references in filter predicates
        builder.HasIndex(e => e.Status)
            .HasFilter("[Status] = 0")
            .HasDatabaseName("FX_QrSession_Active");

        // FK to Staff for UsedByStaffId
        builder.HasOne(e => e.UsedByStaff)
            .WithMany(s => s.UsedQrSessions)
            .HasForeignKey(e => e.UsedByStaffId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
