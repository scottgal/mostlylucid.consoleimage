// SmartFrameSampler - Intelligent frame skipping using perceptual hashing
// Skips visually similar frames while maintaining proper timing
// Optimized for streaming: hash computation runs ahead of rendering

using System.Collections.Concurrent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Core;

/// <summary>
/// Intelligent frame sampling that skips visually similar frames.
/// Uses perceptual hashing to detect similar frames and an LFU cache
/// to reuse previously rendered content.
///
/// Optimized for streaming playback:
/// - Hash computation is fast (8x8 resize + comparison)
/// - Rendering only happens for frames that differ
/// - Previous frame content is reused for skipped frames
/// </summary>
public class SmartFrameSampler
{
    private readonly int _hashSimilarityThreshold;
    private readonly int _maxCacheSize;
    private readonly ConcurrentDictionary<ulong, CachedFrame> _frameCache = new();
    private readonly object _stateLock = new();

    private ulong? _lastFrameHash;
    private int _lastFrameBrightness;
    private string? _lastRenderedContent;
    private int _framesProcessed;
    private int _framesSkipped;
    private int _hashComputations;

    // Brightness tolerance for matching (prevents dark/light frame confusion)
    private const int BrightnessThreshold = 20;

    /// <summary>
    /// Enable debug output to stderr.
    /// </summary>
    public bool DebugMode { get; set; }

    /// <summary>
    /// Create a new smart frame sampler.
    /// </summary>
    /// <param name="hashSimilarityThreshold">Max Hamming distance to consider frames similar (0-64, default: 2)</param>
    /// <param name="maxCacheSize">Maximum number of rendered frames to cache (default: 32)</param>
    public SmartFrameSampler(int hashSimilarityThreshold = 2, int maxCacheSize = 32)
    {
        _hashSimilarityThreshold = Math.Clamp(hashSimilarityThreshold, 0, 64);
        _maxCacheSize = Math.Max(1, maxCacheSize);
    }

    /// <summary>
    /// Number of frames processed so far.
    /// </summary>
    public int FramesProcessed => _framesProcessed;

    /// <summary>
    /// Number of frames skipped (reused from cache or previous frame).
    /// </summary>
    public int FramesSkipped => _framesSkipped;

    /// <summary>
    /// Number of hash computations performed.
    /// </summary>
    public int HashComputations => _hashComputations;

    /// <summary>
    /// Cache hit ratio (0.0 - 1.0).
    /// </summary>
    public float CacheHitRatio => _framesProcessed > 0
        ? (float)_framesSkipped / _framesProcessed
        : 0f;

    /// <summary>
    /// Render time saved by skipping (approximate ratio).
    /// </summary>
    public float RenderTimeSaved => _framesProcessed > 0
        ? (float)_framesSkipped / _framesProcessed
        : 0f;

    /// <summary>
    /// Process a frame and determine whether to render or skip.
    /// Optimized for streaming: hash is computed first (fast), then render only if needed.
    /// </summary>
    /// <param name="image">The frame image</param>
    /// <param name="renderFunc">Function to render the frame if needed</param>
    /// <returns>The rendered content (either freshly rendered or from cache)</returns>
    public string ProcessFrame(Image<Rgba32> image, Func<Image<Rgba32>, string> renderFunc)
    {
        Interlocked.Increment(ref _framesProcessed);
        Interlocked.Increment(ref _hashComputations);

        // Compute perceptual hash with brightness for better matching
        var (hash, brightness) = FrameHasher.ComputeHashWithBrightness(image);

        if (DebugMode)
        {
            Console.Error.WriteLine($"[SmartSampler] Frame {_framesProcessed}: hash={hash:X16}, brightness={brightness}");
        }

        // Check if very similar to last frame (most common case for video)
        lock (_stateLock)
        {
            if (_lastFrameHash.HasValue &&
                _lastRenderedContent != null &&
                FrameHasher.AreSimilar(hash, _lastFrameHash.Value, _hashSimilarityThreshold) &&
                Math.Abs(brightness - _lastFrameBrightness) <= BrightnessThreshold)
            {
                Interlocked.Increment(ref _framesSkipped);
                if (DebugMode) Console.Error.WriteLine($"  -> SKIP (same as last, dist={FrameHasher.HammingDistance(hash, _lastFrameHash.Value)})");
                return _lastRenderedContent;
            }
        }

        // Check cache for exact or similar hash
        if (TryGetFromCache(hash, brightness, out var cachedContent))
        {
            lock (_stateLock)
            {
                _lastFrameHash = hash;
                _lastFrameBrightness = brightness;
                _lastRenderedContent = cachedContent;
            }
            Interlocked.Increment(ref _framesSkipped);
            if (DebugMode) Console.Error.WriteLine($"  -> SKIP (cache hit)");
            return cachedContent!;
        }

        // Slow path: need to render this frame
        if (DebugMode) Console.Error.WriteLine($"  -> RENDER (new frame)");
        var content = renderFunc(image);

        // Cache the result
        AddToCache(hash, brightness, content);

        lock (_stateLock)
        {
            _lastFrameHash = hash;
            _lastFrameBrightness = brightness;
            _lastRenderedContent = content;
        }

        return content;
    }

