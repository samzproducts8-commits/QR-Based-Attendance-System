using QRCoder;

namespace Attendance.Application.Helpers;

/// <summary>
/// Wraps the <c>QRCoder</c> library to produce base64-encoded PNG QR images
/// from a plain string token value.
/// </summary>
/// <remarks>
/// Satisfies Requirement 3.1: the QR code encodes only the opaque GUID token
/// string — no employee identity is embedded in the image payload.
/// </remarks>
public static class QrCodeGeneratorHelper
{
    // 10 pixels per module produces a ~290 × 290 px image for a typical
    // Version-3 QR code, which is comfortably scannable on a kiosk display.
    private const int PixelsPerModule = 10;

    /// <summary>
    /// Generates a PNG QR code for <paramref name="tokenValue"/> and returns
    /// it as a Base64-encoded string suitable for embedding in a JSON response
    /// or an HTML <c>data:</c> URI.
    /// </summary>
    /// <param name="tokenValue">
    /// The opaque token string to encode (typically a GUID). Must not be
    /// <see langword="null"/> or empty.
    /// </param>
    /// <returns>
    /// A Base64 string representing the raw PNG bytes of the QR image.
    /// No <c>data:</c> prefix is included; callers prepend
    /// <c>data:image/png;base64,</c> when constructing an HTML image tag.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="tokenValue"/> is <see langword="null"/> or
    /// whitespace.
    /// </exception>
    public static string GenerateQrCodeBase64(string tokenValue)
    {
        if (string.IsNullOrWhiteSpace(tokenValue))
            throw new ArgumentException("Token value must not be null or empty.", nameof(tokenValue));

        using var generator = new QRCodeGenerator();
        using QRCodeData data = generator.CreateQrCode(tokenValue, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);

        byte[] pngBytes = png.GetGraphic(PixelsPerModule);
        return Convert.ToBase64String(pngBytes);
    }
}
