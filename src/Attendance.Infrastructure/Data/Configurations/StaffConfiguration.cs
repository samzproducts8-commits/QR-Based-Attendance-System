using Attendance.Infrastructure.Enums;
using Attendance.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Data.Configurations;

public class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.ToTable("Staff");

        builder.HasKey(e => e.StaffId);

        builder.Property(e => e.StaffId)
            .UseIdentityColumn();

        builder.Property(e => e.UniqueCode)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnType("nvarchar(20)");

        builder.Property(e => e.FullName)
            .IsRequired()
            .HasMaxLength(150)
            .HasColumnType("nvarchar(150)");

        builder.Property(e => e.Gender)
            .HasMaxLength(10)
            .HasColumnType("nvarchar(10)");

        builder.Property(e => e.DateOfBirth)
            .HasColumnType("date");

        builder.Property(e => e.PhoneNumber)
            .HasMaxLength(20)
            .HasColumnType("nvarchar(20)");

        builder.Property(e => e.Email)
            .HasMaxLength(150)
            .HasColumnType("nvarchar(150)");

        builder.Property(e => e.DepartmentId)
            .IsRequired();

        builder.Property(e => e.JobTitle)
            .HasMaxLength(100)
            .HasColumnType("nvarchar(100)");

        builder.Property(e => e.EmploymentDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<byte>()
            .HasDefaultValue(StaffStatus.Active);

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        // Unique indexes
        builder.HasIndex(e => e.UniqueCode)
            .IsUnique()
            .HasDatabaseName("UX_Staff_Code");

        builder.HasIndex(e => e.Email)
            .IsUnique()
            .HasDatabaseName("UX_Staff_Email");

        // FK to Department
        builder.HasOne(e => e.Department)
            .WithMany(d => d.Staff)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
