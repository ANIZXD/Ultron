@echo off
setlocal
cd /d "%~dp0"

if not exist "Ultron.exe" (
    echo Building Ultron...
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
    if errorlevel 1 (
        echo Build failed.
        pause
        exit /b 1
    )
)

start "" "%~dp0Ultron.exe"
