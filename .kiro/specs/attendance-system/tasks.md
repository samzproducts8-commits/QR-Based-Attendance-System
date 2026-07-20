# Implementation Plan: QR-Based Employee Attendance Management System

## Overview

Implement the attendance system incrementally: scaffold the solution structure first, then build infrastructure (DB, EF Core, Identity), then core backend services from the inside out (domain → application → API), and finally the Angular frontend. SignalR integration and reporting are added once core attendance recording works end-to-end. Each task builds on the previous and wires components together progressively.

**Technology Stack:** ASP.NET Core Web API (Clean Architecture) · Angular 17+ SPA · Microsoft SQL Server · EF Core · SignalR · xUnit / FsCheck

---

## Tasks

- [x] 1. Scaffold backend solution structure (Clean Architecture)
  - Create solution file `Attendance.sln` with four projects: `Attendance.Api`, `Attendance.Application`, `Attendance.Infrastructure`, `Attendance.Tests`
  - Set up project references: Api → Application, Api → Infrastructure, Infrastructure → Application, Tests → Application + Infrastructure
  - Add NuGet packages to each project:
    - Api: `Microsoft.AspNetCore.SignalR`, `Serilog.AspNetCore`, `Swashbuckle.AspNetCore`, `AutoMapper.Extensions.Microsoft.DependencyInjection`, `FluentValidation.AspNetCore`
    - Infrastructure: `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `QRCoder`, `ClosedXML`, `PdfSharpCore`
    - Application: `AutoMapper`, `FluentValidation`
    - Tests: `xunit`, `xunit.runner.visualstudio`, `Moq`, `FsCheck.Xunit`, `Microsoft.AspNetCore.Mvc.Testing`
  - Create `.editorconfig` and `global.json` with pinned SDK version
  - _Requirements: all_

- [x] 2. Define domain models and enums in Infrastructure
  - [x] 2.1 Create enum files in `Attendance.Infrastructure/Enums/`
    - `AttendanceStatus.cs`: `OnTime = 0, Late = 1, ManualEntry = 2`
    - `QrSessionStatus.cs`: `Active = 0, Used = 1, Expired = 2`
    - `SlotType.cs`: `MorningIn, LunchOut, LunchIn, EveningOut`
    - `StaffStatus.cs`: `Inactive = 0, Active = 1`
    - _Requirements: 1.2, 2.2, 3.2_

  - [x] 2.2 Create entity model classes in `Attendance.Infrastructure/Models/`
    - `Department.cs`, `Staff.cs`, `StaffProfile.cs`, `AttendanceSlotConfig.cs`, `QrSession.cs`, `AttendanceLog.cs`, `ApplicationUser.cs` (extends IdentityUser)
    - Include all properties defined in the schema with correct CLR types (e.g., `DateOnly`, `TimeOnly`, `DateTime2` → `DateTime`, `TINYINT` → enum-typed `byte`)
    - _Requirements: 1.2, 2.2, 3.2_

- [x] 3. Set up EF Core DbContext, Fluent API configurations, and initial migration
  - [x] 3.1 Create `ApplicationDbContext` in `Attendance.Infrastructure/Data/`
    - Extend `IdentityDbContext<ApplicationUser>`
    - Register DbSets for all entities
    - Override `OnModelCreating` to apply Fluent API configurations from `Configurations/` folder
    - _Requirements: 1.2, 3.2, 7.3, 7.4_

  - [x] 3.2 Create Fluent API entity configuration classes in `Attendance.Infrastructure/Data/Configurations/`
    - `StaffConfiguration`: unique index on `UniqueCode` and `Email`; FK to `Department`
    - `StaffProfileConfiguration`: one-to-one with `Staff`; CHECK constraint on `PhotoContentType`
    - `QrSessionConfiguration`: unique index on `TokenValue`; filtered index on `Status = 0`
    - `AttendanceLogConfiguration`: composite unique constraint `(StaffId, SlotId, EventDate)`; non-clustered index on `(EventDate, StaffId)`
    - `AttendanceSlotConfigConfiguration`, `DepartmentConfiguration`
    - _Requirements: 1.8, 3.8, 7.3, 7.4_

  - [x] 3.3 Create and apply initial EF Core migration
    - Run `dotnet ef migrations add InitialSchema` targeting `Attendance.Infrastructure`
    - Verify generated migration SQL matches the schema in the design document
    - Create `SeedData.cs` to seed: four default `AttendanceSlotConfig` rows (MorningIn 08:00–09:00, LunchOut 12:00–13:00, LunchIn 13:00–14:00, EveningOut 17:00–18:00) and default Admin role/user
    - _Requirements: 2.2, 5.4_

- [x] 4. Implement Application layer interfaces, DTOs, and helpers
  - [x] 4.1 Define all interface contracts in `Attendance.Application/Interfaces/`
    - `IStaffService`, `IAttendanceService`, `IQrSessionService`, `ISlotConfigService`, `IFileStorageHelper`
    - Include all method signatures exactly as defined in the design document
    - _Requirements: 1.1–1.8, 2.1–2.5, 3.1–3.12_

  - [x] 4.2 Create DTO record classes in `Attendance.Application/DTO/`
    - `CreateStaffRequest`, `UpdateStaffRequest`, `StaffDto`, `StaffFilterRequest`, `PagedResult<T>`
    - `ScanRequestDto`, `AttendanceRecordDto`, `QrCodeResponseDto`, `QrSessionConsumeResult`
    - `SlotConfigDto`, `CreateSlotRequest`, `UpdateSlotRequest`
    - `DailyAttendanceSheet`, `MonthlySummary`, `DailySlotEntry`
    - _Requirements: 1.2, 3.4, 3.12, 4.1, 4.2_

  - [x] 4.3 Create `PhotoValidationHelper` in `Attendance.Application/Helpers/`
    - Implement three-stage validation: file extension → MIME type → PNG magic bytes `[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]`
    - Return a `ValidationResult` record with `IsValid` and `ErrorMessage`
    - _Requirements: 1.3, 1.4_

  - [x] 4.4 Write property tests for PhotoValidationHelper
    - **Property 4: PNG Magic-Byte Validation Soundness** — generate files with all combinations of (valid/invalid extension) × (valid/invalid MIME) × (PNG/JPEG/GIF/random bytes) and verify result matches expected Valid/Invalid
    - **Validates: Requirements 1.3, 1.4**

  - [x] 4.5 Create `DateTimeHelper` and `SlotResolver` in `Attendance.Application/Helpers/`
    - `SlotResolver.ResolveSlotForTime(TimeOnly, IEnumerable<AttendanceSlotConfig>)` — returns first active slot covering the given time or null
    - `DateTimeHelper.ComputeStatusFlag(TimeOnly eventTime, AttendanceSlotConfig slot)` — returns `OnTime` or `Late`
    - _Requirements: 3.9, 3.11, 4.1_

  - [x] 4.6 Write property tests for SlotResolver and DateTimeHelper
    - **Property 2: Slot Resolution Correctness** — for any time within a slot window, correct slot returned; for time outside all windows, null returned
    - **Property 3: Grace-Period Status Consistency** — for any event time ≤ deadline, status is OnTime; for any time > deadline, status is Late
    - **Validates: Requirements 3.9, 3.11, 3.4, 4.1**

  - [x] 4.7 Create `QrCodeGeneratorHelper` wrapping `QRCoder`
    - `GenerateQrCodeBase64(string tokenValue): string` — returns base64-encoded PNG QR image
    - _Requirements: 3.1_

  - [x] 4.8 Create AutoMapper `MappingProfiles` in `Attendance.Application/Helpers/`
    - Map `Staff → StaffDto`, `AttendanceSlotConfig → SlotConfigDto`, `AttendanceLog → AttendanceRecordDto`, `QrSession → QrCodeResponseDto`
    - _Requirements: 1.2, 3.4, 3.12_

- [x] 5. Implement Infrastructure repositories
  - [x] 5.1 Create `IGenericRepository<T>` interface and `GenericRepository<T>` implementation
    - Methods: `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `FindAsync(Expression<Func<T,bool>>)`
    - _Requirements: 1.1–1.8_

  - [x] 5.2 Create `IQrSessionRepository` with the atomic consume method
    - `ConsumeTokenAsync(Guid tokenValue): Task<(int rowsAffected, QrSession? session)>` — executes the single conditional UPDATE and returns the result
    - Use `ExecuteSqlRawAsync` or raw ADO.NET for the atomic UPDATE to avoid EF Core change-tracking interference
    - _Requirements: 3.3, 3.8_

  - [x] 5.3 Create `LocalFileStorageHelper` implementing `IFileStorageHelper`
    - Store photos under `wwwroot/staff-photos/{staffCode}/`
    - Return relative path for storage in `StaffProfile.PhotoPath`
    - _Requirements: 1.3, 1.5_

