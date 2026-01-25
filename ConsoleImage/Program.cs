// ConsoleImage - Unified ASCII art CLI for images, GIFs, videos, and documents
// Supports multiple render modes: ASCII, ColorBlocks, Braille, Matrix

using System.CommandLine;
using System.Reflection;
using ConsoleImage.Cli;
using ConsoleImage.Cli.Handlers;
using ConsoleImage.Cli.Utilities;
using ConsoleImage.Core;
using ConsoleImage.Core.Subtitles;
using ConsoleImage.Transcription;

// Enable ANSI escape sequence processing on Windows
ConsoleHelper.EnableAnsiSupport();

// Easter egg: --ee plays hidden animation
if (args.Length == 1 && args[0] == "--ee")
{
    await PlayEasterEggAsync();
    return 0;
}

// No arguments: show help and prompt for input
if (args.Length == 0)
{
    ShowHelpAndPrompt();
    return 0;
}

// Load saved calibration if exists
var savedCalibration = CalibrationHelper.Load();

// Create CLI options and root command
var cliOptions = new CliOptions();
var rootCommand = new RootCommand("Render images, GIFs, videos, and documents as ASCII art");
cliOptions.AddToCommand(rootCommand);

// Add transcribe subcommand: consoleimage transcribe <input> -o output.vtt
var transcribeCommand = CreateTranscribeSubcommand();
rootCommand.Subcommands.Add(transcribeCommand);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    // Parse all option values
    var inputPath = parseResult.GetValue(cliOptions.Input);
    var input = !string.IsNullOrEmpty(inputPath) ? new FileInfo(inputPath) : null;
    var width = parseResult.GetValue(cliOptions.Width);
    var height = parseResult.GetValue(cliOptions.Height);
    var maxWidth = parseResult.GetValue(cliOptions.MaxWidth);
    var maxHeight = parseResult.GetValue(cliOptions.MaxHeight);
    var start = parseResult.GetValue(cliOptions.Start);
    var end = parseResult.GetValue(cliOptions.End);
    var duration = parseResult.GetValue(cliOptions.Duration);
    var speed = parseResult.GetValue(cliOptions.Speed);
    var loop = parseResult.GetValue(cliOptions.Loop);
    var fps = parseResult.GetValue(cliOptions.Fps);
    var frameStep = parseResult.GetValue(cliOptions.FrameStep);
    var sampling = parseResult.GetValue(cliOptions.Sampling);
    var sceneThreshold = parseResult.GetValue(cliOptions.SceneThreshold);
    var useAsciiOpt = parseResult.GetValue(cliOptions.Ascii);
    var useBlocksOpt = parseResult.GetValue(cliOptions.Blocks);
    var useBrailleOpt = parseResult.GetValue(cliOptions.Braille);
    var useMatrixOpt = parseResult.GetValue(cliOptions.Matrix);
    var useMonochromeOpt = parseResult.GetValue(cliOptions.Monochrome);
    var modeOpt = parseResult.GetValue(cliOptions.Mode);
    var noColor = parseResult.GetValue(cliOptions.NoColor);

    // Resolve render mode: -m/--mode takes priority, then individual flags, then default (braille)
    bool useAscii, useBlocks, useBraille, useMatrix;
    if (!string.IsNullOrEmpty(modeOpt))
    {
        // Mode string specified - use that exclusively
        var modeLower = modeOpt.ToLowerInvariant();
        useAscii = modeLower == "ascii";
        useBlocks = modeLower == "blocks";
        useBraille = modeLower == "braille" || modeLower == "mono" || modeLower == "monochrome";
        useMatrix = modeLower == "matrix";
        // Monochrome mode = braille + no-color
        if (modeLower == "mono" || modeLower == "monochrome")
            noColor = true;
        // Unknown modes fall back to braille
        if (!useAscii && !useBlocks && !useBraille && !useMatrix)
            useBraille = true;
    }
    else
    {
        // Use individual flags - braille is default unless other flags are set
        useAscii = useAsciiOpt;
        useBlocks = useBlocksOpt;
        useMatrix = useMatrixOpt;
        // --monochrome is shorthand for braille + no-color
        if (useMonochromeOpt)
        {
            useBraille = true;
            noColor = true;
        }
        else
        {
            // Braille is default, but --ascii, --blocks, or --matrix override it
            useBraille = !useAscii && !useBlocks && !useMatrix;
        }
    }
    var matrixColor = parseResult.GetValue(cliOptions.MatrixColor);
    var matrixFullColor = parseResult.GetValue(cliOptions.MatrixFullColor);
    var matrixDensity = parseResult.GetValue(cliOptions.MatrixDensity);
    var matrixSpeed = parseResult.GetValue(cliOptions.MatrixSpeed);
    var matrixAlphabet = parseResult.GetValue(cliOptions.MatrixAlphabet);
    var colorCount = parseResult.GetValue(cliOptions.Colors);
    var contrast = parseResult.GetValue(cliOptions.Contrast);
    var gamma = parseResult.GetValue(cliOptions.Gamma);
    var charAspect = parseResult.GetValue(cliOptions.CharAspect);
    var charset = parseResult.GetValue(cliOptions.Charset);
    var preset = parseResult.GetValue(cliOptions.Preset);
    var buffer = parseResult.GetValue(cliOptions.Buffer);
    var noHwAccel = parseResult.GetValue(cliOptions.NoHwAccel);
    var noAltScreen = parseResult.GetValue(cliOptions.NoAltScreen);
    var ffmpegPath = parseResult.GetValue(cliOptions.FfmpegPath);
    var showInfo = parseResult.GetValue(cliOptions.Info);
    var calibrate = parseResult.GetValue(cliOptions.Calibrate);
    var saveCalibrationOpt = parseResult.GetValue(cliOptions.SaveCalibration);
    var showStatus = parseResult.GetValue(cliOptions.Status);
    var gifFontSize = parseResult.GetValue(cliOptions.GifFontSize);
    var gifScale = parseResult.GetValue(cliOptions.GifScale);
    var gifColors = colorCount ?? 64;
    var gifFps = parseResult.GetValue(cliOptions.GifFps);
    var gifLength = parseResult.GetValue(cliOptions.GifLength);
    var gifFrames = parseResult.GetValue(cliOptions.GifFrames);
    var gifWidth = parseResult.GetValue(cliOptions.GifWidth);
    var gifHeight = parseResult.GetValue(cliOptions.GifHeight);
    var rawMode = parseResult.GetValue(cliOptions.Raw);
    var rawWidth = parseResult.GetValue(cliOptions.RawWidth);
    var rawHeight = parseResult.GetValue(cliOptions.RawHeight);
    var smartKeyframes = parseResult.GetValue(cliOptions.SmartKeyframes);
    var quality = parseResult.GetValue(cliOptions.Quality);
    var noAutoDownload = parseResult.GetValue(cliOptions.NoFfmpegDownload);
    var autoConfirmDownload = parseResult.GetValue(cliOptions.FfmpegYes);
    var output = parseResult.GetValue(cliOptions.Output);
    var noInvert = parseResult.GetValue(cliOptions.NoInvert);
    var enableEdge = parseResult.GetValue(cliOptions.Edge);
    var bgThreshold = parseResult.GetValue(cliOptions.BgThreshold);
    var darkBgThreshold = parseResult.GetValue(cliOptions.DarkBgThreshold);
    var autoBg = parseResult.GetValue(cliOptions.AutoBg);
    var noParallel = parseResult.GetValue(cliOptions.NoParallel);
    var noDither = parseResult.GetValue(cliOptions.NoDither);
    var noEdgeChars = parseResult.GetValue(cliOptions.NoEdgeChars);
    var jsonOutput = parseResult.GetValue(cliOptions.Json);
    var darkCutoff = parseResult.GetValue(cliOptions.DarkCutoff);
    var lightCutoff = parseResult.GetValue(cliOptions.LightCutoff);
    var dejitter = parseResult.GetValue(cliOptions.Dejitter);
    var colorThreshold = parseResult.GetValue(cliOptions.ColorThreshold);
    var debug = parseResult.GetValue(cliOptions.Debug);

    // Subtitle options - unified: auto|off|<path>|yt|whisper|whisper+diarize
    var subsValue = parseResult.GetValue(cliOptions.Subs);
    var subtitleLang = parseResult.GetValue(cliOptions.SubtitleLang);
    var whisperModel = parseResult.GetValue(cliOptions.WhisperModel);
    var whisperThreads = parseResult.GetValue(cliOptions.WhisperThreads);
    var transcriptOnly = parseResult.GetValue(cliOptions.Transcript);
    var forceSubs = parseResult.GetValue(cliOptions.ForceSubs);
    var cookiesFromBrowser = parseResult.GetValue(cliOptions.CookiesFromBrowser);
    var cookiesFile = parseResult.GetValue(cliOptions.CookiesFile);

    // Slideshow mode
    var slideDelay = parseResult.GetValue(cliOptions.SlideDelay);
    var shuffle = parseResult.GetValue(cliOptions.Shuffle);
    var recursive = parseResult.GetValue(cliOptions.Recursive);
    var sortBy = parseResult.GetValue(cliOptions.SortBy);
    var sortDescOpt = parseResult.GetValue(cliOptions.SortDesc);
    var sortAscOpt = parseResult.GetValue(cliOptions.SortAsc);
    // --asc overrides --desc (default is descending/newest first)
    var sortDesc = sortAscOpt ? false : sortDescOpt;
    var videoPreview = parseResult.GetValue(cliOptions.VideoPreview);
    var gifLoop = parseResult.GetValue(cliOptions.GifLoop);
    var hideSlideInfo = parseResult.GetValue(cliOptions.HideSlideInfo);

    // Parse unified output option - auto-detect format from extension
    var (outputAsJson, outputAsCompressed, jsonOutputPath, gifOutputPath) =
        ParseOutputOption(output);

    var outputGif = !string.IsNullOrEmpty(gifOutputPath) ? new FileInfo(gifOutputPath) : null;

    // Calibration mode
    if (calibrate)
        return CalibrationHandler.Handle(
            useBraille, useBlocks, useMatrix,
            charAspect, savedCalibration,
            noColor, saveCalibrationOpt);

    // Input required for non-calibration modes
    if (string.IsNullOrEmpty(inputPath))
    {
        Console.Error.WriteLine(
            "Error: No input file specified. Use --calibrate for aspect ratio testing without a file.");
        return 1;
    }

    // Slideshow mode - for directories and glob patterns
    if (CliOptions.IsSlideshowInput(inputPath))
    {
        // Use smaller defaults for slideshow (50x30) unless user specified width/height
        var slideshowMaxWidth = width.HasValue ? maxWidth : Math.Min(maxWidth, 50);
        var slideshowMaxHeight = height.HasValue ? maxHeight : Math.Min(maxHeight, 30);

        var slideshowOptions = new SlideshowOptions
        {
            SlideDelay = slideDelay,
            Shuffle = shuffle,
            Recursive = recursive,
            SortBy = sortBy ?? "date",
            SortDesc = sortDesc,
            VideoPreview = videoPreview,
            GifLoop = gifLoop,
            HideStatus = hideSlideInfo,
            OutputPath = gifOutputPath,
            GifFontSize = gifFontSize,
            GifScale = gifScale,
            GifColors = gifColors,
            Width = width,
            Height = height,
            MaxWidth = slideshowMaxWidth,
            MaxHeight = slideshowMaxHeight,
            UseAscii = useAscii,
            UseBlocks = useBlocks,
            UseBraille = useBraille,
            UseMatrix = useMatrix,
            UseColor = !noColor,
            Contrast = contrast,
            Gamma = gamma,
            CharAspect = charAspect,
            ShowStatus = showStatus
        };
        return await SlideshowHandler.HandleAsync(inputPath, slideshowOptions, cancellationToken);
    }

    // Parse yt-dlp path option
    var ytdlpPath = parseResult.GetValue(cliOptions.YtdlpPath);

    // Check for YouTube URL first
    var isYouTube = YtdlpProvider.IsYouTubeUrl(inputPath);
    var isUrl = isYouTube || UrlHelper.IsUrl(inputPath);
    string inputFullPath;
    string? tempFile = null;
    string? youtubeTitle = null;

    if (isYouTube)
    {
        inputPath = UrlHelper.NormalizeUrl(inputPath);
        Console.Error.WriteLine($"YouTube URL detected: {inputPath}");

        // Check if yt-dlp is available, offer to download if not
        if (!YtdlpProvider.IsAvailable(ytdlpPath))
        {
            var (needsDownload, statusMsg, downloadUrl) = YtdlpProvider.GetDownloadStatus();

            if (needsDownload && downloadUrl != null)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(statusMsg);
                Console.Error.WriteLine();

                // Auto-confirm if -y flag was passed
                if (!autoConfirmDownload)
                {
                    Console.Error.Write("Download yt-dlp now? [y/N]: ");
                    var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                    if (response != "y" && response != "yes")
                    {
                        Console.Error.WriteLine();
                        Console.Error.WriteLine("Install yt-dlp manually:");
                        Console.Error.WriteLine("  Windows: winget install yt-dlp");
                        Console.Error.WriteLine("  macOS:   brew install yt-dlp");
                        Console.Error.WriteLine("  Linux:   pip install yt-dlp");
                        Console.Error.WriteLine();
                        Console.Error.WriteLine("Or use --ytdlp-path to specify location.");
                        return 1;
                    }
                }

                // Download yt-dlp
                var progress = new Progress<(string Status, double Progress)>(p =>
                {
                    Console.Error.Write($"\r{p.Status,-50}");
                });

                try
                {
                    ytdlpPath = await YtdlpProvider.DownloadAsync(progress, cancellationToken);
                    Console.Error.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"\nDownload failed: {ex.Message}");
                    return 1;
                }
            }
            else
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(statusMsg);
                Console.Error.WriteLine();
                Console.Error.WriteLine("Install yt-dlp:");
                Console.Error.WriteLine("  Windows: winget install yt-dlp");
                Console.Error.WriteLine("  macOS:   brew install yt-dlp");
                Console.Error.WriteLine("  Linux:   pip install yt-dlp");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Or use --ytdlp-path to specify location.");
                return 1;
            }
        }

        Console.Error.Write("Extracting video stream URL... ");

        // For ASCII rendering, we don't need high resolution - 480p is plenty
        var ytMaxHeight = useBraille ? 480 : 360;
        // Pass start time to yt-dlp so it can use download-sections for efficient seeking
        var streamInfo = await YtdlpProvider.GetStreamInfoAsync(inputPath, ytdlpPath, ytMaxHeight, start, cookiesFromBrowser, cookiesFile, cancellationToken);

        if (streamInfo == null)
        {
            Console.Error.WriteLine("failed.");
            Console.Error.WriteLine("Could not extract video stream from YouTube URL.");
            return 1;
        }

        Console.Error.WriteLine("done.");
        Console.Error.WriteLine($"  Title: {streamInfo.Title}");
        youtubeTitle = streamInfo.Title;

        // Use the video URL directly with FFmpeg
        inputFullPath = streamInfo.VideoUrl;
        input = new FileInfo("youtube.mp4"); // Dummy for extension detection

        // Download YouTube subtitles if requested (--subs auto, yt, or whisper)
        var (ytSubSource, _, _) = ParseSubsOption(subsValue);
        if (ytSubSource is "auto" or "yt")
        {
            Console.Error.Write($"Downloading subtitles ({subtitleLang})... ");
            var tempDir = Path.Combine(Path.GetTempPath(), "consoleimage_subs");
            var srtPath = await YtdlpProvider.DownloadSubtitlesAsync(
                inputPath, tempDir, subtitleLang, ytdlpPath, cookiesFromBrowser, cookiesFile, cancellationToken);

            if (!string.IsNullOrEmpty(srtPath))
            {
                Console.Error.WriteLine("done.");
                // Override subsValue to use the downloaded file
                subsValue = srtPath;
            }
            else
            {
                Console.Error.WriteLine("not available.");
                // If auto mode and YT subs not available, fall back to whisper
                if (ytSubSource == "auto")
                    subsValue = "whisper";
            }
        }
    }
    else if (isUrl)
    {
        inputPath = UrlHelper.NormalizeUrl(inputPath);
        Console.Error.WriteLine($"URL detected: {inputPath}");

        // For videos, FFmpeg streams directly - no download needed
        if (UrlHelper.IsLikelyVideo(inputPath))
        {
            Console.Error.WriteLine("Streaming video via FFmpeg...");
            inputFullPath = inputPath;
            var urlExt = UrlHelper.GetExtension(inputPath) ?? "mp4";
            input = new FileInfo($"stream.{urlExt}"); // Dummy for extension detection
        }
        // For images/GIFs, download to temp file first
        else
        {
            Console.Error.Write("Downloading... ");
            try
            {
                var ext = UrlHelper.GetExtension(inputPath) ?? "tmp";
                tempFile = Path.Combine(Path.GetTempPath(), $"consoleimage_{Guid.NewGuid()}.{ext}");

                using var stream = await UrlHelper.DownloadAsync(inputPath, (downloaded, total) =>
                {
                    if (total > 0)
                        Console.Error.Write($"\rDownloading: {downloaded * 100 / total}%   ");
                    else
                        Console.Error.Write($"\rDownloading: {downloaded / 1024}KB   ");
                }, cancellationToken);

                await using var fs = File.Create(tempFile);
                await stream.CopyToAsync(fs, cancellationToken);
                Console.Error.WriteLine("done.");

                inputFullPath = tempFile;
                input = new FileInfo(tempFile);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"failed: {ex.Message}");
                return 1;
            }
        }
    }
    else
    {
        // Local file - resolve path (handles AOT and mapped drives)
        input = new FileInfo(inputPath);
        var resolved = ResolveInputPath(inputPath, input);
        if (resolved == null)
            return 1;
        inputFullPath = resolved;
        input = new FileInfo(inputFullPath);
    }

    // Transcript-only mode: generate subtitles without video rendering
    // Works with local files, URLs, and YouTube (after URL resolution above)
    if (transcriptOnly)
    {
        // Determine transcript source: auto, whisper, or existing file
        var transcriptSource = subsValue?.ToLowerInvariant() ?? "auto";

        // For YouTube with auto mode, try to download existing subtitles first
        if (isYouTube && transcriptSource is "auto" or "yt")
        {
            Console.Error.Write($"Checking for YouTube subtitles ({subtitleLang})... ");
            var subsTempDir = Path.Combine(Path.GetTempPath(), "consoleimage_subs");
            var srtPath = await YtdlpProvider.DownloadSubtitlesAsync(
                inputPath, subsTempDir, subtitleLang, ytdlpPath, cookiesFromBrowser, cookiesFile, cancellationToken);

            if (srtPath != null && File.Exists(srtPath))
            {
                Console.Error.WriteLine("found!");

                // Read and output the subtitle file, respecting -t (duration) and --start options
                var track = await SubtitleParser.ParseAsync(srtPath, cancellationToken);
                var effectiveStart = TimeSpan.FromSeconds(start ?? 0);
                var effectiveEnd = duration.HasValue
                    ? effectiveStart + TimeSpan.FromSeconds(duration.Value)
                    : TimeSpan.MaxValue;

                foreach (var entry in track.Entries)
                {
                    // Filter by time range if specified
                    if (entry.EndTime < effectiveStart)
                        continue; // Subtitle ends before our start time
                    if (entry.StartTime > effectiveEnd)
                        break; // Subtitle starts after our end time (sorted list, so we can stop)

                    var displayStart = entry.StartTime < effectiveStart ? effectiveStart : entry.StartTime;
                    var displayEnd = entry.EndTime > effectiveEnd ? effectiveEnd : entry.EndTime;

                    var startTime = FormatTranscriptTime(displayStart);
                    var endTime = FormatTranscriptTime(displayEnd);
                    Console.WriteLine($"[{startTime} --> {endTime}] {entry.Text.Trim()}");
                }

                // Save to output file if specified
                if (!string.IsNullOrEmpty(output))
                {
                    File.Copy(srtPath, output, overwrite: true);
                    Console.Error.WriteLine($"Saved: {output}");
                }

                return 0;
            }
            else
            {
                Console.Error.WriteLine("not available.");
                if (transcriptSource == "yt")
                {
                    Console.Error.WriteLine("YouTube subtitles not found. Use --subs whisper to generate with Whisper.");
                    return 1;
                }
                Console.Error.WriteLine("Falling back to Whisper transcription...");
            }
        }

        // Use Whisper for transcription
        var transcriptOpts = new TranscriptionHandler.TranscriptionOptions
        {
            InputPath = inputFullPath, // Use resolved URL (YouTube stream URL, etc.)
            OutputPath = output, // null = auto-generate from input path
            ModelSize = whisperModel ?? "base",
            Language = subtitleLang ?? "en",
            Diarize = subsValue?.Contains("diarize", StringComparison.OrdinalIgnoreCase) ?? false,
            Threads = whisperThreads,
            StreamToStdout = true, // Stream results to stdout
            Quiet = false // Show progress on stderr
        };

        return await TranscriptionHandler.HandleAsync(transcriptOpts, cancellationToken);
    }

    // Determine JSON output path if needed
    if (outputAsJson && string.IsNullOrEmpty(jsonOutputPath))
        jsonOutputPath = Path.ChangeExtension(input.FullName, outputAsCompressed ? ".cidz" : ".json");

    // Route to appropriate handler based on file type
    var extension = input.Extension.ToLowerInvariant();

    // Document files (.json, .ndjson, .cidz)
    if (IsDocumentFile(extension, input.Name))
        return await DocumentHandler.HandleAsync(
            input.FullName, speed, loop, outputGif,
            gifFontSize, gifScale, gifColors, cancellationToken);

    // Raw mode for GIFs routes to VideoHandler (FFmpeg can extract frames)
    // This extracts actual video frames, not ASCII-rendered output
    if (rawMode && extension == ".gif")
    {
        // Fall through to VideoHandler below - FFmpeg handles GIFs
    }
    // Image files (jpg, png, gif, etc.) - normal rendering
    else if (IsImageFile(extension))
        return await ImageHandler.HandleAsync(
            input, width, height, maxWidth, maxHeight,
            charAspect, savedCalibration,
            useBlocks, useBraille,
            useMatrix, matrixColor, matrixFullColor,
            matrixDensity, matrixSpeed, matrixAlphabet,
            noColor, colorCount, contrast, gamma, loop, speed,
            outputGif, gifFontSize, gifScale, gifColors,
            outputAsJson, jsonOutputPath,
            showStatus, cancellationToken);

    // Load subtitles based on --subs value
    SubtitleTrack? subtitles = null;
    ChunkedTranscriber? chunkedTranscriber = null;
    var (subtitleSource, subtitleFilePath, diarize) = ParseSubsOption(subsValue);

    // Determine if this is playback mode (no output file) - enables streaming transcription
    var isPlaybackMode = outputGif == null && !outputAsJson && string.IsNullOrEmpty(jsonOutputPath);

    // Handle subtitle loading based on source type
    if (subtitleSource != "off")
    {
        // For whisper source in playback mode, use streaming chunked transcription
        if (subtitleSource == "whisper" && isPlaybackMode)
        {
            // First, check if we have a cached subtitle file from previous transcription
            // Skip cache if --force-subs is used
            var cachedSubPath = GetSubtitlePathForVideo(inputFullPath);
            if (!forceSubs && File.Exists(cachedSubPath))
            {
                Console.Error.WriteLine($"Using cached subtitles: {cachedSubPath}");
                Console.Error.WriteLine("(use --force-subs to re-transcribe)");
                subtitles = await SubtitleParser.ParseAsync(cachedSubPath, cancellationToken);
            }
            else
            {
                // Delete existing cache if forcing re-transcription
                if (forceSubs && File.Exists(cachedSubPath))
                {
                    try { File.Delete(cachedSubPath); }
                    catch { /* ignore */ }
                }
                var effectiveStart = start ?? 0.0;

                // Check model availability and prompt if needed
                if (!EnsureWhisperModelOrPrompt(whisperModel ?? "base", subtitleLang ?? "en", autoConfirmDownload))
                {
                    Console.Error.WriteLine("Continuing without subtitles.");
                    subtitleSource = "off";
                }
                else
                {
                Console.Error.WriteLine($"Starting live transcription with Whisper ({whisperModel ?? "base"})...");
                chunkedTranscriber = new ChunkedTranscriber(
                    inputFullPath,
                    modelSize: whisperModel ?? "base",
                    language: subtitleLang ?? "en",
                    chunkDurationSeconds: 15.0,  // Smaller chunks for faster initial results
                    bufferAheadSeconds: 30.0,    // Buffer 30s ahead of playback
                    startTimeOffset: effectiveStart);  // Start from --ss position

                // Hook up progress events for visual feedback (only during initial setup)
                var showProgress = true;
                chunkedTranscriber.OnProgress += (seconds, status) =>
                {
                    if (showProgress)
                        Console.Error.Write($"\r{status,-60}");
                };

                // Initialize whisper (downloads model if needed)
                var downloadProgress = new Progress<(long downloaded, long total, string status)>(p =>
                {
                    if (p.total > 0)
                    {
                        var pct = (int)(p.downloaded * 100 / p.total);
                        Console.Error.Write($"\r{p.status} ({pct}%)          ");
                    }
                    else
                    {
                        Console.Error.Write($"\r{p.status}          ");
                    }
                });
                await chunkedTranscriber.StartAsync(downloadProgress, cancellationToken);
                Console.Error.WriteLine();

                // Buffer just the initial chunk (15s) before starting playback
                // Background transcription will catch up during playback
                await chunkedTranscriber.EnsureTranscribedUpToAsync(effectiveStart + 15.0, cancellationToken);
                Console.Error.WriteLine("\rInitial buffer ready, starting playback...                    ");

                // Disable progress output before starting playback (would interfere with video display)
                showProgress = false;

                // Start background transcription to buffer ahead
                chunkedTranscriber.StartBackgroundTranscription();
                }
            }
        }
        else
        {
            // For file sources or output mode, use full batch transcription
            subtitles = await LoadSubtitlesAsync(
                subtitleSource, subtitleFilePath, inputFullPath, subtitleLang ?? "en",
                whisperModel ?? "base", whisperThreads, diarize, start, duration, autoConfirmDownload, cancellationToken);
        }
    }

    // Video files - requires FFmpeg
    // Default width for braille video is 50 (more CPU intensive)
    var videoWidth = width ?? (useBraille ? 50 : null);

    var videoOpts = new VideoHandlerOptions
    {
        // Output
        OutputGif = outputGif,
        OutputAsJson = outputAsJson,
        OutputAsCompressed = outputAsCompressed,
        JsonOutputPath = jsonOutputPath,

        // Dimensions (braille defaults to 50 wide for performance)
        Width = videoWidth,
        Height = height,
        MaxWidth = videoWidth ?? maxWidth,
        MaxHeight = maxHeight,

        // Time range
        Start = start,
        End = end,
        Duration = duration,

        // Playback
        Speed = speed,
        Loop = loop,
        Fps = fps,
        FrameStep = frameStep,
        Sampling = sampling,
        SceneThreshold = sceneThreshold,

        // Render modes
        UseBlocks = useBlocks,
        UseBraille = useBraille,

        // Color/rendering
        NoColor = noColor,
        ColorCount = colorCount,
        Contrast = contrast,
        Gamma = gamma,
        CharAspect = charAspect,
        Charset = charset,
        Preset = preset,
        SavedCalibration = savedCalibration,

        // Performance
        Buffer = buffer,
        NoHwAccel = noHwAccel,
        NoAltScreen = noAltScreen,
        NoParallel = noParallel,
        NoDither = noDither,
        NoEdgeChars = noEdgeChars,

        // FFmpeg
        FfmpegPath = ffmpegPath,
        NoAutoDownload = noAutoDownload,
        AutoConfirmDownload = autoConfirmDownload,

        // Info/status
        ShowInfo = showInfo,
        ShowStatus = showStatus,

        // GIF output
        GifFontSize = gifFontSize,
        GifScale = gifScale,
        GifColors = gifColors,
        GifLength = gifLength,
        GifFrames = gifFrames,

        // Raw/extract
        RawMode = rawMode,
        RawWidth = rawWidth,
        RawHeight = rawHeight,
        SmartKeyframes = smartKeyframes,
        Quality = quality,

        // Image adjustments
        NoInvert = noInvert,
        EnableEdge = enableEdge,
        BgThreshold = bgThreshold,
        DarkBgThreshold = darkBgThreshold,
        AutoBg = autoBg,
        DarkCutoff = darkCutoff,
        LightCutoff = lightCutoff,

        // Temporal stability
        Dejitter = dejitter,
        ColorThreshold = colorThreshold,

        // Subtitles - use live transcriber if available, otherwise static subtitles
        Subtitles = subtitles,
        LiveSubtitleProvider = chunkedTranscriber,

        // Debug
        DebugMode = debug
    };

    try
    {
        var result = await VideoHandler.HandleAsync(inputFullPath, input, videoOpts, cancellationToken);

        // Save transcribed subtitles for future use if we used live transcription
        if (chunkedTranscriber != null && chunkedTranscriber.Track.HasEntries)
        {
            var subPath = GetSubtitlePathForVideo(inputFullPath);
            try
            {
                // Use a fresh token since the main one may be cancelled
                using var saveCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await chunkedTranscriber.SaveAsync(subPath, saveCts.Token);
                Console.Error.WriteLine($"Subtitles saved: {subPath}");
            }
            catch (OperationCanceledException)
            {
                // Don't warn on cancellation - user intentionally stopped
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Could not save subtitles: {ex.Message}");
            }
        }

        return result;
    }
    finally
    {
        // Clean up chunked transcriber
        if (chunkedTranscriber != null)
        {
            await chunkedTranscriber.DisposeAsync();
        }

        // Clean up temp file if we created one
        if (tempFile != null && File.Exists(tempFile))
        {
            try { File.Delete(tempFile); } catch { }
        }
    }
});

