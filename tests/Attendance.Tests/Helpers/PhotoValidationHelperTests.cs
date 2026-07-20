using Attendance.Application.Helpers;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;

namespace Attendance.Tests.Helpers;

// ---------------------------------------------------------------------------
// Fake IFormFile
// ---------------------------------------------------------------------------

/// <summary>
/// Minimal in-memory IFormFile implementation for testing.
/// </summary>
internal sealed class FakeFormFile : IFormFile
{
    private readonly byte[] _content;

    public FakeFormFile(string fileName, string contentType, byte[] content)
    {
        FileName = fileName;
        ContentType = contentType;
        _content = content;
        Length = content.Length;
    }

    public string ContentType { get; }
    public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
    public IHeaderDictionary Headers => new HeaderDictionary();
    public long Length { get; }
    public string Name => "file";
    public string FileName { get; }

    public void CopyTo(Stream target) => target.Write(_content, 0, _content.Length);
    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
    {
        target.Write(_content, 0, _content.Length);
        return Task.CompletedTask;
    }
    public Stream OpenReadStream() => new MemoryStream(_content);
}

// ---------------------------------------------------------------------------
// Domain constants
// ---------------------------------------------------------------------------

internal static class MagicBytes
{
    public static readonly byte[] Png  = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];
    public static readonly byte[] Gif  = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x00, 0x01];
}

// ---------------------------------------------------------------------------
// Discriminated-union helpers for the 2×2×4 combination space
// ---------------------------------------------------------------------------

public enum ExtensionKind { Valid, Invalid }
public enum MimeKind      { Valid, Invalid }
public enum BytesKind     { Png, Jpeg, Gif, Random }

/// <summary>
/// Captures one of the 2×2×4 = 16 input combinations and the expected outcome.
/// </summary>
public record PhotoScenario(ExtensionKind Extension, MimeKind Mime, BytesKind Bytes)
{
    /// <summary>
    /// A file is valid iff all three checks pass simultaneously.
    /// </summary>
    public bool ExpectedValid =>
        Extension == ExtensionKind.Valid &&
        Mime      == MimeKind.Valid      &&
        Bytes     == BytesKind.Png;

    public IFormFile BuildFile(byte[]? randomBytes = null)
    {
        var fileName    = Extension == ExtensionKind.Valid ? "photo.png" : "photo.jpg";
        var contentType = Mime      == MimeKind.Valid      ? "image/png" : "image/jpeg";

        byte[] content = Bytes switch
        {
            BytesKind.Png    => [..MagicBytes.Png,  0x00, 0x00],
            BytesKind.Jpeg   => [..MagicBytes.Jpeg, 0x00, 0x00],
            BytesKind.Gif    => [..MagicBytes.Gif,  0x00, 0x00],
            BytesKind.Random => randomBytes ?? [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07],
            _                => throw new ArgumentOutOfRangeException(nameof(Bytes))
        };

        return new FakeFormFile(fileName, contentType, content);
    }
}

// ---------------------------------------------------------------------------
// FsCheck v3 Arbitraries (using FsCheck.Fluent static API)
// ---------------------------------------------------------------------------

/// <summary>
/// Generators for the 2×2×4 combination space — compatible with FsCheck v3.
/// </summary>
public static class PhotoArbitraries
{
    // --- leaf generators ---

    public static Arbitrary<ExtensionKind> ExtensionKindArb() =>
        Arb.From(Gen.Elements<ExtensionKind>(ExtensionKind.Valid, ExtensionKind.Invalid));

    public static Arbitrary<MimeKind> MimeKindArb() =>
        Arb.From(Gen.Elements<MimeKind>(MimeKind.Valid, MimeKind.Invalid));

    public static Arbitrary<BytesKind> BytesKindArb() =>
        Arb.From(Gen.Elements<BytesKind>(BytesKind.Png, BytesKind.Jpeg, BytesKind.Gif, BytesKind.Random));

