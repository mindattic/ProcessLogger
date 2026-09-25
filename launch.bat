@echo off
setlocal

set "REPO_DIR=%~dp0"
set "DEPLOY_ROOT=C:\Apps\ProcessLogger"
set "CLI_DIR=%DEPLOY_ROOT%\Cli"
set "FRONTEND_DIR=%DEPLOY_ROOT%\Frontend"

echo Publishing ProcessLogger.Cli to %CLI_DIR% ...
dotnet publish "%REPO_DIR%ProcessLogger.Cli\ProcessLogger.Cli.csproj" -c Release -o "%CLI_DIR%"
if errorlevel 1 goto :error

echo Publishing ProcessLogger.Frontend to %FRONTEND_DIR% ...
dotnet publish "%REPO_DIR%ProcessLogger.Frontend\ProcessLogger.Frontend.csproj" -c Release -o "%FRONTEND_DIR%"
if errorlevel 1 goto :error

echo Launching ProcessLogger.Cli (background logger) ...
start "ProcessLogger Cli" "%CLI_DIR%\ProcessLogger.Cli.exe"

echo Launching ProcessLogger.Frontend (viewer) ...
start "ProcessLogger Frontend" "%FRONTEND_DIR%\ProcessLogger.Frontend.exe"

echo Done.
goto :eof

:error
echo.
echo Deployment failed - see errors above.
exit /b 1
