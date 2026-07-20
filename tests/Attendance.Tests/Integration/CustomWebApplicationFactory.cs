using Attendance.Api.BackgroundServices;
using Attendance.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Attendance.Tests.Integration;

/// <summary>
/// Boots the real API against a throwaway LocalDB database.
/// </summary>
/// <remarks>
/// A real SQL Server (LocalDB) instance is required rather than EF InMemory
/// because <c>QrSessionRepository.ConsumeTokenAsync</c> executes a raw
/// SQL Server <c>UPDATE … SET Status = 1 … WHERE Status = 0 AND
/// ExpiresAt &gt; SYSUTCDATETIME()</c> — the atomic single-use guarantee
/// (Requirement 3.8) cannot be exercised by the InMemory provider.
///
/// Each factory instance gets a uniquely named database so parallel test
/// collections never collide; the database is migrated and seeded by the
/// application's own startup path (<c>SeedData.SeedAsync</c>) and dropped
/// on dispose.
/// </remarks>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"AttendanceDb_Test_{Guid.NewGuid():N}";

    public string ConnectionString =>
        $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;MultipleActiveResultSets=true";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Auth:DefaultEmployeePassword"] = "Employee@123!"
                // NOTE: Jwt:SecretKey is intentionally NOT overridden. The bearer
                // validation key is captured eagerly at Program.cs top-level
                // (before this override applies), while JwtTokenService reads it
                // lazily — overriding here would desync signing and validation
                // keys and 401 every authenticated request. Both use the
                // appsettings value instead.
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // The 5-second token-expiry sweep would race with the tests by
            // expiring/regenerating tokens mid-assertion — remove it.
            var hostedDescriptor = services.SingleOrDefault(
                d => d.ImplementationType == typeof(QrTokenExpiryBackgroundService));
            if (hostedDescriptor is not null)
                services.Remove(hostedDescriptor);
        });
    }

    /// <summary>Runs an action against a fresh DbContext scope (test arrangement).</summary>
    public async Task WithDbContextAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(db);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        // Drop the throwaway database through a standalone context so cleanup
        // never touches the (already-disposing) test host service provider.
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            using var db = new ApplicationDbContext(options);
            db.Database.EnsureDeleted();
        }
        catch
        {
            // Best-effort cleanup — an orphaned test DB is harmless.
        }
    }
}

/// <summary>
/// Shared collection so all integration tests reuse one migrated/seeded
/// database and run serially (avoids LocalDB contention and cross-test
/// interference).
/// </summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationCollection : ICollectionFixture<CustomWebApplicationFactory>;