- [x] 6. Implement QrSessionService (core feature)
  - [x] 6.1 Implement `QrSessionService` in `Attendance.Application/Services/`
    - `GenerateNewTokenAsync()`: create `QrSession` (GUID, ExpiresAt = now + 15s, Status=Active), generate QR image via `QrCodeGeneratorHelper`, push to `IAttendanceHubContext`, return `QrCodeResponseDto`
    - `ValidateAndConsumeAsync(Guid)`: call `IQrSessionRepository.ConsumeTokenAsync`; if rowsAffected=0, re-query to return correct failure reason (`NotFound` / `AlreadyUsed` / `Expired`); if rowsAffected=1, call `GenerateNewTokenAsync` and return success
    - `ExpireStaleTokensAsync()`: UPDATE QrSession SET Status=Expired WHERE Status=Active AND ExpiresAt < NOW()
    - _Requirements: 3.2, 3.3, 3.5, 3.6, 3.7_

  - [x] 6.2 Write property tests for QrSessionService
    - **Property 1: Token Single-Use Guarantee** — mock repository to simulate two concurrent consume calls returning (1 row, 0 rows); verify first gets Success and second gets AlreadyUsed
    - **Property 7: Token State Immutability** — verify that after a Used/Expired result, no further state change is requested
    - Use FsCheck generators for random GUID token values
    - **Validates: Requirements 3.3, 3.5, 3.8**

  - [x] 6.3 Implement `QrTokenExpiryBackgroundService` as `IHostedService`
    - Run every 5 seconds; call `IQrSessionService.ExpireStaleTokensAsync()`
    - Log expired token counts via Serilog
    - _Requirements: 3.7_