    /// <summary>
    /// A random 8-byte array guaranteed NOT to match any known magic signature.
    /// </summary>
    public static Arbitrary<byte[]> NonMagicBytesArb()
    {
        var gen = Gen.ArrayOf(Gen.Choose(0, 255).Select(i => (byte)i), 8)
                     .Where(b => !b.SequenceEqual(MagicBytes.Png)
                              && !b.SequenceEqual(MagicBytes.Jpeg)
                              && !b.SequenceEqual(MagicBytes.Gif));
        return Arb.From(gen);
    }

    /// <summary>
    /// Generates all 16 (extension × mime × bytes) combinations together with a
    /// non-magic random byte array for the Random bytes case.
    /// </summary>
    public static Arbitrary<(PhotoScenario Scenario, byte[] RandomBytes)> ScenarioArb()
    {
        var nonPngBytesGen =
            Gen.ArrayOf(Gen.Choose(0, 255).Select(i => (byte)i), 8)
               .Where(b => !b.SequenceEqual(MagicBytes.Png));

        var gen =
            Gen.Elements<ExtensionKind>(ExtensionKind.Valid, ExtensionKind.Invalid)
               .SelectMany(ext =>
                   Gen.Elements<MimeKind>(MimeKind.Valid, MimeKind.Invalid)
                      .SelectMany(mime =>
                          Gen.Elements<BytesKind>(BytesKind.Png, BytesKind.Jpeg, BytesKind.Gif, BytesKind.Random)
                             .SelectMany(bytesKind =>
                                 nonPngBytesGen.Select(rnd =>
                                     (new PhotoScenario(ext, mime, bytesKind), rnd)))));

        return Arb.From(gen);
    }
}

// ---------------------------------------------------------------------------
// Property 4: PNG Magic-Byte Validation Soundness
// Validates: Requirements 1.3, 1.4
// ---------------------------------------------------------------------------

/// <summary>
/// Property-based tests for <see cref="PhotoValidationHelper"/>.
/// <para>
/// <b>Property 4: PNG Magic-Byte Validation Soundness</b><br/>
/// For every combination of (valid/invalid extension) × (valid/invalid MIME) ×
/// (PNG / JPEG / GIF / random bytes) the validation result must match the
/// expected outcome: Valid iff all three checks pass, Invalid otherwise.
/// </para>
/// <b>Validates: Requirements 1.3, 1.4</b>
/// </summary>
public class PhotoValidationHelperPropertyTests
{
    // -----------------------------------------------------------------------
    // Property 4 — all 16 combinations
    // -----------------------------------------------------------------------

    /// <summary>
    /// For any randomly drawn (extension, mime, bytes-kind) combination, the
    /// validation outcome must equal the expected result derived from the
    /// combination alone.
    /// <b>Validates: Requirements 1.3, 1.4</b>
    /// </summary>
    [Property(Arbitrary = [typeof(PhotoArbitraries)], MaxTest = 500)]
    public Property Property4_ValidationSoundness_AllCombinations(
        (PhotoScenario Scenario, byte[] RandomBytes) input)
    {
        var (scenario, randomBytes) = input;
        var file   = scenario.BuildFile(randomBytes);
        var result = PhotoValidationHelper.Validate(file);

        return Prop.Label(
            result.IsValid == scenario.ExpectedValid,
            $"Extension={scenario.Extension}, Mime={scenario.Mime}, Bytes={scenario.Bytes} " +
            $"=> expected IsValid={scenario.ExpectedValid}, got IsValid={result.IsValid} " +
            $"ErrorMessage='{result.ErrorMessage}'");
    }

    // -----------------------------------------------------------------------
    // Property 4 (explicit axis) — extension is the first gate
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the extension is invalid, the result must always be Invalid
    /// regardless of MIME type or byte content.
    /// <b>Validates: Requirements 1.3, 1.4</b>
    /// </summary>
    [Property(Arbitrary = [typeof(PhotoArbitraries)], MaxTest = 200)]
    public Property Property4_InvalidExtension_AlwaysRejectsFirst(
        MimeKind mime, BytesKind bytesKind)
    {
        var scenario = new PhotoScenario(ExtensionKind.Invalid, mime, bytesKind);
        var file     = scenario.BuildFile();
        var result   = PhotoValidationHelper.Validate(file);

        return Prop.Label(
            !result.IsValid,
            $"Expected rejection for invalid extension; got IsValid={result.IsValid}");
    }

