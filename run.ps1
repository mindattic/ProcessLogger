#Requires -Version 5.1
<#
.SYNOPSIS
    Builds and deploys ProcessLogger to C:\Apps\ProcessLogger\ (Cli\ and Frontend\),
    and optionally starts both. The one script for publishing and launching ProcessLogger.

.DESCRIPTION
    Publishes two components in Release:
      Cli\ProcessLogger.Cli.exe            the background logger (requireAdministrator)
      Frontend\ProcessLogger.Frontend.exe  the WPF viewer
    and writes C:\Apps\ProcessLogger\run.bat, which the desktop shortcut points at.
    This script never prompts (agents and /deploy run it non-interactively); the choice
    lives in run.bat, which (started with no arguments) opens a menu first:
      1) Deploy & Run - runs this script with -Force (always republishes), then starts the
                        logger (UAC prompt) and the viewer.
      2) Run          - starts the already-deployed logger and viewer without calling this
                        script at all (falls through to Deploy & Run if either is missing).
    Started with arguments (another tool calling it) it never prompts: it runs this script
    without -Force (republish only if changed), then starts both. If the source repo is
    missing, or the redeploy fails, it launches what is already deployed.

    Every run (published or not) rewrites run.bat when its text has changed, so an
    existing install picks up launcher changes without a republish.

    Without -Force a component is republished only when its exe is missing, or a
    source file under its project or ProcessLogger.Core (or
    Directory.Build.props) is newer than its deployed exe.

    Before publishing a component, running copies are closed. The logger runs
    elevated, so from a non-elevated shell it usually can't be stopped; in that case
    its republish is skipped (with a message) rather than failing.

.PARAMETER Force
    Clean bin/obj and republish both components even if nothing looks changed.

.PARAMETER LauncherOnly
    Only (re)write the generated run.bat and exit: no build, nothing stopped.

.PARAMETER Launch
    After deploying, start the logger (elevated) and the viewer.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File run.ps1 -Force
    powershell -ExecutionPolicy Bypass -File run.ps1 -LauncherOnly
