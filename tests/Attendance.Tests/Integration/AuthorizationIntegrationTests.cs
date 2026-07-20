using System.Net;
using System.Net.Http.Json;
using Attendance.Application.DTOs;

namespace Attendance.Tests.Integration;

/// <summary>
/// Role-based authorization and data isolation (task 21.3).
/// Validates Requirements 5.2, 5.3, 5.4, 5.5 and Property 9.
/// </summary>
[Collection("Integration")]
public sealed class AuthorizationIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthorizationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var auth = await ApiTestHelpers.LoginAsync(client, ApiTestHelpers.AdminUser, ApiTestHelpers.AdminPassword);
        ApiTestHelpers.SetBearer(client, auth.AccessToken);
        return client;
    }

    [Fact]
    public async Task AnonymousRequest_ToProtectedEndpoint_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/staff");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Employee_AccessingStaffList_Returns403()
    {
        var admin = await AdminClientAsync();
        var staff = await ApiTestHelpers.CreateStaffAsync(admin, $"rbac.emp.{Guid.NewGuid():N}@test.local");

        var employee = _factory.CreateClient();
        var empAuth = await ApiTestHelpers.LoginAsync(employee, staff.UniqueCode, ApiTestHelpers.EmployeePassword);
        ApiTestHelpers.SetBearer(employee, empAuth.AccessToken);

        var response = await employee.GetAsync("/api/staff");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Hr_AccessingSlotConfig_Returns403()
    {
        var hrUser = $"hr_{Guid.NewGuid():N}";
        await ApiTestHelpers.CreateUserAsync(_factory, hrUser, "Hr@12345!", "HR");

        var hr = _factory.CreateClient();
        var hrAuth = await ApiTestHelpers.LoginAsync(hr, hrUser, "Hr@12345!");
        ApiTestHelpers.SetBearer(hr, hrAuth.AccessToken);

        // Slot configuration is Admin-only (Requirement 5.4).
        var response = await hr.GetAsync("/api/slots");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Employee_MyHistory_ReturnsOnlyOwnRecords()
    {
        await ApiTestHelpers.ArrangeSingleSlotCoveringNowAsync(_factory);
        var admin = await AdminClientAsync();

        // Two employees both record attendance for the same slot today.
        var (empA, _) = await CreateEmployeeAsync(admin, $"iso.a.{Guid.NewGuid():N}@test.local");
        var (empB, _) = await CreateEmployeeAsync(admin, $"iso.b.{Guid.NewGuid():N}@test.local");

        await ScanOnceAsync(admin, empA);
        await ScanOnceAsync(admin, empB);

        // Employee A must see exactly their own single record — never B's.
        var history = await empA.GetFromJsonAsync<List<AttendanceHistoryEntry>>(
            "/api/attendance/my-history", ApiTestHelpers.Json);

        Assert.NotNull(history);
        Assert.Single(history!);
    }

    private async Task<(HttpClient client, StaffDto staff)> CreateEmployeeAsync(HttpClient admin, string email)
    {
        var staff = await ApiTestHelpers.CreateStaffAsync(admin, email);
        var client = _factory.CreateClient();
        var auth = await ApiTestHelpers.LoginAsync(client, staff.UniqueCode, ApiTestHelpers.EmployeePassword);
        ApiTestHelpers.SetBearer(client, auth.AccessToken);
        return (client, staff);
    }

    private async Task ScanOnceAsync(HttpClient admin, HttpClient employee)
    {
        var token = await ApiTestHelpers.GenerateTokenAsync(admin);
        var response = await employee.PostAsJsonAsync("/api/attendance/scan", new { token = token.TokenValue });
        response.EnsureSuccessStatusCode();
    }
}