    // -----------------------------------------------------------------------
    // Property 4 (explicit axis) — MIME is the second gate
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the extension is valid but the MIME type is invalid, the result
    /// must always be Invalid regardless of byte content.
    /// <b>Validates: Requirements 1.3, 1.4</b>
    /// </summary>
    [Property(Arbitrary = [typeof(PhotoArbitraries)], MaxTest = 200)]
    public Property Property4_ValidExtension_InvalidMime_AlwaysRejects(BytesKind bytesKind)
    {
        var scenario = new PhotoScenario(ExtensionKind.Valid, MimeKind.Invalid, bytesKind);
        var file     = scenario.BuildFile();
        var result   = PhotoValidationHelper.Validate(file);

        return Prop.Label(
            !result.IsValid,
            $"Expected rejection for invalid MIME; got IsValid={result.IsValid}");
    }

    // -----------------------------------------------------------------------
    // Property 4 (explicit axis) — magic bytes are the third gate
    // -----------------------------------------------------------------------

    /// <summary>
    /// When extension and MIME are both valid but the byte content is NOT the
    /// PNG magic signature, the result must always be Invalid.
    /// <b>Validates: Requirements 1.3, 1.4</b>
    /// </summary>
    [Property(Arbitrary = [typeof(PhotoArbitraries)], MaxTest = 300)]
    public Property Property4_ValidExtensionAndMime_NonPngBytes_AlwaysRejects(
        BytesKind bytesKind)
    {
        // Only test non-PNG byte kinds to verify the magic-byte gate
        if (bytesKind == BytesKind.Png)
            return Prop.ToProperty(true); // skip — this case should be valid

        var scenario = new PhotoScenario(ExtensionKind.Valid, MimeKind.Valid, bytesKind);
        var file     = scenario.BuildFile();
        var result   = PhotoValidationHelper.Validate(file);

        return Prop.Label(
            !result.IsValid,
            $"Expected rejection for non-PNG bytes ({bytesKind}); got IsValid={result.IsValid}");
    }

    // -----------------------------------------------------------------------
    // Property 4 (positive case) — all three gates pass → Valid
    // -----------------------------------------------------------------------

    /// <summary>
    /// When extension is .png, MIME is image/png, and bytes start with the PNG
    /// magic signature, the result must always be Valid.
    /// <b>Validates: Requirements 1.3, 1.4</b>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Property4_AllGatesPass_AlwaysValid(PositiveInt extraLength)
    {
        var extraBytes = new byte[extraLength.Get % 64 + 1];
        var content    = MagicBytes.Png.Concat(extraBytes).ToArray();

        var file   = new FakeFormFile("photo.png", "image/png", content);
        var result = PhotoValidationHelper.Validate(file);

        return Prop.Label(
            result.IsValid,
            $"Expected Valid for proper PNG; got IsValid={result.IsValid}, Error='{result.ErrorMessage}'");
    }

    // -----------------------------------------------------------------------
    // Property 4 (error message soundness) — error messages are meaningful
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the result is Invalid, the ErrorMessage must be non-null and non-empty.
    /// <b>Validates: Requirements 1.3, 1.4</b>
    /// </summary>
    [Property(Arbitrary = [typeof(PhotoArbitraries)], MaxTest = 500)]
    public Property Property4_InvalidResult_AlwaysHasErrorMessage(
        (PhotoScenario Scenario, byte[] RandomBytes) input)
    {
        var (scenario, randomBytes) = input;
        var file   = scenario.BuildFile(randomBytes);
        var result = PhotoValidationHelper.Validate(file);

        if (result.IsValid)
            return Prop.ToProperty(true); // valid result — not the subject of this property

        return Prop.Label(
            !string.IsNullOrWhiteSpace(result.ErrorMessage),
            $"Expected non-empty ErrorMessage for invalid result, got '{result.ErrorMessage}'");
    }
}

