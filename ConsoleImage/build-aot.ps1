# AOT Build Script for ConsoleImage
# Handles mapped drive issues by converting to UNC paths if needed

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

# Get the project path - handle mapped drives
$scriptDir = $PSScriptRoot
$projectDir = Split-Path $scriptDir -Parent

# Check if running from a mapped drive and convert to UNC path
# Mapped drives don't work reliably with AOT/ILC because child processes
# (especially elevated ones) don't inherit drive mappings
function Get-UncPath($path) {
    $drive = Split-Path $path -Qualifier
    if ($drive -match '^[A-Z]:$') {
        # Check if this is a mapped network drive
        $netUse = net use $drive 2>$null | Select-String "Remote name"
        if ($netUse) {
            $uncRoot = ($netUse -split '\s+')[-1]
            $relativePath = $path.Substring($drive.Length)
            return $uncRoot + $relativePath
        }
    }
    return $path
}

$projectPath = Get-UncPath (Join-Path $projectDir "ConsoleImage.Video\ConsoleImage.Video.csproj")

Write-Host "Building: $projectPath" -ForegroundColor Cyan
dotnet publish $projectPath -c Release -r win-x64
