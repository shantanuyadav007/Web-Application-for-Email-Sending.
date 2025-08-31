@echo off
echo Starting Email API Server...

start "" /B cmd /c "dotnet run --urls http://localhost:5005"

:: Wait for 5 seconds to let the server start
timeout /t 5 > nul

:: Open browser after delay
start http://localhost:5005/swagger/index.html

pause
