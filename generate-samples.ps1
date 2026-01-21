# Generate all sample images for README documentation
# Run from the ConsoleImage root directory

$ErrorActionPreference = "Stop"

$samplesDir = "samples"
if (-not (Test-Path $samplesDir)) {
    New-Item -ItemType Directory -Path $samplesDir | Out-Null
}

Write-Host "Building project..." -ForegroundColor Cyan
dotnet build --verbosity quiet

Write-Host "`nGenerating sample images..." -ForegroundColor Cyan

# Source images (absolute paths)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$portrait = Join-Path $scriptDir "demo_portrait.jpg"
$mountain = Join-Path $scriptDir "demo_mountain.jpg"
$wiggum = Join-Path (Join-Path $scriptDir "samples") "wiggum_loop.gif"

# Check if source images exist
if (-not (Test-Path $portrait)) {
    Write-Host "Warning: $portrait not found - skipping portrait samples" -ForegroundColor Yellow
}
if (-not (Test-Path $mountain)) {
    Write-Host "Warning: $mountain not found - skipping mountain samples" -ForegroundColor Yellow
}
if (-not (Test-Path $wiggum)) {
    Write-Host "Warning: $wiggum not found - skipping animation samples" -ForegroundColor Yellow
}

function Generate-Sample {
    param(
        [string]$InputFile,
        [string]$OutputFile,
        [string]$ExtraArgs,
        [string]$Description
    )

    if (-not (Test-Path $InputFile)) {
        Write-Host "  Skipping $OutputFile (input not found)" -ForegroundColor Yellow
        return
    }

    Write-Host "  $Description" -ForegroundColor White
    $cmd = "dotnet run --project ConsoleImage -- `"$InputFile`" -o gif:$samplesDir/$OutputFile $ExtraArgs"
    Invoke-Expression $cmd 2>&1 | Out-Null
}

# Portrait samples - ASCII mode
Generate-Sample $portrait "demo_portrait_ascii.gif" "-w 80" "Portrait ASCII"
Generate-Sample $portrait "demo_portrait_blocks.gif" "-b -w 80" "Portrait Blocks"
Generate-Sample $portrait "demo_portrait_braille.gif" "-B -w 80" "Portrait Braille"
Generate-Sample $portrait "demo_portrait_edge.gif" "-w 80 --edge" "Portrait Edge Detection"
Generate-Sample $portrait "demo_portrait_simple.gif" "-w 80 -p simple" "Portrait Simple Preset"
Generate-Sample $portrait "demo_portrait_block.gif" "-w 80 -p block" "Portrait Block Preset"

# Mountain/landscape samples
Generate-Sample $mountain "demo_mountain_ascii.gif" "-w 100" "Mountain ASCII"
Generate-Sample $mountain "demo_mountain_blocks.gif" "-b -w 100" "Mountain Blocks"
Generate-Sample $mountain "demo_mountain_braille.gif" "-B -w 100" "Mountain Braille"

# Animation samples (wiggum)
Generate-Sample $wiggum "wiggum_ascii.gif" "-w 80" "Animation ASCII"
Generate-Sample $wiggum "wiggum_blocks.gif" "-b -w 80" "Animation Blocks"
Generate-Sample $wiggum "wiggum_braille.gif" "-B -w 80" "Animation Braille"

# Gamma comparison samples
Generate-Sample $portrait "demo_gamma_1.0.gif" "-w 80 --gamma 1.0" "Portrait Gamma 1.0 (no correction)"
Generate-Sample $portrait "demo_gamma_0.85.gif" "-w 80 --gamma 0.85" "Portrait Gamma 0.85 (default - brighter)"
Generate-Sample $portrait "demo_gamma_0.7.gif" "-w 80 --gamma 0.7" "Portrait Gamma 0.7 (much brighter)"

Write-Host "`nDone! Samples generated in $samplesDir/" -ForegroundColor Green
Write-Host "Remember to commit the updated samples to git." -ForegroundColor Cyan