- [x] 7. Implement AttendanceService
  - [x] 7.1 Implement `AttendanceService` in `Attendance.Application/Services/`
    - `RecordAttendanceAsync(staffId, qrSessionId)`: load active slots, call `SlotResolver.ResolveSlotForTime`, compute `StatusFlag`, insert `AttendanceLog`, format greeting message, return `AttendanceRecordDto`
    - Handle `DbUpdateException` with unique constraint violation → re-throw as `DuplicateAttendanceException`
    - _Requirements: 3.4, 3.9, 3.10, 3.11, 3.12_

  - [x] 7.2 Write property tests for AttendanceService
    - **Property 6: Duplicate Attendance Rejection** — mock repository insert to throw on second attempt; verify DuplicateAttendanceException is raised
    - **Property 3 (integration)** — verify StatusFlag assignment matches grace-period boundary for generated time values
    - **Validates: Requirements 3.10, 7.3**

  - [x] 7.3 Implement report methods in `AttendanceService`
    - `GetDailySheetAsync(staffId, date)`: query logs for that staff/date; for each active mandatory slot, include log or Absent marker
    - `GetMonthlySummaryAsync(staffId?, departmentId?, year, month)`: aggregate OnTime/Late/Absent counts per staff per slot
    - `ExportDailyReportAsync(date, format)`: generate XLSX via ClosedXML or PDF via PdfSharpCore
    - `ExportMonthlyReportAsync(year, month, format)`: same for monthly data
    - _Requirements: 4.1–4.5_

  - [x] 7.4 Write property tests for report generation
    - **Property 8: Report Completeness** — generate random sets of AttendanceLogs; verify daily sheet has correct slot count and Absent entries match missing mandatory slots
    - **Validates: Requirements 4.1, 4.2, 4.5**

- [x] 8. Implement StaffService
  - [x] 8.1 Implement `StaffService` in `Attendance.Application/Services/`
    - `CreateStaffAsync`: validate photo via `PhotoValidationHelper`, generate next `UniqueCode` (SELECT MAX + format), save photo via `IFileStorageHelper`, insert `Staff` + `StaffProfile` in a transaction, return `StaffDto`
    - `UpdateStaffAsync`, `DeactivateStaffAsync` (sets Status=Inactive, does not delete), `GetByIdAsync`, `GetAllAsync` with filter/pagination
    - _Requirements: 1.1–1.8_

  - [x] 8.2 Write property tests for StaffService
    - **Property 5: UniqueCode Monotonic Uniqueness** — simulate N sequential registrations with mocked repository; verify all codes are distinct, follow `EMP-\d{4}` pattern, and increment
    - **Property 4 (delegation test)** — verify that StaffService calls PhotoValidationHelper and rejects invalid files before touching the repository
    - **Validates: Requirements 1.1, 1.7, 1.8**

