# Requirements Document

## Introduction

This document specifies the functional and non-functional requirements for the QR-Based Employee Attendance Management System. The system replaces manual attendance tracking with a secure, digital solution that uses single-use, time-limited QR codes to record four daily attendance events per employee: morning check-in, lunch check-out, lunch check-in, and evening check-out. The system is built on ASP.NET Core Web API (Clean Architecture), an Angular SPA frontend, and Microsoft SQL Server.

## Glossary

- **Staff**: A registered employee whose attendance is tracked by the system
- **UniqueCode**: A system-generated identifier for each staff member in the format `EMP-XXXX` (e.g., EMP-0001)
- **AttendanceSlotConfig**: An admin-defined time window representing one of four daily attendance events
- **QrSession**: A server-generated, single-use attendance token with a short expiry window (10–15 seconds)
- **AttendanceLog**: A persisted record of a staff member's attendance event for a specific slot on a specific date
- **Token**: The GUID value encoded inside a QR code; carries no employee identity
- **Kiosk**: A tablet or monitor at the office entrance running the Angular kiosk display in a browser
- **AttendanceHub**: The SignalR hub responsible for pushing new QR images to connected kiosk clients
- **SlotType**: One of four named slots — `MorningIn`, `LunchOut`, `LunchIn`, `EveningOut`
- **StatusFlag**: The computed attendance quality: `OnTime (0)`, `Late (1)`, or `ManualEntry (2)`
- **System**: The ASP.NET Core Web API backend unless otherwise specified
- **API**: The ASP.NET Core Web API
- **SPA**: The Angular single-page application frontend
- **Admin**: A user with the Admin role who has full configuration and staff management access
- **HR**: A user with the HR/Manager role who can view reports and manage staff profiles
- **Employee**: A user with the Employee role who can scan QR codes and view their own attendance history

---

## Requirements

### Requirement 1: Staff Registration

**User Story:** As an Admin, I want to register new staff members with complete profile information and a mandatory PNG photo, so that each employee has a verified digital identity in the system.

#### Acceptance Criteria

1. WHEN an Admin submits a valid staff registration request, THE System SHALL create a new Staff record with an auto-generated UniqueCode following the pattern `EMP-XXXX` where XXXX is a zero-padded, monotonically increasing integer.
2. THE System SHALL capture the following fields for each Staff record: full name, gender, date of birth, phone number, email address, department, job title, employment date, and status (active/inactive).
3. WHEN a staff registration includes a photo upload, THE System SHALL accept only files where the extension is `.png`, the declared MIME type is `image/png`, AND the first eight bytes match the PNG magic signature `[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]`.
4. IF a photo file fails any validation check (extension, MIME type, or magic bytes), THEN THE System SHALL reject the request with HTTP 400 and return a descriptive error message identifying the failure reason.
5. THE System SHALL store each staff photo's file name, content type (`image/png`), and file path in the StaffProfile table, enforcing the content-type constraint at the database level.
6. WHEN an Admin requests to deactivate a Staff record, THE System SHALL set the staff Status to Inactive without deleting the record or any associated AttendanceLog rows (soft delete).
7. WHEN an Admin submits a staff registration with an email address that already exists in the system, THE System SHALL reject the request with HTTP 409 and return an error indicating the email is already in use.
8. THE System SHALL enforce uniqueness of UniqueCode across all Staff records.

---

### Requirement 2: Attendance Slot Configuration

**User Story:** As an Admin, I want to configure the four daily attendance time slots with start times, end times, and grace periods, so that the system can automatically determine whether an employee checks in on time or late.

#### Acceptance Criteria

