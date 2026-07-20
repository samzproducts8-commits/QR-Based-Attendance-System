using Microsoft.AspNetCore.Http;

namespace Attendance.Application.Helpers;

/// <summary>
/// Represents the outcome of a photo validation check.
/// </summary>
/// <param name="IsValid">Whether the file passed all validation stages.</param>
/// <param name="ErrorMessage">Descriptive failure reason, or null when valid.</param>
public record ValidationResult(bool IsValid, string? ErrorMessage)
{
    /// <summary>A pre-built successful result.</summary>
    public static readonly ValidationResult Valid = new(true, null);

    /// <summary>Creates a failed result with the given message.</summary>
    public static ValidationResult Invalid(string errorMessage) =>
        new(false, errorMessage);
}

/// <summary>
/// Performs three-stage photo validation:
///   1. File extension must be <c>.png</c>
///   2. MIME type must be <c>image/png</c>
///   3. First 8 bytes must match the PNG magic signature
/// </summary>
/// <remarks>
/// Satisfies Requirements 1.3 and 1.4.
/// </remarks>
public static class PhotoValidationHelper
{
    // PNG magic bytes: 89 50 4E 47 0D 0A 1A 0A
    private static readonly byte[] PngMagicBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Validates <paramref name="file"/> through extension, MIME type, and magic-byte checks.
    /// </summary>
    /// <param name="file">The uploaded file from a multipart form request.</param>
    /// <returns>
    /// <see cref="ValidationResult.Valid"/> when all checks pass; otherwise an
    /// <see cref="ValidationResult"/> whose <c>ErrorMessage</c> describes the first failure.
    /// </returns>
    public static ValidationResult Validate(IFormFile file)
    {
        // Stage 0: null / empty guard
        if (file == null || file.Length == 0)
            return ValidationResult.Invalid("No file uploaded.");

        // Stage 1: file extension
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".png")
            return ValidationResult.Invalid($"Only PNG images are accepted. Received: {ext}");

        // Stage 2: declared MIME type
        if (!string.Equals(file.ContentType, "image/png", StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Invalid("MIME type must be image/png.");

        // Stage 3: PNG magic bytes
        var header = new byte[PngMagicBytes.Length];
        using var stream = file.OpenReadStream();
        var bytesRead = stream.Read(header, 0, header.Length);

        if (bytesRead < PngMagicBytes.Length || !header.SequenceEqual(PngMagicBytes))
            return ValidationResult.Invalid("File content is not a valid PNG image.");

        return ValidationResult.Valid;
    }
}
