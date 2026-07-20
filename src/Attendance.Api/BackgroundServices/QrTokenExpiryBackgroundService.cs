using Attendance.Application.Interfaces;

namespace Attendance.Api.BackgroundServices;

/// <summary>
/// Timer service that sweeps stale QR tokens: every few seconds it transitions
/// <c>Active</c> tokens past their <c>ExpiresAt</c> to <c>Expired</c>, which
/// also triggers generation of a replacement token pushed to the kiosk —
/// so the screen is never left showing a dead code.
/// </summary>
/// <remarks>
/// Satisfies Requirement 3.7 (task 6.3).  Interval configurable via
/// <c>QrToken:SweepIntervalSeconds</c> (default 5).
/// </remarks>
public sealed class QrTokenExpiryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QrTokenExpiryBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public QrTokenExpiryBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<QrTokenExpiryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _interval     = TimeSpan.FromSeconds(
            configuration.GetValue("QrToken:SweepIntervalSeconds", 5));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "QR token expiry sweep started (interval {Interval}s).", _interval.TotalSeconds);

        using var timer = new PeriodicTimer(_interval);

        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // IQrSessionService is scoped (it owns a DbContext), so each
                // sweep runs in its own service scope.
                using IServiceScope scope = _scopeFactory.CreateScope();
                var qrService = scope.ServiceProvider.GetRequiredService<IQrSessionService>();

                int expiredCount = await qrService.ExpireStaleTokensAsync();

                if (expiredCount > 0)
                    _logger.LogInformation(
                        "Expired {Count} stale QR token(s); a replacement was pushed to the kiosk.",
                        expiredCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let a transient DB failure kill the sweep loop.
                _logger.LogError(ex, "QR token expiry sweep failed; retrying next tick.");
            }
        }
    }
}
