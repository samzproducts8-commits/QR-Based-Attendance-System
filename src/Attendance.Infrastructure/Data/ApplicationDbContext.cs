using Attendance.Infrastructure.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<AttendanceSlotConfig> AttendanceSlotConfigs => Set<AttendanceSlotConfig>();
    public DbSet<QrSession> QrSessions => Set<QrSession>();
    public DbSet<AttendanceLog> AttendanceLogs => Set<AttendanceLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all IEntityTypeConfiguration<T> classes found in this assembly.
        // The Configurations/ folder (task 3.2) will be populated later;
        // this call is a no-op until those classes exist.
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
