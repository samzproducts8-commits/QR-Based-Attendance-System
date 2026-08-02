using Attendance.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Data.Configurations;

public class AttendanceLogConfiguration : IEntityTypeConfiguration<AttendanceLog>
{
    public void Configure(EntityTypeBuilder<AttendanceLog> builder)
    {
        builder.ToTable("AttendanceLog");

        builder.HasKey(e => e.AttendanceLogId);

        builder.Property(e => e.AttendanceLogId)
            .UseIdentityColumn();

        builder.Property(e => e.StaffId)
            .IsRequired();

        builder.Property(e => e.SlotId)
            .IsRequired();

        builder.Property(e => e.QrSessionId);

        builder.Property(e => e.EventTimestamp)
            .IsRequired()
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(e => e.EventDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(e => e.StatusFlag)
            .IsRequired()
            .HasConversion<byte>();

        builder.Property(e => e.AbsenceReason)
            .IsRequired(false)
            .HasMaxLength(500);

        // Composite unique constraint (StaffId, SlotId, EventDate) — one record per slot per day
        builder.HasIndex(e => new { e.StaffId, e.SlotId, e.EventDate })
            .IsUnique()
            .HasDatabaseName("UQ_AttLog_Staff_Slot_Date");

        // Non-clustered index on (EventDate, StaffId) for efficient daily report queries
        builder.HasIndex(e => new { e.EventDate, e.StaffId })
            .HasDatabaseName("IX_AttLog_Date_Staff");

        // FK to Staff
        builder.HasOne(e => e.Staff)
            .WithMany(s => s.AttendanceLogs)
            .HasForeignKey(e => e.StaffId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to AttendanceSlotConfig
        builder.HasOne(e => e.Slot)
            .WithMany(s => s.AttendanceLogs)
            .HasForeignKey(e => e.SlotId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to QrSession (nullable)
        builder.HasOne(e => e.QrSession)
            .WithMany(q => q.AttendanceLogs)
            .HasForeignKey(e => e.QrSessionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
