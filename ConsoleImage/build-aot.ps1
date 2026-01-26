# Build Script for ConsoleImage (Windows)
# Creates native AOT-compiled binary (default) or single-file with Whisper support

param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$RID = "win-x64",

    [switch]$Whisper  # Build with Whisper transcription support (single-file, non-AOT)
)

$ErrorActionPreference = "Stop"

if ($Whisper) {
    Write-Host "=== ConsoleImage Build (Single-File + Whisper) ===" -ForegroundColor Cyan
} else {
    Write-Host "=== ConsoleImage AOT Build ===" -ForegroundColor Cyan
}
Write-Host ""

# Setup Visual Studio environment (needed for AOT only)
if (-not $Whisper) {
    $vsInstallerPath = "C:\Program Files (x86)\Microsoft Visual Studio\Installer"
    if (Test-Path $vsInstallerPath)
    {
        $env:PATH = "$vsInstallerPath;$env:PATH"
    }

    # Find VS installation
    $vsPath = $null
    try
    {
        $vsPath = & vswhere.exe -latest -property installationPath 2> $null
    }
    catch
    {
        # vswhere not in PATH
    }

    if (-not $vsPath)
    {
        # Try common VS paths
        $vsPaths = @(
            "C:\Program Files\Microsoft Visual Studio\2022\Enterprise",
            "C:\Program Files\Microsoft Visual Studio\2022\Professional",
            "C:\Program Files\Microsoft Visual Studio\2022\Community",
            "C:\Program Files\Microsoft Visual Studio\2022\BuildTools"
        )
        foreach ($path in $vsPaths)
        {
            if (Test-Path $path)
            {
                $vsPath = $path
                break
            }
        }
    }

    if ($vsPath)
    {
        $devShellPath = Join-Path $vsPath "Common7\Tools\Launch-VsDevShell.ps1"
        if (Test-Path $devShellPath)
        {
            Write-Host "Loading VS Developer Shell from: $vsPath" -ForegroundColor Gray
            & $devShellPath -SkipAutomaticLocation
        }
        else
        {
            Write-Warning "VS Developer Shell not found - AOT compilation may fail"
        }
    }
    else
    {
        Write-Warning "Visual Studio not found - AOT compilation requires VS Build Tools"
    }
}

# Get paths
$scriptDir = $PSScriptRoot
$projectPath = Join-Path $scriptDir "ConsoleImage.csproj"
$outputDir = Join-Path $scriptDir "bin\Release\net10.0\$RID\publish"

Write-Host "Platform: $RID" -ForegroundColor Gray
Write-Host "Project: $projectPath" -ForegroundColor Gray
Write-Host "Output: $outputDir" -ForegroundColor Gray
if ($Whisper) {
    Write-Host "Mode: Single-file with Whisper" -ForegroundColor Yellow
} else {
    Write-Host "Mode: Native AOT (no Whisper runtime)" -ForegroundColor Yellow
}
Write-Host ""

if ($Whisper) {
    # Single-file build with Whisper native runtime bundled
    # Non-AOT because Whisper.net's native library loading requires JIT
    dotnet publish $projectPath `
        -c Release `
        -r $RID `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:BundleWhisperRuntime=true `
        -p:DebugType=none
} else {
    # Native AOT build (no Whisper)
    dotnet publish $projectPath `
        -c Release `
        -r $RID `
        --self-contained true `
        -p:PublishAot=true `
        -p:OptimizationPreference=Size `
        -p:IlcOptimizationPreference=Size `
        -p:StripSymbols=true `
        -p:IlcGenerateStackTraceData=false
}

Write-Host ""
Write-Host "=== Build complete! ===" -ForegroundColor Green

$exePath = Join-Path $outputDir "consoleimage.exe"
if (Test-Path $exePath)
{
    $size = (Get-Item $exePath).Length / 1MB
    Write-Host "Binary: $exePath ($([math]::Round($size, 1) ) MB)" -ForegroundColor Cyan
}
else
{
    Write-Host "Binary: $exePath" -ForegroundColor Cyan
}

Write-Host ""
if ($Whisper) {
    Write-Host "Run with: $exePath video.mp4 --subs whisper"
} else {
    Write-Host "Run with: $exePath image.jpg"
}
