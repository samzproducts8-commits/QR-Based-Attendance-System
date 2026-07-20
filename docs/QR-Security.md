# How the System Prevents QR Code Reuse and Handles Race Conditions

This document explains the anti-fraud mechanism at the heart of the QR-Based
Employee Attendance Management System: how a displayed QR code can never be
redeemed twice, and how the system stays correct even when two employees scan
at almost the same instant.

## 1. The token carries no identity

The QR code shown on the kiosk encodes **only a random `GUID`** — an opaque,
single-use *session token*. It contains no employee id, name, or any personal
data.

```
QrSession
  QrSessionId    INT      (PK)
  TokenValue     UNIQUEIDENTIFIER  UNIQUE   -- the GUID inside the QR image
  GeneratedAt    DATETIME2
  ExpiresAt      DATETIME2                  -- GeneratedAt + 15 seconds
  Status         TINYINT  (0=Active, 1=Used, 2=Expired)
  UsedByStaffId  INT      NULL
  UsedAt         DATETIME2 NULL
```

Because the token is not tied to a person, **stealing a photo of the QR code is
worthless**: it reveals only a GUID that is valid for at most 15 seconds and can
be redeemed exactly once. The link between a token and an employee is made *only*
by the authenticated scan request — the employee's JWT identifies them, the
token identifies the session. This is what defeats proxy attendance: you cannot
check in "as" someone else, because your identity comes from *your* login, not
from the code on the screen.

## 2. Single-use enforcement: one atomic conditional UPDATE

When an employee scans, their device sends `POST /api/attendance/scan { token }`.
The server does **not** do "read the row, check it, then write it back" — that
classic read-then-write pattern is exactly where a race condition lives. Instead
it consumes the token with a **single conditional `UPDATE`** that both checks and
mutates state in one indivisible database operation
(`QrSessionRepository.ConsumeTokenAsync`):

```sql
UPDATE QrSession
   SET Status        = 1,               -- Used
       UsedAt        = SYSUTCDATETIME(),
       UsedByStaffId = @staffId
 WHERE TokenValue = @token
   AND Status     = 0                    -- still Active
   AND ExpiresAt  > SYSUTCDATETIME();    -- not yet expired
```

The statement returns the number of rows affected:

- **`rowsAffected = 1`** → this caller *won*. The token was Active and unexpired,
  and is now atomically flipped to `Used`. Attendance is recorded and a brand-new
  token is generated and pushed to the kiosk.
- **`rowsAffected = 0`** → the token was **not** Active-and-unexpired. The service
  then does a single follow-up `SELECT` purely to explain *why* to the user:
  no row → `404 Not Found`; `Status = Used` → `409 Conflict` ("already used");
  otherwise → `410 Gone` ("expired").

State transitions are strictly one-way: `Active → Used` (by a scan) or
`Active → Expired` (by the background timer). Nothing ever moves a token back to
`Active`, so a consumed token stays dead forever.

## 3. Why this is race-condition-safe

Suppose two employees scan the **same** token within milliseconds of each other.
Both requests execute the same conditional `UPDATE`. The database engine
**serializes** these writes on the row — they cannot both run "at the same time"
against the same row. One of them arrives first, finds `Status = 0`, and updates
1 row. By the time the second one's `WHERE Status = 0` is evaluated, the row is
already `Status = 1`, so it matches nothing and updates **0 rows**.

The outcome is guaranteed by the database's row-level locking and atomicity — we
never rely on application-level timing, locks in C#, or the order requests happen
to arrive at the web server:

- Exactly **one** request can ever receive `rowsAffected = 1` for a given token.
- The winner records attendance; the loser is cleanly told the code was already
  used and simply scans the *new* code the kiosk is already displaying.

A filtered index on `QrSession(Status) WHERE Status = 0` keeps this `UPDATE` fast
even as the table accumulates millions of historical, consumed tokens.

## 4. Continuous rotation keeps the screen fresh

The kiosk never shows a stale code, via two complementary paths:

1. **On every successful scan**, the server immediately generates a new token and
   pushes its QR image to all kiosks over **SignalR** (`ReceiveQrCode`). The
   moment someone checks in, the code on the wall changes — so the next person in
   line already sees a different code.
2. **A background service** (`QrTokenExpiryBackgroundService`, every 5 seconds)
   sweeps any Active token whose `ExpiresAt` has passed:
   ```sql
   UPDATE QrSession SET Status = 2 WHERE Status = 0 AND ExpiresAt < SYSUTCDATETIME();
   ```
   If anything expired, it generates and pushes a fresh token. This covers the
   case where a code is displayed but nobody scans it within 15 seconds.

So the code on screen is *always* Active and unexpired, and any given code is
redeemable at most once. Combined with the identity coming from the scanner's own
authenticated session, this makes both **QR reuse** and **proxy check-in**
impossible.

## 5. A second integrity guard: one record per slot per day

Even after a valid, single-use scan, an employee could try to record the same
slot twice (e.g., scan two different fresh codes for "MorningIn" on the same day).
A composite unique constraint stops this at the database level:

```sql
CONSTRAINT UQ_AttLog_Staff_Slot_Date UNIQUE (StaffId, SlotId, EventDate)
```

The service does a friendly pre-check, but the constraint is the real guard: if
two inserts for the same `(StaffId, SlotId, EventDate)` race, the database rejects
the second with a unique-violation, which the API surfaces as `409 Conflict`.

## 6. Where to find it in the code

| Concern | File |
| --- | --- |
| Atomic consume UPDATE | `src/Attendance.Infrastructure/Repositories/QrSessionRepository.cs` |
| Token lifecycle / classify failure / refresh | `src/Attendance.Application/Services/QrSessionService.cs` |
| Scan endpoint (identity from JWT, audit log) | `src/Attendance.Api/Controllers/AttendanceController.cs` |
| Duplicate-slot guard + insert | `src/Attendance.Infrastructure/Repositories/AttendanceRepository.cs` |
| Expiry sweep + push | `src/Attendance.Api/BackgroundServices/QrTokenExpiryBackgroundService.cs` |
| Real-time push to kiosk | `src/Attendance.Api/Hubs/AttendanceHub.cs` |

The single-use guarantee is verified by property tests
(`QrSessionServicePropertyTests` — Property 1: Token Single-Use Guarantee) and by
end-to-end integration tests (`ScanFlowIntegrationTests` — scan-twice → 409,
expired → 410).
