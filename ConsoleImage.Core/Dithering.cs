// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Floyd-Steinberg dithering implementation
// Optimized: in-place dithering to reduce allocations, serpentine scanning.
// Reference: https://en.wikipedia.org/wiki/Floyd–Steinberg_dithering

namespace ConsoleImage.Core;

/// <summary>
///     Floyd-Steinberg dithering for smoother gradient rendering in ASCII art.
///     Spreads quantization error to neighboring pixels for perceptually better output.
/// </summary>
public static class Dithering
{
    /// <summary>
    ///     Apply Floyd-Steinberg dithering to a grayscale values array.
    ///     The dithering spreads quantization error to produce smoother gradients.
    /// </summary>
    /// <param name="values">2D array of grayscale values (0-1 range)</param>
    /// <param name="levels">Number of quantization levels (e.g., number of characters)</param>
    /// <returns>Quantized values array with dithering applied</returns>
    public static float[,] ApplyFloydSteinberg(float[,] values, int levels)
    {
        var height = values.GetLength(0);
        var width = values.GetLength(1);

        // Work on a copy to avoid modifying input
        var result = new float[height, width];
        Array.Copy(values, result, values.Length);

        ApplyFloydSteinbergInPlace(result, levels);
        return result;
    }

    /// <summary>
    ///     Apply Floyd-Steinberg dithering in-place (modifies the input array).
    ///     Avoids allocation of a copy array when caller owns the buffer.
    /// </summary>
    public static void ApplyFloydSteinbergInPlace(float[,] values, int levels)
    {
        var height = values.GetLength(0);
        var width = values.GetLength(1);
        var step = 1.0f / (levels - 1);

        // Process with serpentine scanning (alternating direction each row)
        for (var y = 0; y < height; y++)
        {
            var leftToRight = y % 2 == 0;

            var xStart = leftToRight ? 0 : width - 1;
            var xEnd = leftToRight ? width : -1;
            var xStep = leftToRight ? 1 : -1;

            for (var x = xStart; x != xEnd; x += xStep)
            {
                var oldValue = values[y, x];

                // Quantize to nearest level
                var newValue = MathF.Round(oldValue / step) * step;
                newValue = Math.Clamp(newValue, 0, 1);

                values[y, x] = newValue;

                // Calculate quantization error
                var error = oldValue - newValue;

                // Distribute error to neighbors using Floyd-Steinberg coefficients
                // Standard pattern (when going left-to-right):
                //       [*] [7/16]
                // [3/16][5/16][1/16]

                if (leftToRight)
                {
                    if (x + 1 < width)
                        values[y, x + 1] += error * (7f / 16f);
                    if (y + 1 < height && x - 1 >= 0)
                        values[y + 1, x - 1] += error * (3f / 16f);
                    if (y + 1 < height)
                        values[y + 1, x] += error * (5f / 16f);
                    if (y + 1 < height && x + 1 < width)
                        values[y + 1, x + 1] += error * (1f / 16f);
                }
                else
                {
                    if (x - 1 >= 0)
                        values[y, x - 1] += error * (7f / 16f);
                    if (y + 1 < height && x + 1 < width)
                        values[y + 1, x + 1] += error * (3f / 16f);
                    if (y + 1 < height)
                        values[y + 1, x] += error * (5f / 16f);
                    if (y + 1 < height && x - 1 >= 0)
                        values[y + 1, x - 1] += error * (1f / 16f);
                }
            }
        }
    }

    /// <summary>
    ///     Apply Floyd-Steinberg dithering to shape vectors.
    ///     Each component of the 6D vector is dithered independently.
    ///     Optimized: uses in-place dithering to avoid 6 extra copy arrays.
    /// </summary>
    public static ShapeVector[,] ApplyToShapeVectors(ShapeVector[,] vectors, int levels)
    {
        var height = vectors.GetLength(0);
        var width = vectors.GetLength(1);

        // Extract each component into a separate 2D array
        var components = new float[6][,];
        for (var c = 0; c < 6; c++)
        {
            components[c] = new float[height, width];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                components[c][y, x] = vectors[y, x][c];
        }

        // Apply dithering in-place (no copy arrays needed)
        for (var c = 0; c < 6; c++)
            ApplyFloydSteinbergInPlace(components[c], levels);

        // Reconstruct shape vectors
        var result = new ShapeVector[height, width];
        Span<float> vals = stackalloc float[6];

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            for (var c = 0; c < 6; c++) vals[c] = Math.Clamp(components[c][y, x], 0, 1);
            result[y, x] = new ShapeVector(vals);
        }

        return result;
    }
}