- [x] 9. Set up ASP.NET Core API — middleware, auth, and DI wiring
  - [x] 9.1 Configure `Program.cs`
    - Register EF Core with connection string from `appsettings.json`
    - Register ASP.NET Core Identity with `ApplicationUser` and roles (Admin, HR, Employee)
    - Register JWT bearer authentication with `IssuerSigningKey`, `ValidateAudience`, `ValidateIssuer`
    - Register all Application services and Infrastructure repositories via DI
    - Register AutoMapper, FluentValidation, Serilog, SignalR
    - Configure CORS policy allowing only the Angular origin
    - Map SignalR hub: `app.MapHub<AttendanceHub>("/hubs/attendance")`
    - _Requirements: 5.1–5.6, 6.1_

  - [x] 9.2 Implement `ExceptionHandlingMiddleware`
    - Catch all unhandled exceptions; return `ProblemDetails` JSON (RFC 7807) with appropriate HTTP status
    - Map custom exceptions: `PhotoValidationException → 400`, `DuplicateAttendanceException → 409`, `OutsideScheduleException → 422`, `TokenAlreadyUsedException → 409`, `TokenExpiredException → 410`, `NotFoundException → 404`
    - Log exception via Serilog; do NOT include stack trace in response body
    - _Requirements: 7.5_

  - [x] 9.3 Implement `AttendanceHub` (SignalR) in `Attendance.Api/Hubs/`
    - Method `RequestCurrentQr()` — on client connect or explicit call, push the current active QR code to the caller
    - Method `ReceiveQrCode(string base64Png, Guid tokenValue, DateTime expiresAt)` — client-side event name for incoming QR updates
    - Require `[Authorize(Roles = "Admin,Employee")]` on hub (kiosk runs as a special kiosk user or unauthenticated with a dedicated policy)
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 10. Implement API controllers
  - [x] 10.1 Implement `AuthController`
    - `POST /api/auth/login` — validate credentials via ASP.NET Identity, issue JWT with role claim, return access token + expiry
    - `POST /api/auth/refresh` — refresh token rotation
    - _Requirements: 5.1_

  - [x] 10.2 Implement `StaffController`
    - `POST /api/staff` — `[Authorize(Roles="Admin")]`, accept `multipart/form-data`, call `IStaffService.CreateStaffAsync`
    - `PUT /api/staff/{id}` — `[Authorize(Roles="Admin,HR")]`
    - `PATCH /api/staff/{id}/deactivate` — `[Authorize(Roles="Admin")]`
    - `GET /api/staff/{id}` — `[Authorize(Roles="Admin,HR")]`
    - `GET /api/staff` — `[Authorize(Roles="Admin,HR")]` with filter query params
    - _Requirements: 1.1–1.8, 5.3, 5.4_

  - [x] 10.3 Implement `SlotConfigController`
    - `GET/POST /api/slots` — `[Authorize(Roles="Admin")]`
    - `PUT/DELETE /api/slots/{id}` — `[Authorize(Roles="Admin")]`
    - _Requirements: 2.1–2.5, 5.4_

  - [x] 10.4 Implement `QrSessionController`
    - `POST /api/qr/generate` — `[Authorize(Roles="Admin")]`, calls `IQrSessionService.GenerateNewTokenAsync()`, triggers initial kiosk display
    - `GET /api/qr/current` — `[Authorize(Roles="Admin")]`, returns current active token metadata (for admin monitoring)
    - _Requirements: 3.1, 3.2_

  - [x] 10.5 Implement `AttendanceController`
    - `POST /api/attendance/scan` — `[Authorize(Roles="Employee")]`, extract staffId from JWT claims, call `IQrSessionService.ValidateAndConsumeAsync` then `IAttendanceService.RecordAttendanceAsync`; return greeting DTO or error
    - `GET /api/attendance/my-history` — `[Authorize(Roles="Employee")]`, returns own attendance logs
    - _Requirements: 3.3–3.12, 5.4, 5.5_

  - [x] 10.6 Implement `ReportController`
    - `GET /api/reports/daily?staffId=&date=` — `[Authorize(Roles="Admin,HR")]`
    - `GET /api/reports/monthly?year=&month=&staffId=&departmentId=` — `[Authorize(Roles="Admin,HR")]`
    - `GET /api/reports/daily/export?date=&format=xlsx|pdf` — `[Authorize(Roles="Admin,HR")]`, stream file response
    - `GET /api/reports/monthly/export?year=&month=&format=xlsx|pdf` — `[Authorize(Roles="Admin,HR")]`
    - _Requirements: 4.1–4.5, 5.4_

