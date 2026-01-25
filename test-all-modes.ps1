# Comprehensive ConsoleImage Test Script
# Tests ALL render modes, options, and output formats
# Run from ConsoleImage root directory

param(
    [string]$ImagePath = "C:\Users\scott\Downloads\112722.jpg",
    [string]$GifPath = "F:\gifs\spacexSmall.gif",
    [string]$VideoPath = "C:\Users\scott\OneDrive\Videos\Count.Arthur.Strong.S02E03.HDTV.x264-TASTETV.mp4",
    [string]$OutputDir = "samples\test-output",
    [switch]$SkipVideo,
    [switch]$Quick
)

$ErrorActionPreference = "Continue"

# Ensure output directory exists
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  ConsoleImage Comprehensive Test Suite" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Validate inputs
$hasImage = Test-Path $ImagePath
$hasGif = Test-Path $GifPath
$hasVideo = Test-Path $VideoPath

Write-Host "Source files:" -ForegroundColor Yellow
Write-Host "  Image: $ImagePath $(if($hasImage){'[OK]'}else{'[MISSING]'})" -ForegroundColor $(if($hasImage){'Green'}else{'Red'})
Write-Host "  GIF:   $GifPath $(if($hasGif){'[OK]'}else{'[MISSING]'})" -ForegroundColor $(if($hasGif){'Green'}else{'Red'})
Write-Host "  Video: $VideoPath $(if($hasVideo){'[OK]'}else{'[MISSING]'})" -ForegroundColor $(if($hasVideo){'Green'}else{'Red'})
Write-Host ""

$testCount = 0
$passCount = 0
$failCount = 0
$results = @()

