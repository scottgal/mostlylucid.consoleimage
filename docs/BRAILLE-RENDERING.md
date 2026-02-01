# Braille Rendering: 8x Resolution ASCII Art

**How ConsoleImage achieves ultra-high-resolution terminal graphics using Unicode braille characters**

## Introduction

Traditional ASCII art is limited by a fundamental constraint: each character cell can only display one symbol. Whether you use `@`, `#`, or `.`, you get exactly one "pixel" per character position. But what if we could pack **eight pixels** into every character cell?

That's exactly what braille rendering does. By using Unicode braille characters (U+2800–U+28FF), we can represent a 2×4 grid of dots within each character cell, effectively multiplying our resolution by 8x compared to traditional ASCII art.

```
Traditional ASCII (1 pixel/cell):     Braille (8 pixels/cell):

  @@@@@@                              ⣿⣿⣿⣿⣿⣿
  @    @                              ⡇    ⢸
  @    @          vs                  ⡇    ⢸
  @@@@@@                              ⣿⣿⣿⣿⣿⣿
```

## The Unicode Braille Block

### Character Structure

Unicode braille characters encode an 8-dot pattern in a 2×4 grid:

```
┌─────┬─────┐
│  1  │  4  │  ← Row 0
├─────┼─────┤
│  2  │  5  │  ← Row 1
├─────┼─────┤
│  3  │  6  │  ← Row 2
├─────┼─────┤
│  7  │  8  │  ← Row 3
└─────┴─────┘
  Col0  Col1
```

Each dot corresponds to a specific bit in the character code:

| Dot Position | Bit Value |
|--------------|-----------|
| Dot 1 (top-left) | 0x01 |
| Dot 2 | 0x02 |
| Dot 3 | 0x04 |
| Dot 4 (top-right) | 0x08 |
| Dot 5 | 0x10 |
| Dot 6 | 0x20 |
| Dot 7 (bottom-left) | 0x40 |
| Dot 8 (bottom-right) | 0x80 |

The character code is simply: `0x2800 + (bit pattern)`

```mermaid
graph TD
    subgraph "Braille Character Encoding"
        A["Image Region<br/>2×4 pixels"] --> B["Threshold Each Pixel"]
        B --> C["Build 8-bit Pattern"]
        C --> D["0x2800 + pattern"]
        D --> E["Unicode Character ⣿"]
    end

    style A stroke:#4a9eff
    style B stroke:#4a9eff
    style C stroke:#00aa00
    style D stroke:#00aa00
    style E stroke:#ff6600
```

### Why This Encoding?

The braille encoding follows the historical Braille system, where dots 1-6 form the original 6-dot Braille (used for letters), and dots 7-8 were added later for computer Braille. This explains the seemingly odd bit ordering—it preserves compatibility with traditional Braille text.

## The Rendering Pipeline

```mermaid
flowchart LR
    A[Source Image] --> B[Resize to Target]
    B --> C[Convert to Grayscale]
    C --> D[Otsu Threshold]
    D --> E[Atkinson Dither]
    E --> F[Ring Sampling<br/>8D Vectors]
    F --> G[SIMD Shape<br/>Matching]
    G --> H[Apply Colors]
    H --> I[Terminal Output]

    style A stroke:#4a9eff
    style D stroke:#ff6600
    style E stroke:#ff6600
    style F stroke:#9933ff
    style G stroke:#9933ff
    style H stroke:#00aa00
```

### Step 1: Intelligent Resizing

For a terminal width of W characters and height of H characters, we need:
- **Pixel width**: W × 2 (2 dots per character horizontally)
- **Pixel height**: H × 4 (4 dots per character vertically)

The aspect ratio compensation accounts for terminal fonts being taller than wide (typically 2:1).

### Step 2: Optimal Thresholding with Otsu's Method

The key challenge in braille rendering is deciding which dots to turn on. A naive approach might use a fixed threshold (e.g., 50% brightness), but this fails badly for images that are predominantly light or dark.

**Otsu's method** finds the optimal threshold by maximizing the variance between foreground and background classes:

