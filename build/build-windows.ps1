<#
.SYNOPSIS
	Build, publish, and optionally package Local Whisper Transcriber for Windows x64.

.DESCRIPTION
	1. Publishes the MAUI project as a self-contained, unpackaged Windows app.
	2. Copies native whisper-cli.exe, ffmpeg.exe, and model files into the publish folder.
	3. Optionally builds the WiX MSI installer (requires WiX v5 toolset).

.PARAMETER Configuration
	Build configuration. Default: Release

.PARAMETER SkipInstaller
	Skip the WiX MSI build step.

.PARAMETER WixPath
	Path to the wix.exe tool. Auto-detected if on PATH.

.EXAMPLE
	.\build\build-windows.ps1
	.\build\build-windows.ps1 -SkipInstaller
	.\build\build-windows.ps1 -Configuration Debug -SkipInstaller
#>
[CmdletBinding()]
param(
	[string] $Configuration   = "Release",
	[switch] $SkipInstaller,
	[string] $WixPath         = "wix"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Paths ─────────────────────────────────────────────────────────────────────
$RepoRoot       = Resolve-Path "$PSScriptRoot\.."
$ProjectFile    = Join-Path $RepoRoot "src\LocalWhisperTranscriber\LocalWhisperTranscriber.csproj"
$NativeWindows  = Join-Path $RepoRoot "src\LocalWhisperTranscriber\Native\windows"
$PublishDir     = Join-Path $RepoRoot "artifacts\windows\app"
$InstallerDir   = Join-Path $RepoRoot "installer\windows"
$MsiOutputDir   = Join-Path $RepoRoot "artifacts\windows"

$TargetFramework = "net10.0-windows10.0.19041.0"

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Local Whisper Transcriber — Windows Build Script" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Configuration : $Configuration"
Write-Host "  Target TFM    : $TargetFramework"
Write-Host "  Publish dir   : $PublishDir"
Write-Host ""

# ── Step 1: Clean previous publish output ─────────────────────────────────────
Write-Host "[1/4] Cleaning previous publish output…" -ForegroundColor Yellow
if (Test-Path $PublishDir) {
	Remove-Item -Recurse -Force $PublishDir
	Write-Host "      Removed: $PublishDir"
}
New-Item -ItemType Directory -Force $PublishDir | Out-Null

# ── Step 2: Publish MAUI app ──────────────────────────────────────────────────
Write-Host ""
Write-Host "[2/4] Publishing MAUI app (self-contained, unpackaged)…" -ForegroundColor Yellow

$publishArgs = @(
	"publish"
	$ProjectFile
	"-f", $TargetFramework
	"-c", $Configuration
	"-p:WindowsPackageType=None"
	"-p:WindowsAppSDKSelfContained=true"
	"--output", $PublishDir
	"--nologo"
)

Write-Host "      dotnet $($publishArgs -join ' ')"
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
	Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
}
Write-Host "      ✅ Publish succeeded." -ForegroundColor Green

# ── Step 3: Copy native binaries & models ─────────────────────────────────────
Write-Host ""
Write-Host "[3/4] Copying native binaries…" -ForegroundColor Yellow

function Copy-IfExists {
	param([string]$Source, [string]$Destination)
	if (Test-Path $Source) {
		$destDir = Split-Path $Destination -Parent
		if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Force $destDir | Out-Null }
		Copy-Item -Force $Source $Destination
		Write-Host "      Copied: $(Split-Path $Source -Leaf)"
	} else {
		Write-Warning "      NOT FOUND (place manually): $Source"
	}
}

Copy-IfExists (Join-Path $NativeWindows "whisper-cli.exe") (Join-Path $PublishDir "whisper-cli.exe")
Copy-IfExists (Join-Path $NativeWindows "ffmpeg.exe")      (Join-Path $PublishDir "ffmpeg.exe")

$NativeModelsDir  = Join-Path $NativeWindows "models"
$PublishModelsDir = Join-Path $PublishDir "models"

if (Test-Path $NativeModelsDir) {
	$modelFiles = Get-ChildItem -Path $NativeModelsDir -Filter "*.bin" -ErrorAction SilentlyContinue
	if ($modelFiles.Count -gt 0) {
		New-Item -ItemType Directory -Force $PublishModelsDir | Out-Null
		foreach ($f in $modelFiles) {
			Copy-Item -Force $f.FullName (Join-Path $PublishModelsDir $f.Name)
			Write-Host "      Copied model: $($f.Name)"
		}
	} else {
		Write-Warning "      No model .bin files found in $NativeModelsDir — add them before running the app."
	}
} else {
	Write-Warning "      Native models folder not found: $NativeModelsDir"
}

# ── Step 4: Build WiX MSI ─────────────────────────────────────────────────────
if ($SkipInstaller) {
	Write-Host ""
	Write-Host "[4/4] Skipping WiX MSI build (-SkipInstaller)." -ForegroundColor DarkGray
} else {
	Write-Host ""
	Write-Host "[4/4] Building WiX MSI installer…" -ForegroundColor Yellow

	# Verify wix is available
	$wixCmd = Get-Command $WixPath -ErrorAction SilentlyContinue
	if (-not $wixCmd) {
		Write-Warning @"
	  wix.exe not found on PATH. Install WiX v5 with:
		dotnet tool install --global wix
		wix extension add WixToolset.UI.wixext
	  Then re-run this script without -SkipInstaller.
"@
	} else {
		$wixBuildArgs = @(
			"build"
			(Join-Path $InstallerDir "LocalWhisperTranscriber.Installer.wixproj")
			"-p", "PublishDir=$PublishDir\"
			"-o", (Join-Path $MsiOutputDir "LocalWhisperTranscriber-1.0.0-x64.msi")
		)

		Write-Host "      wix $($wixBuildArgs -join ' ')"
		& $WixPath @wixBuildArgs
		if ($LASTEXITCODE -ne 0) {
			Write-Error "WiX build failed with exit code $LASTEXITCODE"
		}
		Write-Host "      ✅ MSI created at: $MsiOutputDir" -ForegroundColor Green
	}
}

# ── Summary ───────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Build complete!" -ForegroundColor Green
Write-Host "  App publish folder : $PublishDir"
if (-not $SkipInstaller) {
	Write-Host "  MSI output folder  : $MsiOutputDir"
}
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
