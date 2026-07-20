using System.Net;
using System.Net.Http.Json;
using Attendance.Application.DTOs;

namespace Attendance.Tests.Integration;

/// <summary>
/// Staff registration photo validation through the real endpoint (task 21.2).
/// Validates Requirements 1.3, 1.4.
/// </summary>
[Collection("Integration")]
public sealed class StaffPhotoIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public StaffPhotoIntegrationTests(CustomWebApplicationFactory factory)
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
    public async Task UploadValidPng_Returns201_WithGeneratedCode()
    {
        var admin = await AdminClientAsync();
        var form = ApiTestHelpers.BuildStaffForm(
            $"png.valid.{Guid.NewGuid():N}@test.local",
            ApiTestHelpers.ValidPngBytes(), "photo.png", "image/png");

        var response = await admin.PostAsync("/api/staff", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var staff = await response.Content.ReadFromJsonAsync<StaffDto>(ApiTestHelpers.Json);
        Assert.NotNull(staff);
        Assert.Matches(@"^EMP-\d{4}$", staff!.UniqueCode);
    }

    [Fact]
    public async Task UploadJpegRenamedToPng_Returns400_ByMagicBytes()
    {
        var admin = await AdminClientAsync();
        // JPEG bytes, but .png extension and image/png MIME — only the
        // magic-byte stage can catch this.
        var form = ApiTestHelpers.BuildStaffForm(
            $"jpeg.magic.{Guid.NewGuid():N}@test.local",
            ApiTestHelpers.JpegBytes(), "photo.png", "image/png");

        var response = await admin.PostAsync("/api/staff", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadJpegMimeType_Returns400_ByMimeCheck()
    {
        var admin = await AdminClientAsync();
        var form = ApiTestHelpers.BuildStaffForm(
            $"jpeg.mime.{Guid.NewGuid():N}@test.local",
            ApiTestHelpers.JpegBytes(), "photo.jpg", "image/jpeg");

        var response = await admin.PostAsync("/api/staff", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