#>
param(
    [switch]$Force,
    [switch]$LauncherOnly,
    [switch]$Launch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo       = $PSScriptRoot
$deployDir  = 'C:\Apps\ProcessLogger'
$runBat     = Join-Path $deployDir 'run.bat'
$props      = Join-Path $repo 'Directory.Build.props'
$coreDir    = Join-Path $repo 'ProcessLogger.Core'
$sourceExts = @('.cs', '.csproj', '.xaml', '.json', '.props', '.targets', '.resx',
                '.manifest', '.html', '.js', '.css')

$components = @(
    [pscustomobject]@{
        Name     = 'ProcessLogger.Cli'
        ProjDir  = Join-Path $repo 'ProcessLogger.Cli'
        OutDir   = Join-Path $deployDir 'Cli'
        Graceful = $false   # console app, usually elevated
    },
    [pscustomobject]@{
        Name     = 'ProcessLogger.Frontend'
        ProjDir  = Join-Path $repo 'ProcessLogger.Frontend'
        OutDir   = Join-Path $deployDir 'Frontend'
        Graceful = $true    # WPF window: CloseMainWindow
    }
)

function Get-Exe($c) { Join-Path $c.OutDir ($c.Name + '.exe') }

function Test-NeedsPublish($c) {
    $exe = Get-Exe $c
    if (-not (Test-Path $exe)) { return $true }
    $exeTime = (Get-Item $exe).LastWriteTimeUtc
    if ((Test-Path $props) -and (Get-Item $props).LastWriteTimeUtc -gt $exeTime) { return $true }

    foreach ($dir in @($c.ProjDir, $coreDir)) {
        $newer = Get-ChildItem -Path $dir -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object {
                $_.FullName -notmatch '\\(bin|obj)\\' -and
                ($sourceExts -contains $_.Extension.ToLowerInvariant()) -and
                $_.LastWriteTimeUtc -gt $exeTime
            } |
            Select-Object -First 1
        if ($newer) { return $true }
    }
    return $false
}

# Returns $true when no instance is left running.
function Stop-Component($c) {
    $procs = @(Get-Process -Name $c.Name -ErrorAction SilentlyContinue)
    if ($procs.Count -eq 0) { return $true }

    Write-Host "Closing running $($c.Name) ($($procs.Count))..." -ForegroundColor Yellow
    foreach ($p in $procs) {
        try { if ($p.MainWindowHandle -ne [IntPtr]::Zero) { [void]$p.CloseMainWindow() } } catch { }
    }
    foreach ($p in $procs) {
        try { [void]$p.WaitForExit(5000) } catch { }
    }
    foreach ($p in @(Get-Process -Name $c.Name -ErrorAction SilentlyContinue)) {
        try { Stop-Process -Id $p.Id -Force -ErrorAction Stop; [void]$p.WaitForExit(3000) } catch { }
    }
    return (@(Get-Process -Name $c.Name -ErrorAction SilentlyContinue).Count -eq 0)
}

function Publish($c) {
    if ($Force) {
        foreach ($d in @((Join-Path $c.ProjDir 'bin'), (Join-Path $c.ProjDir 'obj'),
                         (Join-Path $coreDir 'bin'), (Join-Path $coreDir 'obj'))) {
            if (Test-Path $d) {
                try { Remove-Item -Recurse -Force $d -ErrorAction Stop }
                catch { Write-Host "  (could not fully remove $d : $($_.Exception.Message))" -ForegroundColor DarkYellow }
            }
        }
    }

    $proj = Join-Path $c.ProjDir ($c.Name + '.csproj')
    Write-Host "Publishing $($c.Name) -> $($c.OutDir)" -ForegroundColor Cyan
    dotnet publish $proj --configuration Release --output $c.OutDir | Out-Host
    if ($LASTEXITCODE -ne 0) { return $false }
    return (Test-Path (Get-Exe $c))
}

# Writes the double-click launcher (only when its text changed, so a launcher that is running
# this script is not rewritten under itself) and removes the stale deploy.bat. The menu lives
# there, not in this script, so the script never blocks.
function Write-Launcher {
    $cliExe = 'Cli\ProcessLogger.Cli.exe'
    $feExe  = 'Frontend\ProcessLogger.Frontend.exe'
    $content = @"
@echo off
title ProcessLogger
rem Generated by $repo\run.ps1 - edits here are overwritten.
set "RUN_PS1=$repo\run.ps1"
rem Started with arguments (another tool calling it): no menu, never prompt.
set "ARG1=%~1"
if defined ARG1 goto auto
echo.
echo   ProcessLogger
echo     1^) Deploy ^& Run
echo     2^) Run
echo.
choice /c 12 /n /m "  Choose 1 or 2: "
if errorlevel 2 goto run
:deploy
if exist "%RUN_PS1%" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%RUN_PS1%" -Force
    if errorlevel 1 echo Redeploy failed - launching whatever is already in this folder instead.
) else (
    echo Source repo not found at "%RUN_PS1%" - launching existing build without redeploying.
)
goto launch
:auto
if exist "%RUN_PS1%" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%RUN_PS1%"
    if errorlevel 1 echo Redeploy failed - launching whatever is already in this folder instead.
) else (
    echo Source repo not found at "%RUN_PS1%" - launching existing build without redeploying.
)
goto launch
:run
if exist "%~dp0$cliExe" if exist "%~dp0$feExe" goto launch
echo Nothing is deployed yet - deploying first.
goto deploy
:launch
if not exist "%~dp0$feExe" (
    echo Nothing deployed at "%~dp0$feExe".
    if not defined ARG1 pause
    exit /b 1
)
echo Launching ProcessLogger.Cli (background logger, UAC prompt) ...
start "ProcessLogger Cli" "%~dp0$cliExe"
echo Launching ProcessLogger.Frontend (viewer) ...
start "ProcessLogger Frontend" "%~dp0$feExe"
"@
    # CRLF throughout: cmd mis-finds labels (goto) in LF-only batch files.
    $content = ($content -replace "`r?`n", "`r`n") + "`r`n"
    New-Item -ItemType Directory -Force -Path $deployDir | Out-Null
    $current = if (Test-Path $runBat) { [IO.File]::ReadAllText($runBat) } else { $null }
    if ($current -ne $content) { [IO.File]::WriteAllText($runBat, $content, [Text.Encoding]::ASCII) }

    # The stale launcher this one replaced (deploy.bat, renamed to run.bat).
    $oldBat = Join-Path $deployDir 'deploy.bat'
    if (Test-Path $oldBat) { Remove-Item -LiteralPath $oldBat -Force }
}

if ($LauncherOnly) {
    Write-Launcher
    Write-Host "Wrote $runBat" -ForegroundColor Gray
    exit 0
}

# ── Deploy ──────────────────────────────────────────────────────────────────
New-Item -ItemType Directory -Force -Path $deployDir | Out-Null

$failed = $false
$didSomething = $false
foreach ($c in $components) {
    if (-not ($Force -or (Test-NeedsPublish $c))) { continue }
    $didSomething = $true

    if (-not (Stop-Component $c)) {
        Write-Host "$($c.Name) is running and could not be stopped (it runs elevated)." -ForegroundColor Yellow
        Write-Host "  Skipping its republish - close it (or run this from an elevated shell) and deploy again." -ForegroundColor Yellow
        continue
    }

    if (Publish $c) {
        Write-Host "Deployed $(Get-Exe $c)" -ForegroundColor Green
    } else {
        Write-Host "Publishing $($c.Name) FAILED." -ForegroundColor Red
        $failed = $true
    }
}

# run.bat is cheap to regenerate; keep it current on every run.
Write-Launcher

if (-not $didSomething) { Write-Host 'ProcessLogger is up to date.' -ForegroundColor DarkGray }

if ($Launch) {
    foreach ($c in $components) {
        $exe = Get-Exe $c
        if (Test-Path $exe) { Start-Process -FilePath $exe -WorkingDirectory $c.OutDir }
        else { Write-Host "Nothing deployed at $exe." -ForegroundColor Red; $failed = $true }
    }
}

if ($failed) { Write-Host 'Deploy FAILED.' -ForegroundColor Red; exit 1 }
exit 0
