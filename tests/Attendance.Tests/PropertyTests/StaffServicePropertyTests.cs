using System.Text;
using Attendance.Application.DTOs;
using Attendance.Application.Exceptions;
using Attendance.Application.Interfaces;
using Attendance.Application.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Attendance.Tests.PropertyTests;

// ---------------------------------------------------------------------------
// Test-file factory
// ---------------------------------------------------------------------------

internal static class TestFiles
{
    private static readonly byte[] PngMagic =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF, 0xE0];

    public static IFormFile ValidPng()
        => Make(PngMagic.Concat(Encoding.ASCII.GetBytes("fakepngbody")).ToArray(),
                "photo.png", "image/png");

    /// <summary>A JPEG renamed to .png with a spoofed MIME — must be rejected by magic bytes.</summary>
    public static IFormFile RenamedJpeg()
        => Make(JpegMagic.Concat(Encoding.ASCII.GetBytes("fakejpegbody")).ToArray(),
                "photo.png", "image/png");

    private static IFormFile Make(byte[] content, string fileName, string contentType)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "photo", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}

internal static class StaffServiceBuilder
{
    public static CreateStaffRequest Request(string email = "person@example.com") => new(
        FullName: "Test Person",
        Gender: "Female",
        DateOfBirth: new DateOnly(1990, 1, 1),
        PhoneNumber: "0900000000",
        Email: email,
        DepartmentId: 1,
        JobTitle: "Engineer",
        EmploymentDate: new DateOnly(2024, 1, 1),
        Address: null,
        EmergencyContact: null);

    public static (StaffService Service,
                   Mock<IStaffRepository> RepoMock,
                   Mock<IFileStorageHelper> StorageMock)
        Build(Action<Mock<IStaffRepository>>? configure = null)
    {
        var repoMock    = new Mock<IStaffRepository>(MockBehavior.Strict);
        var storageMock = new Mock<IFileStorageHelper>(MockBehavior.Loose);

        storageMock
            .Setup(s => s.SavePhotoAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync((IFormFile _, string code) => $"staff-photos/{code}/{code}.png");

        configure?.Invoke(repoMock);

        return (new StaffService(repoMock.Object, storageMock.Object), repoMock, storageMock);
    }
}

// ---------------------------------------------------------------------------
// Property 5: UniqueCode Monotonic Uniqueness
// Validates: Requirements 1.1, 1.7, 1.8
// ---------------------------------------------------------------------------

/// <summary>
/// <b>Property 5: UniqueCode Monotonic Uniqueness</b>
/// <para>
/// Simulating N sequential registrations against a mocked repository, every
/// generated code matches <c>EMP-\d{4}</c>, all codes are distinct, and the
/// numeric suffix strictly increases.
/// </para>
/// <b>Validates: Requirements 1.1, 1.8</b>
/// </summary>
public class StaffServiceProperty5_UniqueCodeMonotonic
{
    [Property(MaxTest = 50)]
    public Property Property5_SequentialRegistrations_ProduceMonotonicUniqueCodes(
        int countSeed, int startSeed)
    {
        int registrations = Math.Abs(countSeed) % 10 + 2;    // 2..11 sequential creates
        int startNumber   = Math.Abs(startSeed) % 500;       // existing max code number

        var issuedCodes = new List<string>();
        int currentMax = startNumber;

        var (service, _, _) = StaffServiceBuilder.Build(repo =>
        {
            repo.Setup(r => r.DepartmentExistsAsync(1)).ReturnsAsync(true);
            repo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), null)).ReturnsAsync(false);
            // Each call reflects the codes issued so far (as the DB would).
            repo.Setup(r => r.GetMaxUniqueCodeNumberAsync())
                .ReturnsAsync(() => currentMax);
            repo.Setup(r => r.CreateStaffWithProfileAsync(
                    It.IsAny<CreateStaffRequest>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>()))
                .Callback<CreateStaffRequest, string, string, string>((_, code, _, _) =>
                {
                    issuedCodes.Add(code);
                    currentMax++;
                })
                .ReturnsAsync((CreateStaffRequest req, string code, string _, string path) =>
                    new StaffDto(issuedCodes.Count, code, req.FullName, "Engineering", req.JobTitle, 1, path));
        });

        for (int i = 0; i < registrations; i++)
        {
            service.CreateStaffAsync(
                StaffServiceBuilder.Request($"p{i}@example.com"),
                TestFiles.ValidPng()).GetAwaiter().GetResult();
        }

        bool allMatchPattern = issuedCodes.All(c =>
            System.Text.RegularExpressions.Regex.IsMatch(c, @"^EMP-\d{4}$"));
        bool allDistinct = issuedCodes.Distinct().Count() == issuedCodes.Count;

        var numbers = issuedCodes.Select(c => int.Parse(c[4..])).ToList();
        bool strictlyIncreasing = numbers.Zip(numbers.Skip(1), (a, b) => b == a + 1).All(x => x);

        return Prop.Label(
            allMatchPattern && allDistinct && strictlyIncreasing,
            $"start={startNumber} codes=[{string.Join(", ", issuedCodes)}] | " +
            $"pattern={allMatchPattern} distinct={allDistinct} increasing={strictlyIncreasing}");
    }

    /// <summary>
    /// A duplicate email is rejected with <see cref="DuplicateEmailException"/>
    /// before any code is generated or file saved.
    /// <b>Validates: Requirement 1.7</b>
    /// </summary>
    [Property(MaxTest = 30)]
    public Property Property5_DuplicateEmail_Rejected(int seed)
    {
        string email = $"dup{Math.Abs(seed)}@example.com";
        var createCalls = 0;

        var (service, _, _) = StaffServiceBuilder.Build(repo =>
        {
            repo.Setup(r => r.DepartmentExistsAsync(1)).ReturnsAsync(true);
            repo.Setup(r => r.EmailExistsAsync(email, null)).ReturnsAsync(true);
            repo.Setup(r => r.CreateStaffWithProfileAsync(
                    It.IsAny<CreateStaffRequest>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>()))
                .Callback(() => createCalls++)
                .ReturnsAsync((StaffDto)null!);
        });

        Exception? thrown = Record.ExceptionAsync(() =>
            service.CreateStaffAsync(StaffServiceBuilder.Request(email), TestFiles.ValidPng()))
            .GetAwaiter().GetResult();

        return Prop.Label(
            thrown is DuplicateEmailException && createCalls == 0,
            $"Expected DuplicateEmailException with no create; got {thrown?.GetType().Name ?? "none"}, creates={createCalls}");
    }
}