1. THE System SHALL allow an Admin to create, read, update, and activate/deactivate AttendanceSlotConfig records through a dedicated API endpoint.
2. WHEN creating or updating an AttendanceSlotConfig, THE System SHALL require a SlotName (one of `MorningIn`, `LunchOut`, `LunchIn`, `EveningOut`), a StartTime, an EndTime, a GracePeriodMinutes value, and an IsMandatory flag.
3. WHEN an Admin updates an AttendanceSlotConfig, THE System SHALL apply the changes immediately without requiring an application restart or redeployment.
4. THE System SHALL support marking individual slots as non-mandatory to accommodate half-day schedules.
5. IF an Admin attempts to create an AttendanceSlotConfig where EndTime is not after StartTime, THEN THE System SHALL reject the request with HTTP 400 and a descriptive validation error.

---

### Requirement 3: QR Code Attendance Recording

**User Story:** As an Employee, I want to scan a QR code displayed on the office kiosk using my authenticated mobile browser to record my attendance, so that my check-in and check-out events are captured accurately without the possibility of another person checking in on my behalf.

#### Acceptance Criteria

1. THE System SHALL continuously display a QR code on the kiosk screen that encodes a single-use GUID token; the token SHALL NOT encode any employee identity information.
2. WHEN a QrSession token is generated, THE System SHALL store it in the database with `Status = Active` and an expiry timestamp set to 10–15 seconds from the generation time.
3. WHEN an Employee submits a scan request with a valid, unused, non-expired token, THE System SHALL atomically transition the QrSession status from `Active` to `Used` using a single conditional `UPDATE` statement that includes `WHERE Status = Active AND ExpiresAt > SYSUTCDATETIME()`.
4. WHEN the atomic token consumption UPDATE affects exactly 1 row, THE System SHALL record an AttendanceLog entry for the authenticated Employee with the resolved SlotId, current timestamp, EventDate, and computed StatusFlag.
5. WHEN the atomic token consumption UPDATE affects 0 rows, THE System SHALL NOT create an AttendanceLog entry and SHALL return an appropriate error response indicating whether the token was not found, already used, or expired.
6. WHEN a QrSession token has been successfully consumed, THE System SHALL generate a new QrSession token and push a new QR image to all connected kiosk clients via the AttendanceHub SignalR hub.
7. WHEN a QrSession token reaches its ExpiresAt timestamp without being consumed, THE System SHALL transition its status to `Expired` and generate a replacement token; the replacement SHALL be pushed to the kiosk via SignalR.
8. WHEN two concurrent scan requests submit the same token simultaneously, THE System SHALL ensure exactly one request receives a success response and the other receives a conflict error, with no duplicate AttendanceLog entries created.
9. WHEN recording an AttendanceLog entry, THE System SHALL automatically resolve the correct AttendanceSlotConfig by comparing the current server UTC time against all active slot windows.
10. WHEN an Employee attempts to record attendance for a slot they have already completed today, THE System SHALL reject the request with HTTP 409.
11. WHEN an Employee scans outside of any configured slot window, THE System SHALL reject the request with HTTP 422 and indicate no slot is currently open.
12. WHEN attendance is recorded successfully, THE System SHALL return a greeting message to the Employee's device including their name, the slot name, the recorded timestamp, and the computed status (On Time or Late).

---

### Requirement 4: Attendance Reporting

**User Story:** As an HR user, I want to view and export daily and monthly attendance reports per staff member and department, so that I can track punctuality, absences, and attendance trends.

#### Acceptance Criteria

1. WHEN an HR user requests a daily attendance sheet for a specific staff member and date, THE System SHALL return a report showing all four slot timestamps (or absence indicators for mandatory slots with no log entry) and the computed StatusFlag for each recorded event.
2. WHEN an HR user requests a monthly summary, THE System SHALL return aggregated data per staff member and per department showing counts of On Time, Late, and Absent events for each mandatory slot.
3. WHEN an HR user requests a report export, THE System SHALL generate and return a downloadable file in either Excel (`.xlsx`) or PDF format based on the requested format parameter.
4. THE System SHALL support filtering monthly reports by department, by individual staff member, or for all staff.
5. WHEN a mandatory slot has no AttendanceLog entry for a given staff member on a working day, THE System SHALL represent that slot as `Absent` in the daily and monthly reports.

---

### Requirement 5: Authentication and Authorization

