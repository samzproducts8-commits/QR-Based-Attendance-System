using Attendance.Infrastructure.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Attendance.Infrastructure.Data;

/// <summary>
/// Seeds required reference data on application startup:
/// <list type="bullet">
///   <item>Four default <see cref="AttendanceSlotConfig"/> records (MorningIn, LunchOut, LunchIn, EveningOut)</item>
///   <item>Default <c>Admin</c>, <c>HR</c>, and <c>Employee</c> roles</item>
///   <item>Default admin user (<c>admin</c> / <c>admin@attendance.local</c>)</item>
/// </list>
/// Requirements: 2.2, 5.4
/// </summary>
public static class SeedData
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        // Ensure the database schema is up to date before seeding.
        await db.Database.MigrateAsync();

        await SeedAttendanceSlotsAsync(db, logger);
        await SeedDepartmentsAsync(db, logger);
        await SeedRolesAsync(roleManager, logger);
        await SeedAdminUserAsync(userManager, logger);
    }

    // -------------------------------------------------------------------------
    // Default departments (staff registration requires an existing department)
    // -------------------------------------------------------------------------

    private static readonly string[] DefaultDepartments =
        ["Administration", "Engineering", "Finance", "Human Resources", "Operations"];

    private static async Task SeedDepartmentsAsync(ApplicationDbContext db, ILogger logger)
    {
        //It checks existing departments
        var existingNames = await db.Departments
            .Select(d => d.DepartmentName)
            .ToListAsync();
            
        //Only add departments that do NOT already exist.
        var toAdd = DefaultDepartments
            .Where(name => !existingNames.Contains(name))// add department that not already exist
            .Select(name => new Department { DepartmentName = name, IsActive = true })
            .ToList();

        if (toAdd.Count > 0)
        {
            db.Departments.AddRange(toAdd);// store on the sql server
            await db.SaveChangesAsync();
            logger.LogInformation(
                "Seeded {Count} department(s): {Names}",
                toAdd.Count,
                string.Join(", ", toAdd.Select(d => d.DepartmentName)));
        }
    }

    // -------------------------------------------------------------------------
    // Attendance slot configuration seed (Requirement 2.2)
    // -------------------------------------------------------------------------

    private static async Task SeedAttendanceSlotsAsync(
        ApplicationDbContext db,
        ILogger logger)
    {
        // If any slot already exists, skip — avoids duplicating seed data on
        // subsequent startups. Each slot's name is used as the idempotency key.
        var existingSlotNames = await db.AttendanceSlotConfigs
            .Select(s => s.SlotName)
            .ToListAsync();

        var defaultSlots = new List<AttendanceSlotConfig>
        {
            new()
            {
                SlotName           = "MorningIn",
                StartTime          = new TimeOnly(8, 0, 0),
                EndTime            = new TimeOnly(9, 0, 0),
                GracePeriodMinutes = 0,
                IsMandatory        = true,
                IsActive           = true
            },
            new()
            {
                SlotName           = "LunchOut",
                StartTime          = new TimeOnly(12, 0, 0),
                EndTime            = new TimeOnly(13, 0, 0),
                GracePeriodMinutes = 0,
                IsMandatory        = true,
                IsActive           = true
            },
            new()
            {
                SlotName           = "LunchIn",
                StartTime          = new TimeOnly(13, 0, 0),
                EndTime            = new TimeOnly(14, 0, 0),
                GracePeriodMinutes = 0,
                IsMandatory        = true,
                IsActive           = true
            },
            new()
            {
                SlotName           = "EveningOut",
                StartTime          = new TimeOnly(17, 0, 0),
                EndTime            = new TimeOnly(18, 0, 0),
                GracePeriodMinutes = 0,
                IsMandatory        = true,
                IsActive           = true
            }
        };

        var slotsToAdd = defaultSlots
            .Where(s => !existingSlotNames.Contains(s.SlotName))
            .ToList();

        if (slotsToAdd.Count > 0)
        {
            db.AttendanceSlotConfigs.AddRange(slotsToAdd);
            await db.SaveChangesAsync();
            logger.LogInformation(
                "Seeded {Count} attendance slot(s): {Names}",
                slotsToAdd.Count,
                string.Join(", ", slotsToAdd.Select(s => s.SlotName)));
        }
        else
        {
            logger.LogDebug("Attendance slots already seeded — skipping.");
        }
    }

    // -------------------------------------------------------------------------
    // Role seed (Requirement 5.4)
    // -------------------------------------------------------------------------

    private static readonly string[] DefaultRoles = ["Admin", "HR", "Employee"];

    private static async Task SeedRolesAsync(
        RoleManager<IdentityRole> roleManager,
        ILogger logger)
    {
        foreach (var roleName in DefaultRoles)
        { 
            if (!await roleManager.RoleExistsAsync(roleName))  //by checking "RoleExistsAsync(roleName)"
           //On the second run, it tries to create Admin again it says Role 'Admin' already exists "skip"..
           // 1, check the dataabase
           // 2,if the role didint find on the database. false i.e if (!false) = true start excute next to seed the unseeded role   
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (result.Succeeded)  // if role name is exist skip
                    logger.LogInformation("Created role '{Role}'.", roleName);
                else //create role name
                    logger.LogError(
                        "Failed to create role '{Role}': {Errors}",
                        roleName,
                        string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    // -------------------------------------------------------------------------
    // Default admin user seed (Requirement 5.4)
    // -------------------------------------------------------------------------

    private const string AdminUserName   = "admin";
    private const string AdminEmail      = "admin@attendance.local";
    // The default password must be changed immediately after first login in production.
    private const string AdminDefaultPwd = "Admin@123!";

    private static async Task SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        var existing = await userManager.FindByNameAsync(AdminUserName);// finding admin by ADMIN name if he registered
        if (existing is not null)
        {
            logger.LogDebug("Admin user already exists — skipping.");
            return;
        }

        var adminUser = new ApplicationUser
        {
            UserName       = AdminUserName,
            Email          = AdminEmail,
            EmailConfirmed = true   // no email verification flow required for seed user
        };
// creating username and passw if ADMIN is not found.
        var createResult = await userManager.CreateAsync(adminUser, AdminDefaultPwd);
        if (!createResult.Succeeded)
        {
            logger.LogError(
                "Failed to create admin user: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }
// finally add the new admin to the ROlE
        var roleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
        if (roleResult.Succeeded)
            logger.LogInformation(
                "Seeded admin user '{User}' with role 'Admin'.", AdminUserName);
        else
            logger.LogError(
                "Failed to assign Admin role to '{User}': {Errors}",
                AdminUserName,
                string.Join(", ", roleResult.Errors.Select(e => e.Description)));
    }
}