- [x] 11. Checkpoint — Ensure all backend tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 12. Scaffold Angular SPA
  - [x] 12.1 Create Angular workspace
    - `ng new attendance-spa --routing --style=scss --standalone=false`
    - Install dependencies: `@microsoft/signalr`, `@angular/material` (or PrimeNG), `rxjs`
    - Create environment files: `environment.ts` (dev) and `environment.prod.ts` with `apiUrl` and `hubUrl`
    - _Requirements: 8.1–8.6_

  - [x] 12.2 Set up Angular `core/` module
    - `AuthService`: login(), logout(), getToken(), `currentUser$` observable, token storage in `localStorage`
    - `ApiInterceptor`: attach `Authorization: Bearer <token>` to every outbound HTTP request
    - `ErrorHandlerService`: map HTTP error shapes to user-facing toast messages
    - Register `ApiInterceptor` as an HTTP interceptor in `AppModule`
    - _Requirements: 5.1, 5.6, 8.2_

  - [x] 12.3 Write unit tests for ApiInterceptor
    - Verify that every request sent after login contains the `Authorization: Bearer` header
    - Verify that requests sent before login do NOT contain the header
    - **Validates: Requirements 5.6**

  - [x] 12.4 Set up `shared/` module
    - `PhotoUploadComponent`: file input with `accept=".png"`, client-side MIME-type validation before emit, shows preview and error state
    - `DataTableComponent`: generic table with pagination and sort
    - `ToastComponent`: displays API error and success messages
    - _Requirements: 8.1, 8.2_

  - [x] 12.5 Write unit tests for PhotoUploadComponent
    - Verify that selecting a non-PNG file triggers the error state and does NOT emit the file
    - Verify that selecting a valid PNG emits the file
    - **Validates: Requirements 8.1**

- [x] 13. Implement Angular feature: Authentication
  - Create `features/auth/` with `LoginComponent` — reactive form for username/password, calls `AuthService.login()`, redirects on success
  - Add `AuthGuard` and `RoleGuard` for route protection
  - _Requirements: 5.1, 5.2, 5.3_

- [x] 14. Implement Angular feature: Staff management
  - [x] 14.1 Create `features/staff/StaffListComponent`
    - Display paginated staff list with search/filter; links to view and edit
    - `[Authorize(Roles="Admin,HR")]` route guard
    - _Requirements: 1.2, 5.4_

  - [x] 14.2 Create `features/staff/StaffFormComponent` (create and edit)
    - Reactive form covering all required fields; include `PhotoUploadComponent` for PNG-only upload
    - Call `POST /api/staff` or `PUT /api/staff/{id}` on submit
    - Display field-level validation errors returned from the API
    - _Requirements: 1.1–1.8, 8.1, 8.2_

  - [x] 14.3 Create `features/staff/StaffProfileComponent`
    - Read-only view of staff details and photo
    - Deactivate button that calls `PATCH /api/staff/{id}/deactivate`
    - _Requirements: 1.6, 5.4_

- [x] 15. Implement Angular feature: Attendance slot configuration
  - Create `features/attendance-config/SlotConfigComponent`
  - Display all four slot configs in a table with edit-in-place or modal form
  - Time pickers for StartTime and EndTime using `@angular/material` time picker or native `<input type="time">`
  - Call `POST/PUT /api/slots` on save; show validation errors inline
  - `[Authorize(Roles="Admin")]` route guard
  - _Requirements: 2.1–2.5, 8.5_

- [x] 16. Implement Angular feature: Kiosk QR display
  - Create `features/kiosk/KioskComponent`
  - Establish SignalR connection to `/hubs/attendance` using `@microsoft/signalr`; configure `withAutomaticReconnect()`
  - On `ReceiveQrCode` event: update `<img>` src with `data:image/png;base64,{base64}`
  - On connect: invoke `RequestCurrentQr` to receive the current token immediately
  - Full-screen CSS layout (`height: 100vh`, centered), no navigation chrome
  - On SignalR disconnect: display a "Reconnecting…" overlay; on reconnect, restore QR display
  - _Requirements: 6.1–6.5, 8.3_

