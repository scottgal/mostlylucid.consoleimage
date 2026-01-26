using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConsoleImage.Core;
using ConsoleImage.Core.Subtitles;
using ConsoleImage.Video.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ConsoleImageTools>();

var app = builder.Build();
await app.RunAsync();

// AOT-compatible JSON serialization context
[JsonSerializable(typeof(ImageInfo))]
[JsonSerializable(typeof(VideoInfoResult))]
[JsonSerializable(typeof(RenderModeInfo))]
[JsonSerializable(typeof(RenderModeInfo[]))]
[JsonSerializable(typeof(GifResult))]
[JsonSerializable(typeof(MatrixPreset))]
[JsonSerializable(typeof(MatrixPreset[]))]
[JsonSerializable(typeof(RenderResult))]
[JsonSerializable(typeof(DetailedImageInfo))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(ExtractFramesResult))]
[JsonSerializable(typeof(RenderVideoResult))]
[JsonSerializable(typeof(YouTubeInfoResult))]
[JsonSerializable(typeof(DetailedVideoInfo))]
[JsonSerializable(typeof(SubtitleStreamDto))]
[JsonSerializable(typeof(SubtitleStreamDto[]))]
[JsonSerializable(typeof(SceneDetectionResult))]
[JsonSerializable(typeof(SubtitleEntryDto))]
[JsonSerializable(typeof(SubtitleEntryDto[]))]
[JsonSerializable(typeof(SubtitleParseResult))]
[JsonSerializable(typeof(ExportResult))]
[JsonSerializable(typeof(DocumentInfoResult))]
[JsonSerializable(typeof(ExtractSubtitlesResult))]
[JsonSerializable(typeof(PerceptualHashResult))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class McpJsonContext : JsonSerializerContext
{
}

// DTOs for JSON serialization
internal record ImageInfo(string Path, int Width, int Height, int FrameCount, bool IsAnimated);

internal record VideoInfoResult(
    string Path,
    double Duration,
    string DurationFormatted,
    int Width,
    int Height,
    double FrameRate,
    string? VideoCodec);

internal record RenderModeInfo(string Mode, string Description, string Resolution, string BestFor, string Characters);

internal record GifResult(bool Success, string OutputPath, int FrameCount, double FileSizeKB);

internal record MatrixPreset(string Name, string Color, string Description);

internal record RenderResult(bool Success, string OutputPath, string Mode, int Width, int Height, double FileSizeKB);

internal record DetailedImageInfo(
    string FileName,
    string FullPath,
    string Format,
    int Width,
    int Height,
    string AspectRatio,
    long FileSizeBytes,
    string FileSizeFormatted,
    int FrameCount,
    bool IsAnimated,
    int BitsPerPixel,
    string PixelFormat,
    double MegaPixels,
    Dictionary<string, string>? Metadata
);

internal record ExtractFramesResult(
    bool Success,
    string OutputPath,
    int FrameCount,
    int Width,
    int Height,
    double FileSizeKB,
    string? Message = null
);

internal record RenderVideoResult(
    bool Success,
    string OutputPath,
    int FrameCount,
    string Mode,
    int Width,
    int Height,
    double FileSizeKB,
    string? Message = null
);

internal record YouTubeInfoResult(
    bool IsYouTubeUrl,
    bool YtdlpAvailable,
    string Status,
    string? VideoUrl = null,
    string? Title = null
);

internal record DetailedVideoInfo(
    string FileName,
    string FullPath,
    double Duration,
    string DurationFormatted,
    int Width,
    int Height,
    string AspectRatio,
    double FrameRate,
    int TotalFrames,
    long BitRate,
    string? VideoCodec,
    long FileSizeBytes,
    string FileSizeFormatted,
    SubtitleStreamDto[] SubtitleStreams
);

internal record SubtitleStreamDto(
    int Index,
    string Codec,
    string? Language,
    string? Title,
    bool IsTextBased
);

internal record SceneDetectionResult(
    int SceneCount,
    double[] Timestamps,
    string[] TimestampsFormatted,
    double Duration,
    string? Message = null
);

internal record SubtitleEntryDto(
    int Index,
    string StartTime,
    string EndTime,
    double StartSeconds,
    double EndSeconds,
    string Text,
    string? SpeakerId = null
);

internal record SubtitleParseResult(
    bool Success,
    string? SourceFile,
    string? Language,
    int EntryCount,
    string TotalDuration,
    SubtitleEntryDto[] Entries
);

internal record ExportResult(
    bool Success,
    string OutputPath,
    string Format,
    double FileSizeKB,
    string? Message = null
);

internal record DocumentInfoResult(
    string FileName,
    string FullPath,
    string Type,
    string Version,
    string Created,
    string? SourceFile,
    string RenderMode,
    int FrameCount,
    bool IsAnimated,
    int TotalDurationMs,
    string DurationFormatted,
    int MaxWidth,
    int MaxHeight,
    bool UseColor,
    bool HasSubtitles,
    int SubtitleEntryCount,
    long FileSizeBytes,
    string FileSizeFormatted
);

internal record ExtractSubtitlesResult(
    bool Success,
    string? OutputPath,
    int EntryCount,
    string? Language,
    string? Message = null
);

internal record PerceptualHashResult(
    string Hash,
    int AverageBrightness,
    int Width,
    int Height,
    int FrameCount,
    string? ComparisonHash = null,
    int? HammingDistance = null,
    string? Message = null
);

/// <summary>
///     MCP tools for ConsoleImage - render images to ASCII art
/// </summary>
[McpServerToolType]
public sealed class ConsoleImageTools
{
    /// <summary>
    ///     Render an image file to ASCII art text
    /// </summary>
    [McpServerTool(Name = "render_image")]
    [Description("Render an image or GIF to ASCII art. Returns ANSI-colored text for terminal display.")]
    public static string RenderImage(
        [Description("Path to the image file (JPG, PNG, GIF, WebP, BMP)")]
        string path,
        [Description("Render mode: ascii, blocks, braille, or matrix")]
        string mode = "ascii",
        [Description("Maximum width in characters (default: 80)")]
        int maxWidth = 80,
        [Description("Maximum height in characters (default: 40)")]
        int maxHeight = 40,
        [Description("Enable color output (default: true)")]
        bool useColor = true,
        [Description("Frame index for GIFs (default: 0 for first frame)")]
        int frameIndex = 0,
        [Description("Optional: save to file instead of returning content (reduces context usage)")]
        string? outputPath = null)
    {
        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        try
        {
            var options = new RenderOptions
            {
                MaxWidth = maxWidth,
                MaxHeight = maxHeight,
                UseColor = useColor
            };

            var content = mode.ToLowerInvariant() switch
            {
                "blocks" or "colorblocks" => RenderWithColorBlocks(path, options),
                "braille" => RenderWithBraille(path, options),
                "matrix" => RenderWithMatrix(path, options),
                _ => RenderWithAscii(path, options, frameIndex)
            };

            // If outputPath provided, write to file and return metadata instead
            if (!string.IsNullOrEmpty(outputPath))
            {
                File.WriteAllText(outputPath, content);
                var fileInfo = new FileInfo(outputPath);
                var lines = content.Split('\n');
                var width = lines.Length > 0 ? lines[0].Length : 0;
                var height = lines.Length;
                var result = new RenderResult(true, outputPath, mode, width, height, fileInfo.Length / 1024.0);
                return JsonSerializer.Serialize(result, McpJsonContext.Default.RenderResult);
            }

            return content;
        }
        catch (Exception ex)
        {
            return $"Error rendering image: {ex.Message}";
        }
    }

    private static string RenderWithAscii(string path, RenderOptions options, int frameIndex)
    {
        using var renderer = new AsciiRenderer(options);

        // Check if it's a GIF with multiple frames
        if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            var frames = renderer.RenderGif(path);
            if (frameIndex >= 0 && frameIndex < frames.Count)
                return options.UseColor ? frames[frameIndex].ToAnsiString() : frames[frameIndex].ToString();
            return options.UseColor ? frames[0].ToAnsiString() : frames[0].ToString();
        }

        var frame = renderer.RenderFile(path);
        return options.UseColor ? frame.ToAnsiString() : frame.ToString();
    }

    private static string RenderWithColorBlocks(string path, RenderOptions options)
    {
        using var renderer = new ColorBlockRenderer(options);
        return renderer.RenderFile(path);
    }

    private static string RenderWithBraille(string path, RenderOptions options)
    {
        using var renderer = new BrailleRenderer(options);
        return renderer.RenderFile(path);
    }

    private static string RenderWithMatrix(string path, RenderOptions options)
    {
        using var renderer = new MatrixRenderer(options, MatrixOptions.ClassicGreen);
        var frame = renderer.RenderFile(path);
        return frame.Content;
    }

    /// <summary>
    ///     Get information about a GIF file
    /// </summary>
    [McpServerTool(Name = "get_gif_info")]
    [Description("Get information about a GIF file including frame count and dimensions.")]
    public static string GetGifInfo(
        [Description("Path to the GIF file")] string path)
    {
        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        try
        {
            using var image = Image.Load(path);
            var frameCount = image.Frames.Count;

            var info = new ImageInfo(
                Path.GetFileName(path),
                image.Width,
                image.Height,
                frameCount,
                frameCount > 1
            );
            return JsonSerializer.Serialize(info, McpJsonContext.Default.ImageInfo);
        }
        catch (Exception ex)
        {
            return $"Error reading GIF: {ex.Message}";
        }
    }

    /// <summary>
    ///     Get detailed information about any image file
    /// </summary>
    [McpServerTool(Name = "get_image_info")]
    [Description(
        "Get detailed information about any image file (JPG, PNG, GIF, WebP, BMP) including dimensions, format, color depth, and metadata.")]
    public static string GetImageInfo(
        [Description("Path to the image file")]
        string path)
    {
        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        try
        {
            var fileInfo = new FileInfo(path);
            using var image = Image.Load(path);

            var frameCount = image.Frames.Count;
            var isAnimated = frameCount > 1;

            // Get format from file extension
            var format = Path.GetExtension(path).TrimStart('.').ToUpperInvariant() switch
            {
                "JPG" or "JPEG" => "JPEG",
                "PNG" => "PNG",
                "GIF" => "GIF",
                "BMP" => "BMP",
                "WEBP" => "WebP",
                "TIF" or "TIFF" => "TIFF",
                _ => Path.GetExtension(path).TrimStart('.').ToUpperInvariant()
            };

            // Calculate aspect ratio as simplified fraction
            var gcd = GCD(image.Width, image.Height);
            var aspectRatio = $"{image.Width / gcd}:{image.Height / gcd}";

            // Get pixel format info
            var pixelType = image.PixelType;
            var bitsPerPixel = pixelType.BitsPerPixel;
            var pixelFormat = $"{pixelType.AlphaRepresentation?.ToString() ?? "NoAlpha"}, {bitsPerPixel}bpp";

            // Calculate megapixels
            var megaPixels = Math.Round(image.Width * image.Height / 1_000_000.0, 2);

            // Format file size
            var fileSizeFormatted = fileInfo.Length switch
            {
                < 1024 => $"{fileInfo.Length} B",
                < 1024 * 1024 => $"{fileInfo.Length / 1024.0:F1} KB",
                _ => $"{fileInfo.Length / (1024.0 * 1024.0):F2} MB"
            };

            // Get EXIF/metadata if available
            Dictionary<string, string>? metadata = null;
            var exif = image.Metadata.ExifProfile;
            if (exif != null && exif.Values.Any())
            {
                metadata = new Dictionary<string, string>();
                foreach (var tag in exif.Values.Take(15)) // Limit to 15 tags
                {
                    var value = tag.GetValue()?.ToString();
                    if (!string.IsNullOrEmpty(value) && value.Length < 100) metadata[tag.Tag.ToString()] = value;
                }
            }

            var info = new DetailedImageInfo(
                fileInfo.Name,
                fileInfo.FullName,
                format,
                image.Width,
                image.Height,
                aspectRatio,
                fileInfo.Length,
                fileSizeFormatted,
                frameCount,
                isAnimated,
                bitsPerPixel,
                pixelFormat,
                megaPixels,
                metadata
            );

            return JsonSerializer.Serialize(info, McpJsonContext.Default.DetailedImageInfo);
        }
        catch (Exception ex)
        {
            return $"Error reading image: {ex.Message}";
        }
    }

    private static int GCD(int a, int b)
    {
        while (b != 0)
        {
            var temp = b;
            b = a % b;
            a = temp;
        }

        return a;
    }

    /// <summary>
    ///     Get detailed information about a video file using FFmpeg
    /// </summary>
    [McpServerTool(Name = "get_video_info")]
    [Description(
        "Get detailed information about a video file including duration, resolution, codec, bitrate, frame count, file size, and embedded subtitle streams.")]
    public static async Task<string> GetVideoInfo(
        [Description("Path to the video file")]
        string path)
    {
        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        try
        {
            using var ffmpeg = new FFmpegService();
            var info = await ffmpeg.GetVideoInfoAsync(path);

            if (info == null)
                return "Error: Could not retrieve video information";

            var fileInfo = new FileInfo(path);
            var fileSizeFormatted = fileInfo.Length switch
            {
                < 1024 => $"{fileInfo.Length} B",
                < 1024 * 1024 => $"{fileInfo.Length / 1024.0:F1} KB",
                < 1024L * 1024 * 1024 => $"{fileInfo.Length / (1024.0 * 1024.0):F1} MB",
                _ => $"{fileInfo.Length / (1024.0 * 1024.0 * 1024.0):F2} GB"
            };

            var gcd = GCD(info.Width, info.Height);
            var aspectRatio = gcd > 0 ? $"{info.Width / gcd}:{info.Height / gcd}" : $"{info.Width}:{info.Height}";

            // Get embedded subtitle streams
            var subtitleStreams = Array.Empty<SubtitleStreamDto>();
            try
            {
                var streams = await ffmpeg.GetSubtitleStreamsAsync(path);
                subtitleStreams = streams.Select(s => new SubtitleStreamDto(
                    s.Index, s.Codec, s.Language, s.Title, s.IsTextBased
                )).ToArray();
            }
            catch
            {
                // Subtitle stream detection is best-effort
            }

            var result = new DetailedVideoInfo(
                fileInfo.Name,
                fileInfo.FullName,
                info.Duration,
                TimeSpan.FromSeconds(info.Duration).ToString(@"hh\:mm\:ss\.fff"),
                info.Width,
                info.Height,
                aspectRatio,
                info.FrameRate,
                info.TotalFrames,
                info.BitRate,
                info.VideoCodec,
                fileInfo.Length,
                fileSizeFormatted,
                subtitleStreams
            );
            return JsonSerializer.Serialize(result, McpJsonContext.Default.DetailedVideoInfo);
        }
        catch (Exception ex)
        {
            return $"Error reading video: {ex.Message}";
        }
    }

    /// <summary>
    ///     List all available render modes with descriptions
    /// </summary>
    [McpServerTool(Name = "list_render_modes")]
    [Description("List all available render modes with descriptions and use cases.")]
    public static string ListRenderModes()
    {
        var modes = new RenderModeInfo[]
        {
            new("ascii", "Classic ASCII art using shape-matched characters", "Standard - 1 character per pixel",
                "Maximum compatibility, nostalgic look", "Uses alphanumeric and punctuation characters"),
            new("blocks", "Unicode half-block characters with separate foreground/background colors",
                "2x vertical - 2 pixels per character height", "High color fidelity, photo-realistic images",
                "Uses Unicode block characters: \u2580\u2584\u2588"),
            new("braille", "Braille Unicode characters for ultra-high resolution",
                "2x4 - 8 dots per character (2 wide, 4 tall)", "Maximum detail, fine patterns",
                "Uses Braille patterns: \u2800-\u28FF"),
            new("matrix", "Matrix digital rain effect with falling characters", "Standard - 1 character per pixel",
                "Stylized effect, cyberpunk aesthetic", "Uses half-width katakana, numbers, symbols")
        };
        return JsonSerializer.Serialize(modes, McpJsonContext.Default.RenderModeInfoArray);
    }

    /// <summary>
    ///     Render image to GIF file
    /// </summary>
    [McpServerTool(Name = "render_to_gif")]
    [Description("Render an image or animation to an animated GIF file.")]
    public static string RenderToGif(
        [Description("Path to the source image or GIF")]
        string inputPath,
        [Description("Path for the output GIF file")]
        string outputPath,
        [Description("Render mode: ascii, blocks, braille, or matrix")]
        string mode = "ascii",
        [Description("Maximum width in characters (default: 60)")]
        int maxWidth = 60,
        [Description("Font size for rendering (default: 10)")]
        int fontSize = 10,
        [Description("Maximum colors in palette (default: 64)")]
        int maxColors = 64)
    {
        if (!File.Exists(inputPath))
            return $"Error: File not found: {inputPath}";

        try
        {
            var options = new RenderOptions
            {
                MaxWidth = maxWidth,
                UseColor = true
            };

            var gifOptions = new GifWriterOptions
            {
                FontSize = fontSize,
                MaxColors = maxColors,
                LoopCount = 0
            };

            using var gifWriter = new GifWriter(gifOptions);
            var frameCount = 0;

            switch (mode.ToLowerInvariant())
            {
                case "blocks" or "colorblocks":
                    using (var renderer = new ColorBlockRenderer(options))
                    {
                        if (inputPath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                        {
                            var frames = renderer.RenderGifFrames(inputPath);
                            foreach (var frame in frames)
                            {
                                gifWriter.AddColorBlockFrame(frame, frame.DelayMs);
                                frameCount++;
                            }
                        }
                        else
                        {
                            var frame = renderer.RenderFileToFrame(inputPath);
                            gifWriter.AddColorBlockFrame(frame);
                            frameCount = 1;
                        }
                    }

                    break;

                case "braille":
                    using (var renderer = new BrailleRenderer(options))
                    {
                        if (inputPath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                        {
                            var frames = renderer.RenderGifFrames(inputPath);
                            foreach (var frame in frames)
                            {
                                gifWriter.AddBrailleFrame(frame, frame.DelayMs);
                                frameCount++;
                            }
                        }
                        else
                        {
                            var frame = renderer.RenderFileToFrame(inputPath);
                            gifWriter.AddBrailleFrame(frame);
                            frameCount = 1;
                        }
                    }

                    break;

                case "matrix":
                    using (var renderer = new MatrixRenderer(options, MatrixOptions.ClassicGreen))
                    {
                        var frame = renderer.RenderFile(inputPath);
                        gifWriter.AddMatrixFrame(frame);
                        frameCount = 1;
                    }

                    break;

                default:
                    using (var renderer = new AsciiRenderer(options))
                    {
                        if (inputPath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                        {
                            var frames = renderer.RenderGif(inputPath);
                            foreach (var frame in frames)
                            {
                                gifWriter.AddFrame(frame, frame.DelayMs);
                                frameCount++;
                            }
                        }
                        else
                        {
                            var frame = renderer.RenderFile(inputPath);
                            gifWriter.AddFrame(frame);
                            frameCount = 1;
                        }
                    }

                    break;
            }

            gifWriter.Save(outputPath);

            var fileInfo = new FileInfo(outputPath);
            var result = new GifResult(true, outputPath, frameCount, fileInfo.Length / 1024.0);
            return JsonSerializer.Serialize(result, McpJsonContext.Default.GifResult);
        }
        catch (Exception ex)
        {
            return $"Error creating GIF: {ex.Message}";
        }
    }

    /// <summary>
    ///     Get available matrix color presets
    /// </summary>
    [McpServerTool(Name = "list_matrix_presets")]
    [Description("List available Matrix digital rain color presets.")]
    public static string ListMatrixPresets()
    {
        var presets = new MatrixPreset[]
        {
            new("ClassicGreen", "#00FF41", "The iconic Matrix green"),
            new("RedPill", "#FF0000", "Red Matrix theme"),
            new("BluePill", "#0080FF", "Blue Matrix theme"),
            new("Amber", "#FFB000", "Amber/orange retro terminal"),
            new("FullColor", "varies", "Uses source image colors with Matrix lighting")
        };
        return JsonSerializer.Serialize(presets, McpJsonContext.Default.MatrixPresetArray);
    }

    /// <summary>
    ///     Extract raw video frames to GIF (no ASCII rendering)
    /// </summary>
    [McpServerTool(Name = "extract_frames")]
    [Description(
        "Extract raw video frames to an animated GIF file. No ASCII rendering - preserves actual video frames. Useful for creating thumbnails, previews, or scene slideshows.")]
    public static async Task<string> ExtractFrames(
        [Description("Path to video file (MP4, MKV, AVI, WebM, etc.)")]
        string inputPath,
        [Description("Path for the output GIF file")]
        string outputPath,
        [Description("Output width in pixels (default: 320)")]
        int width = 320,
        [Description("Maximum number of frames to extract (default: 8)")]
        int maxFrames = 8,
        [Description("Maximum duration in seconds to sample from (default: 10)")]
        double maxLength = 10,
        [Description("Start time in seconds (default: 0)")]
        double startTime = 0,
        [Description("Use smart scene detection instead of uniform sampling (default: false)")]
        bool smartKeyframes = false,
        [Description("Scene detection threshold 0.0-1.0 (default: 0.4, lower = more sensitive)")]
        double sceneThreshold = 0.4)
    {
        if (!File.Exists(inputPath))
            return $"Error: File not found: {inputPath}";

        try
        {
            using var ffmpeg = new FFmpegService();
            await ffmpeg.InitializeAsync(null, CancellationToken.None);

            var videoInfo = await ffmpeg.GetVideoInfoAsync(inputPath);
            if (videoInfo == null)
                return "Error: Could not read video info";

            var targetHeight = (int)(width * videoInfo.Height / (double)videoInfo.Width);
            var endTime = startTime + maxLength;

            if (smartKeyframes)
            {
                // Scene detection mode
                var sceneTimestamps = await ffmpeg.DetectSceneChangesAsync(
                    inputPath, sceneThreshold, startTime, endTime, CancellationToken.None);

                List<double> keyframeTimes = new() { startTime };
                keyframeTimes.AddRange(sceneTimestamps);

                if (keyframeTimes.Count > maxFrames)
                    keyframeTimes = keyframeTimes.Take(maxFrames).ToList();

                using var gif = new Image<Rgba32>(width, targetHeight);
                var gifMeta = gif.Metadata.GetGifMetadata();
                gifMeta.RepeatCount = 0;

                var frameCount = 0;
                foreach (var timestamp in keyframeTimes)
                {
                    var frame = await ffmpeg.ExtractFrameAsync(inputPath, timestamp, width, targetHeight,
                        CancellationToken.None);
                    if (frame != null)
                    {
                        var frameMeta = frame.Frames.RootFrame.Metadata.GetGifMetadata();
                        frameMeta.FrameDelay = 100; // 1 second between frames
                        gif.Frames.AddFrame(frame.Frames.RootFrame);
                        frame.Dispose();
                        frameCount++;
                    }
                }

                if (gif.Frames.Count > 1)
                    gif.Frames.RemoveFrame(0);

                await gif.SaveAsGifAsync(outputPath);

                var fileInfo = new FileInfo(outputPath);
                var result = new ExtractFramesResult(true, outputPath, frameCount, width, targetHeight,
                    fileInfo.Length / 1024.0, $"Extracted {frameCount} keyframes using scene detection");
                return JsonSerializer.Serialize(result, McpJsonContext.Default.ExtractFramesResult);
            }
            else
            {
                // Uniform sampling mode
                var targetFps = Math.Min(videoInfo.FrameRate, maxFrames / maxLength);

                await using var streamingGif = new FFmpegGifWriter(
                    outputPath, width, targetHeight,
                    (int)Math.Max(1, targetFps), 0, 64, maxLength, maxFrames);

                await streamingGif.StartAsync(null, CancellationToken.None);

                var frameCount = 0;
                await foreach (var frame in ffmpeg.StreamFramesAsync(
                                   inputPath, width, targetHeight, startTime, endTime, 1, targetFps,
                                   videoInfo.VideoCodec, CancellationToken.None))
                {
                    if (streamingGif.ShouldStop)
                    {
                        frame.Dispose();
                        break;
                    }

                    await streamingGif.AddFrameAsync(frame, CancellationToken.None);
                    frame.Dispose();
                    frameCount++;
                }

                await streamingGif.FinishAsync(CancellationToken.None);

                var fileInfo = new FileInfo(outputPath);
                var result = new ExtractFramesResult(true, outputPath, frameCount, width, targetHeight,
                    fileInfo.Length / 1024.0, $"Extracted {frameCount} frames with uniform sampling");
                return JsonSerializer.Serialize(result, McpJsonContext.Default.ExtractFramesResult);
            }
        }
        catch (Exception ex)
        {
            return $"Error extracting frames: {ex.Message}";
        }
    }

    /// <summary>
    ///     Compare an image across all render modes
    /// </summary>
    [McpServerTool(Name = "compare_render_modes")]
    [Description("Render the same image in all modes for comparison. Returns a dictionary of mode->output.")]
    public static string CompareRenderModes(
        [Description("Path to the image file")]
        string path,
        [Description("Maximum width in characters (default: 40)")]
        int maxWidth = 40)
    {
        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        try
        {
            var options = new RenderOptions
            {
                MaxWidth = maxWidth,
                UseColor = true
            };

            var results = new Dictionary<string, string>();

            // ASCII
            using (var renderer = new AsciiRenderer(options))
            {
                var frame = renderer.RenderFile(path);
                results["ascii"] = frame.ToAnsiString();
            }

            // Blocks
            using (var renderer = new ColorBlockRenderer(options))
            {
                results["blocks"] = renderer.RenderFile(path);
            }

            // Braille
            using (var renderer = new BrailleRenderer(options))
            {
                results["braille"] = renderer.RenderFile(path);
            }

            // Matrix
            using (var renderer = new MatrixRenderer(options, MatrixOptions.ClassicGreen))
            {
                var frame = renderer.RenderFile(path);
                results["matrix"] = frame.Content;
            }

            return JsonSerializer.Serialize(results, McpJsonContext.Default.DictionaryStringString);
        }
        catch (Exception ex)
        {
            return $"Error comparing modes: {ex.Message}";
        }
    }

    /// <summary>
    ///     Render a video file to an animated GIF with ASCII art
    /// </summary>
    [McpServerTool(Name = "render_video")]
    [Description(
        "Render a video file (MP4, MKV, AVI, WebM) to an animated ASCII art GIF. Requires FFmpeg (auto-downloads on first use).")]
    public static async Task<string> RenderVideo(
        [Description("Path to the video file")]
        string inputPath,
        [Description("Path for the output GIF file")]
        string outputPath,
        [Description("Render mode: ascii, blocks, braille, or matrix")]
        string mode = "braille",
        [Description("Width in characters (default: 60)")]
        int width = 60,
        [Description("Start time in seconds (default: 0)")]
        double startTime = 0,
        [Description("Duration in seconds (default: 10)")]
        double duration = 10,
        [Description("Target FPS (default: 10)")]
        int fps = 10,
        [Description("Font size for GIF rendering (default: 10)")]
        int fontSize = 10,
        [Description("Max colors in GIF palette (default: 64)")]
        int maxColors = 64)
    {
        if (!File.Exists(inputPath))
            return $"Error: File not found: {inputPath}";

        try
        {
            using var ffmpeg = new FFmpegService();
            await ffmpeg.InitializeAsync(null, CancellationToken.None);

            var videoInfo = await ffmpeg.GetVideoInfoAsync(inputPath);
            if (videoInfo == null)
                return "Error: Could not read video info";

            var options = new RenderOptions
            {
                MaxWidth = width,
                UseColor = true
            };

            var gifOptions = new GifWriterOptions
            {
                FontSize = fontSize,
                MaxColors = maxColors,
                LoopCount = 0
            };

            using var gifWriter = new GifWriter(gifOptions);
            var frameCount = 0;
            var endTime = startTime + duration;
            var targetHeight = (int)(width / (videoInfo.Width / (double)videoInfo.Height) * 0.5);

            await foreach (var frame in ffmpeg.StreamFramesAsync(
                               inputPath, width, targetHeight, startTime, endTime, 1, fps,
                               videoInfo.VideoCodec, CancellationToken.None))
            {
                var delayMs = (int)(1000.0 / fps);
                switch (mode.ToLowerInvariant())
                {
                    case "blocks" or "colorblocks":
                        using (var renderer = new ColorBlockRenderer(options))
                        {
                            var content = renderer.RenderImage(frame);
                            var rendered = new ColorBlockFrame(content, delayMs);
                            gifWriter.AddColorBlockFrame(rendered, delayMs);
                        }

                        break;

                    case "braille":
                        using (var renderer = new BrailleRenderer(options))
                        {
                            var rendered = renderer.RenderImageToFrame(frame);
                            gifWriter.AddBrailleFrame(rendered, delayMs);
                        }

                        break;

                    case "matrix":
                        using (var renderer = new MatrixRenderer(options, MatrixOptions.ClassicGreen))
                        {
                            var rendered = renderer.RenderImage(frame);
                            gifWriter.AddMatrixFrame(rendered);
                        }

                        break;

                    default:
                        using (var renderer = new AsciiRenderer(options))
                        {
                            var rendered = renderer.RenderImage(frame);
                            gifWriter.AddFrame(rendered, delayMs);
                        }

                        break;
                }

                frame.Dispose();
                frameCount++;

                if (frameCount >= fps * duration)
                    break;
            }

            gifWriter.Save(outputPath);

            var fileInfo = new FileInfo(outputPath);
            var result = new RenderVideoResult(true, outputPath, frameCount, mode, width, targetHeight,
                fileInfo.Length / 1024.0, $"Rendered {frameCount} frames in {mode} mode");
            return JsonSerializer.Serialize(result, McpJsonContext.Default.RenderVideoResult);
        }
        catch (Exception ex)
        {
            return $"Error rendering video: {ex.Message}";
        }
    }

    /// <summary>
    ///     Check if a URL is a YouTube video and get status
    /// </summary>
    [McpServerTool(Name = "check_youtube_url")]
    [Description("Check if a URL is a YouTube video and whether yt-dlp is available to process it.")]
    public static string CheckYouTubeUrl(
        [Description("URL to check")] string url)
    {
        try
        {
            var isYouTube = YtdlpProvider.IsYouTubeUrl(url);
            var ytdlpAvailable = YtdlpProvider.IsAvailable();
            var status = YtdlpProvider.GetStatus();

            var result = new YouTubeInfoResult(
                isYouTube,
                ytdlpAvailable,
                status
            );
            return JsonSerializer.Serialize(result, McpJsonContext.Default.YouTubeInfoResult);
        }
        catch (Exception ex)
        {
            return $"Error checking URL: {ex.Message}";
        }
    }

    /// <summary>
    ///     Get YouTube video stream URL using yt-dlp
    /// </summary>
    [McpServerTool(Name = "get_youtube_stream")]
    [Description(
        "Extract the direct video stream URL from a YouTube video using yt-dlp. Requires yt-dlp (auto-downloads on first use).")]
    public static async Task<string> GetYouTubeStream(
        [Description("YouTube video URL")] string url,
        [Description("Maximum video height (e.g., 720, 1080). Default: best available")]
        int? maxHeight = null)
    {
        try
        {
            if (!YtdlpProvider.IsYouTubeUrl(url))
                return "Error: Not a valid YouTube URL";

            // Get yt-dlp path (will download if needed)
            var ytdlpPath = await YtdlpProvider.GetYtdlpPathAsync();

            var streamInfo = await YtdlpProvider.GetStreamInfoAsync(url, ytdlpPath, maxHeight);
            if (streamInfo == null)
                return "Error: Could not extract stream URL";

            var result = new YouTubeInfoResult(
                true,
                true,
                "Stream extracted successfully",
                streamInfo.VideoUrl,
                streamInfo.Title
            );
            return JsonSerializer.Serialize(result, McpJsonContext.Default.YouTubeInfoResult);
        }
        catch (Exception ex)
        {
            return $"Error extracting YouTube stream: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Scene Detection & Video Analysis
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Detect scene changes in a video
    /// </summary>
    [McpServerTool(Name = "detect_scenes")]
    [Description(
        "Detect scene changes in a video file and return timestamps where cuts/transitions occur. Useful for finding key moments, creating chapter markers, or extracting representative frames.")]
    public static async Task<string> DetectScenes(
        [Description("Path to the video file")]
        string path,
        [Description("Detection sensitivity 0.0-1.0 (default: 0.4, lower = more sensitive, detects subtle changes)")]
        double threshold = 0.4,
        [Description("Start time in seconds (default: 0)")]
        double startTime = 0,
        [Description("End time in seconds (default: entire video)")]
        double? endTime = null)
    {
        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        try
        {
            using var ffmpeg = new FFmpegService();
            await ffmpeg.InitializeAsync(null, CancellationToken.None);

            var videoInfo = await ffmpeg.GetVideoInfoAsync(path);
            if (videoInfo == null)
                return "Error: Could not read video info";

            var end = endTime ?? videoInfo.Duration;
            var timestamps = await ffmpeg.DetectSceneChangesAsync(
                path, threshold, startTime, end, CancellationToken.None);

            var formatted = timestamps.Select(t =>
                TimeSpan.FromSeconds(t).ToString(@"hh\:mm\:ss\.fff")).ToArray();

            var result = new SceneDetectionResult(
                timestamps.Count,
                timestamps.ToArray(),
                formatted,
                videoInfo.Duration,
                $"Detected {timestamps.Count} scene changes between {startTime:F1}s and {end:F1}s (threshold: {threshold})"
            );
            return JsonSerializer.Serialize(result, McpJsonContext.Default.SceneDetectionResult);
        }
        catch (Exception ex)
        {
            return $"Error detecting scenes: {ex.Message}";
        }
    }

    /// <summary>
    ///     Render a single video frame at a specific timestamp to ASCII art
    /// </summary>
    [McpServerTool(Name = "render_video_frame")]
    [Description(
        "Render a single video frame at a specific timestamp to ASCII art text. Much faster than render_video - ideal for inspecting specific moments in a video.")]
    public static async Task<string> RenderVideoFrame(
        [Description("Path to the video file")]
        string path,
        [Description("Timestamp in seconds to extract the frame from")]
        double timestamp = 0,
        [Description("Render mode: ascii, blocks, braille, or matrix")]
        string mode = "braille",
        [Description("Maximum width in characters (default: 80)")]
        int maxWidth = 80,
        [Description("Enable color output (default: true)")]
        bool useColor = true,
        [Description("Optional: save to file instead of returning content")]
        string? outputPath = null)
    {
        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        try
        {
            using var ffmpeg = new FFmpegService();
            await ffmpeg.InitializeAsync(null, CancellationToken.None);

            var videoInfo = await ffmpeg.GetVideoInfoAsync(path);
            if (videoInfo == null)
                return "Error: Could not read video info";

            var targetHeight = (int)(maxWidth / (videoInfo.Width / (double)videoInfo.Height) * 0.5);

            using var frame = await ffmpeg.ExtractFrameAsync(path, timestamp, maxWidth, targetHeight,
                CancellationToken.None);
            if (frame == null)
                return $"Error: Could not extract frame at {timestamp}s";

            var options = new RenderOptions
            {
                MaxWidth = maxWidth,
                UseColor = useColor
            };

            string content;
            switch (mode.ToLowerInvariant())
            {
                case "blocks" or "colorblocks":
                    using (var r = new ColorBlockRenderer(options))
                        content = r.RenderImage(frame);
                    break;
                case "braille":
                    using (var r = new BrailleRenderer(options))
                        content = r.RenderImage(frame);
                    break;
                case "matrix":
                    using (var r = new MatrixRenderer(options, MatrixOptions.ClassicGreen))
                        content = r.RenderImage(frame).Content;
                    break;
                default:
                    using (var r = new AsciiRenderer(options))
                        content = useColor ? r.RenderImage(frame).ToAnsiString() : r.RenderImage(frame).ToString();
                    break;
            }

            if (!string.IsNullOrEmpty(outputPath))
            {
                File.WriteAllText(outputPath, content);
                var fileInfo = new FileInfo(outputPath);
                var result = new RenderResult(true, outputPath, mode, maxWidth, targetHeight,
                    fileInfo.Length / 1024.0);
                return JsonSerializer.Serialize(result, McpJsonContext.Default.RenderResult);
            }

            return content;
        }
        catch (Exception ex)
        {
            return $"Error rendering video frame: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Subtitle Tools
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     List embedded subtitle streams in a video file
    /// </summary>
    [McpServerTool(Name = "get_subtitle_streams")]
    [Description(
        "List all embedded subtitle streams in a video file (MKV, MP4, etc.). Shows language, codec, and whether they can be extracted as text.")]
    public static async Task<string> GetSubtitleStreams(
        [Description("Path to the video file")]
        string path)
    {
        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        try
        {
            using var ffmpeg = new FFmpegService();
            var streams = await ffmpeg.GetSubtitleStreamsAsync(path);

            var dtos = streams.Select(s => new SubtitleStreamDto(
                s.Index, s.Codec, s.Language, s.Title, s.IsTextBased
            )).ToArray();

            if (dtos.Length == 0)
                return "No subtitle streams found in this video.";

            return JsonSerializer.Serialize(dtos, McpJsonContext.Default.SubtitleStreamDtoArray);
        }
        catch (Exception ex)
        {
            return $"Error reading subtitle streams: {ex.Message}";
        }
    }

    /// <summary>
    ///     Extract embedded subtitles from a video file to SRT
    /// </summary>
    [McpServerTool(Name = "extract_subtitles")]
    [Description(
        "Extract embedded subtitles from a video file (MKV, MP4) to an SRT file. Can target a specific stream by index or auto-select by language.")]
    public static async Task<string> ExtractSubtitles(
        [Description("Path to the video file")]
        string path,
        [Description("Path for the output SRT file")]
        string outputPath,
        [Description("Subtitle stream index to extract (from get_subtitle_streams). If not specified, auto-selects first text-based stream.")]
        int? streamIndex = null,
        [Description("Preferred language code (e.g., 'en', 'es', 'fr'). Used when streamIndex is not specified.")]
        string? language = null)
    {
        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        try
        {
            using var ffmpeg = new FFmpegService();

            // If no stream index, find the best match
            if (streamIndex == null)
            {
                var streams = await ffmpeg.GetSubtitleStreamsAsync(path);
                var textStreams = streams.Where(s => s.IsTextBased).ToList();

                if (textStreams.Count == 0)
                    return "No text-based subtitle streams found in this video. Only image-based subtitles (DVB, PGS) detected, which cannot be extracted as text.";

                // Prefer matching language
                if (!string.IsNullOrEmpty(language))
                {
                    var langMatch = textStreams.FirstOrDefault(s =>
                        s.Language?.Equals(language, StringComparison.OrdinalIgnoreCase) == true);
                    if (langMatch != null)
                        streamIndex = langMatch.Index;
                }

                streamIndex ??= textStreams[0].Index;
            }

            var success = await ffmpeg.ExtractSubtitlesAsync(path, outputPath, streamIndex);

            if (!success)
                return "Error: Failed to extract subtitles from video";

            // Parse the extracted file to count entries
            var entryCount = 0;
            string? detectedLanguage = null;
            if (File.Exists(outputPath))
            {
                try
                {
                    var track = await SubtitleParser.ParseAsync(outputPath);
                    entryCount = track.Count;
                    detectedLanguage = track.Language;
                }
                catch
                {
                    // Count lines as fallback
                    entryCount = File.ReadAllLines(outputPath).Count(l => l.Contains("-->"));
                }
            }

            var result = new ExtractSubtitlesResult(true, outputPath, entryCount, detectedLanguage ?? language,
                $"Extracted {entryCount} subtitle entries from stream {streamIndex}");
            return JsonSerializer.Serialize(result, McpJsonContext.Default.ExtractSubtitlesResult);
        }
        catch (Exception ex)
        {
            return $"Error extracting subtitles: {ex.Message}";
        }
    }

    /// <summary>
    ///     Parse an SRT or VTT subtitle file
    /// </summary>
    [McpServerTool(Name = "parse_subtitles")]
    [Description(
        "Parse an SRT or VTT subtitle file and return all entries with timestamps and text. Useful for analyzing dialogue, searching for specific content, or extracting timing information.")]
    public static async Task<string> ParseSubtitles(
        [Description("Path to the subtitle file (.srt or .vtt)")]
        string path,
        [Description("Maximum number of entries to return (default: all). Use to limit output for large files.")]
        int? maxEntries = null)
    {
        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        if (!SubtitleParser.IsSupportedFormat(path))
            return "Error: Unsupported subtitle format. Supported formats: .srt, .vtt";

        try
        {
            var track = await SubtitleParser.ParseAsync(path);

            var entries = track.Entries.AsEnumerable();
            if (maxEntries.HasValue)
                entries = entries.Take(maxEntries.Value);

            var dtos = entries.Select(e => new SubtitleEntryDto(
                e.Index,
                e.StartTime.ToString(@"hh\:mm\:ss\.fff"),
                e.EndTime.ToString(@"hh\:mm\:ss\.fff"),
                e.StartTime.TotalSeconds,
                e.EndTime.TotalSeconds,
                e.Text,
                e.SpeakerId
            )).ToArray();

            var totalDuration = track.GetTotalDuration();
            var result = new SubtitleParseResult(
                true,
                Path.GetFileName(path),
                track.Language,
                track.Count,
                totalDuration.ToString(@"hh\:mm\:ss"),
                dtos
            );
            return JsonSerializer.Serialize(result, McpJsonContext.Default.SubtitleParseResult);
        }
        catch (Exception ex)
        {
            return $"Error parsing subtitles: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Export Tools (SVG, Markdown)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Render an image to SVG format
    /// </summary>
    [McpServerTool(Name = "export_to_svg")]
    [Description(
        "Render an image to ASCII art and export as an SVG file. SVG preserves colors and can be embedded in web pages, GitHub READMEs, and documents.")]
    public static async Task<string> ExportToSvg(
        [Description("Path to the source image file")]
        string inputPath,
        [Description("Path for the output SVG file")]
        string outputPath,
        [Description("Render mode: ascii, blocks, braille, or matrix")]
        string mode = "braille",
        [Description("Maximum width in characters (default: 60)")]
        int maxWidth = 60,
        [Description("SVG font size in pixels (default: 14)")]
        int fontSize = 14)
    {
        if (!File.Exists(inputPath))
            return $"Error: File not found: {inputPath}";

        try
        {
            var options = new RenderOptions
            {
                MaxWidth = maxWidth,
                UseColor = true
            };

            // Render the image to ANSI content
            var ansiContent = mode.ToLowerInvariant() switch
            {
                "blocks" or "colorblocks" => RenderWithColorBlocks(inputPath, options),
                "braille" => RenderWithBraille(inputPath, options),
                "matrix" => RenderWithMatrix(inputPath, options),
                _ => RenderWithAscii(inputPath, options, 0)
            };

            // Convert to SVG and save
            await MarkdownRenderer.SaveSvgAsync(ansiContent, outputPath, fontSize: fontSize);

            var fileInfo = new FileInfo(outputPath);
            var result = new ExportResult(true, outputPath, "SVG", fileInfo.Length / 1024.0,
                $"Exported {mode} render as SVG ({fileInfo.Length / 1024.0:F1} KB)");
            return JsonSerializer.Serialize(result, McpJsonContext.Default.ExportResult);
        }
        catch (Exception ex)
        {
            return $"Error exporting to SVG: {ex.Message}";
        }
    }

    /// <summary>
    ///     Render an image to markdown format
    /// </summary>
    [McpServerTool(Name = "export_to_markdown")]
    [Description(
        "Render an image to ASCII art and export as a markdown file. Supports plain text (universal), HTML spans, SVG (GitHub/GitLab), or ANSI code blocks.")]
    public static async Task<string> ExportToMarkdown(
        [Description("Path to the source image file")]
        string inputPath,
        [Description("Path for the output markdown file")]
        string outputPath,
        [Description("Render mode: ascii, blocks, braille, or matrix")]
        string mode = "braille",
        [Description("Markdown format: plain (universal), html (inline CSS), svg (GitHub/GitLab), ansi (terminal only)")]
        string format = "svg",
        [Description("Maximum width in characters (default: 60)")]
        int maxWidth = 60,
        [Description("Optional title for the markdown document")]
        string? title = null)
    {
        if (!File.Exists(inputPath))
            return $"Error: File not found: {inputPath}";

        try
        {
            var options = new RenderOptions
            {
                MaxWidth = maxWidth,
                UseColor = true
            };

            var ansiContent = mode.ToLowerInvariant() switch
            {
                "blocks" or "colorblocks" => RenderWithColorBlocks(inputPath, options),
                "braille" => RenderWithBraille(inputPath, options),
                "matrix" => RenderWithMatrix(inputPath, options),
                _ => RenderWithAscii(inputPath, options, 0)
            };

            var mdFormat = format.ToLowerInvariant() switch
            {
                "html" => MarkdownRenderer.MarkdownFormat.Html,
                "svg" => MarkdownRenderer.MarkdownFormat.Svg,
                "ansi" => MarkdownRenderer.MarkdownFormat.Ansi,
                _ => MarkdownRenderer.MarkdownFormat.Plain
            };

            await MarkdownRenderer.SaveMarkdownAsync(ansiContent, outputPath, mdFormat,
                title ?? Path.GetFileNameWithoutExtension(inputPath));

            var fileInfo = new FileInfo(outputPath);
            var result = new ExportResult(true, outputPath, format.ToUpperInvariant(), fileInfo.Length / 1024.0,
                $"Exported {mode} render as {format} markdown ({fileInfo.Length / 1024.0:F1} KB)");
            return JsonSerializer.Serialize(result, McpJsonContext.Default.ExportResult);
        }
        catch (Exception ex)
        {
            return $"Error exporting to markdown: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Document Tools (CIDZ / JSON)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Get information about a saved ConsoleImage document
    /// </summary>
    [McpServerTool(Name = "get_document_info")]
    [Description(
        "Get information about a saved ConsoleImage document (.cidz, .json, .ndjson). Shows frame count, duration, render mode, settings, and whether subtitles are embedded.")]
    public static async Task<string> GetDocumentInfo(
        [Description("Path to the document file (.cidz, .json, or .ndjson)")]
        string path)
    {
        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        try
        {
            var doc = await ConsoleImageDocument.LoadAsync(path);
            var fileInfo = new FileInfo(path);

            var fileSizeFormatted = fileInfo.Length switch
            {
                < 1024 => $"{fileInfo.Length} B",
                < 1024 * 1024 => $"{fileInfo.Length / 1024.0:F1} KB",
                < 1024L * 1024 * 1024 => $"{fileInfo.Length / (1024.0 * 1024.0):F1} MB",
                _ => $"{fileInfo.Length / (1024.0 * 1024.0 * 1024.0):F2} GB"
            };

            var result = new DocumentInfoResult(
                fileInfo.Name,
                fileInfo.FullName,
                doc.Type,
                doc.Version,
                doc.Created.ToString("O"),
                doc.SourceFile,
                doc.RenderMode,
                doc.FrameCount,
                doc.IsAnimated,
                doc.TotalDurationMs,
                TimeSpan.FromMilliseconds(doc.TotalDurationMs).ToString(@"hh\:mm\:ss\.fff"),
                doc.Settings.MaxWidth,
                doc.Settings.MaxHeight,
                doc.Settings.UseColor,
                doc.Subtitles != null && doc.Subtitles.Entries.Count > 0,
                doc.Subtitles?.Entries.Count ?? 0,
                fileInfo.Length,
                fileSizeFormatted
            );
            return JsonSerializer.Serialize(result, McpJsonContext.Default.DocumentInfoResult);
        }
        catch (Exception ex)
        {
            return $"Error reading document: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // YouTube Tools
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Download subtitles from a YouTube video
    /// </summary>
    [McpServerTool(Name = "get_youtube_subtitles")]
    [Description(
        "Download subtitles/captions from a YouTube video. Downloads existing captions (manual or auto-generated) without requiring Whisper transcription. Returns parsed subtitle entries.")]
    public static async Task<string> GetYouTubeSubtitles(
        [Description("YouTube video URL")]
        string url,
        [Description("Output directory for the subtitle file")]
        string outputDirectory,
        [Description("Preferred language code (default: 'en')")]
        string language = "en",
        [Description("Maximum number of entries to return in response (default: all)")]
        int? maxEntries = null)
    {
        if (!YtdlpProvider.IsYouTubeUrl(url))
            return "Error: Not a valid YouTube URL";

        try
        {
            var ytdlpPath = await YtdlpProvider.GetYtdlpPathAsync();

            var subtitlePath = await YtdlpProvider.DownloadSubtitlesAsync(
                url, outputDirectory, language, ytdlpPath);

            if (subtitlePath == null)
                return $"No subtitles available for this video in language '{language}'.";

            // Parse the downloaded subtitle file
            var track = await SubtitleParser.ParseAsync(subtitlePath);

            var entries = track.Entries.AsEnumerable();
            if (maxEntries.HasValue)
                entries = entries.Take(maxEntries.Value);

            var dtos = entries.Select(e => new SubtitleEntryDto(
                e.Index,
                e.StartTime.ToString(@"hh\:mm\:ss\.fff"),
                e.EndTime.ToString(@"hh\:mm\:ss\.fff"),
                e.StartTime.TotalSeconds,
                e.EndTime.TotalSeconds,
                e.Text,
                e.SpeakerId
            )).ToArray();

            var totalDuration = track.GetTotalDuration();
            var result = new SubtitleParseResult(
                true,
                subtitlePath,
                language,
                track.Count,
                totalDuration.ToString(@"hh\:mm\:ss"),
                dtos
            );
            return JsonSerializer.Serialize(result, McpJsonContext.Default.SubtitleParseResult);
        }
        catch (Exception ex)
        {
            return $"Error downloading YouTube subtitles: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Perceptual Hash Tools
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Compute perceptual hash of an image for visual comparison
    /// </summary>
    [McpServerTool(Name = "compute_hash")]
    [Description(
        "Compute a perceptual hash (aHash) of an image file. Returns a 64-bit hash and average brightness. Compare hashes to detect visually similar images — useful for verifying color tuning, checking render fidelity, or detecting duplicate frames. Optionally compare against a second image.")]
    public static string ComputeHash(
        [Description("Path to the image file (JPG, PNG, GIF, WebP, BMP)")]
        string path,
        [Description("Optional: path to a second image for comparison (returns Hamming distance)")]
        string? comparePath = null)
    {
        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        try
        {
            using var image = Image.Load<Rgba32>(path);
            var (hash, avgBrightness) = FrameHasher.ComputeHashWithBrightness(image);

            string? comparisonHash = null;
            int? hammingDistance = null;
            string? message = null;

            if (!string.IsNullOrEmpty(comparePath))
            {
                if (!File.Exists(comparePath))
                    return $"Error: Comparison file not found: {comparePath}";

                using var compareImage = Image.Load<Rgba32>(comparePath);
                var (hash2, _) = FrameHasher.ComputeHashWithBrightness(compareImage);
                comparisonHash = $"0x{hash2:X16}";
                hammingDistance = FrameHasher.HammingDistance(hash, hash2);

                var similarity = hammingDistance.Value switch
                {
                    0 => "identical",
                    <= 5 => "very similar",
                    <= 10 => "similar",
                    <= 20 => "somewhat different",
                    _ => "very different"
                };
                message = $"Hamming distance: {hammingDistance} / 64 bits ({similarity})";
            }

            var result = new PerceptualHashResult(
                $"0x{hash:X16}",
                avgBrightness,
                image.Width,
                image.Height,
                image.Frames.Count,
                comparisonHash,
                hammingDistance,
                message
            );
            return JsonSerializer.Serialize(result, McpJsonContext.Default.PerceptualHashResult);
        }
        catch (Exception ex)
        {
            return $"Error computing hash: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // System Tools
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Check status of external tool dependencies
    /// </summary>
    [McpServerTool(Name = "check_dependencies")]
    [Description(
        "Check the availability and status of external tools (FFmpeg, yt-dlp) required for video processing and YouTube support. Reports what's installed and what needs to be downloaded.")]
    public static string CheckDependencies()
    {
        var results = new Dictionary<string, string>();

        // FFmpeg
        results["ffmpeg_available"] = FFmpegService.IsAvailable().ToString();
        results["ffmpeg_status"] = FFmpegService.GetStatus();

        // yt-dlp
        results["ytdlp_available"] = YtdlpProvider.IsAvailable().ToString();
        results["ytdlp_status"] = YtdlpProvider.GetStatus();
        var (needsDownload, statusMessage, _) = YtdlpProvider.GetDownloadStatus();
        results["ytdlp_needs_download"] = needsDownload.ToString();
        results["ytdlp_detail"] = statusMessage;

        // Supported formats
        results["image_formats"] = "JPG, PNG, GIF, WebP, BMP, TIFF";
        results["video_formats"] = "MP4, MKV, AVI, WebM, MOV, FLV, WMV (requires FFmpeg)";
        results["subtitle_formats"] = "SRT, VTT";
        results["document_formats"] = "CIDZ (compressed), JSON, NDJSON";
        results["export_formats"] = "GIF, SVG, Markdown (plain/html/svg/ansi)";
        results["render_modes"] = "ascii, blocks, braille, matrix";

        return JsonSerializer.Serialize(results, McpJsonContext.Default.DictionaryStringString);
    }
}