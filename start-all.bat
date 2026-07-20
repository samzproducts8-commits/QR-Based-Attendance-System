@echo off
REM ─── QR Attendance System — one-click startup ─────────────────────────────
REM Starts LocalDB, the backend API (port 5080), and the Angular SPA over
REM HTTPS (port 4200), each in its own window.
REM
REM HTTPS is required so the phone's CAMERA works on the scan page — browsers
REM block camera access on plain http:// over a LAN. The Angular dev server
REM proxies /api and /hubs to the backend (see attendance-spa/proxy.conf.json),
REM so the browser only ever talks to one HTTPS origin: no CORS, no mixed
REM content, and no LAN IP hardcoded anywhere (survives Wi-Fi IP changes).

echo [1/3] Starting SQL Server LocalDB...
sqllocaldb start MSSQLLocalDB

echo [2/3] Starting backend API (http://0.0.0.0:5080)...
start "Attendance API" cmd /k "cd /d %~dp0src\Attendance.Api && dotnet run --urls http://0.0.0.0:5080"

echo [3/3] Starting Angular SPA over HTTPS (https://0.0.0.0:4200)...
start "Attendance SPA" cmd /k "cd /d %~dp0attendance-spa && npm run start:mobile"

echo.
echo ============================================================
echo  Both servers are starting in their own windows.
echo  Give them ~25 seconds, then open (note: HTTPS):
echo.
echo    On this PC:   https://localhost:4200        (login: admin / Admin@123!)
echo    Kiosk screen: https://localhost:4200/kiosk  (no login needed)
echo.
echo  On phones (same Wi-Fi), use this PC's IPv4 address:
ipconfig | findstr /i "IPv4"
echo.
echo    Phone URL:    https://^<IPv4 above^>:4200
echo.
echo  FIRST TIME on each device you'll see a "Your connection is not
echo  private" warning (self-signed dev certificate). Tap:
echo    Advanced  ->  Proceed to ^<address^> (unsafe)
echo  This is expected for a local dev server and is safe here.
echo  The phone camera on the Scan page needs this HTTPS step to work.
echo ============================================================
pause
