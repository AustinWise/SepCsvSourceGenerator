@echo off
setlocal

cd %~dp0
set GENERATE_BASELINES=1
dotnet build
if %ERRORLEVEL% neq 0 (
    echo Build failed. Exiting.
    exit /b %ERRORLEVEL%
)
dotnet test
rem whether or not the tests pass, we indicate that we successfully updated the baselines
exit /b 0
