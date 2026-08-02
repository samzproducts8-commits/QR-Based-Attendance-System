using System.Net.Http.Json;
using Attendance.Application.DTOs;
using Attendance.Application.Helpers;
using Attendance.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Tests.Integration;

/// <summary>
/// Report completeness through the real endpoints (task 21.4).
/// Validates Requirements 4.1, 4.2, 4.5 and Property 8.
/// </summary>
[Collection("Integration")]
public sealed class ReportIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Arranges exactly two active mandatory slots: one covering "now"
    /// (scannable) and one parked far away (never scannable today, so it
    /// must surface as Absent).
    /// </summary>
    private async Task ArrangeTwoSlotsAsync()
    {
        await _factory.WithDbContextAsync(async db =>
        {
            foreach (var slot in await db.AttendanceSlotConfigs.ToListAsync())
                slot.IsActive = false;

            int nowMin = (int)TimeOnly.FromDateTime(DateTimeHelper.OfficeNow()).ToTimeSpan().TotalMinutes;
            int startMin = Math.Max(0, nowMin - 5);
            int endMin = Math.Min(1439, nowMin + 30);

            // "Now" slot — will be scanned.
            db.AttendanceSlotConfigs.Add(new AttendanceSlotConfig
            {
                SlotName = "MorningIn",
                StartTime = new TimeOnly(startMin / 60, startMin % 60),
                EndTime = new TimeOnly(endMin / 60, endMin % 60),
                GracePeriodMinutes = 15,
                IsMandatory = true,
                IsActive = true
            });

            // A parked mandatory slot on the opposite side of the day — never
            // covers "now", so it has no log and must show Absent.
            int parkStart = (nowMin + 600) % 1440;
            int parkEnd = Math.Min(1439, parkStart + 10);
            if (parkEnd <= parkStart) { parkStart = 0; parkEnd = 10; }

            db.AttendanceSlotConfigs.Add(new AttendanceSlotConfig
            {
                SlotName = "EveningOut",
                StartTime = new TimeOnly(parkStart / 60, parkStart % 60),
                EndTime = new TimeOnly(parkEnd / 60, parkEnd % 60),
                GracePeriodMinutes = 0,
                IsMandatory = true,
                IsActive = true
            });

            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task DailyReport_ShowsRecordedStatus_AndAbsentForMissingMandatorySlot()
    {
        await ArrangeTwoSlotsAsync();

        var admin = _factory.CreateClient();
        var adminAuth = await ApiTestHelpers.LoginAsync(admin, ApiTestHelpers.AdminUser, ApiTestHelpers.AdminPassword);
        ApiTestHelpers.SetBearer(admin, adminAuth.AccessToken);

        var staff = await ApiTestHelpers.CreateStaffAsync(admin, $"report.{Guid.NewGuid():N}@test.local");

        var employee = _factory.CreateClient();
        var empAuth = await ApiTestHelpers.LoginAsync(employee, staff.UniqueCode, ApiTestHelpers.EmployeePassword);
        ApiTestHelpers.SetBearer(employee, empAuth.AccessToken);

        var token = await ApiTestHelpers.GenerateTokenAsync(admin);
        (await employee.PostAsJsonAsync("/api/attendance/scan", new { token = token.TokenValue }))
            .EnsureSuccessStatusCode();

        // The service stamps EventDate in office-local time, so query that day.
        var today = DateOnly.FromDateTime(DateTimeHelper.OfficeNow()).ToString("yyyy-MM-dd");
        var sheets = await admin.GetFromJsonAsync<List<DailyAttendanceSheet>>(
            $"/api/reports/daily?date={today}&staffId={staff.StaffId}", ApiTestHelpers.Json);

        Assert.NotNull(sheets);
        var sheet = Assert.Single(sheets!);

        // One entry per active slot (Property 8).
        Assert.Equal(2, sheet.Entries.Count);

        var morning = sheet.Entries.Single(e => e.SlotName == "MorningIn");
        Assert.NotNull(morning.EventTimestamp);
        Assert.Contains(morning.StatusLabel, new[] { "On Time", "Late" });

        var evening = sheet.Entries.Single(e => e.SlotName == "EveningOut");
        Assert.Null(evening.EventTimestamp);
        Assert.Equal("Absent", evening.StatusLabel);
    }

    [Fact]
    public async Task MonthlyReport_CountsScannedSlotOnce_ForStaff()
    {
        await ArrangeTwoSlotsAsync();

        var admin = _factory.CreateClient();
        var adminAuth = await ApiTestHelpers.LoginAsync(admin, ApiTestHelpers.AdminUser, ApiTestHelpers.AdminPassword);
        ApiTestHelpers.SetBearer(admin, adminAuth.AccessToken);

        var staff = await ApiTestHelpers.CreateStaffAsync(admin, $"report.month.{Guid.NewGuid():N}@test.local");

        var employee = _factory.CreateClient();
        var empAuth = await ApiTestHelpers.LoginAsync(employee, staff.UniqueCode, ApiTestHelpers.EmployeePassword);
        ApiTestHelpers.SetBearer(employee, empAuth.AccessToken);

        var token = await ApiTestHelpers.GenerateTokenAsync(admin);
        (await employee.PostAsJsonAsync("/api/attendance/scan", new { token = token.TokenValue }))
            .EnsureSuccessStatusCode();

        var now = DateTimeHelper.OfficeNow();
        var summary = await admin.GetFromJsonAsync<MonthlySummary>(
            $"/api/reports/monthly?year={now.Year}&month={now.Month}&staffId={staff.StaffId}",
            ApiTestHelpers.Json);

        Assert.NotNull(summary);
        var staffSummary = Assert.Single(summary!.StaffSummaries);

        var morning = staffSummary.SlotSummaries.Single(s => s.SlotName == "MorningIn");
        Assert.Equal(1, morning.OnTimeCount + morning.LateCount);
    }

    [Fact]
    public async Task LiveDashboard_ReturnsExpectedMetrics()
    {
        await ArrangeTwoSlotsAsync();

        var admin = _factory.CreateClient();
        var adminAuth = await ApiTestHelpers.LoginAsync(admin, ApiTestHelpers.AdminUser, ApiTestHelpers.AdminPassword);
        ApiTestHelpers.SetBearer(admin, adminAuth.AccessToken);

        var metrics = await admin.GetFromJsonAsync<LiveDashboardMetricsDto>(
            "/api/reports/live-dashboard", ApiTestHelpers.Json);

        Assert.NotNull(metrics);
        Assert.True(metrics!.TotalActiveStaff >= 0);
        Assert.NotNull(metrics.RecentActivities);
    }

    [Fact]
    public async Task PayrollReport_ReturnsMonthlyPayrollSummary()
    {
        await ArrangeTwoSlotsAsync();

        var admin = _factory.CreateClient();
        var adminAuth = await ApiTestHelpers.LoginAsync(admin, ApiTestHelpers.AdminUser, ApiTestHelpers.AdminPassword);
        ApiTestHelpers.SetBearer(admin, adminAuth.AccessToken);

        var now = DateTimeHelper.OfficeNow();
        var payroll = await admin.GetFromJsonAsync<MonthlyPayrollSummaryDto>(
            $"/api/reports/payroll?year={now.Year}&month={now.Month}", ApiTestHelpers.Json);

        Assert.NotNull(payroll);
        Assert.Equal(now.Year, payroll!.Year);
        Assert.Equal(now.Month, payroll.Month);
        Assert.NotNull(payroll.StaffSummaries);
    }

    [Fact]
    public async Task PayrollExport_ReturnsCsvAndXlsxFiles()
    {
        await ArrangeTwoSlotsAsync();

        var admin = _factory.CreateClient();
        var adminAuth = await ApiTestHelpers.LoginAsync(admin, ApiTestHelpers.AdminUser, ApiTestHelpers.AdminPassword);
        ApiTestHelpers.SetBearer(admin, adminAuth.AccessToken);

        var now = DateTimeHelper.OfficeNow();

        // CSV Export
        var csvResponse = await admin.GetAsync($"/api/reports/payroll/export?year={now.Year}&month={now.Month}&format=csv");
        csvResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", csvResponse.Content.Headers.ContentType?.MediaType);
        string csvText = await csvResponse.Content.ReadAsStringAsync();
        Assert.Contains("Employee Code", csvText);
        Assert.Contains("Total Days Worked", csvText);

        // XLSX Export
        var xlsxResponse = await admin.GetAsync($"/api/reports/payroll/export?year={now.Year}&month={now.Month}&format=xlsx");
        xlsxResponse.EnsureSuccessStatusCode();
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsxResponse.Content.Headers.ContentType?.MediaType);
        byte[] xlsxBytes = await xlsxResponse.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(xlsxBytes);
    }
}
