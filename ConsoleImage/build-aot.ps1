# AOT Build Script for ConsoleImage
# Handles VS toolchain setup for native compilation

$ErrorActionPreference = "Stop"

# Setup Visual Studio environment
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;" + $env:PATH

# Find VS installation
$vsPath = & vswhere.exe -latest -property installationPath 2>$null
if (-not $vsPath) {
    $vsPath = "C:\Program Files\Microsoft Visual Studio\18\Community"
}

$devShellPath = Join-Path $vsPath "Common7\Tools\Launch-VsDevShell.ps1"
if (Test-Path $devShellPath) {
    & $devShellPath
} else {
    Write-Warning "VS Developer Shell not found at $devShellPath"
}

# Get the project path
$scriptDir = $PSScriptRoot
$projectPath = Join-Path $scriptDir "ConsoleImage.csproj"

Write-Host "Building: $projectPath" -ForegroundColor Cyan
dotnet publish $projectPath -c Release -r win-x64 --self-contained