return await rootCommand.Parse(args).InvokeAsync();

// === Helper Functions ===

static (bool outputAsJson, bool outputAsCompressed, string? jsonOutputPath, string? gifOutputPath)
    ParseOutputOption(string? output)
{
    if (string.IsNullOrEmpty(output))
        return (false, false, null, null);

    // Check for explicit format prefixes first
    if (output.Equals("json", StringComparison.OrdinalIgnoreCase))
        return (true, true, null, null);

    if (output.StartsWith("json:", StringComparison.OrdinalIgnoreCase))
    {
        var path = output[5..];
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            path = path[..^5] + ".cidz";
        return (true, true, path, null);
    }

    if (output.StartsWith("raw:", StringComparison.OrdinalIgnoreCase) ||
        output.StartsWith("uncompressed:", StringComparison.OrdinalIgnoreCase))
    {
        var prefixLen = output.StartsWith("raw:") ? 4 : 13;
        return (true, false, output[prefixLen..], null);
    }

    if (output.Equals("gif", StringComparison.OrdinalIgnoreCase))
        return (false, false, null, null);

    if (output.StartsWith("gif:", StringComparison.OrdinalIgnoreCase))
        return (false, false, null, output[4..]);

    if (output.Equals("cidz", StringComparison.OrdinalIgnoreCase) ||
        output.Equals("compressed", StringComparison.OrdinalIgnoreCase))
        return (true, true, null, null);

    if (output.StartsWith("cidz:", StringComparison.OrdinalIgnoreCase))
        return (true, true, output[5..], null);

    // Auto-detect from extension
    if (output.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        return (false, false, null, output);

    if (output.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        return (true, false, output, null);

    if (output.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase))
        return (true, false, output, null);

    if (output.EndsWith(".cidz", StringComparison.OrdinalIgnoreCase) ||
        output.EndsWith(".cid.7z", StringComparison.OrdinalIgnoreCase))
        return (true, true, output, null);

    // Unknown extension - warn and default to GIF
    Console.Error.WriteLine(
        $"Warning: Unknown output format for '{output}'. Use .gif for GIF, .cidz for compressed JSON.");
    Console.Error.WriteLine("Assuming GIF output. Use 'json:path' for compressed, 'raw:path.json' for uncompressed.");
    var gifPath = output.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? output : output + ".gif";
    return (false, false, null, gifPath);
}

static string? ResolveInputPath(string inputPath, FileInfo input)
{
    var inputFullPath = inputPath;

    if (!File.Exists(inputFullPath))
        inputFullPath = input.FullName;

    if (!File.Exists(inputFullPath))
    {
        Console.Error.WriteLine($"Error: File not found: {inputPath}");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Troubleshooting:");
        Console.Error.WriteLine($"  Original path: {inputPath}");
        Console.Error.WriteLine($"  Resolved path: {input.FullName}");

        if (inputPath.Length >= 2 && inputPath[1] == ':')
        {
            var driveLetter = char.ToUpper(inputPath[0]);
            if (driveLetter >= 'D')
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"  Drive {driveLetter}: may be a mapped network drive.");
                Console.Error.WriteLine(
                    "  Mapped drives are user-specific and may not be accessible in some contexts.");
                Console.Error.WriteLine(
                    "  Try using the full UNC path instead (e.g., \\\\server\\share\\path\\file.mkv)");
            }
        }

        var dir = Path.GetDirectoryName(inputFullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            var dirExists = false;
            try { dirExists = Directory.Exists(dir); } catch { }
            Console.Error.WriteLine($"  Directory exists: {dirExists}");
        }

        return null;
    }

    return inputFullPath;
}