```mermaid
graph TB
    subgraph "Otsu's Method"
        H[Build Histogram] --> V[For Each Threshold t]
        V --> W1["ω₀ = P(pixel < t)"]
        V --> W2["ω₁ = P(pixel ≥ t)"]
        W1 --> M1["μ₀ = mean of class 0"]
        W2 --> M2["μ₁ = mean of class 1"]
        M1 --> VAR["σ²_b = ω₀ω₁(μ₀-μ₁)²"]
        M2 --> VAR
        VAR --> MAX["Find t that maximizes σ²_b"]
    end

    style H stroke:#4a9eff
    style VAR stroke:#ff6600
    style MAX stroke:#00aa00
```

The algorithm:

1. **Build a histogram** of pixel intensities (256 bins)
2. **For each possible threshold** (0-255):
   - Calculate the probability of each class (background/foreground)
   - Calculate the mean intensity of each class
   - Compute the between-class variance
3. **Select the threshold** that maximizes between-class variance

This automatically adapts to any image—dark images get low thresholds, bright images get high thresholds, and high-contrast images get thresholds right in the middle.

```csharp
private static byte ComputeOtsuThreshold(ReadOnlySpan<byte> grayPixels)
{
    // Build histogram
    Span<int> histogram = stackalloc int[256];
    foreach (var pixel in grayPixels)
        histogram[pixel]++;

    int total = grayPixels.Length;
    float sumTotal = 0;
    for (int i = 0; i < 256; i++)
        sumTotal += i * histogram[i];

    float sumBackground = 0;
    int weightBackground = 0;
    float maxVariance = 0;
    byte optimalThreshold = 0;

    for (int t = 0; t < 256; t++)
    {
        weightBackground += histogram[t];
        if (weightBackground == 0) continue;

        int weightForeground = total - weightBackground;
        if (weightForeground == 0) break;

        sumBackground += t * histogram[t];
        float meanBackground = sumBackground / weightBackground;
        float meanForeground = (sumTotal - sumBackground) / weightForeground;

        float variance = (float)weightBackground * weightForeground
                       * (meanBackground - meanForeground)
                       * (meanBackground - meanForeground);

        if (variance > maxVariance)
        {
            maxVariance = variance;
            optimalThreshold = (byte)t;
        }
    }

    return optimalThreshold;
}
```

### Step 3: Atkinson Dithering

After thresholding, we have a binary image, but hard edges create ugly banding. **Dithering** diffuses quantization error to neighboring pixels, creating the illusion of intermediate tones.

We use **Atkinson dithering** (developed by Bill Atkinson for the original Macintosh) instead of the more common Floyd-Steinberg algorithm. Here's why:

```mermaid
graph LR
    subgraph "Floyd-Steinberg (7+3+5+1=16)"
        FS_X["X"] --> FS_R["7/16"]
        FS_X --> FS_D["5/16"]
        FS_X --> FS_DL["3/16"]
        FS_X --> FS_DR["1/16"]
    end

    subgraph "Atkinson (1+1+1+1+1+1=6)"
        AT_X["X"] --> AT_R1["1/8"]
        AT_X --> AT_R2["1/8"]
        AT_X --> AT_D["1/8"]
        AT_X --> AT_DL["1/8"]
        AT_X --> AT_DR["1/8"]
        AT_X --> AT_D2["1/8"]
    end

    style FS_X stroke:#ff6600
    style AT_X stroke:#00aa00
```

**Why Atkinson is better for braille:**

| Aspect | Floyd-Steinberg | Atkinson |
|--------|-----------------|----------|
| Error diffused | 100% (16/16) | 75% (6/8) |
| Spread pattern | 4 pixels | 6 pixels |
| Result | Softer gradients | Higher contrast |
| Best for | Photos | Line art, text |

Atkinson deliberately discards 25% of the error, which:
- Produces **sharper edges** (critical for small dot patterns)
- Creates **more distinct light/dark regions**
- Avoids the "muddy" look that Floyd-Steinberg can produce

The Atkinson diffusion pattern:

```
        X   1   1
    1   1   1
        1

(each "1" receives 1/8 of the error)
```

