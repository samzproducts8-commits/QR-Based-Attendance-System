using System.Net;
using System.Net.Http.Json;
using Attendance.Application.DTOs;
using Attendance.Infrastructure.Enums;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Tests.Integration;

/// <summary>
/// End-to-end QR scan flow (task 21.1).
/// Validates Requirements 3.3, 3.5, 3.8, 3.10.
/// </summary>
[Collection("Integration")]
public sealed class ScanFlowIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ScanFlowIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient admin, HttpClient employee, StaffDto staff)> SetupAsync(string email)
    {
        var admin = _factory.CreateClient();
        var adminAuth = await ApiTestHelpers.LoginAsync(admin, ApiTestHelpers.AdminUser, ApiTestHelpers.AdminPassword);
        ApiTestHelpers.SetBearer(admin, adminAuth.AccessToken);

        var staff = await ApiTestHelpers.CreateStaffAsync(admin, email);

        var employee = _factory.CreateClient();
        var empAuth = await ApiTestHelpers.LoginAsync(employee, staff.UniqueCode, ApiTestHelpers.EmployeePassword);
        ApiTestHelpers.SetBearer(employee, empAuth.AccessToken);

        return (admin, employee, staff);
    }

    [Fact]
    public async Task ValidScan_RecordsAttendance_AndRefreshesToken()
    {
        await ApiTestHelpers.ArrangeSingleSlotCoveringNowAsync(_factory);
        var (admin, employee, _) = await SetupAsync($"scan.valid.{Guid.NewGuid():N}@test.local");

        var token = await ApiTestHelpers.GenerateTokenAsync(admin);

        var response = await employee.PostAsJsonAsync("/api/attendance/scan", new { token = token.TokenValue });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = await response.Content.ReadFromJsonAsync<AttendanceRecordDto>(ApiTestHelpers.Json);
        Assert.NotNull(record);
        Assert.False(string.IsNullOrWhiteSpace(record!.GreetingMessage));

        // The scanned token is now Used, and a fresh Active token was generated
        // and pushed (Requirement 3.5 refresh).
        await _factory.WithDbContextAsync(async db =>
        {
            var consumed = await db.QrSessions.SingleAsync(s => s.TokenValue == token.TokenValue);
            Assert.Equal(QrSessionStatus.Used, consumed.Status);

            bool hasFreshActive = await db.QrSessions
                .AnyAsync(s => s.Status == QrSessionStatus.Active && s.TokenValue != token.TokenValue);
            Assert.True(hasFreshActive, "A new active token should have been generated after a successful scan.");
        });
    }

    [Fact]
    public async Task ScanSameTokenTwice_SecondReturns409()
    {
        await ApiTestHelpers.ArrangeSingleSlotCoveringNowAsync(_factory);
        var (admin, employee, _) = await SetupAsync($"scan.twice.{Guid.NewGuid():N}@test.local");

        var token = await ApiTestHelpers.GenerateTokenAsync(admin);

        var first = await employee.PostAsJsonAsync("/api/attendance/scan", new { token = token.TokenValue });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await employee.PostAsJsonAsync("/api/attendance/scan", new { token = token.TokenValue });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task ScanExpiredToken_Returns410()
    {
        await ApiTestHelpers.ArrangeSingleSlotCoveringNowAsync(_factory);
        var (_, employee, _) = await SetupAsync($"scan.expired.{Guid.NewGuid():N}@test.local");

        var expiredToken = await ApiTestHelpers.InsertExpiredTokenAsync(_factory);

        var response = await employee.PostAsJsonAsync("/api/attendance/scan", new { token = expiredToken });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task ScanUnknownToken_Returns404()
    {
        await ApiTestHelpers.ArrangeSingleSlotCoveringNowAsync(_factory);
        var (_, employee, _) = await SetupAsync($"scan.unknown.{Guid.NewGuid():N}@test.local");

        var response = await employee.PostAsJsonAsync(
            "/api/attendance/scan", new { token = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateSlotSameDay_Returns409()
    {
        await ApiTestHelpers.ArrangeSingleSlotCoveringNowAsync(_factory);
        var (admin, employee, _) = await SetupAsync($"scan.dup.{Guid.NewGuid():N}@test.local");

        var token1 = await ApiTestHelpers.GenerateTokenAsync(admin);
        var first = await employee.PostAsJsonAsync("/api/attendance/scan", new { token = token1.TokenValue });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // A fresh token for the same employee + same slot + same day.
        var token2 = await ApiTestHelpers.GenerateTokenAsync(admin);
        var second = await employee.PostAsJsonAsync("/api/attendance/scan", new { token = token2.TokenValue });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
