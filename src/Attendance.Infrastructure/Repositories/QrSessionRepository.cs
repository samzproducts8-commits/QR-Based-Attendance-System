using Attendance.Application.Enums;
using Attendance.Application.Interfaces;
using Attendance.Application.Models;
using Attendance.Infrastructure.Data;
using Attendance.Infrastructure.Enums;
using Attendance.Infrastructure.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Repositories;

/// <summary>
/// Concrete implementation of <see cref="IQrSessionRepository"/> backed by
/// <see cref="ApplicationDbContext"/> (EF Core / SQL Server).
/// </summary>
/// <remarks>
/// The atomic consume path uses <c>ExecuteSqlRawAsync</c> to bypass EF Core
/// change-tracking so that the single conditional UPDATE reaches the database
/// without being re-ordered or split by the ORM — preserving the
/// race-condition-free guarantee required by Requirement 3.8.
/// </remarks>
public sealed class QrSessionRepository : IQrSessionRepository
{
    private readonly ApplicationDbContext _context;

    public QrSessionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // -------------------------------------------------------------------------
    // IQrSessionRepository — atomic consume
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    /// <remarks>
    /// Executes the following single-statement UPDATE atomically:
    /// <code>
    /// UPDATE QrSession
    ///    SET Status = 1, UsedAt = SYSUTCDATETIME()
    ///  WHERE TokenValue = @token
    ///    AND Status     = 0
    ///    AND ExpiresAt  &gt; SYSUTCDATETIME()
    /// </code>
    /// If exactly one row was affected the token has been consumed; the method
    /// then fetches the updated row and returns it as a <see cref="QrSessionSnapshot"/>.
    /// If zero rows were affected the session snapshot is <see langword="null"/>
    /// and the caller should invoke <see cref="GetByTokenAsync"/> to determine
    /// the reason (not found / already used / expired).
    /// </remarks>
    public async Task<(int rowsAffected, QrSessionSnapshot? session)> ConsumeTokenAsync(
        Guid tokenValue, int? staffId = null)
    {
        // Parameterised SQL prevents injection and keeps the plan cache hot.
        var tokenParam = new SqlParameter("@token", tokenValue);
        var staffParam = new SqlParameter("@staffId", (object?)staffId ?? DBNull.Value);
// rawaffectd=0,i.e not allow change status and 1= allow change status
        int rowsAffected = await _context.Database.ExecuteSqlRawAsync(
            """
            UPDATE QrSession
               SET Status        = 1,
                   UsedAt        = SYSUTCDATETIME(),  
                   UsedByStaffId = @staffId
             WHERE TokenValue = @token 
               AND Status     = 0
               AND ExpiresAt  > SYSUTCDATETIME()
            """,
            tokenParam,
            staffParam);

        if (rowsAffected == 0)
        {
            return (0, null);
        }

        // Fetch the freshly-updated row so the caller has the full snapshot.
        QrSessionSnapshot? snapshot = await _context.QrSessions
            .AsNoTracking()
            .Where(s => s.TokenValue == tokenValue)
            .Select(s => new QrSessionSnapshot(
                s.QrSessionId,
                s.TokenValue,
                s.GeneratedAt,
                s.ExpiresAt,
                (QrSessionStatusCode)(byte)s.Status,
                s.UsedByStaffId,
                s.UsedAt))
            .FirstOrDefaultAsync();

        return (rowsAffected, snapshot);
    }

    // -------------------------------------------------------------------------
    // IQrSessionRepository — look-up helpers
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    /// This method is a Read Operation. Its job is to find a specific
    ///  QR session in the database and return a "Snapshot" (a read-only view) of it.
    public async Task<QrSessionSnapshot?> GetByTokenAsync(Guid tokenValue)
    {
        return await _context.QrSessions
            .AsNoTracking()
            .Where(s => s.TokenValue == tokenValue) //This is the rule. It compares the column in the database(s.TokenValue) against the ID you provided as a parameter (tokenValue).
            .Select(s => new QrSessionSnapshot(
                s.QrSessionId,
                s.TokenValue,
                s.GeneratedAt,
                s.ExpiresAt,
                (QrSessionStatusCode)(byte)s.Status,
                s.UsedByStaffId,
                s.UsedAt))
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<QrSessionSnapshot?> GetActiveTokenAsync()
    {
        return await _context.QrSessions
            .AsNoTracking()
            .Where(s => s.Status == QrSessionStatus.Active)
            .OrderByDescending(s => s.GeneratedAt)
            .Select(s => new QrSessionSnapshot(
                s.QrSessionId,
                s.TokenValue,
                s.GeneratedAt,
                s.ExpiresAt,
                (QrSessionStatusCode)(byte)s.Status,
                s.UsedByStaffId,
                s.UsedAt))
            .FirstOrDefaultAsync();
    }

    // -------------------------------------------------------------------------
    // IQrSessionRepository — write operations
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public async Task AddAsync(Guid tokenValue, DateTime generatedAt, DateTime expiresAt)
    {
        var session = new QrSession
        {
            TokenValue  = tokenValue,
            GeneratedAt = generatedAt,
            ExpiresAt   = expiresAt,
            Status      = QrSessionStatus.Active
        };

        await _context.QrSessions.AddAsync(session);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<int> ExpireStaleTokensAsync()
    {
        // Uses ExecuteSqlRawAsync to perform a bulk UPDATE without loading rows
        // into the change tracker.
        return await _context.Database.ExecuteSqlRawAsync(
            """
            UPDATE QrSession
               SET Status = 2
             WHERE Status    = 0
               AND ExpiresAt < SYSUTCDATETIME()
            """);
    }
}