**User Story:** As a system administrator, I want role-based access control enforced on all API endpoints, so that employees can only access their own attendance data while Admins and HR users can access management features.

#### Acceptance Criteria

1. THE System SHALL issue JWT bearer tokens upon successful login via `POST /api/auth/login`, including the user's role claim in the token payload.
2. WHEN a request is received without a valid JWT bearer token, THE System SHALL reject it with HTTP 401.
3. WHEN an authenticated user attempts to access an endpoint requiring a role they do not hold, THE System SHALL reject the request with HTTP 403.
4. THE System SHALL enforce the following role permissions:
   - Admin: full access to staff management, slot configuration, QR session management, and all reports
   - HR/Manager: access to staff profile views, all attendance reports, and report exports
   - Employee: access to the QR scan endpoint and their own attendance history only
5. WHEN an Employee requests attendance history, THE System SHALL return only records belonging to that Employee's StaffId.
6. THE SPA SHALL attach the JWT bearer token to every outbound API request via an Angular HTTP interceptor.

---

### Requirement 6: Real-Time Kiosk QR Display

**User Story:** As a kiosk device, I want to receive updated QR codes in real time via a persistent connection, so that the displayed QR code is always current without requiring page refreshes.

#### Acceptance Criteria

1. THE System SHALL provide a SignalR hub endpoint (`/hubs/attendance`) that kiosk clients connect to for receiving QR code updates.
2. WHEN a new QrSession token is generated (after a scan or after an expiry), THE System SHALL broadcast the new QR image (as a base64-encoded PNG string) and the token's expiry timestamp to all connected kiosk clients via the AttendanceHub.
3. WHEN a kiosk client connects to the AttendanceHub, THE System SHALL immediately send the current active QR code to that client.
4. THE SPA kiosk component SHALL display the QR image in full-screen mode and SHALL automatically reconnect to the hub if the connection is dropped.
5. WHERE a kiosk environment does not support WebSockets, THE SPA SHALL fall back to SignalR's Server-Sent Events or long-polling transport automatically.

---

### Requirement 7: Data Integrity and Audit

**User Story:** As a system auditor, I want all attendance events and QR scan attempts logged with full context, so that fraudulent or erroneous entries can be identified and investigated.

#### Acceptance Criteria

1. THE System SHALL log every QR scan attempt — both successful and failed — using Serilog, capturing the StaffId, TokenValue, outcome (success/failure reason), server timestamp, and client IP address.
2. THE System SHALL preserve all AttendanceLog records even when a Staff record is deactivated; historical records SHALL remain queryable via the reporting API.
3. THE System SHALL enforce the unique constraint `(StaffId, SlotId, EventDate)` on the AttendanceLog table at the database level.
4. THE System SHALL enforce the database-level CHECK constraint that `StaffProfile.PhotoContentType = 'image/png'`.
5. WHEN an unhandled exception occurs, THE System SHALL return a ProblemDetails-compliant JSON response (RFC 7807) and SHALL NOT expose internal stack traces to the client.

---

### Requirement 8: Frontend Usability

**User Story:** As any system user, I want the Angular SPA to provide a responsive, role-appropriate interface that guides me through my tasks without unnecessary complexity.

#### Acceptance Criteria

1. THE SPA SHALL provide a staff registration form with client-side validation that restricts the photo file picker to `.png` files using the HTML `accept=".png"` attribute and validates the MIME type before submitting.
2. THE SPA SHALL display clear, user-friendly error messages when API responses contain validation errors or conflict responses.
3. THE SPA kiosk display SHALL render in a full-screen, distraction-free layout suitable for a tablet or monitor at an office entrance.
4. THE SPA scan confirmation page SHALL be optimized for mobile browsers with large touch targets and minimal scrolling.
5. THE SPA SHALL provide admin screens for managing attendance slot configurations, including time pickers for StartTime and EndTime fields.
6. THE SPA SHALL provide report screens with filtering controls (date range, department, staff member) and export buttons for Excel and PDF formats.