static string FormatTranscriptTime(TimeSpan ts)
{
    return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
}

static bool IsDocumentFile(string extension, string fileName)
{
    return extension == ".json" ||
           extension == ".ndjson" ||
           extension == ".cidz" ||
           extension == ".7z" ||
           fileName.EndsWith(".cid.7z", StringComparison.OrdinalIgnoreCase);
}

static bool IsImageFile(string extension)
{
    return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp" or ".tiff" or ".tif";
}

// Easter egg: play embedded Star Wars animation
static async Task PlayEasterEggAsync()
{
    try
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("ConsoleImage.star_wars.json");
        if (stream == null) return;

        // Load JSON directly from embedded resource
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        var doc = ConsoleImageDocument.FromJson(json);
        if (doc == null || doc.Frames.Count == 0) return;

        // Play with cancellation on any key press
        using var cts = new CancellationTokenSource();

        // Start playback
        var player = new UnifiedPlayer(doc, new UnifiedPlayerOptions
        {
            UseAltScreen = true,
            LoopCountOverride = 1
        });

        // Start key listener only if we have interactive console
        Task? keyTask = null;
        if (!Console.IsInputRedirected)
        {
            keyTask = Task.Run(() =>
            {
                try
                {
                    Console.ReadKey(true);
                    cts.Cancel();
                }
                catch { }
            });
        }

        // Play the animation
        try
        {
            await player.PlayAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        // Cancel key listener if still running
        cts.Cancel();
    }
    catch
    {
        // Silently ignore any errors with easter egg
    }
}

