// CLI Option definitions for consoleimage

using System.CommandLine;

namespace ConsoleImage.Cli;

/// <summary>
/// All CLI option definitions for the consoleimage command.
/// </summary>
public class CliOptions
{
    // Input
    public Argument<string?> Input { get; }

    // Dimensions
    public Option<int?> Width { get; }
    public Option<int?> Height { get; }
    public Option<int> MaxWidth { get; }
    public Option<int> MaxHeight { get; }

    // Time range (video)
    public Option<double?> Start { get; }
    public Option<double?> End { get; }
    public Option<double?> Duration { get; }

    // Playback
    public Option<float> Speed { get; }
    public Option<int> Loop { get; }
    public Option<double?> Fps { get; }
    public Option<string> FrameStep { get; }
    public Option<string> Sampling { get; }
    public Option<double> SceneThreshold { get; }

    // Render modes
    public Option<bool> Ascii { get; }
    public Option<bool> Blocks { get; }
    public Option<bool> Braille { get; }
    public Option<bool> Matrix { get; }
    public Option<bool> Monochrome { get; }
    public Option<string?> MatrixColor { get; }
    public Option<bool> MatrixFullColor { get; }
    public Option<float?> MatrixDensity { get; }
    public Option<float?> MatrixSpeed { get; }
    public Option<string?> MatrixAlphabet { get; }

    // Color/rendering
    public Option<bool> NoColor { get; }
    public Option<int?> Colors { get; }
    public Option<float> Contrast { get; }
    public Option<float> Gamma { get; }
    public Option<float?> CharAspect { get; }
    public Option<string?> Charset { get; }
    public Option<string?> Preset { get; }
    public Option<string?> Mode { get; }

    // Performance
    public Option<int> Buffer { get; }
    public Option<bool> NoHwAccel { get; }
    public Option<bool> NoAltScreen { get; }
    public Option<bool> NoParallel { get; }
    public Option<bool> NoDither { get; }
    public Option<bool> NoEdgeChars { get; }

    // FFmpeg
    public Option<string?> FfmpegPath { get; }
    public Option<bool> NoFfmpegDownload { get; }
    public Option<bool> FfmpegYes { get; }

    // yt-dlp (YouTube support)
    public Option<string?> YtdlpPath { get; }
    public Option<string?> CookiesFromBrowser { get; }
    public Option<string?> CookiesFile { get; }

    // Subtitles - unified flag: auto|off|<path>|yt|whisper|whisper+diarize
    public Option<string?> Subs { get; }
    public Option<string> SubtitleLang { get; }
    public Option<string> WhisperModel { get; }
    public Option<int?> WhisperThreads { get; }

    // Transcript-only mode (no video rendering)
    public Option<bool> Transcript { get; }
    public Option<bool> ForceSubs { get; }

    // Output
    public Option<string?> Output { get; }
    public Option<bool> Info { get; }
    public Option<bool> Json { get; }
    public Option<bool> Status { get; }

    // GIF output
    public Option<int> GifFontSize { get; }
    public Option<float> GifScale { get; }
    public Option<int> GifFps { get; }
    public Option<double?> GifLength { get; }
    public Option<int?> GifFrames { get; }
    public Option<int?> GifWidth { get; }
    public Option<int?> GifHeight { get; }

    // Raw/extract mode
    public Option<bool> Raw { get; }
    public Option<int?> RawWidth { get; }
    public Option<int?> RawHeight { get; }
    public Option<bool> SmartKeyframes { get; }
    public Option<int> Quality { get; }

    // Calibration
    public Option<bool> Calibrate { get; }
    public Option<bool> SaveCalibration { get; }

    // Image adjustments
    public Option<bool> NoInvert { get; }
    public Option<bool> Edge { get; }
    public Option<float?> BgThreshold { get; }
    public Option<float?> DarkBgThreshold { get; }
    public Option<bool> AutoBg { get; }
    public Option<float?> DarkCutoff { get; }
    public Option<float?> LightCutoff { get; }

    // Temporal stability
    public Option<bool> Dejitter { get; }
    public Option<int?> ColorThreshold { get; }

    // Slideshow mode
    public Option<float> SlideDelay { get; }
    public Option<bool> Shuffle { get; }
    public Option<bool> Recursive { get; }
    public Option<string> SortBy { get; }
    public Option<bool> SortDesc { get; }
    public Option<bool> SortAsc { get; }
    public Option<float> VideoPreview { get; }
    public Option<bool> GifLoop { get; }
    public Option<bool> HideSlideInfo { get; }

