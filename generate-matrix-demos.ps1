# Matrix Effect Demo Generator
# Generates animated GIFs showing different Matrix rendering modes

$ErrorActionPreference = "Stop"

# Sample images to use
$sampleImages = @(
    "samples/portrait.jpg",
    "samples/mountain.jpg"
)

# Check for sample images
$testImage = $null
foreach ($img in $sampleImages) {
    if (Test-Path $img) {
        $testImage = $img
        break
    }
}

if (-not $testImage) {
    Write-Host "No sample images found. Looking for any jpg/png..."
    $testImage = Get-ChildItem -Path "." -Include "*.jpg","*.png" -Recurse | Select-Object -First 1
    if (-not $testImage) {
        Write-Host "Error: No image files found for demo generation"
        exit 1
    }
    $testImage = $testImage.FullName
}

Write-Host "Using image: $testImage"
Write-Host ""

# Output directory
$outDir = "samples/matrix-demos"
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

# CLI path
$cli = "ConsoleImage/bin/Debug/net10.0/consoleimage.dll"
if (-not (Test-Path $cli)) {
    Write-Host "Building CLI..."
    dotnet build ConsoleImage/ConsoleImage.csproj -c Debug
}

Write-Host "Generating Matrix demos..."
Write-Host "========================="
Write-Host ""

# Demo 1: Classic Green Matrix
Write-Host "1. Classic Green Matrix..."
dotnet $cli $testImage --matrix -w 60 -o "gif:$outDir/matrix_classic_green.gif" --gif-length 3 --matrix-color green

# Demo 2: Red Pill
Write-Host "2. Red Pill Matrix..."
dotnet $cli $testImage --matrix -w 60 -o "gif:$outDir/matrix_red_pill.gif" --gif-length 3 --matrix-color red

# Demo 3: Blue Pill
Write-Host "3. Blue Pill Matrix..."
dotnet $cli $testImage --matrix -w 60 -o "gif:$outDir/matrix_blue_pill.gif" --gif-length 3 --matrix-color blue

# Demo 4: Amber/Retro Terminal
Write-Host "4. Amber Terminal..."
dotnet $cli $testImage --matrix -w 60 -o "gif:$outDir/matrix_amber.gif" --gif-length 3 --matrix-color amber

# Demo 5: Cyberpunk Purple
Write-Host "5. Cyberpunk Purple..."
dotnet $cli $testImage --matrix -w 60 -o "gif:$outDir/matrix_cyberpunk.gif" --gif-length 3 --matrix-color purple

# Demo 6: Full Color (from source image)
Write-Host "6. Full Color (from image)..."
dotnet $cli $testImage --matrix -w 60 -o "gif:$outDir/matrix_full_color.gif" --gif-length 3 --matrix-fullcolor

# Demo 7: Edge Detection / Image Reveal (rain collects on shoulders)
Write-Host "7. Image Reveal (edge detection)..."
dotnet $cli $testImage --matrix -w 60 -o "gif:$outDir/matrix_image_reveal.gif" --gif-length 3 --matrix-edge-detect --matrix-bright-persist

# Demo 8: ASCII-only (no katakana)
Write-Host "8. ASCII Only..."
dotnet $cli $testImage --matrix -w 60 -o "gif:$outDir/matrix_ascii_only.gif" --gif-length 3 --matrix-ascii

# Demo 9: Binary rain
Write-Host "9. Binary Rain..."
dotnet $cli $testImage --matrix -w 60 -o "gif:$outDir/matrix_binary.gif" --gif-length 3 --matrix-alphabet "01"

# Demo 10: Custom word
Write-Host "10. Custom Word..."
dotnet $cli $testImage --matrix -w 60 -o "gif:$outDir/matrix_custom.gif" --gif-length 3 --matrix-alphabet "MATRIX"

# Demo 11: Edge Reveal + Full Color (best image visibility)
Write-Host "11. Edge Reveal + Full Color..."
dotnet $cli $testImage --matrix -w 60 -o "gif:$outDir/matrix_reveal_fullcolor.gif" --gif-length 3 --matrix-fullcolor --matrix-edge-detect --matrix-bright-persist

Write-Host ""
Write-Host "========================="
Write-Host "Demos generated in: $outDir"
Write-Host ""
Get-ChildItem $outDir -Filter "*.gif" | ForEach-Object { Write-Host "  $($_.Name) ($([math]::Round($_.Length/1KB, 1)) KB)" }
