<#
.SYNOPSIS
    Command-line build for the 三三正體中文輸入法 (TT) portable exe.

.DESCRIPTION
    Locates MSBuild (VS2022/2019 via vswhere, then well-known paths), restores the
    packages.config NuGet packages when needed, and builds TT.csproj.

    x64 only - 32-bit Windows is no longer supported.

.EXAMPLE
    .\build.ps1                          # Debug   -> bin\x64\Debug
    .\build.ps1 -Configuration Release   # Release -> bin\x64\Release
    .\build.ps1 -Release                 # same as above (shorthand)
    .\build.ps1 -Configuration Release -Clean -Run
    .\build.ps1 -Configuration Release -Package   # also zips the portable folder
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    # shorthand: -Release is the same as -Configuration Release
    [switch]$Release,

    [switch]$Clean,
    [switch]$Rebuild,
    [switch]$Run,
    [switch]$Package,

    # -Package zip file name (no path). Default: tt_<configuration>_<yyyyMMdd>.zip
    [string]$PackageName = '',

    # 有 TT.exe 行緊就自動 close 佢 (唔加就只會提示然後停低)
    [switch]$CloseRunning,

    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$Verbosity = 'minimal'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $root

if ($Release) { $Configuration = 'Release' }

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host "!!  $msg" -ForegroundColor Yellow }
function Fail($msg) { Write-Host "XX  $msg" -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------- find MSBuild
function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -prerelease -products * `
            -requires Microsoft.Component.MSBuild `
            -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null
        if ($found) { return ($found | Select-Object -Last 1) }
    }

    $candidates = @()
    foreach ($pf in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if (-not $pf) { continue }
        foreach ($year in @('2022', '2019', '2017')) {
            foreach ($ed in @('Enterprise', 'Professional', 'Community', 'BuildTools')) {
                foreach ($v in @('Current', '15.0')) {
                    $candidates += Join-Path $pf "Microsoft Visual Studio\$year\$ed\MSBuild\$v\Bin\MSBuild.exe"
                }
            }
        }
    }
    # last resort: the .NET Framework msbuild (works for classic csproj)
    $candidates += Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'

    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    return $null
}

$msbuild = Find-MSBuild
if (-not $msbuild) {
    Fail "MSBuild not found. Install 'Visual Studio 2022' or the '.NET desktop build tools' workload."
}
Write-Step "MSBuild: $msbuild"

# --------------------------------------------------------------- nuget restore
# TT uses the old packages.config format, so `msbuild -t:restore` is not enough:
# it needs nuget.exe. The packages\ folder is git-ignored, so a fresh clone must restore.
$needRestore = -not (Test-Path (Join-Path $root 'packages\Stub.System.Data.SQLite.Core.NetFramework.1.0.118.0'))
if ($needRestore) {
    Write-Step 'Restoring NuGet packages (packages.config)'
    $nuget = (Get-Command nuget.exe -ErrorAction SilentlyContinue)
    if ($nuget) { $nugetPath = $nuget.Source }
    else {
        $nugetPath = Join-Path $root '.tools\nuget.exe'
        if (-not (Test-Path $nugetPath)) {
            New-Item -ItemType Directory -Force (Split-Path $nugetPath) | Out-Null
            Write-Step 'Downloading nuget.exe'
            try {
                [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
                Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile $nugetPath
            } catch {
                Fail "Could not download nuget.exe: $($_.Exception.Message)`n    Restore packages manually, or open TT.sln in Visual Studio once."
            }
        }
    }
    & $nugetPath restore (Join-Path $root 'TT.sln') -NonInteractive
    if ($LASTEXITCODE -ne 0) { Fail 'NuGet restore failed.' }
} else {
    Write-Step 'NuGet packages already restored'
}

# ------------------------------------------------------- running instance?
# 程式係 single-instance 而且會 lock 住 output folder 的檔案，行緊就 build 唔到。
$outDir = Join-Path $root "bin\x64\$Configuration"
$running = @(Get-Process TT -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($outDir, [StringComparison]::OrdinalIgnoreCase) })
if ($running.Count -gt 0) {
    $pids = ($running | ForEach-Object { $_.Id }) -join ', '
    if ($CloseRunning) {
        Write-Step "Closing running TT.exe (pid $pids)"
        $running | ForEach-Object { $_.CloseMainWindow() | Out-Null }
        Start-Sleep -Milliseconds 500
        $running | Where-Object { -not $_.HasExited } | Stop-Process -Force
        Start-Sleep -Milliseconds 500
    } else {
        Fail ("TT.exe 行緊 (pid $pids)，會 lock 住 $outDir 入面的檔案。`n" +
              '    Close 咗佢再 build，或者加 -CloseRunning。')
    }
}

