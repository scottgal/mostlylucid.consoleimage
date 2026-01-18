// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Floyd-Steinberg dithering implementation
// Reference: https://en.wikipedia.org/wiki/Floyd–Steinberg_dithering

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Core;

/// <summary>
/// Floyd-Steinberg dithering for smoother gradient rendering in ASCII art.
/// Spreads quantization error to neighboring pixels for perceptually better output.
/// </summary>
public static class Dithering
{
    /// <summary>
    /// Apply Floyd-Steinberg dithering to a grayscale values array.
    /// The dithering spreads quantization error to produce smoother gradients.
    /// </summary>
    /// <param name="values">2D array of grayscale values (0-1 range)</param>
    /// <param name="levels">Number of quantization levels (e.g., number of characters)</param>
    /// <returns>Quantized values array with dithering applied</returns>
    public static float[,] ApplyFloydSteinberg(float[,] values, int levels)
    {
        int height = values.GetLength(0);
        int width = values.GetLength(1);

        // Work on a copy to avoid modifying input
        var result = new float[height, width];
        Array.Copy(values, result, values.Length);

        float step = 1.0f / (levels - 1);

        // Process with serpentine scanning (alternating direction each row)
        for (int y = 0; y < height; y++)
        {
            bool leftToRight = (y % 2) == 0;

            int xStart = leftToRight ? 0 : width - 1;
            int xEnd = leftToRight ? width : -1;
            int xStep = leftToRight ? 1 : -1;

            for (int x = xStart; x != xEnd; x += xStep)
            {
                float oldValue = result[y, x];

                // Quantize to nearest level
                float newValue = MathF.Round(oldValue / step) * step;
                newValue = Math.Clamp(newValue, 0, 1);

                result[y, x] = newValue;

                // Calculate quantization error
                float error = oldValue - newValue;

                // Distribute error to neighbors using Floyd-Steinberg coefficients
                // Standard pattern (when going left-to-right):
                //       [*] [7/16]
                // [3/16][5/16][1/16]

                if (leftToRight)
                {
                    // Right neighbor: 7/16
                    if (x + 1 < width)
                        result[y, x + 1] += error * (7f / 16f);

                    // Below-left: 3/16
                    if (y + 1 < height && x - 1 >= 0)
                        result[y + 1, x - 1] += error * (3f / 16f);

                    // Below: 5/16
                    if (y + 1 < height)
                        result[y + 1, x] += error * (5f / 16f);

                    // Below-right: 1/16
                    if (y + 1 < height && x + 1 < width)
                        result[y + 1, x + 1] += error * (1f / 16f);
                }
                else
                {
                    // Going right-to-left (mirrored pattern)
                    // Left neighbor: 7/16
                    if (x - 1 >= 0)
                        result[y, x - 1] += error * (7f / 16f);

                    // Below-right: 3/16
                    if (y + 1 < height && x + 1 < width)
                        result[y + 1, x + 1] += error * (3f / 16f);

                    // Below: 5/16
                    if (y + 1 < height)
                        result[y + 1, x] += error * (5f / 16f);

                    // Below-left: 1/16
                    if (y + 1 < height && x - 1 >= 0)
                        result[y + 1, x - 1] += error * (1f / 16f);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Apply Floyd-Steinberg dithering to shape vectors.
    /// Each component of the 6D vector is dithered independently.
    /// </summary>
    public static ShapeVector[,] ApplyToShapeVectors(ShapeVector[,] vectors, int levels)
    {
        int height = vectors.GetLength(0);
        int width = vectors.GetLength(1);

        // Extract each component into a separate 2D array
        var components = new float[6][,];
        for (int c = 0; c < 6; c++)
        {
            components[c] = new float[height, width];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    components[c][y, x] = vectors[y, x][c];
                }
            }
        }

        // Apply dithering to each component
        var ditheredComponents = new float[6][,];
        for (int c = 0; c < 6; c++)
        {
            ditheredComponents[c] = ApplyFloydSteinberg(components[c], levels);
        }

        // Reconstruct shape vectors
        var result = new ShapeVector[height, width];
        Span<float> vals = stackalloc float[6];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                for (int c = 0; c < 6; c++)
                {
                    vals[c] = Math.Clamp(ditheredComponents[c][y, x], 0, 1);
                }
                result[y, x] = new ShapeVector(vals);
            }
        }

        return result;
    }
}
