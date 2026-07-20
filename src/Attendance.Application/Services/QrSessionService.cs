using Attendance.Application.DTOs;
using Attendance.Application.Enums;
using Attendance.Application.Helpers;
using Attendance.Application.Interfaces;
using Attendance.Application.Models;

namespace Attendance.Application.Services;

/// <summary>
/// Manages the full lifecycle of QR session tokens:
/// generation, atomic consumption, and background expiry.
/// </summary>
/// <remarks>
/// Satisfies Requirements 3.2, 3.3, 3.5, 3.6, 3.7.
/// </remarks>
public sealed class QrSessionService : IQrSessionService
{
    // Requirement 3.2: default token lifetime is 15 seconds.
    // Configurable via the optional constructor parameter (appsettings QrToken:ExpirySeconds).
    private static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromSeconds(15);

    private readonly TimeSpan _tokenLifetime;
    private readonly IQrSessionRepository _repository;
    private readonly IAttendanceHubContext _hubContext;

   //scanbaseUrl=The QR code becomes a Deep Link. When the employee scans it,
   //  their phone says "Open in Browser," and it takes them 
   // directly to your website with the token already filled in.
    private readonly string? _scanBaseUrl;
 
    public QrSessionService(
        IQrSessionRepository repository,
        IAttendanceHubContext hubContext,// it push new qrcode to the KIOsk and
                                          //hub context it refresh automatically
        TimeSpan? tokenLifetime = null, 
        string? scanBaseUrl = null)
    {
        _repository = repository;
        _hubContext  = hubContext;
        _tokenLifetime = tokenLifetime ?? DefaultTokenLifetime;
        _scanBaseUrl = string.IsNullOrWhiteSpace(scanBaseUrl) ? null : scanBaseUrl;
    }

    /// <summary>
    /// Builds the string encoded into the QR image: a deep link when
    /// <see cref="_scanBaseUrl"/> is configured, otherwise the bare token
    /// (Requirement 3.1 — the payload carries no employee identity either way).
    /// </summary>
    private string BuildQrPayload(Guid tokenValue) =>
        _scanBaseUrl is null // i.e null means there is no url on the qr.
            ? tokenValue.ToString()
            : $"{_scanBaseUrl}?token={tokenValue}";

    /// <inheritdoc/>
    /// <remarks>
    /// Satisfies Requirements 3.1, 3.2, 3.6.
    /// Algorithm:
    /// <list type="number">
    ///   <item>Generate a new random GUID token (no employee identity embedded).</item>
    ///   <item>Persist the new <c>QrSession</c> row (Status = Active, ExpiresAt = now + 15 s).</item>
    ///   <item>Render a base64 PNG QR code via <see cref="QrCodeGeneratorHelper"/>.</item>
    ///   <item>Push the image and token to all kiosk clients via <see cref="IAttendanceHubContext"/>.</item>
    ///   <item>Return a <see cref="QrCodeResponseDto"/> to the caller.</item>
    /// </list>
    /// </remarks>
    public async Task<QrCodeResponseDto> GenerateNewTokenAsync()
    {
        Guid tokenValue  = Guid.NewGuid();
        DateTime now      = DateTime.UtcNow;
        DateTime expiresAt = now.Add(_tokenLifetime);

        await _repository.AddAsync(tokenValue, now, expiresAt);

        string base64Image = QrCodeGeneratorHelper.GenerateQrCodeBase64(BuildQrPayload(tokenValue));

        await _hubContext.SendNewQrCodeAsync(base64Image, tokenValue, expiresAt);

        return new QrCodeResponseDto(tokenValue, base64Image, expiresAt);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Satisfies Requirements 3.3, 3.5, 3.8.
    /// Algorithm:
    /// <list type="number">
    ///   <item>
    ///     Call <see cref="IQrSessionRepository.ConsumeTokenAsync"/> which executes a single
    ///     conditional UPDATE.  The database serialises concurrent calls, guaranteeing
    ///     at most one caller receives <c>rowsAffected = 1</c> for a given token.
    ///   </item>
    ///   <item>
    ///     When <c>rowsAffected = 0</c>, re-query via <see cref="IQrSessionRepository.GetByTokenAsync"/>
    ///     to classify the failure as NotFound / AlreadyUsed / Expired.
    ///   </item>
    ///   <item>
    ///     When <c>rowsAffected = 1</c>, regenerate the kiosk QR via
    ///     <see cref="GenerateNewTokenAsync"/> and return a success result.
    ///   </item>
    /// </list>
    /// </remarks>
    public async Task<QrSessionConsumeResult> ValidateAndConsumeAsync(Guid tokenValue, int? staffId = null)
    {
        (int rowsAffected, QrSessionSnapshot? session) = await _repository.ConsumeTokenAsync(tokenValue, staffId);

        if (rowsAffected == 0)
        {
            // Re-query to determine why the consume failed.
            QrSessionSnapshot? snapshot = await _repository.GetByTokenAsync(tokenValue);

            if (snapshot is null)
                return QrSessionConsumeResult.NotFound();

            if (snapshot.Status == QrSessionStatusCode.Used)
                return QrSessionConsumeResult.AlreadyUsed();

            // Status is Expired, or ExpiresAt has passed (defensive check).
            return QrSessionConsumeResult.Expired();
        }
        // rowsAffected == 1: token was valid and is now consumed.
        // Immediately refresh the kiosk display with a new token (Requirement 3.5).
        await GenerateNewTokenAsync();

        return QrSessionConsumeResult.Success(session!.QrSessionId);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Satisfies Requirement 6.2.
    /// Re-renders the QR image from the stored token value when a valid
    /// active token exists; otherwise falls through to
    /// <see cref="GenerateNewTokenAsync"/>.
    /// </remarks>
    public async Task<QrCodeResponseDto> GetOrCreateCurrentAsync()
    {
        QrSessionSnapshot? active = await _repository.GetActiveTokenAsync();

        if (active is null || active.ExpiresAt <= DateTime.UtcNow)
            return await GenerateNewTokenAsync();

        string base64Image = QrCodeGeneratorHelper.GenerateQrCodeBase64(BuildQrPayload(active.TokenValue));
        return new QrCodeResponseDto(active.TokenValue, base64Image, active.ExpiresAt);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Satisfies Requirement 3.7.
    /// Delegates bulk expiry to the repository (single UPDATE statement) and
    /// then generates a fresh replacement token so the kiosk always shows a
    /// valid code.
    /// </remarks>
    public async Task<int> ExpireStaleTokensAsync()
    {
        int expiredCount = await _repository.ExpireStaleTokensAsync();

        // If any tokens were expired, regenerate the kiosk QR code so the
        // display is never left showing a dead token.
        if (expiredCount > 0)
        {
            await GenerateNewTokenAsync();
        }

        return expiredCount;
    }
}
