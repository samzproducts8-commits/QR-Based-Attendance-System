using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Attendance.Application.DTOs;
using Attendance.Application.Helpers;
using Attendance.Infrastructure.Data;
using Attendance.Infrastructure.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Attendance.Tests.Integration;

/// <summary>
/// Shared helpers for the API integration tests: authentication, staff
/// creation, and direct database arrangement of slots / tokens.
/// </summary>
internal static class ApiTestHelpers
{
    public const string AdminUser = "admin";
    public const string AdminPassword = "Admin@123!";
    public const string EmployeePassword = "Employee@123!";

    public static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);

    // 8-byte PNG signature followed by filler.
    public static byte[] ValidPngBytes() =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. "png-body"u8];

    // JPEG SOI + APP0 marker followed by filler.
    public static byte[] JpegBytes() =>
        [0xFF, 0xD8, 0xFF, 0xE0, .. "jpeg-body"u8];

    // -------------------------------------------------------------------------
    // Authentication
    // -------------------------------------------------------------------------

    public static async Task<AuthResponse> LoginAsync(
        HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }

    public static void SetBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    /// <summary>Creates an Identity user with the given role via UserManager.</summary>
    public static async Task CreateUserAsync(
        CustomWebApplicationFactory factory, string username, string password, string role)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (await userManager.FindByNameAsync(username) is not null)
            return;

        var user = new ApplicationUser
        {
            UserName = username,
            Email = $"{username}@test.local",
            EmailConfirmed = true
        };
        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                "Failed to create test user: " + string.Join(", ", created.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, role);
    }

    // -------------------------------------------------------------------------
    // Staff registration (multipart)
    // -------------------------------------------------------------------------

    public static MultipartFormDataContent BuildStaffForm(
        string email, byte[] photoBytes, string photoFileName, string photoContentType,
        int departmentId = 1)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("Integration Person"), "FullName" },
            { new StringContent("Female"), "Gender" },
            { new StringContent("1990-01-01"), "DateOfBirth" },
            { new StringContent("0900000000"), "PhoneNumber" },
            { new StringContent(email), "Email" },
            { new StringContent(departmentId.ToString()), "DepartmentId" },
            { new StringContent("Engineer"), "JobTitle" },
            { new StringContent("2024-01-01"), "EmploymentDate" }
        };

        var photo = new ByteArrayContent(photoBytes);
        photo.Headers.ContentType = new MediaTypeHeaderValue(photoContentType);
        form.Add(photo, "photo", photoFileName);

        return form;
    }

    /// <summary>Registers a staff member (admin auth required) and returns the created DTO.</summary>
    public static async Task<StaffDto> CreateStaffAsync(HttpClient adminClient, string email)
    {
        var form = BuildStaffForm(email, ValidPngBytes(), "photo.png", "image/png");
        var response = await adminClient.PostAsync("/api/staff", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StaffDto>(Json))!;
    }

    // -------------------------------------------------------------------------
    // Database arrangement
    // -------------------------------------------------------------------------

    /// <summary>
    /// Deactivates every existing slot and inserts a single active slot whose
    /// window covers the current office-local time (the clock the service
    /// resolves against, <see cref="DateTimeHelper.OfficeNow"/>), so scan tests
    /// are deterministic regardless of when they run.
    /// </summary>
    public static async Task ArrangeSingleSlotCoveringNowAsync(
        CustomWebApplicationFactory factory, string slotName = "MorningIn", int graceMinutes = 15)
    {
        await factory.WithDbContextAsync(async db =>
        {
            foreach (var slot in await db.AttendanceSlotConfigs.ToListAsync())
                slot.IsActive = false;

            int nowMin = (int)TimeOnly.FromDateTime(DateTimeHelper.OfficeNow()).ToTimeSpan().TotalMinutes;
            int startMin = Math.Max(0, nowMin - 5);
            int endMin = Math.Min(1439, nowMin + 30);

            db.AttendanceSlotConfigs.Add(new AttendanceSlotConfig
            {
                SlotName = slotName,
                StartTime = new TimeOnly(startMin / 60, startMin % 60),
                EndTime = new TimeOnly(endMin / 60, endMin % 60),
                GracePeriodMinutes = graceMinutes,
                IsMandatory = true,
                IsActive = true
            });

            await db.SaveChangesAsync();
        });
    }

    /// <summary>Inserts an already-expired active token and returns its GUID.</summary>
    public static async Task<Guid> InsertExpiredTokenAsync(CustomWebApplicationFactory factory)
    {
        var token = Guid.NewGuid();
        await factory.WithDbContextAsync(async db =>
        {
            db.QrSessions.Add(new QrSession
            {
                TokenValue = token,
                GeneratedAt = DateTime.UtcNow.AddSeconds(-30),
                ExpiresAt = DateTime.UtcNow.AddSeconds(-15),
                Status = Attendance.Infrastructure.Enums.QrSessionStatus.Active
            });
            await db.SaveChangesAsync();
        });
        return token;
    }

    // -------------------------------------------------------------------------
    // QR / scan
    // -------------------------------------------------------------------------

    public static async Task<QrCodeResponseDto> GenerateTokenAsync(HttpClient adminClient)
    {
        var response = await adminClient.PostAsync("/api/qr/generate", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<QrCodeResponseDto>(Json))!;
    }
}