# ---------------------------------------------------------------------- build
$target = 'Build'
if ($Rebuild) { $target = 'Rebuild' }
if ($Clean) { $target = 'Clean;Build' }

# NOTE: building the .csproj directly, so the platform must be spelled the way the
# PropertyGroup conditions in TT.csproj spell it ('x64'); the solution-level
# 'Any CPU' spelling would make MSBuild miss them.
$msbuildPlatform = 'x64'

Write-Step "$target $Configuration|$msbuildPlatform"
& $msbuild 'TT.csproj' `
    "/t:$target" `
    "/p:Configuration=$Configuration" `
    "/p:Platform=$msbuildPlatform" `
    "/v:$Verbosity" `
    /nologo `
    /m
if ($LASTEXITCODE -ne 0) { Fail 'Build failed.' }

# --------------------------------------------------------------- output folder
$exe = Join-Path $outDir 'TT.exe'

if (-not (Test-Path $exe)) { Fail "Build reported success but $exe is missing." }
Write-Step "Output: $outDir"
Get-ChildItem $outDir -Filter *.exe | ForEach-Object {
    Write-Host ("    {0}  {1:N0} bytes  {2}" -f $_.Name, $_.Length, $_.LastWriteTime)
}

# files\dataset.db + files\default90.png are copied by the csproj as Content, but
# double check because a portable build is useless without them.
foreach ($need in @('files\dataset.db', 'files\default90.png', 'i.ico')) {
    if (-not (Test-Path (Join-Path $outDir $need))) { Write-Warn "missing in output: $need" }
}

# -------------------------------------------------------------------- package
if ($Package) {
    $stamp = Get-Date -Format 'yyyyMMdd'
    if ($PackageName) {
        if ($PackageName -notlike '*.zip') { $PackageName = "$PackageName.zip" }
        $zip = Join-Path $root "dist\$PackageName"
    } else {
        $zip = Join-Path $root "dist\tt_$($Configuration.ToLower())_$stamp.zip"
    }
    New-Item -ItemType Directory -Force (Split-Path $zip) | Out-Null
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Write-Step "Packaging -> $zip"
    # tt_settings.ini / user_freq.db are per-user runtime state, keep them out
    $staging = Join-Path $env:TEMP "tt_pkg_$stamp"
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
    Copy-Item $outDir $staging -Recurse
    # The last four are dead SQLite baggage the csproj no longer copies; listed here
    # only to clean up stale copies still sitting in an old bin\ folder.
    foreach ($junk in @('tt_settings.ini', 'files\user_freq.db', 'TT.pdb',
                        'System.Data.SQLite.xml', 'sqlite3.dll',
                        'System.Data.SQLite.EF6.dll', 'System.Data.SQLite.Linq.dll')) {
        $p = Join-Path $staging $junk
        if (Test-Path $p) { Remove-Item $p -Force }
    }
    Get-ChildItem $staging -Filter *.zip -File | Remove-Item -Force
    # x64-only build: safety net in case an old x86\ interop is still in bin\
    $x86 = Join-Path $staging 'x86'
    if (Test-Path $x86) { Remove-Item $x86 -Recurse -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($staging, $zip)
    Remove-Item $staging -Recurse -Force
    Write-Host "    $zip"
}

# ------------------------------------------------------------------------ run
if ($Run) {
    Write-Step 'Launching (single-instance mutex "TT" - close any running copy first)'
    Start-Process -FilePath $exe -WorkingDirectory $outDir
}

Write-Host '==> done' -ForegroundColor Green