```csharp
private static void ApplyAtkinsonDithering(Span<short> buffer, int width, int height, byte threshold)
{
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            int idx = y * width + x;
            short oldPixel = buffer[idx];
            byte newPixel = oldPixel > threshold ? (byte)255 : (byte)0;
            buffer[idx] = newPixel;

            short error = (short)(oldPixel - newPixel);
            short diffuse = (short)(error / 8);  // 1/8 of error

            // Diffuse to 6 neighbors (only 6/8 = 75% of error)
            if (x + 1 < width)
                buffer[idx + 1] += diffuse;
            if (x + 2 < width)
                buffer[idx + 2] += diffuse;
            if (y + 1 < height)
            {
                if (x > 0)
                    buffer[idx + width - 1] += diffuse;
                buffer[idx + width] += diffuse;
                if (x + 1 < width)
                    buffer[idx + width + 1] += diffuse;
            }
            if (y + 2 < height)
                buffer[idx + width * 2] += diffuse;
        }
    }
}
```

## Vector Glyph Shape Matching

After dithering produces a brightness field, we don't simply threshold each dot independently. Instead, we use **8-dimensional shape vector matching** to find the braille character whose dot pattern best represents each 2×4 pixel region. This is the same core idea behind the ASCII renderer's 6D shape matching (from Alex Harri's algorithm), adapted for braille's 8-dot grid.

### Why Not Just Threshold Each Dot?

A naive approach checks each of the 8 pixel positions against a threshold and builds the braille code bit by bit. This works, but it's sensitive to noise at dot boundaries—a single pixel hovering near the threshold can flicker on and off between frames, and spatial aliasing produces jagged edges.

Shape vector matching treats the entire 2×4 cell as a unit, finding the braille pattern whose overall shape is the closest geometric match. This produces smoother, more stable output because the decision considers all 8 positions together rather than independently.

### Concentric Ring Sampling

Each dot position is sampled using **concentric rings** rather than a single pixel lookup. This averages brightness over a small circular area around each dot center, making the result robust to sub-pixel positioning:

```
           ┌─ Outer ring (8 points, radius r)
           │   ┌─ Inner ring (4 points, radius r/2)
           │   │   ┌─ Center point
           │   │   │
           ·   ·   ●   ·   ·
         ·       ·   ·       ·
        ·    ·   ●   ·    ·
         ·       ·   ·       ·
           ·   ·   ●   ·   ·

  Total: 1 center + 4 inner + 8 outer = 13 samples per dot
```

For each of the 8 dot positions in the 2×4 cell:

1. **Center point**: Direct pixel lookup at dot center
2. **Inner ring**: 4 points at radius × 0.5, evenly spaced
3. **Outer ring**: 8 points at full radius, offset by π/8 from inner ring

The average of all samples gives a **coverage value** between 0.0 (white/empty) and 1.0 (black/filled)—a continuous value rather than a binary on/off.

```csharp
// Dot center positions within a 2×4 pixel cell:
//   Col 0: x = px + 0.5    Col 1: x = px + 1.5
//   Row 0: y = py + 0.5    Row 1: y = py + 1.5
//   Row 2: y = py + 2.5    Row 3: y = py + 3.5
//
// Sampling radius: 0.35 pixels (relative to half-pixel spacing)
```

### 8D Shape Vectors

Each braille pattern (0x00–0xFF) is represented as an **8-dimensional vector** where each component corresponds to one dot position:

```
Pattern ⣿ (0xFF, all dots ON):  [1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0]
Pattern ⠀ (0x00, all dots OFF): [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]
Pattern ⡇ (0x47, left column):  [1.0, 0.0, 1.0, 0.0, 1.0, 0.0, 1.0, 0.0]
Pattern ⣤ (0xE4, bottom half):  [0.0, 0.0, 1.0, 0.0, 1.0, 1.0, 1.0, 1.0]
Pattern ⠛ (0x1B, top half):     [1.0, 1.0, 1.0, 1.0, 0.0, 0.0, 0.0, 0.0]
```

The vector index maps to the 2×4 grid in row-major order:

```
Index:  [0] [1]     Braille:  1  4
        [2] [3]               2  5
        [4] [5]               3  6
        [6] [7]               7  8
```

These 256 vectors are generated **mathematically** from the Unicode braille standard—no font rendering is needed (unlike ASCII mode, which must render each glyph to measure its shape). Each component is simply 1.0 if the corresponding bit is set in the braille code, 0.0 otherwise:

```csharp
// Dot bit positions: index → Unicode braille bit
private static readonly int[] DotBits = [0x01, 0x08, 0x02, 0x10, 0x04, 0x20, 0x40, 0x80];

// Generate all 256 pattern vectors
for (int code = 0; code < 256; code++)
    for (int dot = 0; dot < 8; dot++)
        vectorData[code * 8 + dot] = (code & DotBits[dot]) != 0 ? 1.0f : 0.0f;
```

### SIMD Brute-Force Matching

To find the best braille character for a sampled cell, we compute the **squared Euclidean distance** between the 8D sample vector and all 256 pattern vectors, selecting the minimum. With only 256 candidates × 8 dimensions, brute force is faster than any tree structure—and it vectorizes perfectly.

On CPUs with AVX support, all 8 floats fit in a single `Vector256<float>`, making the inner loop a single SIMD subtraction, multiplication, and horizontal sum:

```csharp
// Load 8-float sample vector once
var targetVec = Vector256.Create(
    target[0], target[1], target[2], target[3],
    target[4], target[5], target[6], target[7]);

// Compare against all 256 braille patterns
for (int i = 0; i < 256; i++)
{
    var charVec = Vector256.Create(/* 8 floats for pattern i */);
    var diff = targetVec - charVec;
    var squared = diff * diff;
    var dist = Vector256.Sum(squared);  // Single instruction on AVX

    if (dist < bestDist) { bestDist = dist; bestIdx = i; }
}
```

### Quantized Caching

Many cells produce similar coverage vectors (e.g., solid regions of an image). To avoid redundant matching, vectors are **quantized to 4 bits per component** and used as cache keys:

```
8 components × 4 bits = 32 bits → fits in a single int
```

Each component value (0.0–1.0) maps to one of 16 levels (0–15). This gives a maximum of 2³² ≈ 4 billion possible keys, but in practice images produce far fewer unique patterns. The cache uses a `ConcurrentDictionary<int, char>` for thread-safe access during parallel rendering.

### Worked Example

Consider a cell from a diagonal edge where the top-left is dark and bottom-right is bright:

```
Pixel brightness:        Sampled coverage (13-point rings):
┌──────┬──────┐          ┌──────┬──────┐
│ 0.05 │ 0.25 │          │ 0.92 │ 0.71 │
├──────┼──────┤          ├──────┼──────┤
│ 0.10 │ 0.45 │   →      │ 0.88 │ 0.52 │
├──────┼──────┤          ├──────┼──────┤
│ 0.40 │ 0.80 │          │ 0.57 │ 0.18 │
├──────┼──────┤          ├──────┼──────┤
│ 0.75 │ 0.95 │          │ 0.23 │ 0.04 │
└──────┴──────┘          └──────┴──────┘

Sample vector: [0.92, 0.71, 0.88, 0.52, 0.57, 0.18, 0.23, 0.04]

Closest patterns (by squared distance):
  ⠛ (0x1B) = [1,1,1,1,0,0,0,0]  dist = 0.41   ← top half
  ⡇ (0x47) = [1,0,1,0,1,0,1,0]  dist = 0.93
  ⠫ (0x2B) = [1,1,0,1,1,0,0,0]  dist = 0.72
  ⠻ (0x3B) = [1,1,0,1,1,1,0,0]  dist = 0.36   ← best match ✓

Result: ⠻ (top-left 5 dots lit, bottom-right 3 dots off)
```

The shape matcher picks `⠻` because its dot pattern best approximates the continuous coverage gradient—better than simple thresholding which might produce `⠛` (hard top/bottom split) or `⣿`/`⠀` (all-or-nothing).

### Comparison: ASCII 6D vs Braille 8D

| Aspect | ASCII (`CharacterMap`) | Braille (`BrailleCharacterMap`) |
|--------|----------------------|-------------------------------|
| **Vector dimensions** | 6D (3×2 staggered grid) | 8D (2×4 dot grid) |
| **Character count** | 95 (printable ASCII) | 256 (all braille patterns) |
| **Vector generation** | Font rendering (needs `.ttf`) | Mathematical (Unicode-defined) |
| **Sampling method** | 37 points per circle, 6 circles | 13 points per dot, 8 dots |
| **Matching strategy** | KD-tree + SIMD brute force | SIMD brute force (256 is small) |
| **Cache key** | 5 bits × 6 = 30 bits | 4 bits × 8 = 32 bits |
| **Resolution** | 1 pixel per cell | 8 pixels per cell |

The ASCII renderer uses a **KD-tree** for fast lookup among 95 characters in 6D space. The braille renderer skips the tree entirely—with only 256 patterns and AVX processing all 8 dimensions in one instruction, linear scan is faster than tree traversal overhead.

Both approaches share the same insight from Alex Harri's algorithm: represent visual patterns as vectors in a metric space, then find the nearest neighbor. The difference is that ASCII must render each font glyph to discover its shape, while braille patterns are defined by the Unicode standard and can be generated purely from the bit encoding.

## Color Handling: The Hybrid Approach

Plain braille (white dots on black) works, but color adds tremendous visual impact. However, terminal colors present a challenge: we can only set **one foreground color** per character, but our braille character represents **up to 8 different source pixels**.

### The Naive Approach (and why it fails)

Averaging all 8 pixel colors produces a "solarized" look—colors mix into muddy browns and grays that don't match the source image.

### The Hybrid Solution

We only sample colors from pixels where dots are actually **lit**:

```mermaid
flowchart TD
    A[2×4 Pixel Block] --> B{Which pixels<br/>are lit?}
    B -->|"Dots 1,4,7"| C[Sample only those 3 pixels]
    B -->|"Dots 2,3,5,6,8"| D[Sample only those 5 pixels]
    C --> E[Average lit pixel colors]
    D --> E
    E --> F[Apply color boost]
    F --> G[Set as foreground color]

    style A stroke:#4a9eff
    style B stroke:#ff6600
    style E stroke:#00aa00
    style F stroke:#00aa00
```

This ensures the displayed color matches what the user actually sees—the lit dots.

```csharp
private static Rgba32 ComputeHybridColor(
    ReadOnlySpan<Rgba32> pixels,
    ReadOnlySpan<byte> gray,
    int brailleCode,
    byte threshold)
{
    int totalR = 0, totalG = 0, totalB = 0;
    int litCount = 0;

    // Only sample colors from pixels that will be displayed as dots
    for (int i = 0; i < 8; i++)
    {
        bool isLit = (brailleCode & (1 << GetBitPosition(i))) != 0;
        if (isLit)
        {
            totalR += pixels[i].R;
            totalG += pixels[i].G;
            totalB += pixels[i].B;
            litCount++;
        }
    }

    if (litCount == 0)
        return new Rgba32(0, 0, 0);

    return new Rgba32(
        (byte)(totalR / litCount),
        (byte)(totalG / litCount),
        (byte)(totalB / litCount));
}
```

### Color Boosting

Braille characters are inherently sparse—a character with only 2-3 dots lit appears dimmer than a solid block. To compensate, we apply **color boosting**:

- **Saturation boost**: +25% (makes colors more vivid)
- **Brightness boost**: +15% (compensates for sparse dots)

```mermaid
graph LR
    A[Original Color] --> B[Convert to HSL]
    B --> C["S × 1.25"]
    C --> D["L × 1.15"]
    D --> E[Convert to RGB]
    E --> F[Boosted Color]

    style A stroke:#4a9eff
    style C stroke:#ff6600
    style D stroke:#ff6600
    style F stroke:#00aa00
```

The boost amount is configurable:

```csharp
private static Rgba32 BoostColor(Rgba32 color, float saturationBoost, float brightnessBoost)
{
    // Convert to HSL
    float r = color.R / 255f;
    float g = color.G / 255f;
    float b = color.B / 255f;

    float max = Math.Max(r, Math.Max(g, b));
    float min = Math.Min(r, Math.Min(g, b));
    float l = (max + min) / 2;
    float s = 0;
    float h = 0;

    if (max != min)
    {
        float d = max - min;
        s = l > 0.5f ? d / (2 - max - min) : d / (max + min);
        // ... calculate hue
    }

    // Apply boosts
    s = Math.Min(1f, s * saturationBoost);
    l = Math.Min(1f, l * brightnessBoost);

    // Convert back to RGB
    return HslToRgb(h, s, l);
}
```

## Video Optimization: Delta Rendering

For animations and video, redrawing every character every frame is wasteful. Most frames only differ slightly from the previous one. **Delta rendering** tracks changes at the cell level:

```mermaid
sequenceDiagram
    participant F1 as Frame N
    participant F2 as Frame N+1
    participant Out as Output

    F1->>Out: Full render (all cells)
    Note over F1,Out: ⣿⡇⢸⣿⡇⢸⣿
    F2->>F2: Compare to previous
    F2->>Out: Only changed cells
    Note over F2,Out: Move to (3,2), output ⣏
```

The algorithm:

1. **Store previous frame** in a cell buffer (character + color per cell)
2. **For each cell in new frame**:
   - Compare character AND color to previous
   - If identical, skip (no output)
   - If different, output cursor move + new character
3. **Track cursor position** to minimize escape sequences

```csharp
public string RenderFrameDelta(Image<Rgba32> image, string[]? previousFrame)
{
    var currentFrame = RenderToGrid(image);
    var output = new StringBuilder();

    int lastOutputRow = -1;
    int lastOutputCol = -1;

    for (int row = 0; row < height; row++)
    {
        for (int col = 0; col < width; col++)
        {
            string cell = currentFrame[row * width + col];
            string? prevCell = previousFrame?[row * width + col];

            if (cell == prevCell)
                continue;  // Skip unchanged cells

            // Move cursor if needed
            if (row != lastOutputRow || col != lastOutputCol)
            {
                output.Append($"\x1b[{row + 1};{col + 1}H");
            }

            output.Append(cell);
            lastOutputRow = row;
            lastOutputCol = col + 1;
        }
    }

    return output.ToString();
}
```

This optimization typically reduces output by **70-90%** for video content where most of the frame is static.

## Performance Optimizations

### Parallel Processing

For larger images, we process rows in parallel:

```csharp
if (pixelHeight > 64)  // Worth parallelizing
{
    Parallel.For(0, cellHeight, row =>
    {
        ProcessRow(row, buffer, width, threshold, output);
    });
}
else
{
    for (int row = 0; row < cellHeight; row++)
    {
        ProcessRow(row, buffer, width, threshold, output);
    }
}
```

### Stack Allocation

For small buffers (histograms, pixel blocks), we use stack allocation to avoid heap pressure:

```csharp
Span<int> histogram = stackalloc int[256];
Span<Rgba32> cellPixels = stackalloc Rgba32[8];
```

### Lookup Tables

Bit position mapping uses a precomputed lookup table:

```csharp
private static readonly int[] BitPositions = { 0, 1, 2, 6, 3, 4, 5, 7 };

private static int GetBitPosition(int dotIndex)
{
    return BitPositions[dotIndex];
}
```

## Results

The braille renderer produces remarkably detailed output:

| Mode | Resolution per Cell | Best For |
|------|---------------------|----------|
| ASCII | 1×1 | Compatibility, artistic style |
| ColorBlocks | 1×2 | Photos, high color fidelity |
| **Braille** | **2×4** | **Maximum detail, line art, text** |

### Example Comparison

For a 100×50 character output:

| Mode | Effective Pixels |
|------|------------------|
| ASCII | 100 × 50 = 5,000 |
| ColorBlocks | 100 × 100 = 10,000 |
| Braille | 200 × 200 = 40,000 |

Braille provides **8x the resolution** of ASCII and **4x the resolution** of color blocks.

## Conclusion

The braille rendering technique combines several clever algorithms:

1. **Unicode braille encoding** packs 8 dots into each character
2. **Otsu's method** automatically finds the optimal threshold
3. **Atkinson dithering** creates smooth gradients with sharp edges
4. **Concentric ring sampling** converts pixel regions to continuous 8D coverage vectors
5. **SIMD shape vector matching** finds the closest braille pattern across all 256 candidates
6. **Quantized caching** avoids redundant matching for similar pixel regions
7. **Hybrid color sampling** matches displayed colors to lit dots
8. **Color boosting** compensates for sparse dot patterns
9. **Delta rendering** optimizes video/animation playback

The vector matching step (4–6) is what elevates braille output beyond simple thresholding. By treating each cell as an 8-dimensional shape problem, the renderer makes holistic decisions about dot patterns rather than independent per-pixel binary choices. This produces smoother edges, better detail preservation, and more temporally stable output for video.

## References

- [Unicode Braille Patterns Block](https://unicode.org/charts/PDF/U2800.pdf)
- [Otsu's Method (Wikipedia)](https://en.wikipedia.org/wiki/Otsu%27s_method)
- [Atkinson Dithering](https://beyondloom.com/blog/dither.html)
- [Original ASCII Rendering Algorithm by Alex Harri](https://alexharri.com/blog/ascii-rendering)
