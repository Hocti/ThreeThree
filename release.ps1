<#
.SYNOPSIS
    Version bump + Release build + (optional) GitHub release for 三三正體中文輸入法 (TT).

.DESCRIPTION
    1. 讀 Properties\AssemblyInfo.cs 的版本，細版本 +1 (1.1.1 -> 1.1.2)
    2. 用 build.ps1 build Release|x64，打包成 dist\tt_v<version>.zip
    3. (可選) 用 gh CLI commit + tag + 開 GitHub release，再上載個 zip

    兩個開關:
      -BumpVersion  預設 $true   加唔加版本號
      -Publish      預設 $false  上唔上 GitHub release

.EXAMPLE
    .\release.ps1                                # 版本 +1，build，唔上載
    .\release.ps1 -Publish $true                 # 版本 +1，build，上 GitHub release
    .\release.ps1 -BumpVersion $false -Publish $true   # 用返而家個版本上載
    .\release.ps1 -Part minor                    # 1.1.2 -> 1.2.0
    .\release.ps1 -DryRun                        # 只係講會做乜，唔改嘢
#>
[CmdletBinding()]
param(
    # 加唔加版本號 (default on)
    [bool]$BumpVersion = $true,

    # 上唔上 GitHub release (default off)
    [bool]$Publish = $false,

    # 加邊個位
    [ValidateSet('patch', 'minor', 'major')]
    [string]$Part = 'patch',

    # release notes；唔寫就試 CHANGELOG.md，再唔係就 gh --generate-notes
    [string]$Notes = '',

    # 有 TT.exe 行緊就自動 close 佢 (唔加就會停低叫你自己 close)
    [switch]$CloseRunning,

    # 有未 commit 的改動都照 publish
    [switch]$AllowDirty,

    # 開草稿 release (唔會即刻公開)
    [switch]$Draft,

    # 只講唔做
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $root

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host "!!  $msg" -ForegroundColor Yellow }
function Fail($msg) { Write-Host "XX  $msg" -ForegroundColor Red; exit 1 }

$asmInfo = Join-Path $root 'Properties\AssemblyInfo.cs'
if (-not (Test-Path $asmInfo)) { Fail "找唔到 $asmInfo" }

# ------------------------------------------------------------------- version
$asmText = Get-Content $asmInfo -Raw -Encoding UTF8
$m = [regex]::Match($asmText, '(?m)^\[assembly:\s*AssemblyVersion\("(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?"\)\]')
if (-not $m.Success) { Fail 'AssemblyInfo.cs 入面讀唔到 AssemblyVersion。' }

$major = [int]$m.Groups[1].Value
$minor = [int]$m.Groups[2].Value
$patch = [int]$m.Groups[3].Value
$oldVersion = "$major.$minor.$patch"

if ($BumpVersion) {
    switch ($Part) {
        'major' { $major++; $minor = 0; $patch = 0 }
        'minor' { $minor++; $patch = 0 }
        'patch' { $patch++ }
    }
}
$version = "$major.$minor.$patch"
$tag = "v$version"

if ($BumpVersion) { Write-Step "版本 $oldVersion -> $version" }
else { Write-Step "版本 $version (唔加)" }

