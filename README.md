# QR-Based Employee Attendance Management System

A digital, QR-code-driven attendance system that records employee morning
check-in, lunch check-out, lunch check-in, and evening check-out securely, using
a continuously-rotating, single-use QR code that prevents proxy attendance.

**Stack:** ASP.NET Core Web API (Clean Architecture) · Angular 19 SPA ·
Microsoft SQL Server (LocalDB) · EF Core · SignalR · JWT · xUnit / FsCheck.

## Solution layout

```
Attendance.slnx                     # .NET solution
src/
  Attendance.Api/                   # Controllers, middleware, SignalR hub, JWT, DI, Program.cs
  Attendance.Application/           # Interfaces, DTOs, services, helpers (business logic)
  Attendance.Infrastructure/        # EF Core entities, DbContext, repositories, migrations
tests/
  Attendance.Tests/                 # Unit, property (FsCheck), and integration tests
attendance-spa/                     # Angular 19 SPA (NgModule-based, Angular Material)
docs/QR-Security.md                 # How single-use QR + race-condition safety works
.kiro/specs/attendance-system/      # requirements.md, design.md, tasks.md
```

The backend follows Clean Architecture: `Api → Application`, and
`Infrastructure → Application` (domain entities live in Infrastructure; the
Application layer defines interfaces the Infrastructure implements).

## Prerequisites

- .NET SDK 10 (pinned in `global.json`)
- SQL Server LocalDB (`sqllocaldb` — ships with Visual Studio / SQL Server Express)
- Node.js 18+ and npm (for the Angular app)

## Running the backend

```bash
# Ensure LocalDB is running
sqllocaldb start MSSQLLocalDB

# Run the API (applies migrations + seeds on startup)
cd src/Attendance.Api
dotnet run --urls "http://localhost:5080"
```

On first run the app migrates the database and seeds:
- The four default attendance slots (MorningIn, LunchOut, LunchIn, EveningOut)
- Roles: `Admin`, `HR`, `Employee`
- A default admin login — **username `admin`, password `Admin@123!`**
- Five default departments

Swagger UI: `http://localhost:5080/swagger`. Click **Authorize**, paste the JWT
from `POST /api/auth/login`, and try the endpoints.

Each staff member registered through `POST /api/staff` automatically gets an
`Employee`-role login: **username = their `EMP-XXXX` code, password
`Employee@123!`** (configurable via `Auth:DefaultEmployeePassword`).

## Running the frontend

```bash
cd attendance-spa
npm install

# Desktop dev (http, camera works because localhost is a secure context):
npm start           # → http://localhost:4200

# Phone-ready (HTTPS — required for the phone camera on the scan page):
npm run start:mobile   # → https://0.0.0.0:4200
```

The dev server proxies `/api`, `/hubs`, and `/staff-photos` to the backend on
port 5080 (see `attendance-spa/proxy.conf.json`), so the browser only ever
talks to one origin. `src/environments/environment.ts` uses **relative** URLs
(`/api`, `/hubs/attendance`) — there is **no LAN IP hardcoded anywhere**, so a
Wi-Fi/DHCP IP change never breaks the app. Log in as `admin` / `Admin@123!`.

Key screens:
- **/staff** — register staff (PNG-only photo), list, view, deactivate (Admin/HR)
- **/slots** — configure the four daily time windows (Admin)
- **/kiosk** — full-screen live QR display (SignalR); point a tablet here (no login)
- **/scan** — mobile page: **opens the phone camera and scans the kiosk QR live**,
  with a manual code-entry fallback (Employee)
- **/reports/daily** and **/reports/monthly** — with Excel/PDF export (Admin/HR)

### Phone access & the camera (important)

Browsers only allow camera access in a **secure context** (HTTPS or
`localhost`). To use the live camera scanner from a phone on the same Wi-Fi:

1. Start the frontend with `npm run start:mobile` (HTTPS) — or just run
   `start-all.bat` from the repo root, which does everything.
2. On the phone, open `https://<this-PC-IPv4>:4200` (the batch file prints the
   IP). The **first** time, you'll see a "Your connection is not private"
   warning from the self-signed dev certificate → **Advanced → Proceed**. This
   is expected and safe for a local dev server.
3. Log in as an employee (`EMP-XXXX` / `Employee@123!`), open **Scan**, allow
   the camera when prompted, and point it at the kiosk QR. Attendance records
   automatically. (If the camera is blocked/unavailable, the page falls back to
   manual code entry.)

## Running the tests

```bash
# Backend: unit + property + integration tests (integration needs LocalDB running)
sqllocaldb start MSSQLLocalDB
dotnet test Attendance.slnx

# Frontend unit tests
cd attendance-spa
npm test -- --watch=false
```

Integration tests spin up the real API via `WebApplicationFactory` against a
throwaway LocalDB database (required because the atomic token-consume path uses
SQL Server raw SQL that the EF InMemory provider cannot execute).

## The core anti-fraud mechanism

The centerpiece — how a QR code can never be reused and how simultaneous scans
are handled safely at the database level — is documented in
[docs/QR-Security.md](docs/QR-Security.md).

## Configuration (`src/Attendance.Api/appsettings.json`)

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | SQL Server / LocalDB connection |
| `Jwt:*` | Issuer, Audience, SecretKey, access/refresh token lifetimes |
| `Cors:AllowedOrigins` | Allowed SPA origin(s) (default `http://localhost:4200`) |
| `QrToken:ExpirySeconds` | Token lifetime (default 15s) |
| `QrToken:SweepIntervalSeconds` | Expiry background sweep interval (default 5s) |
| `Auth:DefaultEmployeePassword` | Initial password for auto-provisioned employee logins |

> The default secrets in `appsettings.json` are for local development only and
> must be replaced before any real deployment.
