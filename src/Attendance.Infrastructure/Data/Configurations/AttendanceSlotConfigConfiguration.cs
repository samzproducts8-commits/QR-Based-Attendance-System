using Attendance.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Data.Configurations;

public class AttendanceSlotConfigConfiguration : IEntityTypeConfiguration<AttendanceSlotConfig>
{
    public void Configure(EntityTypeBuilder<AttendanceSlotConfig> builder)
    {
        builder.ToTable("AttendanceSlotConfig");

        builder.HasKey(e => e.SlotId);

        builder.Property(e => e.SlotId)
            .UseIdentityColumn();

        // SlotName stored as string (not an enum int) with max length
        builder.Property(e => e.SlotName)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnType("nvarchar(50)");

        // StartTime and EndTime stored as time(0) (second precision, no fractional seconds)
        builder.Property(e => e.StartTime)
            .IsRequired()
            .HasColumnType("time(0)");

        builder.Property(e => e.EndTime)
            .IsRequired()
            .HasColumnType("time(0)");

        builder.Property(e => e.GracePeriodMinutes)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.IsMandatory)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);
    }
}