if ($BumpVersion -and -not $DryRun) {
    $newText = [regex]::Replace($asmText,
        '(?m)^\[assembly:\s*Assembly(File)?Version\("[^"]*"\)\]',
        { param($x) '[assembly: Assembly' + $x.Groups[1].Value + "Version(`"$version.0`")]" })
    # BOM + CRLF，同原本個檔一樣
    [IO.File]::WriteAllText($asmInfo, $newText, (New-Object Text.UTF8Encoding $true))
    Write-Step "寫咗 $asmInfo"
}

# --------------------------------------------------------------------- build
$zipName = "tt_$tag.zip"
$zip = Join-Path $root "dist\$zipName"

if ($DryRun) {
    Write-Step "[DryRun] .\build.ps1 -Release -Rebuild -Package -PackageName $zipName"
} else {
    Write-Step 'Build Release|x64'
    & (Join-Path $root 'build.ps1') -Release -Rebuild -Package -PackageName $zipName -CloseRunning:$CloseRunning
    if ($LASTEXITCODE -ne 0) { Fail 'build.ps1 失敗。' }
    if (-not (Test-Path $zip)) { Fail "搵唔到 $zip" }
    Write-Step "Package: $zip"
}

if (-not $Publish) {
    Write-Host ''
    Write-Host "==> 冇 -Publish `$true，冇上 GitHub。" -ForegroundColor Green
    if (-not $DryRun) { Write-Host "    $zip" }
    exit 0
}

# ------------------------------------------------------------------- publish
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Fail '搵唔到 gh CLI。https://cli.github.com/ 裝完再 gh auth login。'
}
# 注意: PS 5.1 唔好用 2>&1 接 native command 的 stderr，會變 NativeCommandError
& gh auth status | Out-Null
if ($LASTEXITCODE -ne 0) { Fail 'gh 未登入，行 gh auth login。' }

# 未 commit 的改動 (版本號嗰個檔唔算)
$dirty = @(git status --porcelain | Where-Object { $_ -and ($_ -notmatch 'Properties/AssemblyInfo\.cs$') })
if ($dirty.Count -gt 0 -and -not $AllowDirty) {
    Write-Warn '有未 commit 的改動:'
    $dirty | ForEach-Object { Write-Host "    $_" }
    Fail '先 commit 咗佢，或者加 -AllowDirty。'
}

# 同名 tag / release 已經有就唔好覆蓋
& git ls-remote --exit-code --tags origin "refs/tags/$tag" | Out-Null
if ($LASTEXITCODE -eq 0) { Fail "$tag 喺 origin 已經存在，改個版本號再嚟。" }

# release notes
if (-not $Notes) {
    $changelog = Join-Path $root 'CHANGELOG.md'
    if (Test-Path $changelog) {
        $cl = Get-Content $changelog -Raw -Encoding UTF8
        # 揀 "## [1.0.1]" 嗰段，冇就揀 "## [Unreleased]"
        foreach ($head in @([regex]::Escape("[$version]"), [regex]::Escape("[$tag]"), '\[Unreleased\]')) {
            $sec = [regex]::Match($cl, "(?ms)^##\s+$head.*?\r?\n(.*?)(?=^##\s|\z)")
            if ($sec.Success -and $sec.Groups[1].Value.Trim()) {
                $Notes = $sec.Groups[1].Value.Trim()
                break
            }
        }
    }
}

$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($Notes) { Write-Step "Release notes: $(($Notes -split "`n").Count) 行" }
else { Write-Step 'Release notes: gh --generate-notes' }

if ($DryRun) {
    Write-Step "[DryRun] git commit -m `"release $tag`" + git push origin $branch"
    Write-Step "[DryRun] gh release create $tag `"$zip`""
    exit 0
}

# commit 版本號 (有改先)
if ($BumpVersion) {
    $verDirty = @(git status --porcelain -- 'Properties/AssemblyInfo.cs')
    if ($verDirty.Count -gt 0) {
        Write-Step "Commit $tag"
        & git add -- 'Properties/AssemblyInfo.cs'
        & git commit -m "release $tag"
        if ($LASTEXITCODE -ne 0) { Fail 'git commit 失敗。' }
    }
}

Write-Step "Push origin $branch"
& git push origin $branch
if ($LASTEXITCODE -ne 0) { Fail 'git push 失敗。' }

$ghArgs = @('release', 'create', $tag, $zip, '--title', $tag, '--target', $branch)
if ($Draft) { $ghArgs += '--draft' }
if ($Notes) {
    $notesFile = Join-Path $env:TEMP "tt_notes_$tag.md"
    [IO.File]::WriteAllText($notesFile, $Notes, (New-Object Text.UTF8Encoding $false))
    $ghArgs += @('--notes-file', $notesFile)
} else {
    $ghArgs += '--generate-notes'
}

Write-Step "gh $($ghArgs -join ' ')"
& gh @ghArgs
if ($LASTEXITCODE -ne 0) { Fail 'gh release create 失敗。' }
if ($notesFile -and (Test-Path $notesFile)) { Remove-Item $notesFile -Force }

Write-Host "==> done: $tag" -ForegroundColor Green
