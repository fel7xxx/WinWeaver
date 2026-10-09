@echo off
rem WinWeaver - build script. Copyright (C) 2026 fel7xxx. GPL-3.0-or-later.
rem Builds WinWeaver.exe. Needs .NET SDK 8+ (https://dotnet.microsoft.com/download); targets .NET Framework 4.8.
cd /d "%~dp0"
dotnet build -c Release -o out || (pause & exit /b 1)
echo.
echo Done: %~dp0out\WinWeaver.exe
pause
