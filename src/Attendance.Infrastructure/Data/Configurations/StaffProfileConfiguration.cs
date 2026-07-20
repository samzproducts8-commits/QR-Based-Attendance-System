using Attendance.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Data.Configurations;

public class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
{
    public void Configure(EntityTypeBuilder<StaffProfile> builder)
    {
        builder.ToTable("StaffProfile", t =>
            t.HasCheckConstraint("CK_StaffProfile_ContentType", "PhotoContentType = 'image/png'"));

        builder.HasKey(e => e.StaffProfileId);

        builder.Property(e => e.StaffProfileId)
            .UseIdentityColumn();

        builder.Property(e => e.StaffId)
            .IsRequired();

        builder.Property(e => e.PhotoFileName)
            .IsRequired()
            .HasMaxLength(255)
            .HasColumnType("nvarchar(255)");

        builder.Property(e => e.PhotoContentType)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnType("nvarchar(50)");

        builder.Property(e => e.PhotoPath)
            .IsRequired()
            .HasMaxLength(400)
            .HasColumnType("nvarchar(400)");

        builder.Property(e => e.Address)
            .HasMaxLength(250)
            .HasColumnType("nvarchar(250)");

        builder.Property(e => e.EmergencyContact)
            .HasMaxLength(100)
            .HasColumnType("nvarchar(100)");

        // CHECK constraint is defined in ToTable() above

        // Unique index on StaffId (enforces one-to-one from the FK side)
        builder.HasIndex(e => e.StaffId)
            .IsUnique()
            .HasDatabaseName("UX_StaffProfile_StaffId");

        // One-to-one with Staff
        builder.HasOne(e => e.Staff)
            .WithOne(s => s.Profile)
            .HasForeignKey<StaffProfile>(e => e.StaffId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