    // Easter egg
    public Option<bool> EasterEgg { get; }

    // Debug mode
    public Option<bool> Debug { get; }

    public CliOptions()
    {
        // Detect console window size for defaults
        var defaultMaxWidth = 120;
        var defaultMaxHeight = 40;
        try
        {
            if (Console.WindowWidth > 0) defaultMaxWidth = Console.WindowWidth - 1;
            if (Console.WindowHeight > 0) defaultMaxHeight = Console.WindowHeight - 2;
        }
        catch { }

        // Input
        Input = new Argument<string?>("input")
        {
            Description = "Path to image, GIF, video, or cidz/json document",
            Arity = ArgumentArity.ZeroOrOne
        };

        // Dimensions
        Width = new Option<int?>("--width") { Description = "Output width in characters" };
        Width.Aliases.Add("-w");

        Height = new Option<int?>("--height") { Description = "Output height in characters" };
        Height.Aliases.Add("-h");

        MaxWidth = new Option<int>("--max-width") { Description = "Maximum output width" };
        MaxWidth.DefaultValueFactory = _ => defaultMaxWidth;

        MaxHeight = new Option<int>("--max-height") { Description = "Maximum output height" };
        MaxHeight.DefaultValueFactory = _ => defaultMaxHeight;

        // Time range
        Start = new Option<double?>("--start") { Description = "Start time in seconds" };
        Start.Aliases.Add("-ss");
        Start.Aliases.Add("--ss");

        End = new Option<double?>("--end") { Description = "End time in seconds" };
        End.Aliases.Add("-to");

        Duration = new Option<double?>("--duration") { Description = "Duration to play in seconds" };
        Duration.Aliases.Add("-t");

        // Playback
        Speed = new Option<float>("--speed") { Description = "Playback speed multiplier" };
        Speed.DefaultValueFactory = _ => 1.0f;
        Speed.Aliases.Add("-s");

        Loop = new Option<int>("--loop") { Description = "Number of loops (0 = infinite)" };
        Loop.DefaultValueFactory = _ => 1;
        Loop.Aliases.Add("-l");

        Fps = new Option<double?>("--fps") { Description = "Target framerate" };
        Fps.Aliases.Add("-r");

        FrameStep = new Option<string>("--frame-step") { Description = "Frame step: 1 (every frame), 2 (every 2nd), s/smart (skip similar frames)" };
        FrameStep.DefaultValueFactory = _ => "1";
        FrameStep.Aliases.Add("-f");

        Sampling = new Option<string>("--sampling") { Description = "Sampling strategy: uniform, keyframe, scene, adaptive" };
        Sampling.DefaultValueFactory = _ => "uniform";

        SceneThreshold = new Option<double>("--scene-threshold") { Description = "Scene detection threshold (0.0-1.0)" };
        SceneThreshold.DefaultValueFactory = _ => 0.4;

        // Render modes (braille is default - highest detail, smallest output)
        Ascii = new Option<bool>("--ascii") { Description = "Use ASCII characters instead of braille" };
        Ascii.Aliases.Add("-a");

        Blocks = new Option<bool>("--blocks") { Description = "Use colored Unicode blocks instead of braille" };
        Blocks.Aliases.Add("-b");

        Braille = new Option<bool>("--braille") { Description = "Use braille characters (DEFAULT - 2x4 dots per cell, highest detail)" };
        Braille.Aliases.Add("-B");
        Braille.DefaultValueFactory = _ => true;

        Matrix = new Option<bool>("--matrix") { Description = "Use Matrix digital rain effect" };
        Matrix.Aliases.Add("-M");

        Monochrome = new Option<bool>("--monochrome") { Description = "Use braille mode without color (compact, high-detail greyscale)" };
        Monochrome.Aliases.Add("--mono");

        MatrixColor = new Option<string?>("--matrix-color") { Description = "Matrix color: green, red, blue, amber, cyan, purple, or hex (#RRGGBB)" };
        MatrixFullColor = new Option<bool>("--matrix-fullcolor") { Description = "Use source image colors with Matrix lighting" };
        MatrixDensity = new Option<float?>("--matrix-density") { Description = "Rain density (0.1-2.0, default 0.5)" };
        MatrixSpeed = new Option<float?>("--matrix-speed") { Description = "Rain speed multiplier (0.5-3.0, default 1.0)" };
        MatrixAlphabet = new Option<string?>("--matrix-alphabet") { Description = "Custom character set for rain" };

        // Color/rendering
        NoColor = new Option<bool>("--no-color") { Description = "Disable colored output" };

        Colors = new Option<int?>("--colors") { Description = "Max colors in palette (4, 16, 256)" };
        Colors.Aliases.Add("-c");
        Colors.Aliases.Add("--colours"); // British English alias

        Contrast = new Option<float>("--contrast") { Description = "Contrast enhancement (1.0 = none)" };
        Contrast.DefaultValueFactory = _ => 2.5f;

        Gamma = new Option<float>("--gamma") { Description = "Gamma correction (< 1.0 brighter)" };
        Gamma.DefaultValueFactory = _ => 0.65f;
        Gamma.Aliases.Add("-g");

        CharAspect = new Option<float?>("--char-aspect") { Description = "Character aspect ratio (width/height)" };
        Charset = new Option<string?>("--charset") { Description = "Custom character set (light to dark)" };

        Preset = new Option<string?>("--preset") { Description = "Preset: extended, simple, block, classic" };
        Preset.Aliases.Add("-p");

        Mode = new Option<string?>("--mode") { Description = "Render mode: ascii, blocks, braille, mono (greyscale braille), matrix" };
        Mode.Aliases.Add("-m");

        // Performance
        Buffer = new Option<int>("--buffer") { Description = "Frames to buffer ahead (2-10)" };
        Buffer.DefaultValueFactory = _ => 3;

        NoHwAccel = new Option<bool>("--no-hwaccel") { Description = "Disable hardware acceleration" };
        NoAltScreen = new Option<bool>("--no-alt-screen") { Description = "Disable alternate screen buffer" };
        NoParallel = new Option<bool>("--no-parallel") { Description = "Disable parallel processing" };
        NoDither = new Option<bool>("--no-dither") { Description = "Disable Floyd-Steinberg dithering" };
        NoEdgeChars = new Option<bool>("--no-edge-chars") { Description = "Disable directional edge characters" };

        // FFmpeg
        FfmpegPath = new Option<string?>("--ffmpeg-path") { Description = "Path to FFmpeg executable" };
        NoFfmpegDownload = new Option<bool>("--no-ffmpeg-download") { Description = "Don't auto-download FFmpeg" };
        FfmpegYes = new Option<bool>("--yes") { Description = "Auto-confirm FFmpeg/yt-dlp download" };
        FfmpegYes.Aliases.Add("-y");

        YtdlpPath = new Option<string?>("--ytdlp-path") { Description = "Path to yt-dlp executable (for YouTube URLs)" };
        CookiesFromBrowser = new Option<string?>("--cookies-from-browser") { Description = "Use cookies from browser (chrome, firefox, edge) for YouTube" };
        CookiesFile = new Option<string?>("--cookies") { Description = "Path to Netscape cookies.txt file for YouTube" };

        // Subtitles - unified: auto|off|<path>|yt|whisper|whisper+diarize
        Subs = new Option<string?>("--subs") { Description = "Subtitles: auto, off, <path>, yt, whisper, whisper+diarize" };
        Subs.Aliases.Add("--sub");
        Subs.Aliases.Add("--srt");

        SubtitleLang = new Option<string>("--sub-lang") { Description = "Subtitle language (default: en)" };
        SubtitleLang.DefaultValueFactory = _ => "en";

        // Whisper options (used with --subs whisper)
        WhisperModel = new Option<string>("--whisper-model") { Description = "Whisper model: tiny, base, small, medium, large" };
        WhisperModel.DefaultValueFactory = _ => "base";
        WhisperModel.Aliases.Add("-wm");

        WhisperThreads = new Option<int?>("--whisper-threads") { Description = "CPU threads for Whisper (default: half available)" };

        // Transcript-only mode - generates subtitles, no video rendering
        Transcript = new Option<bool>("--transcript") { Description = "Transcript only: generate subtitles without video (streams text to stdout)" };
        Transcript.Aliases.Add("-T");
        Transcript.Aliases.Add("--transcribe");

        ForceSubs = new Option<bool>("--force-subs") { Description = "Force re-transcription, ignoring cached subtitles" };
        ForceSubs.Aliases.Add("--force-transcribe");

        // Output
        Output = new Option<string?>("--output") { Description = "Output file (.gif, .cidz, .json)" };
        Output.Aliases.Add("-o");

        Info = new Option<bool>("--info") { Description = "Show info and exit" };
        Info.Aliases.Add("-i");

        Json = new Option<bool>("--json") { Description = "Output as JSON" };
        Json.Aliases.Add("-j");

        Status = new Option<bool>("--status") { Description = "Show status line below output" };
        Status.Aliases.Add("-S");

        // GIF output
        GifFontSize = new Option<int>("--gif-font-size") { Description = "Font size for GIF output" };
        GifFontSize.DefaultValueFactory = _ => 10;

        GifScale = new Option<float>("--gif-scale") { Description = "Scale factor for GIF output" };
        GifScale.DefaultValueFactory = _ => 1.0f;

        GifFps = new Option<int>("--gif-fps") { Description = "Target FPS for GIF output" };
        GifFps.DefaultValueFactory = _ => 15;

        GifLength = new Option<double?>("--gif-length") { Description = "Max GIF length in seconds" };
        GifFrames = new Option<int?>("--gif-frames") { Description = "Max frames for GIF output" };
        GifWidth = new Option<int?>("--gif-width") { Description = "GIF output width in characters" };
        GifHeight = new Option<int?>("--gif-height") { Description = "GIF output height in characters" };

        // Raw/extract
        Raw = new Option<bool>("--raw") { Description = "Extract raw frames as GIF (no ASCII)" };
        Raw.Aliases.Add("--extract");

        RawWidth = new Option<int?>("--raw-width") { Description = "Width for raw GIF output in pixels" };
        RawHeight = new Option<int?>("--raw-height") { Description = "Height for raw GIF output in pixels" };

        SmartKeyframes = new Option<bool>("--smart-keyframes") { Description = "Use smart scene detection" };
        SmartKeyframes.Aliases.Add("--smart");

        Quality = new Option<int>("--quality") { Description = "Output quality 1-100 (for JPEG, WebP)" };
        Quality.DefaultValueFactory = _ => 85;
        Quality.Aliases.Add("-q");

        // Calibration
        Calibrate = new Option<bool>("--calibrate") { Description = "Display calibration pattern" };
        SaveCalibration = new Option<bool>("--save") { Description = "Save calibration to calibration.json" };

        // Image adjustments
        NoInvert = new Option<bool>("--no-invert") { Description = "Don't invert (for light backgrounds)" };
        Edge = new Option<bool>("--edge") { Description = "Enable edge detection" };
        Edge.Aliases.Add("-e");

        BgThreshold = new Option<float?>("--bg-threshold") { Description = "Light background suppression threshold" };
        DarkBgThreshold = new Option<float?>("--dark-bg-threshold") { Description = "Dark background suppression threshold" };
        AutoBg = new Option<bool>("--auto-bg") { Description = "Auto-detect and suppress background" };
        DarkCutoff = new Option<float?>("--dark-cutoff") { Description = "Skip colors below this brightness" };
        LightCutoff = new Option<float?>("--light-cutoff") { Description = "Skip colors above this brightness" };

        // Temporal stability
        Dejitter = new Option<bool>("--dejitter") { Description = "Enable temporal stability (reduce flickering in animations)" };
        Dejitter.Aliases.Add("--stabilize");

        ColorThreshold = new Option<int?>("--color-threshold") { Description = "Color stability threshold (0-255, default 15)" };

        // Slideshow mode
        SlideDelay = new Option<float>("--slide-delay") { Description = "Delay between images in seconds (0 = manual only, no auto-advance)" };
        SlideDelay.DefaultValueFactory = _ => 3.0f;
        SlideDelay.Aliases.Add("--interval");
        SlideDelay.Aliases.Add("-d");

        Shuffle = new Option<bool>("--shuffle") { Description = "Randomize image order (slideshow mode)" };

        Recursive = new Option<bool>("--recursive") { Description = "Include subdirectories (slideshow mode)" };
        Recursive.Aliases.Add("-R");

        SortBy = new Option<string>("--sort") { Description = "Sort order: name, date, size, random (slideshow mode)" };
        SortBy.DefaultValueFactory = _ => "date";
        SortBy.Aliases.Add("--order-by");

        SortDesc = new Option<bool>("--desc") { Description = "Sort descending (slideshow mode, default: true for newest first)" };
        SortDesc.DefaultValueFactory = _ => true;

        SortAsc = new Option<bool>("--asc") { Description = "Sort ascending (oldest first, slideshow mode)" };

        VideoPreview = new Option<float>("--video-preview") { Description = "Max video preview duration in seconds (slideshow mode)" };
        VideoPreview.DefaultValueFactory = _ => 30.0f;
        VideoPreview.Aliases.Add("--preview");

        GifLoop = new Option<bool>("--gif-loop") { Description = "Loop GIFs in slideshow (default: play once)" };

        HideSlideInfo = new Option<bool>("--hide-info") { Description = "Hide [1/N] filename header in slideshow" };
        HideSlideInfo.Aliases.Add("--no-header");

        // Easter egg
        EasterEgg = new Option<bool>("--ee") { Description = "Play animation demo" };

        Debug = new Option<bool>("--debug") { Description = "Enable debug output for smart frame sampling" };
    }