    /// <summary>
    /// Try to get content from cache (exact or similar hash with brightness check).
    /// </summary>
    private bool TryGetFromCache(ulong hash, int brightness, out string? content)
    {
        // Check exact match first
        if (_frameCache.TryGetValue(hash, out var cached))
        {
            if (Math.Abs(brightness - cached.Brightness) <= BrightnessThreshold)
            {
                cached.UseCount++;
                cached.LastUsed = _framesProcessed;
                content = cached.Content;
                return true;
            }
        }

        // Check for similar hashes (more expensive) - must also match brightness
        foreach (var kvp in _frameCache)
        {
            if (FrameHasher.AreSimilar(hash, kvp.Key, _hashSimilarityThreshold) &&
                Math.Abs(brightness - kvp.Value.Brightness) <= BrightnessThreshold)
            {
                kvp.Value.UseCount++;
                kvp.Value.LastUsed = _framesProcessed;
                content = kvp.Value.Content;
                return true;
            }
        }

        content = null;
        return false;
    }

    /// <summary>
    /// Add rendered content to cache with LFU eviction.
    /// </summary>
    private void AddToCache(ulong hash, int brightness, string content)
    {
        // Evict if cache is full
        while (_frameCache.Count >= _maxCacheSize)
        {
            EvictLeastUsed();
        }

        _frameCache.TryAdd(hash, new CachedFrame
        {
            Content = content,
            Brightness = brightness,
            UseCount = 1,
            LastUsed = _framesProcessed
        });
    }

    /// <summary>
    /// Check if a frame should be skipped (without rendering).
    /// Use this for timing decisions before committing to render.
    /// </summary>
    public bool ShouldSkipFrame(Image<Rgba32> image)
    {
        var hash = FrameHasher.ComputeHash(image);

        lock (_stateLock)
        {
            // Check against last frame
            if (_lastFrameHash.HasValue &&
                FrameHasher.AreSimilar(hash, _lastFrameHash.Value, _hashSimilarityThreshold))
            {
                return true;
            }

            // Check cache
            if (_frameCache.ContainsKey(hash))
                return true;

            foreach (var cachedHash in _frameCache.Keys)
            {
                if (FrameHasher.AreSimilar(hash, cachedHash, _hashSimilarityThreshold))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get cached content for a similar frame if available.
    /// </summary>
    public string? GetCachedContent(Image<Rgba32> image)
    {
        var hash = FrameHasher.ComputeHash(image);

        lock (_stateLock)
        {
            // Check against last frame
            if (_lastFrameHash.HasValue &&
                _lastRenderedContent != null &&
                FrameHasher.AreSimilar(hash, _lastFrameHash.Value, _hashSimilarityThreshold))
            {
                return _lastRenderedContent;
            }

            // Check exact match
            if (_frameCache.TryGetValue(hash, out var exact))
            {
                exact.UseCount++;
                exact.LastUsed = _framesProcessed;
                return exact.Content;
            }

            // Check similar
            foreach (var (cachedHash, cached) in _frameCache)
            {
                if (FrameHasher.AreSimilar(hash, cachedHash, _hashSimilarityThreshold))
                {
                    cached.UseCount++;
                    cached.LastUsed = _framesProcessed;
                    return cached.Content;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Reset the sampler state (for new video/loop).
    /// </summary>
    public void Reset()
    {
        _frameCache.Clear();
        lock (_stateLock)
        {
            _lastFrameHash = null;
            _lastRenderedContent = null;
        }
        Interlocked.Exchange(ref _framesProcessed, 0);
        Interlocked.Exchange(ref _framesSkipped, 0);
        Interlocked.Exchange(ref _hashComputations, 0);
    }

    /// <summary>
    /// Evict the least frequently used cache entry.
    /// </summary>
    private void EvictLeastUsed()
    {
        ulong? evictHash = null;
        var minScore = int.MaxValue;

        foreach (var kvp in _frameCache)
        {
            // LFU with recency bias: lower use count and older = higher eviction priority
            var score = kvp.Value.UseCount * 1000 + kvp.Value.LastUsed;
            if (score < minScore)
            {
                minScore = score;
                evictHash = kvp.Key;
            }
        }

        if (evictHash.HasValue)
        {
            _frameCache.TryRemove(evictHash.Value, out _);
        }
    }

    private class CachedFrame
    {
        public string Content { get; set; } = "";
        public int Brightness { get; set; }
        public int UseCount { get; set; }
        public int LastUsed { get; set; }
    }
}

