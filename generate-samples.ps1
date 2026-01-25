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

# Portrait samples - ASCII mode (note: -a required since braille is now default)
Generate-Sample $portrait "demo_portrait_ascii.gif" "-a -w 80" "Portrait ASCII"
Generate-Sample $portrait "demo_portrait_blocks.gif" "-b -w 80" "Portrait Blocks"
Generate-Sample $portrait "demo_portrait_braille.gif" "-B -w 80" "Portrait Braille"
Generate-Sample $portrait "demo_portrait_edge.gif" "-a -w 80 --edge" "Portrait Edge Detection"
Generate-Sample $portrait "demo_portrait_simple.gif" "-a -w 80 -p simple" "Portrait Simple Preset"
Generate-Sample $portrait "demo_portrait_block.gif" "-a -w 80 -p block" "Portrait Block Preset"

# Mountain/landscape samples
Generate-Sample $mountain "demo_mountain_ascii.gif" "-a -w 100" "Mountain ASCII"
Generate-Sample $mountain "demo_mountain_blocks.gif" "-b -w 100" "Mountain Blocks"
Generate-Sample $mountain "demo_mountain_braille.gif" "-B -w 100" "Mountain Braille"

# Animation samples (wiggum)
Generate-Sample $wiggum "wiggum_ascii.gif" "-a -w 80" "Animation ASCII"
Generate-Sample $wiggum "wiggum_blocks.gif" "-b -w 80" "Animation Blocks"
Generate-Sample $wiggum "wiggum_braille.gif" "-B -w 80" "Animation Braille"

# Gamma comparison samples (ASCII mode)
Generate-Sample $portrait "demo_gamma_1.0.gif" "-a -w 80 --gamma 1.0" "Portrait Gamma 1.0 (no correction)"
Generate-Sample $portrait "demo_gamma_0.85.gif" "-a -w 80 --gamma 0.85" "Portrait Gamma 0.85 (default - brighter)"
Generate-Sample $portrait "demo_gamma_0.7.gif" "-a -w 80 --gamma 0.7" "Portrait Gamma 0.7 (much brighter)"

# Matrix mode samples
Generate-Sample $portrait "matrix_portrait_final.gif" "-w 60 --matrix --gif-length 3" "Matrix Classic Green"
Generate-Sample $mountain "matrix_mountain_fullcolor.gif" "-w 80 --matrix --matrix-fullcolor --gif-length 3" "Matrix Full Color"
Generate-Sample $portrait "matrix_binary.gif" "-w 60 --matrix --matrix-alphabet 01 --gif-length 3" "Matrix Binary Rain"

# Moviebill samples (logo animation)
$moviebill = Join-Path (Join-Path $scriptDir "samples") "moviebill-logo.gif"
Generate-Sample $moviebill "moviebill_ascii.gif" "-a -w 60" "Moviebill ASCII"
Generate-Sample $moviebill "moviebill_blocks.gif" "-b -w 60" "Moviebill Blocks"
Generate-Sample $moviebill "moviebill_braille.gif" "-B -w 60" "Moviebill Braille"

# Boingball samples (classic Amiga demo)
$boingball = "F:/gifs/boingball_10_80x80_256.gif"
Generate-Sample $boingball "boingball_ascii.gif" "-a -w 60" "Boingball ASCII"
Generate-Sample $boingball "boingball_blocks.gif" "-b -w 60" "Boingball Blocks"
Generate-Sample $boingball "boingball_braille.gif" "-B -w 60" "Boingball Braille"

# Cat wag samples (animated GIF)
$catwag = "F:/gifs/cat_wag.gif"
Generate-Sample $catwag "cat_wag_ascii.gif" "-a -w 60" "Cat Wag ASCII"
Generate-Sample $catwag "cat_wag_blocks.gif" "-b -w 60" "Cat Wag Blocks"
Generate-Sample $catwag "cat_wag_braille.gif" "-B -w 60" "Cat Wag Braille"

# Status line samples - showing progress/info below the frame
Generate-Sample $wiggum "status_ascii.gif" "-a -w 80 --status" "Animation with Status Line (ASCII)"
Generate-Sample $wiggum "status_braille.gif" "-B -w 80 --status" "Braille with Status Line (burned in)"

# Video samples with status line (short clips)
$video = "C:/Users/scott/OneDrive/Videos/Count.Arthur.Strong.S02E03.HDTV.x264-TASTETV.mp4"
if (Test-Path $video) {
    Write-Host "  Video ASCII with Status" -ForegroundColor White
    dotnet run --project ConsoleImage -- "$video" -a -o "gif:$samplesDir/video_status_ascii.gif" -w 80 --status --duration 3 2>&1 | Out-Null

    Write-Host "  Video Braille with Status" -ForegroundColor White
    dotnet run --project ConsoleImage -- "$video" -o "gif:$samplesDir/video_status_braille.gif" -B -w 80 --status --duration 3 2>&1 | Out-Null

    Write-Host "  Video to CIDZ (compressed, ASCII)" -ForegroundColor White
    dotnet run --project ConsoleImage -- "$video" -a -o "$samplesDir/video_sample.cidz" -w 60 --duration 5 2>&1 | Out-Null
} else {
    Write-Host "  Skipping video samples (video not found)" -ForegroundColor Yellow
}

# Monochrome braille samples (high-detail greyscale)
Generate-Sample $portrait "demo_portrait_mono.gif" "--monochrome -w 80" "Portrait Monochrome"
Generate-Sample $mountain "demo_mountain_mono.gif" "--mono -w 100" "Mountain Monochrome"

Write-Host "`nDone! Samples generated in $samplesDir/" -ForegroundColor Green
Write-Host "Remember to commit the updated samples to git." -ForegroundColor Cyan
