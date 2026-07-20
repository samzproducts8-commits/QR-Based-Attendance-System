@echo off
REM ─── QR Attendance System — one-click shutdown ────────────────────────────
REM Kills whatever is listening on the backend (5080) and frontend (4200)
REM ports. Safe to run even when nothing is running.

echo Stopping backend (port 5080)...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr :5080 ^| findstr LISTENING') do taskkill /F /PID %%a 2>nul

echo Stopping frontend (port 4200)...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr :4200 ^| findstr LISTENING') do taskkill /F /PID %%a 2>nul

echo Done.
pause
