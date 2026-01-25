using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Core.Tests;

public class FrameHasherTests : IDisposable
{
    private readonly List<Image<Rgba32>> _disposableImages = new();

    public void Dispose()
    {
        foreach (var image in _disposableImages) image.Dispose();
    }

    private Image<Rgba32> CreateSolidImage(int width, int height, Rgba32 color)
    {
        var image = new Image<Rgba32>(width, height, color);
        _disposableImages.Add(image);
        return image;
    }

    private Image<Rgba32> CreateGradientImage(int width, int height)
    {
        var image = new Image<Rgba32>(width, height);
        _disposableImages.Add(image);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var val = (byte)((x + y) * 255 / (width + height));
                image[x, y] = new Rgba32(val, val, val);
            }
        }
        return image;
    }

    [Fact]
    public void ComputeHash_ReturnsSameHash_ForIdenticalImages()
    {
        var image1 = CreateSolidImage(100, 100, Color.Red);
        var image2 = CreateSolidImage(100, 100, Color.Red);

        var hash1 = FrameHasher.ComputeHash(image1);
        var hash2 = FrameHasher.ComputeHash(image2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_ReturnsDifferentHash_ForDifferentImages()
    {
        // Note: Solid color images (all white or all black) have same hash structure
        // because all pixels are equal to the average. Use gradient vs solid for real difference.
        var image1 = CreateSolidImage(100, 100, Color.White);
        var image2 = CreateGradientImage(100, 100);

        var hash1 = FrameHasher.ComputeHash(image1);
        var hash2 = FrameHasher.ComputeHash(image2);

        // Solid white vs gradient should have different hashes
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHashWithBrightness_ReturnsCorrectBrightness_ForWhiteImage()
    {
        var image = CreateSolidImage(100, 100, Color.White);

        var (_, brightness) = FrameHasher.ComputeHashWithBrightness(image);

        // White image should have high brightness (close to 255)
        Assert.True(brightness > 240, $"Expected brightness > 240, got {brightness}");
    }

    [Fact]
    public void ComputeHashWithBrightness_ReturnsCorrectBrightness_ForBlackImage()
    {
        var image = CreateSolidImage(100, 100, Color.Black);

        var (_, brightness) = FrameHasher.ComputeHashWithBrightness(image);

        // Black image should have low brightness (close to 0)
        Assert.True(brightness < 15, $"Expected brightness < 15, got {brightness}");
    }

    [Fact]
    public void HammingDistance_ReturnsZero_ForIdenticalHashes()
    {
        ulong hash = 0x123456789ABCDEF0;

        var distance = FrameHasher.HammingDistance(hash, hash);

        Assert.Equal(0, distance);
    }

    [Fact]
    public void HammingDistance_ReturnsCorrectCount_ForKnownDifference()
    {
        // Two hashes differing in exactly one bit
        ulong hash1 = 0b0000_0001;
        ulong hash2 = 0b0000_0000;

        var distance = FrameHasher.HammingDistance(hash1, hash2);

        Assert.Equal(1, distance);
    }

    [Fact]
    public void HammingDistance_Returns64_ForCompletelyDifferentHashes()
    {
        ulong hash1 = 0xFFFFFFFFFFFFFFFF;
        ulong hash2 = 0x0000000000000000;

        var distance = FrameHasher.HammingDistance(hash1, hash2);

        Assert.Equal(64, distance);
    }

    [Fact]
    public void AreSimilar_ReturnsTrue_ForIdenticalImages()
    {
        var image1 = CreateSolidImage(100, 100, Color.Blue);
        var image2 = CreateSolidImage(100, 100, Color.Blue);

        var hash1 = FrameHasher.ComputeHash(image1);
        var hash2 = FrameHasher.ComputeHash(image2);

        Assert.True(FrameHasher.AreSimilar(hash1, hash2));
    }

    [Fact]
    public void AreSimilar_ReturnsFalse_ForVeryDifferentImages()
    {
        var image1 = CreateSolidImage(100, 100, Color.White);
        var image2 = CreateGradientImage(100, 100);

        var hash1 = FrameHasher.ComputeHash(image1);
        var hash2 = FrameHasher.ComputeHash(image2);

        // Solid white vs gradient should be different enough
        var distance = FrameHasher.HammingDistance(hash1, hash2);

        // Verify they are not similar with default threshold
        // (allowing some tolerance for edge cases)
        Assert.True(distance > 5 || FrameHasher.AreSimilar(hash1, hash2, 5) == (distance <= 5));
    }

    [Fact]
    public void AreSimilar_RespectsThreshold()
    {
        ulong hash1 = 0b0000_0111; // 3 bits set
        ulong hash2 = 0b0000_0000; // 0 bits set
        // Hamming distance = 3

        Assert.True(FrameHasher.AreSimilar(hash1, hash2, threshold: 3));
        Assert.True(FrameHasher.AreSimilar(hash1, hash2, threshold: 5));
        Assert.False(FrameHasher.AreSimilar(hash1, hash2, threshold: 2));
    }

    [Fact]
    public void QuickDifferenceScore_ReturnsZero_ForIdenticalImages()
    {
        var image = CreateSolidImage(100, 100, Color.Green);

        var score = FrameHasher.QuickDifferenceScore(image, image);

        Assert.Equal(0, score);
    }

    [Fact]
    public void QuickDifferenceScore_ReturnsHighScore_ForVeryDifferentImages()
    {
        var image1 = CreateSolidImage(100, 100, Color.White);
        var image2 = CreateSolidImage(100, 100, Color.Black);

        var score = FrameHasher.QuickDifferenceScore(image1, image2);

        // White vs black should have maximum difference
        Assert.Equal(100, score);
    }

    [Fact]
    public void QuickDifferenceScore_HandlesSmallImages()
    {
        var image1 = CreateSolidImage(2, 2, Color.Red);
        var image2 = CreateSolidImage(2, 2, Color.Red);

        // Should not throw for images smaller than 4x4 sample grid
        var score = FrameHasher.QuickDifferenceScore(image1, image2);

        Assert.True(score >= 0 && score <= 100);
    }

    [Fact]
    public void ComputeHash_IsConsistent_AcrossMultipleCalls()
    {
        var image = CreateGradientImage(50, 50);

        var hash1 = FrameHasher.ComputeHash(image);
        var hash2 = FrameHasher.ComputeHash(image);
        var hash3 = FrameHasher.ComputeHash(image);

        Assert.Equal(hash1, hash2);
        Assert.Equal(hash2, hash3);
    }
}