static void ShowHelpAndPrompt()
{
    Console.WriteLine();
    Console.WriteLine("ConsoleImage v3.0 - ASCII Art Renderer");
    Console.WriteLine("=======================================");
    Console.WriteLine();
    Console.WriteLine("Render modes:");
    Console.WriteLine("  (default)    Braille dots - highest detail");
    Console.WriteLine("  -a, --ascii  Classic ASCII characters");
    Console.WriteLine("  -b, --blocks Unicode half-blocks");
    Console.WriteLine("  -M, --matrix Matrix digital rain effect");
    Console.WriteLine();
    Console.WriteLine("Subtitles: --subs <source>");
    Console.WriteLine("  auto         Try YouTube, then whisper if available");
    Console.WriteLine("  off          Disable subtitles");
    Console.WriteLine("  <path>       Load from SRT/VTT file");
    Console.WriteLine("  whisper      Generate with local Whisper (live, ahead of playback)");
    Console.WriteLine();
    Console.WriteLine("Transcript-only (no video):");
    Console.WriteLine("  consoleimage input.mp4 --transcript           Stream text to stdout");
    Console.WriteLine("  consoleimage transcribe input.mp4 -o out.vtt  Save VTT file");
    Console.WriteLine("  consoleimage transcribe input.mp4 --stream    Stream + save");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  consoleimage photo.jpg");
    Console.WriteLine("  consoleimage movie.mp4 -w 80");
    Console.WriteLine("  consoleimage movie.mp4 --subs movie.srt");
    Console.WriteLine("  consoleimage https://youtu.be/VIDEO_ID --subs auto");
    Console.WriteLine("  consoleimage https://youtu.be/VIDEO_ID --transcript");
    Console.WriteLine();
    Console.WriteLine("Run 'consoleimage --help' for all options.");
    Console.WriteLine();
    Console.Write("Enter filename or URL (or press Enter to exit): ");

    try
    {
        if (!Console.IsInputRedirected)
        {
            var input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
            {
                // Re-invoke with the provided input
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = Environment.ProcessPath ?? "consoleimage",
                        Arguments = $"\"{input}\"",
                        UseShellExecute = false
                    }
                };
                process.Start();
                process.WaitForExit();
            }
        }
    }
    catch { }
}

