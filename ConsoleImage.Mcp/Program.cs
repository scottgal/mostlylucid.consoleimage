using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConsoleImage.Core;
using ConsoleImage.Video.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
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
    ///     Get information about a video file using FFmpeg
    /// </summary>
    [McpServerTool(Name = "get_video_info")]
    [Description("Get information about a video file including duration, resolution, and codec.")]
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

            var result = new VideoInfoResult(
                Path.GetFileName(path),
                info.Duration,
                TimeSpan.FromSeconds(info.Duration).ToString(@"hh\:mm\:ss"),
                info.Width,
                info.Height,
                info.FrameRate,
                info.VideoCodec
            );
            return JsonSerializer.Serialize(result, McpJsonContext.Default.VideoInfoResult);
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
    [Description("Extract raw video frames to an animated GIF file. No ASCII rendering - preserves actual video frames. Useful for creating thumbnails, previews, or scene slideshows.")]
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

                using var gif = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, targetHeight);
                var gifMeta = gif.Metadata.GetGifMetadata();
                gifMeta.RepeatCount = 0;

                var frameCount = 0;
                foreach (var timestamp in keyframeTimes)
                {
                    var frame = await ffmpeg.ExtractFrameAsync(inputPath, timestamp, width, targetHeight, CancellationToken.None);
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
                    if (streamingGif.ShouldStop) { frame.Dispose(); break; }
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
}