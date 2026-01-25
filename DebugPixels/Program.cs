using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

var imagePath = args.Length > 0 ? args[0] : @"E:\source\vectorascii\ConsoleImage\sami_flag.png";

Console.WriteLine($"Loading: {imagePath}");

using var image = Image.Load<Rgba32>(imagePath);
Console.WriteLine($"Original Size: {image.Width}x{image.Height}");

// Resize like braille renderer does (2x4 dots per char, 60 chars wide)
var charWidth = 60;
var charHeight = 30;
var pixelWidth = charWidth * 2;
var pixelHeight = charHeight * 4;

using var resized = image.Clone(ctx => ctx.Resize(pixelWidth, pixelHeight));
Console.WriteLine($"Resized to: {resized.Width}x{resized.Height}");

// Extract brightness and colors like BrailleRenderer.PrecomputePixelData
var totalPixels = pixelWidth * pixelHeight;
var brightness = new float[totalPixels];
var colors = new Rgba32[totalPixels];

resized.ProcessPixelRows(accessor =>
{
    for (var y = 0; y < accessor.Height; y++)
    {
        var row = accessor.GetRowSpan(y);
        var rowOffset = y * pixelWidth;
        for (var x = 0; x < row.Length; x++)
        {
            var pixel = row[x];
            brightness[rowOffset + x] = (0.299f * pixel.R + 0.587f * pixel.G + 0.114f * pixel.B) / 255f;
            colors[rowOffset + x] = pixel;
        }
    }
});

// Sample a few cells and show what colors we get
Console.WriteLine("\nSampling braille cells (2x4 pixels each):");

int[] sampleCx = { 5, 20, 40 }; // char x positions
int[] sampleCy = { 5, 15 }; // char y positions

foreach (var cy in sampleCy)
{
    foreach (var cx in sampleCx)
    {
        var px = cx * 2;
        var py = cy * 4;

        int totalR = 0, totalG = 0, totalB = 0;
        var colorCount = 0;

        Console.WriteLine($"\n  Cell ({cx},{cy}) - pixels ({px},{py}) to ({px+1},{py+3}):");

        for (var dy = 0; dy < 4; dy++)
        {
            for (var dx = 0; dx < 2; dx++)
            {
                var idx = (py + dy) * pixelWidth + (px + dx);
                var c = colors[idx];
                totalR += c.R;
                totalG += c.G;
                totalB += c.B;
                colorCount++;
                Console.WriteLine($"    [{dx},{dy}]: R={c.R,3} G={c.G,3} B={c.B,3}");
            }
        }

        var avgR = (byte)(totalR / colorCount);
        var avgG = (byte)(totalG / colorCount);
        var avgB = (byte)(totalB / colorCount);
        Console.WriteLine($"    Average: R={avgR,3} G={avgG,3} B={avgB,3}");
    }
}