// === Subtitle handling ===

// Subtitle source types: "off", "file", "yt", "whisper", "auto"
static (string source, string? path, bool diarize) ParseSubsOption(string? value)
{
    if (string.IsNullOrEmpty(value))
        return ("off", null, false);

    var lower = value.ToLowerInvariant();

    // Check for explicit keywords
    if (lower is "off" or "no" or "none" or "disable")
        return ("off", null, false);

    if (lower is "auto")
        return ("auto", null, false);

    if (lower is "yt" or "youtube")
        return ("yt", null, false);

    if (lower is "whisper")
        return ("whisper", null, false);

    if (lower is "whisper+diarize" or "whisper+diarization")
        return ("whisper", null, true);

    // Otherwise treat as file path
    return ("file", value, false);
}

static async Task<SubtitleTrack?> LoadSubtitlesAsync(
    string source,
    string? filePath,
    string inputPath,
    string lang,
    string whisperModel,
    int? whisperThreads,
    bool diarize,
    double? startTime,
    double? duration,
    bool autoConfirm,
    CancellationToken ct)
{
    try
    {
        switch (source)
        {
            case "file":
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    var track = await SubtitleParser.ParseAsync(filePath, ct);
                    Console.Error.WriteLine($"Loaded {track.Count} subtitles from {Path.GetFileName(filePath)}");
                    return track;
                }
                Console.Error.WriteLine($"Warning: Subtitle file not found: {filePath}");
                return null;

            case "whisper":
                return await TranscribeWithWhisperAsync(inputPath, lang, whisperModel, whisperThreads, diarize, startTime, duration, autoConfirm, ct);

            case "auto":
                // For auto mode, whisper is fallback if YouTube subs not available
                // (YouTube subs handled separately in the YouTube URL flow)
                if (WhisperTranscriptionService.IsAvailable())
                {
                    return await TranscribeWithWhisperAsync(inputPath, lang, whisperModel, whisperThreads, diarize, startTime, duration, autoConfirm, ct);
                }
                Console.Error.WriteLine("Note: Whisper not available for auto-transcription. Run: consoleimage transcribe --help");
                return null;

            default:
                return null;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Warning: Failed to load subtitles: {ex.Message}");
        return null;
    }
}