function Test-Render {
    param(
        [string]$Name,
        [string]$InputFile,
        [string]$OutputFile,
        [string[]]$RenderArgs
    )

    $script:testCount++
    $outPath = Join-Path $OutputDir $OutputFile

    Write-Host "[$script:testCount] $Name" -NoNewline
    Write-Host " -> $OutputFile" -ForegroundColor DarkGray

    try {
        # Build argument list
        $allArgs = @($InputFile) + $RenderArgs + @("-o", $outPath)

        # Run dotnet
        & dotnet run --project ConsoleImage -- @allArgs 2>&1 | Out-Null
        $exitCode = $LASTEXITCODE

        if ($exitCode -eq 0 -and (Test-Path $outPath)) {
            $fileInfo = Get-Item $outPath
            $sizeKB = [math]::Round($fileInfo.Length / 1KB, 1)
            Write-Host "    PASS " -ForegroundColor Green -NoNewline
            Write-Host "($sizeKB KB)" -ForegroundColor DarkGray
            $script:passCount++
            $script:results += [PSCustomObject]@{
                Test = $Name
                Status = "PASS"
                Output = $OutputFile
                SizeKB = $sizeKB
            }
        } else {
            Write-Host "    FAIL " -ForegroundColor Red -NoNewline
            Write-Host "(exit: $exitCode)" -ForegroundColor DarkGray
            $script:failCount++
            $script:results += [PSCustomObject]@{
                Test = $Name
                Status = "FAIL"
                Output = $OutputFile
                SizeKB = 0
            }
        }
    } catch {
        Write-Host "    ERROR: $($_.Exception.Message)" -ForegroundColor Red
        $script:failCount++
        $script:results += [PSCustomObject]@{
            Test = $Name
            Status = "ERROR"
            Output = $OutputFile
            SizeKB = 0
        }
    }
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  PART 1: Static Image Tests" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

if ($hasImage) {
    # Basic render modes
    Test-Render "ASCII mode" $ImagePath "image_ascii.gif" @("-a", "-w", "100")
    Test-Render "Blocks mode" $ImagePath "image_blocks.gif" @("-b", "-w", "100")
    Test-Render "Braille mode" $ImagePath "image_braille.gif" @("-B", "-w", "100")
    Test-Render "Monochrome braille" $ImagePath "image_mono.gif" @("--monochrome", "-w", "100")

    # Monochrome at different widths (showcasing column efficiency)
    Test-Render "Mono 80 cols" $ImagePath "image_mono_80.gif" @("--mono", "-w", "80")
    Test-Render "Mono 120 cols" $ImagePath "image_mono_120.gif" @("--mono", "-w", "120")
    Test-Render "Mono 160 cols (wide)" $ImagePath "image_mono_160.gif" @("--mono", "-w", "160")
    Test-Render "Mono 200 cols (ultra)" $ImagePath "image_mono_200.gif" @("--mono", "-w", "200")

    # Matrix modes
    Test-Render "Matrix classic" $ImagePath "image_matrix.gif" @("--matrix", "-w", "80", "--gif-length", "2")
    Test-Render "Matrix fullcolor" $ImagePath "image_matrix_full.gif" @("--matrix", "--matrix-fullcolor", "-w", "80", "--gif-length", "2")

    if (-not $Quick) {
        Test-Render "Matrix red" $ImagePath "image_matrix_red.gif" @("--matrix", "--matrix-color", "red", "-w", "60", "--gif-length", "2")
        Test-Render "Matrix blue" $ImagePath "image_matrix_blue.gif" @("--matrix", "--matrix-color", "blue", "-w", "60", "--gif-length", "2")
        Test-Render "Matrix amber" $ImagePath "image_matrix_amber.gif" @("--matrix", "--matrix-color", "amber", "-w", "60", "--gif-length", "2")
        Test-Render "Matrix binary" $ImagePath "image_matrix_bin.gif" @("--matrix", "--matrix-alphabet", "01", "-w", "60", "--gif-length", "2")

        # Presets and options
        Test-Render "ASCII edge detect" $ImagePath "image_edge.gif" @("-a", "-w", "100", "--edge")
        Test-Render "Greyscale blocks" $ImagePath "image_grey.gif" @("-b", "-w", "100", "--no-color")

        # Output formats
        Test-Render "CIDZ compressed" $ImagePath "image.cidz" @("-a", "-w", "80")
    }
} else {
    Write-Host "  Skipping image tests (file not found)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  PART 2: Animated GIF Tests" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

if ($hasGif) {
    Test-Render "GIF ASCII" $GifPath "gif_ascii.gif" @("-a", "-w", "80")
    Test-Render "GIF Blocks" $GifPath "gif_blocks.gif" @("-b", "-w", "80")
    Test-Render "GIF Braille" $GifPath "gif_braille.gif" @("-B", "-w", "80")
    Test-Render "GIF Monochrome" $GifPath "gif_mono.gif" @("--mono", "-w", "80")
    Test-Render "GIF with status" $GifPath "gif_status.gif" @("-a", "-w", "80", "--status")

    if (-not $Quick) {
        Test-Render "GIF frame step 2" $GifPath "gif_step2.gif" @("-a", "-w", "80", "-f", "2")
        Test-Render "GIF smart skip" $GifPath "gif_smart.gif" @("-a", "-w", "80", "-f", "smart")
        Test-Render "GIF dejitter" $GifPath "gif_dejitter.gif" @("-a", "-w", "80", "--dejitter")
        Test-Render "GIF to CIDZ" $GifPath "gif.cidz" @("-a", "-w", "60")
    }
} else {
    Write-Host "  Skipping GIF tests (file not found)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  PART 3: Video Tests" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

if ($hasVideo -and -not $SkipVideo) {
    $dur = if ($Quick) { "3" } else { "5" }

    Test-Render "Video ASCII" $VideoPath "video_ascii.gif" @("-a", "-w", "80", "--duration", $dur)
    Test-Render "Video Blocks" $VideoPath "video_blocks.gif" @("-b", "-w", "80", "--duration", $dur)
    Test-Render "Video Braille" $VideoPath "video_braille.gif" @("-B", "-w", "80", "--duration", $dur)
    Test-Render "Video Mono" $VideoPath "video_mono.gif" @("--mono", "-w", "100", "--duration", $dur)
    Test-Render "Video with status" $VideoPath "video_status.gif" @("-a", "-w", "80", "--status", "--duration", $dur)

    if (-not $Quick) {
        Test-Render "Video Mono wide" $VideoPath "video_mono_wide.gif" @("--mono", "-w", "140", "--duration", "3")
        Test-Render "Video to CIDZ" $VideoPath "video.cidz" @("-a", "-w", "60", "--duration", "5")
    }
} elseif ($SkipVideo) {
    Write-Host "  Skipping video tests (--SkipVideo)" -ForegroundColor Yellow
} else {
    Write-Host "  Skipping video tests (file not found)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  PART 4: Playback Tests" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$cidzFile = Join-Path $OutputDir "image.cidz"
if (Test-Path $cidzFile) {
    $testCount++
    Write-Host "[$testCount] CIDZ playback" -NoNewline
    & dotnet run --project ConsoleImage -- $cidzFile --no-animate 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "    PASS" -ForegroundColor Green
        $passCount++
    } else {
        Write-Host "    FAIL" -ForegroundColor Red
        $failCount++
    }
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  TEST SUMMARY" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Total Tests: $testCount" -ForegroundColor White
Write-Host "  Passed: $passCount" -ForegroundColor Green
Write-Host "  Failed: $failCount" -ForegroundColor $(if($failCount -gt 0){'Red'}else{'DarkGray'})
Write-Host ""

if ($failCount -gt 0) {
    Write-Host "Failed tests:" -ForegroundColor Red
    $results | Where-Object { $_.Status -ne "PASS" } | ForEach-Object {
        Write-Host "  - $($_.Test)" -ForegroundColor Red
    }
    Write-Host ""
}

# Show size comparison for monochrome efficiency
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  MONOCHROME BRAILLE EFFICIENCY" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1-bit braille is perfect for:" -ForegroundColor Yellow
Write-Host "  - Quick preview (3-5x smaller files)" -ForegroundColor White
Write-Host "  - Text/terminal output (many columns)" -ForegroundColor White
Write-Host "  - SSH/low bandwidth connections" -ForegroundColor White
Write-Host "  - High detail in small terminal" -ForegroundColor White
Write-Host ""

$monoFiles = Get-ChildItem $OutputDir -Filter "*_mono*.gif" -ErrorAction SilentlyContinue
if ($monoFiles) {
    Write-Host "Monochrome samples:" -ForegroundColor Cyan
    $monoFiles | ForEach-Object {
        $kb = [math]::Round($_.Length / 1KB, 1)
        Write-Host "  $($_.Name): $kb KB" -ForegroundColor White
    }
}

Write-Host ""
$totalSize = (Get-ChildItem $OutputDir -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
if ($totalSize) {
    $totalSizeMB = [math]::Round($totalSize / 1MB, 2)
    Write-Host "Total output: $totalSizeMB MB in $OutputDir" -ForegroundColor Cyan
}
Write-Host ""

if ($failCount -eq 0) {
    Write-Host "ALL TESTS PASSED!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "SOME TESTS FAILED" -ForegroundColor Red
    exit 1
}