// ---------------------------------------------------------------------------
// Property 4 (delegation): photo validation gates the repository
// Validates: Requirements 1.3, 1.4
// ---------------------------------------------------------------------------

/// <summary>
/// <b>Property 4 (delegation test):</b> StaffService delegates to
/// PhotoValidationHelper and rejects invalid files before touching the
/// repository or file storage.
/// <b>Validates: Requirements 1.3, 1.4</b>
/// </summary>
public class StaffServiceProperty4_PhotoValidationDelegation
{
    [Property(MaxTest = 30)]
    public Property Property4_RenamedJpeg_RejectedBeforeAnyPersistence(int seed)
    {
        var repoTouched = 0;

        var (service, repoMock, storageMock) = StaffServiceBuilder.Build(repo =>
        {
            // Strict mock: any call would throw — but we also count defensively.
            repo.Setup(r => r.DepartmentExistsAsync(It.IsAny<int>()))
                .Callback(() => repoTouched++)
                .ReturnsAsync(true);
        });

        Exception? thrown = Record.ExceptionAsync(() =>
            service.CreateStaffAsync(
                StaffServiceBuilder.Request($"x{Math.Abs(seed)}@example.com"),
                TestFiles.RenamedJpeg()))
            .GetAwaiter().GetResult();

        storageMock.Verify(
            s => s.SavePhotoAsync(It.IsAny<IFormFile>(), It.IsAny<string>()),
            Times.Never);

        return Prop.Label(
            thrown is PhotoValidationException && repoTouched == 0,
            $"Expected PhotoValidationException before persistence; " +
            $"got {thrown?.GetType().Name ?? "none"}, repoCalls={repoTouched}");
    }
}
