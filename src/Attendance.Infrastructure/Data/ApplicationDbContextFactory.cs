using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Attendance.Infrastructure.Data;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations</c> to create an
/// <see cref="ApplicationDbContext"/> without the full ASP.NET Core host.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Use a placeholder connection string for migration generation only.
        // The real connection string is supplied via appsettings.json at runtime.
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=AttendanceDb;Trusted_Connection=True;",
            sqlOptions => sqlOptions.MigrationsAssembly("Attendance.Infrastructure"));

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
