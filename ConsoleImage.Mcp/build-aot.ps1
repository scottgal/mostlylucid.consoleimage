# Build AOT for MCP Server
# Requires Visual Studio C++ build tools

param(
    [ValidateSet("win-x64", "linux-x64", "osx-x64", "osx-arm64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

# Add vswhere to PATH if on Windows
if ($IsWindows -or $env:OS -eq "Windows_NT")
{
    $vswhereDir = "C:\Program Files (x86)\Microsoft Visual Studio\Installer"
    if (Test-Path $vswhereDir)
    {
        $env:PATH = "$vswhereDir;$env:PATH"
    }

    # Find and import VS Developer environment
    $vsPath = & vswhere -latest -property installationPath 2> $null
    if ($vsPath)
    {
        $devShell = Join-Path $vsPath "Common7\Tools\Microsoft.VisualStudio.DevShell.dll"
        if (Test-Path $devShell)
        {
            Import-Module $devShell
            Enter-VsDevShell -VsInstallPath $vsPath -SkipAutomaticLocation -DevCmdArguments "-arch=amd64"
        }
    }
}

Write-Host "Building AOT for $Runtime..." -ForegroundColor Cyan
Push-Location $PSScriptRoot

try
{
    dotnet publish -c Release -r $Runtime --self-contained

    $outputDir = "bin\Release\net10.0\$Runtime\publish"
    if (Test-Path $outputDir)
    {
        $exeName = if ($Runtime -like "win-*")
        {
            "consoleimage-mcp.exe"
        }
        else
        {
            "consoleimage-mcp"
        }
        $exePath = Join-Path $outputDir $exeName
        if (Test-Path $exePath)
        {
            $size = (Get-Item $exePath).Length / 1MB
            Write-Host "Success! Output: $exePath ($([math]::Round($size, 2) ) MB)" -ForegroundColor Green
        }
    }
}
finally
{
    Pop-Location
}