- [x] 17. Implement Angular feature: Employee scan confirmation
  - Create `features/scan/ScanConfirmComponent`
  - Text input or URL-param capture for the token value (scanner app pastes/redirects the GUID)
  - Call `POST /api/attendance/scan { token }` on submit
  - On success: display greeting message (name, slot, time, status) with a green confirmation banner
  - On error: display specific error (already used, expired, outside window, duplicate) with guidance
  - Mobile-optimized layout: large font, large touch targets, minimal scrolling
  - `[Authorize(Roles="Employee")]` route guard
  - _Requirements: 3.12, 5.4, 8.4_

- [x] 18. Implement Angular feature: Attendance reports
  - [x] 18.1 Create `features/reports/DailyReportComponent`
    - Date picker and staff selector filters
    - Display table with all four slot columns and status badges (On Time = green, Late = amber, Absent = red)
    - Export buttons calling `/api/reports/daily/export?format=xlsx` and `?format=pdf`
    - _Requirements: 4.1, 4.3, 5.4, 8.6_

  - [x] 18.2 Create `features/reports/MonthlyReportComponent`
    - Month picker, department dropdown, and optional staff selector
    - Summary table with OnTime/Late/Absent counts per employee
    - Export buttons calling `/api/reports/monthly/export`
    - _Requirements: 4.2, 4.3, 4.4, 5.4, 8.6_

- [x] 19. Set up Angular routing, lazy loading, and app shell
  - Define `AppRoutingModule` with lazy-loaded routes for each feature module
  - Apply `AuthGuard` and `RoleGuard` to protected routes
  - Implement `AppComponent` shell with navigation sidebar (hidden for kiosk route) and `RouterOutlet`
  - Handle 401/403 HTTP errors in `ErrorHandlerService` by redirecting to login or showing access-denied page
  - _Requirements: 5.2, 5.3, 8.2_

- [x] 20. Checkpoint — Ensure all frontend tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 21. Write integration and end-to-end tests
  - [x] 21.1 Write API integration tests for the complete QR scan flow
    - Use `WebApplicationFactory<Program>` with a test SQL Server database (or `UseInMemoryDatabase` for speed)
    - Test: generate token → scan with authenticated employee → verify log created + new token pushed
    - Test: scan same token twice → second returns 409
    - Test: scan expired token → returns 410
    - **Validates: Requirements 3.3, 3.5, 3.8, 3.10**

  - [x] 21.2 Write API integration tests for staff registration photo validation
    - Test: upload valid PNG → 201 Created
    - Test: upload JPEG with `.png` extension → 400 (magic-byte rejection)
    - Test: upload file with MIME `image/jpeg` → 400 (MIME rejection)
    - **Validates: Requirements 1.3, 1.4**

  - [x] 21.3 Write API integration tests for role-based authorization
    - Test: Employee accessing `/api/staff` → 403
    - Test: HR accessing `/api/slots` → 403
    - Test: Unauthenticated request to any protected endpoint → 401
    - **Property 9: Role-Based Data Isolation** — verify Employee's `/api/attendance/my-history` returns only their own records
    - **Validates: Requirements 5.2, 5.3, 5.4, 5.5**

  - [x] 21.4 Write integration tests for report completeness
    - Seed known attendance data; call daily/monthly report endpoints; verify counts match seeded data
    - Verify absent slots appear correctly for mandatory slots with no log
    - **Validates: Requirements 4.1, 4.2, 4.5**

- [x] 22. Configure Serilog structured logging and audit trail
  - Configure Serilog in `Program.cs` with console and file sinks (rolling daily log files)
  - Add `RequestLoggingMiddleware` to log all incoming requests with method, path, status, duration
  - In `QrSessionService.ValidateAndConsumeAsync`: log every attempt with `StaffId`, `TokenValue` (truncated to first 8 chars for security), outcome, and client IP extracted from `IHttpContextAccessor`
  - _Requirements: 7.1_

