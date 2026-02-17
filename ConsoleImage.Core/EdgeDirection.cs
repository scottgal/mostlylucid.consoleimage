// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Edge-direction aware character selection
// Reference: https://github.com/dijkstracula/Asciimatic

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Core;

/// <summary>
///     Edge direction detection for selecting directional ASCII characters.
///     Uses Sobel operator to detect edge angles and maps them to appropriate characters.
/// </summary>
public static class EdgeDirection
{
    // Directional characters mapped by angle range
    // Angles are in radians, ranging from -PI to PI
    private static readonly (float minAngle, float maxAngle, char character)[] DirectionalChars =
    [
        (-MathF.PI / 8, MathF.PI / 8, '-'), // Horizontal (0 deg)
        (MathF.PI / 8, 3 * MathF.PI / 8, '/'), // Diagonal up-right (45 deg)
        (3 * MathF.PI / 8, 5 * MathF.PI / 8, '|'), // Vertical (90 deg)
        (5 * MathF.PI / 8, 7 * MathF.PI / 8, '\\'), // Diagonal up-left (135 deg)
        (-3 * MathF.PI / 8, -MathF.PI / 8, '\\'), // Diagonal down-right (-45 deg)
        (-5 * MathF.PI / 8, -3 * MathF.PI / 8, '|'), // Vertical (-90 deg)
        (-7 * MathF.PI / 8, -5 * MathF.PI / 8, '/') // Diagonal down-left (-135 deg)
    ];

    // Sobel kernels for edge detection
    private static readonly int[,] SobelX = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
    private static readonly int[,] SobelY = { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };

    /// <summary>
    ///     Compute edge magnitude and direction for each cell in the image.
    /// </summary>
    /// <param name="image">Source image</param>
    /// <param name="outputWidth">Number of output columns (cells)</param>
    /// <param name="outputHeight">Number of output rows (cells)</param>
    /// <returns>Tuple of (magnitudes, angles) arrays</returns>
    public static (float[,] magnitudes, float[,] angles) ComputeEdges(
        Image<Rgba32> image, int outputWidth, int outputHeight)
    {
        var cellWidth = image.Width / outputWidth;
        var cellHeight = image.Height / outputHeight;

        var magnitudes = new float[outputHeight, outputWidth];
        var angles = new float[outputHeight, outputWidth];

        // Convert to linear-light grayscale for physically correct edge detection
        var grayscale = new float[image.Height, image.Width];
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    grayscale[y, x] = BrightnessHelper.GetLinearBrightness(p);
                }
            }
        });

        // Apply lightweight unsharp mask on linear luminance to sharpen edges before Sobel.
        // USM = original + amount * (original - blurred), with radius-1 box blur.
        const float usmAmount = 0.25f;
        var sharpened = new float[image.Height, image.Width];
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            // 3x3 box blur (radius 1)
            float sum = 0;
            var count = 0;
            for (var ky = -1; ky <= 1; ky++)
            for (var kx = -1; kx <= 1; kx++)
            {
                var ny = y + ky;
                var nx = x + kx;
                if (ny >= 0 && ny < image.Height && nx >= 0 && nx < image.Width)
                {
                    sum += grayscale[ny, nx];
                    count++;
                }
            }

            var blurred = sum / count;
            var val = grayscale[y, x] + usmAmount * (grayscale[y, x] - blurred);
            sharpened[y, x] = Math.Clamp(val, 0f, 1f);
        }

        grayscale = sharpened;

        // Compute edge gradients for each cell
        for (var cy = 0; cy < outputHeight; cy++)
        for (var cx = 0; cx < outputWidth; cx++)
        {
            var startX = cx * cellWidth;
            var startY = cy * cellHeight;
            var endX = Math.Min(startX + cellWidth, image.Width);
            var endY = Math.Min(startY + cellHeight, image.Height);

            float totalGx = 0, totalGy = 0;
            var samples = 0;

            // Sample multiple points in the cell
            for (var y = startY + 1; y < endY - 1; y++)
            for (var x = startX + 1; x < endX - 1; x++)
            {
                float gx = 0, gy = 0;

                // Apply Sobel kernels
                for (var ky = -1; ky <= 1; ky++)
                for (var kx = -1; kx <= 1; kx++)
                {
                    var ix = x + kx;
                    var iy = y + ky;

                    if (ix >= 0 && ix < image.Width && iy >= 0 && iy < image.Height)
                    {
                        var pixel = grayscale[iy, ix];
                        gx += pixel * SobelX[ky + 1, kx + 1];
                        gy += pixel * SobelY[ky + 1, kx + 1];
                    }
                }

                totalGx += gx;
                totalGy += gy;
                samples++;
            }

            if (samples > 0)
            {
                var avgGx = totalGx / samples;
                var avgGy = totalGy / samples;

                // Magnitude: strength of edge
                magnitudes[cy, cx] = MathF.Sqrt(avgGx * avgGx + avgGy * avgGy);

                // Angle: direction of edge (perpendicular to gradient)
                // Add PI/2 to rotate from gradient direction to edge direction
                var angle = MathF.Atan2(avgGy, avgGx) + MathF.PI / 2;
                // Normalize to [-PI, PI]
                if (angle > MathF.PI) angle -= 2 * MathF.PI;
                else if (angle < -MathF.PI) angle += 2 * MathF.PI;
                angles[cy, cx] = angle;
            }
        }

        // Normalize magnitudes to 0-1 range
        float maxMag = 0;
        foreach (var m in magnitudes)
            if (m > maxMag)
                maxMag = m;

        if (maxMag > 0)
            for (var y = 0; y < outputHeight; y++)
            for (var x = 0; x < outputWidth; x++)
                magnitudes[y, x] /= maxMag;

        return (magnitudes, angles);
    }

    /// <summary>
    ///     Get the appropriate directional character for an edge angle.
    /// </summary>
    /// <param name="angle">Edge angle in radians (-PI to PI)</param>
    /// <param name="magnitude">Edge magnitude (0-1), used to decide if edge is strong enough</param>
    /// <param name="threshold">Minimum magnitude to use directional char (default: 0.2)</param>
    /// <returns>Directional character, or null if edge is too weak</returns>
    public static char? GetDirectionalChar(float angle, float magnitude, float threshold = 0.2f)
    {
        if (magnitude < threshold)
            return null;

        // Normalize angle to handle wraparound at +/- PI
        // Check the angle ranges for each directional character
        foreach (var (minAngle, maxAngle, character) in DirectionalChars)
            if (angle >= minAngle && angle < maxAngle)
                return character;

        // Handle wraparound near +/- PI (both map to horizontal)
        if (angle >= 7 * MathF.PI / 8 || angle < -7 * MathF.PI / 8)
            return '-';

        return null;
    }

    /// <summary>
    ///     Blend a shape-matched character with an edge-direction character.
    ///     Strong edges use directional chars, weak edges use shape-matched chars.
    /// </summary>
    /// <param name="shapeMatchedChar">Character from shape matching algorithm</param>
    /// <param name="angle">Edge angle in radians</param>
    /// <param name="magnitude">Edge magnitude (0-1)</param>
    /// <param name="blendThreshold">Threshold for using directional char (default: 0.3)</param>
    /// <returns>Final character to use</returns>
    public static char BlendCharacter(char shapeMatchedChar, float angle, float magnitude, float blendThreshold = 0.3f)
    {
        var directionalChar = GetDirectionalChar(angle, magnitude, blendThreshold);
        return directionalChar ?? shapeMatchedChar;
    }
}