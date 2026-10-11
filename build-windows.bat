@echo off
rem Нужен .NET 8 SDK: https://dotnet.microsoft.com/download
dotnet publish WinWeaver\WinWeaver.csproj -f net48 -c Release -o publish
echo.
echo Готово: publish\WinWeaver.exe
pause