// ---------------------------------------------------------------------------
// Conventional xUnit tests — edge cases and specific scenarios
// ---------------------------------------------------------------------------

/// <summary>
/// Specific example-based tests for <see cref="PhotoValidationHelper"/> covering
/// null/empty files and the renamed-JPEG (magic-byte spoofing) scenario.
/// <b>Validates: Requirements 1.3, 1.4</b>
/// </summary>
public class PhotoValidationHelperEdgeCaseTests
{
    [Fact]
    public void Validate_NullFile_ReturnsInvalid()
    {
        var result = PhotoValidationHelper.Validate(null!);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Fact]
    public void Validate_EmptyFile_ReturnsInvalid()
    {
        var file   = new FakeFormFile("photo.png", "image/png", []);
        var result = PhotoValidationHelper.Validate(file);

        Assert.False(result.IsValid);
        Assert.Contains("No file", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ValidExtension_ValidMime_WrongBytes_ReturnsInvalid()
    {
        byte[] wrongBytes = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07];
        var file   = new FakeFormFile("photo.png", "image/png", wrongBytes);
        var result = PhotoValidationHelper.Validate(file);

        Assert.False(result.IsValid);
        Assert.Contains("valid PNG", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RenamedJpeg_PngExtension_PngMime_JpegBytes_ReturnsInvalid()
    {
        // Classic attack: rename a JPEG as photo.png and claim image/png MIME —
        // must be rejected by the magic-byte gate
        var content = MagicBytes.Jpeg.Concat(new byte[] { 0x00, 0x00 }).ToArray();
        var file    = new FakeFormFile("photo.png", "image/png", content);
        var result  = PhotoValidationHelper.Validate(file);

        Assert.False(result.IsValid);
        Assert.Contains("valid PNG", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RenamedGif_PngExtension_PngMime_GifBytes_ReturnsInvalid()
    {
        var content = MagicBytes.Gif.Concat(new byte[] { 0x00, 0x00 }).ToArray();
        var file    = new FakeFormFile("photo.png", "image/png", content);
        var result  = PhotoValidationHelper.Validate(file);

        Assert.False(result.IsValid);
        Assert.Contains("valid PNG", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ValidPngFile_ReturnsValid()
    {
        var content = MagicBytes.Png.Concat(new byte[] { 0x00, 0x00, 0x00 }).ToArray();
        var file    = new FakeFormFile("photo.png", "image/png", content);
        var result  = PhotoValidationHelper.Validate(file);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_JpegExtension_ValidMime_PngBytes_RejectsAtExtensionStage()
    {
        // Extension fails first, even if bytes are valid PNG
        var content = MagicBytes.Png.Concat(new byte[] { 0x00 }).ToArray();
        var file    = new FakeFormFile("photo.jpg", "image/png", content);
        var result  = PhotoValidationHelper.Validate(file);

        Assert.False(result.IsValid);
        Assert.Contains(".jpg", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_PngExtension_JpegMime_PngBytes_RejectsAtMimeStage()
    {
        // MIME fails second, even if bytes are valid PNG
        var content = MagicBytes.Png.Concat(new byte[] { 0x00 }).ToArray();
        var file    = new FakeFormFile("photo.png", "image/jpeg", content);
        var result  = PhotoValidationHelper.Validate(file);

        Assert.False(result.IsValid);
        Assert.Contains("image/png", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_FileShorterThanMagicSignature_ReturnsInvalid()
    {
        // Only 4 bytes — not enough to match the 8-byte PNG magic
        byte[] shortContent = [0x89, 0x50, 0x4E, 0x47];
        var file    = new FakeFormFile("photo.png", "image/png", shortContent);
        var result  = PhotoValidationHelper.Validate(file);

        Assert.False(result.IsValid);
        Assert.Contains("valid PNG", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