/// <summary>
/// Check if Whisper model is cached, prompt to download if not.
/// Returns true if model is available, false if user declined.
/// </summary>
static bool EnsureWhisperModelOrPrompt(string model, string lang, bool autoConfirm)
{
    if (WhisperModelDownloader.IsModelCached(model, lang))
        return true;

    var (fileName, sizeMB) = WhisperModelDownloader.GetModelInfo(model, lang);

    Console.Error.WriteLine($"Whisper model '{fileName}' not found locally.");
    Console.Error.WriteLine($"Download required: ~{sizeMB}MB");

    if (autoConfirm)
    {
        Console.Error.WriteLine("Auto-downloading (--yes flag)...");
        return true;
    }

    Console.Error.Write("Download now? [y/N] ");
    var response = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (response is "y" or "yes")
        return true;

    Console.Error.WriteLine("Transcription cancelled. Use -y to auto-download.");
    return false;
}

static async Task<SubtitleTrack?> TranscribeWithWhisperAsync(
    string inputPath,
    string lang,
    string model,
    int? threads,
    bool diarize,
    double? startTime,
    double? duration,
    bool autoConfirm,
    CancellationToken ct)
{
    // Check model availability and prompt if needed
    if (!EnsureWhisperModelOrPrompt(model, lang, autoConfirm))
        return null;

    Console.Error.WriteLine($"Transcribing with Whisper ({model})...");

    // Create temp output file
    var tempVtt = Path.Combine(Path.GetTempPath(), $"consoleimage_sub_{Guid.NewGuid():N}.vtt");

    try
    {
        var opts = new TranscriptionHandler.TranscriptionOptions
        {
            InputPath = inputPath,
            OutputPath = tempVtt,
            ModelSize = model,
            Language = lang,
            Diarize = diarize,
            Threads = threads,
            StartTime = startTime,
            Duration = duration
        };

        var result = await TranscriptionHandler.HandleAsync(opts, ct);
        if (result != 0 || !File.Exists(tempVtt))
        {
            Console.Error.WriteLine("Whisper transcription failed.");
            return null;
        }

        var track = await SubtitleParser.ParseAsync(tempVtt, ct);

        // Offset subtitle times if we extracted a time range
        // (Whisper sees times relative to extracted audio, but we need original video timeline)
        if (startTime.HasValue && startTime.Value > 0)
        {
            var offset = TimeSpan.FromSeconds(startTime.Value);
            foreach (var entry in track.Entries)
            {
                entry.StartTime += offset;
                entry.EndTime += offset;
            }
        }

        Console.Error.WriteLine($"Transcribed {track.Count} segments");

        // Debug: show subtitle time range
        if (track.Entries.Count > 0)
        {
            var first = track.Entries.First();
            var last = track.Entries.Last();
            Console.Error.WriteLine($"Subtitle range: {first.StartTime:mm\\:ss\\.ff} - {last.EndTime:mm\\:ss\\.ff} (relative to extraction start)");
        }

        return track;
    }
    finally
    {
        if (File.Exists(tempVtt))
        {
            try { File.Delete(tempVtt); } catch { }
        }
    }
}