- [x] 23. Finalize configuration, documentation, and solution cleanup
  - [x] 23.1 Write explanation document `docs/QR-Security.md`
    - Explain the single-use token mechanism: why the token carries no identity, how it prevents proxy check-ins
    - Explain race condition prevention: the atomic conditional UPDATE and why it guarantees exactly-once consumption
    - Explain the 15-second expiry window and auto-refresh cycle
    - _Requirements: 3.3, 3.8 (assessment deliverable)_

  - [x] 23.2 Finalize `appsettings.json` and `appsettings.Development.json`
    - Connection string placeholder, JWT settings (Issuer, Audience, SecretKey), CORS origin, QrToken ExpirySeconds (configurable, default 15), file storage base path
    - _Requirements: 2.3_

  - [x] 23.3 Add Swagger/OpenAPI documentation
    - Add XML doc comments to all controller actions and DTOs
    - Configure Swagger UI with JWT bearer scheme in `Program.cs`
    - _Requirements: all_

- [x] 24. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

---

## Notes

- Tasks marked with `*` are optional and can be skipped for a faster MVP delivery
- Each task references specific requirements for traceability
- Checkpoints at tasks 11, 20, and 24 ensure incremental validation at each layer
- Property tests validate universal correctness; unit tests validate specific examples and edge cases
- The atomic token consumption (task 6.1) is the most critical correctness requirement — implement and test it before any other attendance logic
- The `PhotoValidationHelper` magic-byte check (task 4.3) must be implemented before `StaffService` (task 8.1) since it is a hard dependency

## Task Dependency Graph

```json
{
  "waves": [
    {
      "wave": 1,
      "tasks": ["1"],
      "description": "Solution scaffolding — no dependencies"
    },
    {
      "wave": 2,
      "tasks": ["2", "2.1", "2.2"],
      "description": "Domain models and enums — depends on solution structure (wave 1)"
    },
    {
      "wave": 3,
      "tasks": ["3", "3.1", "3.2", "3.3"],
      "description": "EF Core DbContext and migrations — depends on domain models (wave 2)"
    },
    {
      "wave": 4,
      "tasks": ["4", "4.1", "4.2", "4.3", "4.4", "4.5", "4.6", "4.7", "4.8"],
      "description": "Application layer interfaces, DTOs, helpers — depends on models (wave 2)"
    },
    {
      "wave": 5,
      "tasks": ["5", "5.1", "5.2", "5.3"],
      "description": "Infrastructure repositories — depends on DbContext (wave 3) and interfaces (wave 4)"
    },
    {
      "wave": 6,
      "tasks": ["6", "6.1", "6.2", "6.3"],
      "description": "QrSessionService — depends on repositories (wave 5)"
    },
    {
      "wave": 7,
      "tasks": ["7", "7.1", "7.2", "7.3", "7.4"],
      "description": "AttendanceService — depends on QrSessionService (wave 6)"
    },
    {
      "wave": 8,
      "tasks": ["8", "8.1", "8.2"],
      "description": "StaffService — depends on helpers (wave 4) and repositories (wave 5)"
    },
    {
      "wave": 9,
      "tasks": ["9", "9.1", "9.2", "9.3"],
      "description": "API middleware, auth, DI wiring — depends on all services (waves 6-8)"
    },
    {
      "wave": 10,
      "tasks": ["10", "10.1", "10.2", "10.3", "10.4", "10.5", "10.6"],
      "description": "API controllers — depends on API setup (wave 9)"
    },
    {
      "wave": 11,
      "tasks": ["11"],
      "description": "Backend checkpoint"
    },
    {
      "wave": 12,
      "tasks": ["12", "12.1", "12.2", "12.3", "12.4", "12.5"],
      "description": "Angular scaffold and core — depends on backend checkpoint (wave 11)"
    },
    {
      "wave": 13,
      "tasks": ["13", "14", "14.1", "14.2", "14.3", "15", "16", "17", "18", "18.1", "18.2"],
      "description": "Angular features — depends on Angular scaffold (wave 12)"
    },
    {
      "wave": 14,
      "tasks": ["19"],
      "description": "Angular routing and app shell — depends on all features (wave 13)"
    },
    {
      "wave": 15,
      "tasks": ["20"],
      "description": "Frontend checkpoint"
    },
    {
      "wave": 16,
      "tasks": ["21", "21.1", "21.2", "21.3", "21.4", "22", "23", "23.1", "23.2", "23.3"],
      "description": "Integration tests, logging, documentation — depends on frontend checkpoint (wave 15)"
    },
    {
      "wave": 17,
      "tasks": ["24"],
      "description": "Final checkpoint"
    }
  ]
}
```