    /// <summary>
    /// Check if input looks like a glob pattern or directory for slideshow mode.
    /// </summary>
    public static bool IsSlideshowInput(string? input)
    {
        if (string.IsNullOrEmpty(input)) return false;

        // URLs are never slideshows (YouTube URLs contain '?' which would match glob)
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;

        // Glob patterns
        if (input.Contains('*') || input.Contains('?')) return true;

        // Directory
        if (Directory.Exists(input)) return true;

        return false;
    }

    /// <summary>
    /// Add all options to a command.
    /// </summary>
    public void AddToCommand(RootCommand command)
    {
        command.Arguments.Add(Input);

        // Add in logical groups
        command.Options.Add(Width);
        command.Options.Add(Height);
        command.Options.Add(MaxWidth);
        command.Options.Add(MaxHeight);

        command.Options.Add(Start);
        command.Options.Add(End);
        command.Options.Add(Duration);

        command.Options.Add(Speed);
        command.Options.Add(Loop);
        command.Options.Add(Fps);
        command.Options.Add(FrameStep);
        command.Options.Add(Sampling);
        command.Options.Add(SceneThreshold);

        command.Options.Add(Ascii);
        command.Options.Add(Blocks);
        command.Options.Add(Braille);
        command.Options.Add(Matrix);
        command.Options.Add(Monochrome);
        command.Options.Add(MatrixColor);
        command.Options.Add(MatrixFullColor);
        command.Options.Add(MatrixDensity);
        command.Options.Add(MatrixSpeed);
        command.Options.Add(MatrixAlphabet);

        command.Options.Add(NoColor);
        command.Options.Add(Colors);
        command.Options.Add(Contrast);
        command.Options.Add(Gamma);
        command.Options.Add(CharAspect);
        command.Options.Add(Charset);
        command.Options.Add(Preset);
        command.Options.Add(Mode);

        command.Options.Add(Buffer);
        command.Options.Add(NoHwAccel);
        command.Options.Add(NoAltScreen);
        command.Options.Add(NoParallel);
        command.Options.Add(NoDither);
        command.Options.Add(NoEdgeChars);

        command.Options.Add(FfmpegPath);
        command.Options.Add(NoFfmpegDownload);
        command.Options.Add(FfmpegYes);
        command.Options.Add(YtdlpPath);
        command.Options.Add(CookiesFromBrowser);
        command.Options.Add(CookiesFile);

        // Subtitles
        command.Options.Add(Subs);
        command.Options.Add(SubtitleLang);
        command.Options.Add(WhisperModel);
        command.Options.Add(WhisperThreads);
        command.Options.Add(Transcript);
        command.Options.Add(ForceSubs);

        command.Options.Add(Output);
        command.Options.Add(Info);
        command.Options.Add(Json);
        command.Options.Add(Status);

        command.Options.Add(GifFontSize);
        command.Options.Add(GifScale);
        command.Options.Add(GifFps);
        command.Options.Add(GifLength);
        command.Options.Add(GifFrames);
        command.Options.Add(GifWidth);
        command.Options.Add(GifHeight);

        command.Options.Add(Raw);
        command.Options.Add(RawWidth);
        command.Options.Add(RawHeight);
        command.Options.Add(SmartKeyframes);
        command.Options.Add(Quality);

        command.Options.Add(Calibrate);
        command.Options.Add(SaveCalibration);

        command.Options.Add(NoInvert);
        command.Options.Add(Edge);
        command.Options.Add(BgThreshold);
        command.Options.Add(DarkBgThreshold);
        command.Options.Add(AutoBg);
        command.Options.Add(DarkCutoff);
        command.Options.Add(LightCutoff);

        command.Options.Add(Dejitter);
        command.Options.Add(ColorThreshold);

        // Slideshow mode
        command.Options.Add(SlideDelay);
        command.Options.Add(Shuffle);
        command.Options.Add(Recursive);
        command.Options.Add(SortBy);
        command.Options.Add(SortDesc);
        command.Options.Add(SortAsc);
        command.Options.Add(VideoPreview);
        command.Options.Add(GifLoop);
        command.Options.Add(HideSlideInfo);
        command.Options.Add(EasterEgg);
        command.Options.Add(Debug);
    }
}