// === Transcribe subcommand ===

static Command CreateTranscribeSubcommand()
{
    var inputArg = new Argument<string>("input") { Description = "Input video or audio file" };

    var outputOpt = new Option<string?>("--output") { Description = "Output file (default: input.vtt)" };
    outputOpt.Aliases.Add("-o");

    var modelOpt = new Option<string>("--model") { Description = "Whisper model: tiny, base, small, medium, large" };
    modelOpt.DefaultValueFactory = _ => "base";
    modelOpt.Aliases.Add("-m");

    var langOpt = new Option<string>("--lang") { Description = "Language code (en, es, ja, etc.) or 'auto'" };
    langOpt.DefaultValueFactory = _ => "en";
    langOpt.Aliases.Add("-l");

    var diarizeOpt = new Option<bool>("--diarize") { Description = "Enable speaker diarization" };
    diarizeOpt.Aliases.Add("-d");

    var threadsOpt = new Option<int?>("--threads") { Description = "CPU threads (default: half available)" };
    threadsOpt.Aliases.Add("-t");

    var urlOpt = new Option<string?>("--whisper-url") { Description = "Custom Whisper API URL (for hosted servers)" };

    var streamOpt = new Option<bool>("--stream") { Description = "Stream transcript text to stdout as generated" };
    streamOpt.Aliases.Add("-s");

    var quietOpt = new Option<bool>("--quiet") { Description = "Suppress progress messages (output only transcribed text)" };
    quietOpt.Aliases.Add("-q");

    var cmd = new Command("transcribe", "Generate subtitles from video/audio using Whisper");
    cmd.Arguments.Add(inputArg);
    cmd.Options.Add(outputOpt);
    cmd.Options.Add(modelOpt);
    cmd.Options.Add(langOpt);
    cmd.Options.Add(diarizeOpt);
    cmd.Options.Add(threadsOpt);
    cmd.Options.Add(urlOpt);
    cmd.Options.Add(streamOpt);
    cmd.Options.Add(quietOpt);

    cmd.SetAction(async (parseResult, ct) =>
    {
        var input = parseResult.GetValue(inputArg);
        var output = parseResult.GetValue(outputOpt);
        var model = parseResult.GetValue(modelOpt);
        var lang = parseResult.GetValue(langOpt);
        var diarize = parseResult.GetValue(diarizeOpt);
        var threads = parseResult.GetValue(threadsOpt);
        var whisperUrl = parseResult.GetValue(urlOpt);
        var stream = parseResult.GetValue(streamOpt);
        var quiet = parseResult.GetValue(quietOpt);

        if (string.IsNullOrEmpty(input))
        {
            Console.Error.WriteLine("Error: Input file required");
            return 1;
        }

        // Allow URLs (YouTube, etc.) - don't check File.Exists for them
        var isUrl = input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    input.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        if (!isUrl && !File.Exists(input))
        {
            Console.Error.WriteLine($"Error: File not found: {input}");
            return 1;
        }

        // If whisper URL specified, use remote API instead
        if (!string.IsNullOrEmpty(whisperUrl))
        {
            return await TranscribeWithRemoteApiAsync(input, output, lang ?? "en", whisperUrl, ct);
        }

        var opts = new TranscriptionHandler.TranscriptionOptions
        {
            InputPath = input,
            OutputPath = output,
            ModelSize = model ?? "base",
            Language = lang ?? "en",
            Diarize = diarize,
            Threads = threads,
            StreamToStdout = stream,
            Quiet = quiet
        };

        return await TranscriptionHandler.HandleAsync(opts, ct);
    });

    return cmd;
}

/// <summary>
/// Get the path where subtitles should be saved for a video file.
/// Saves alongside the video with .vtt extension.
/// </summary>
static string GetSubtitlePathForVideo(string videoPath)
{
    // For URLs (YouTube etc), use a temp directory
    if (videoPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        videoPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        // Extract video ID or use hash for filename
        var fileName = Path.GetFileNameWithoutExtension(videoPath);
        if (string.IsNullOrEmpty(fileName) || fileName.Length < 3)
            fileName = $"video_{videoPath.GetHashCode():X8}";
        return Path.Combine(Path.GetTempPath(), $"{fileName}.vtt");
    }

    // For local files, save next to the video
    var dir = Path.GetDirectoryName(videoPath);
    var name = Path.GetFileNameWithoutExtension(videoPath);

    // If can't write to video directory, use temp
    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        return Path.Combine(Path.GetTempPath(), $"{name}.vtt");

    return Path.Combine(dir, $"{name}.vtt");
}

static async Task<int> TranscribeWithRemoteApiAsync(
    string input,
    string? output,
    string lang,
    string whisperUrl,
    CancellationToken ct)
{
    Console.Error.WriteLine($"Using remote Whisper API: {whisperUrl}");

    // Determine output path
    output ??= Path.ChangeExtension(input, ".vtt");

    try
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(30); // Long timeout for transcription

        // Prepare multipart form data
        using var form = new MultipartFormDataContent();
        await using var fileStream = File.OpenRead(input);
        var fileContent = new StreamContent(fileStream);
        form.Add(fileContent, "file", Path.GetFileName(input));
        form.Add(new StringContent(lang), "language");
        form.Add(new StringContent("vtt"), "response_format");

        Console.Error.WriteLine("Uploading and transcribing...");
        var response = await client.PostAsync(whisperUrl, form, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            Console.Error.WriteLine($"API error: {response.StatusCode} - {error}");
            return 1;
        }

        var vttContent = await response.Content.ReadAsStringAsync(ct);
        await File.WriteAllTextAsync(output, vttContent, ct);
        Console.Error.WriteLine($"Saved: {output}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Remote transcription failed: {ex.Message}");
        return 1;
    }
}
