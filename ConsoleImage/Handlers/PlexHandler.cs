// Plex Media Server integration handler
// Supports: browse libraries, search, direct play, login

using ConsoleImage.Plex;
using ConsoleImage.Core;
using Spectre.Console;

namespace ConsoleImage.Cli.Handlers;

public static class PlexHandler
{
    /// <summary>
    ///     Entry point for all plex subcommand modes.
    /// </summary>
    public static async Task<int> HandleAsync(
        string?          query,
        string?          directPlayId,
        bool             doLogin,
        bool             useBraille,
        bool             useBlocks,
        int              maxWidth,
        int              maxHeight,
        string?          ffmpegPath,
        bool             noAutoDownload,
        bool             autoConfirm,
        CancellationToken ct)
    {
        if (doLogin)
            return await LoginAsync(ct);

        var config = PlexConfigHelper.Load();
        if (config == null)
        {
            AnsiConsole.MarkupLine("[red]No Plex config found.[/]");
            AnsiConsole.MarkupLine("Run [yellow]consoleimage plex login[/] or set [yellow]PLEX_URL[/] and [yellow]PLEX_TOKEN[/].");
            return 1;
        }

        using var client = new PlexClient(config);

        // Non-interactive direct play
        if (directPlayId != null)
            return await PlayByIdAsync(client, directPlayId, useBraille, useBlocks, maxWidth, maxHeight,
                ffmpegPath, noAutoDownload, autoConfirm, ct);

        // Interactive: search mode if query provided, browser mode otherwise
        if (query != null)
            return await SearchAndSelectAsync(client, query, useBraille, useBlocks, maxWidth, maxHeight,
                ffmpegPath, noAutoDownload, autoConfirm, ct);

        return await BrowseLibrariesAsync(client, useBraille, useBlocks, maxWidth, maxHeight,
            ffmpegPath, noAutoDownload, autoConfirm, ct);
    }

    // ─── Login ───────────────────────────────────────────────────────────────

    private static async Task<int> LoginAsync(CancellationToken ct)
    {
        AnsiConsole.MarkupLine("[bold cyan]Plex Login[/]");
        AnsiConsole.MarkupLine("[dim]Find your token at: Account → XML tab → authToken[/]");
        AnsiConsole.WriteLine();

        var url   = AnsiConsole.Ask<string>("Plex server URL [dim](e.g. http://localhost:32400)[/]:");
        var token = AnsiConsole.Ask<string>("Plex token:");

        url = url.TrimEnd('/');

        using var client = new PlexClient(new PlexConfig(url, token));

        string? serverName = null;
        await AnsiConsole.Status().StartAsync("Connecting...", async ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);
            try { serverName = await client.PingAsync(ct); }
            catch (Exception ex) { serverName = null; AnsiConsole.MarkupLine($"[red]Connection failed:[/] {ex.Message}"); }
        });

        if (serverName == null) return 1;

        PlexConfigHelper.Save(new PlexConfig(url, token));
        AnsiConsole.MarkupLine($"[green]Connected to[/] [bold]{Markup.Escape(serverName)}[/] and config saved.");
        return 0;
    }

    // ─── Library browser ─────────────────────────────────────────────────────

    private static async Task<int> BrowseLibrariesAsync(PlexClient client, bool useBraille, bool useBlocks,
        int maxWidth, int maxHeight, string? ffmpegPath, bool noAutoDownload, bool autoConfirm, CancellationToken ct)
    {
        IReadOnlyList<PlexLibrary> libraries = [];

        await AnsiConsole.Status().StartAsync("Loading libraries...", async ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);
            libraries = await client.GetLibrariesAsync(ct);
        });

        if (libraries.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No libraries found.[/]");
            return 0;
        }

        // Sentinel item for exiting the browser
        var exitItem = new PlexLibrary("__exit__", "← Quit", "");

        while (true)
        {
            var allChoices = libraries.Concat([exitItem]).ToList();
            var lib = AnsiConsole.Prompt(
                new SelectionPrompt<PlexLibrary>()
                    .Title("[bold]Select a library:[/]")
                    .PageSize(12)
                    .UseConverter(l => l.DisplayTitle)
                    .AddChoices(allChoices));

            if (lib.Key == "__exit__") return 0;

            var result = await BrowseLibraryAsync(client, lib, useBraille, useBlocks,
                maxWidth, maxHeight, ffmpegPath, noAutoDownload, autoConfirm, ct);
            if (result != 0) return result;
            // result == 0 means "back to library list"
        }
    }

    private static async Task<int> BrowseLibraryAsync(PlexClient client, PlexLibrary library,
        bool useBraille, bool useBlocks, int maxWidth, int maxHeight,
        string? ffmpegPath, bool noAutoDownload, bool autoConfirm, CancellationToken ct)
    {
        IReadOnlyList<PlexMediaItem> items = [];

        await AnsiConsole.Status().StartAsync($"Loading {library.Title}...", async ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);
            items = await client.GetLibraryItemsAsync(library.Key, ct);
        });

        if (items.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Library is empty.[/]");
            return 0;
        }

        while (true)
        {
            var choices = items
                .Concat([new PlexMediaItem("__back__", "← Back", null, "", "", null, null, null)])
                .ToList();

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<PlexMediaItem>()
                    .Title($"[bold]{Markup.Escape(library.Title)}[/] [dim]({items.Count} items)[/]")
                    .PageSize(15)
                    .UseConverter(i => i.RatingKey == "__back__" ? "← Back" : i.DisplayTitle)
                    .AddChoices(choices));

            if (selected.RatingKey == "__back__") return 0;

            // TV shows: drill into episodes
            if (selected.Type == "show")
            {
                await DrillIntoShowAsync(client, selected, useBraille, useBlocks,
                    maxWidth, maxHeight, ffmpegPath, noAutoDownload, autoConfirm, ct);
                continue;
            }

            // Playable item
            await ShowItemPreviewAndPlayAsync(client, selected, useBraille, useBlocks,
                maxWidth, maxHeight, ffmpegPath, noAutoDownload, autoConfirm, ct);
        }
    }

    private static async Task DrillIntoShowAsync(PlexClient client, PlexMediaItem show,
        bool useBraille, bool useBlocks, int maxWidth, int maxHeight,
        string? ffmpegPath, bool noAutoDownload, bool autoConfirm, CancellationToken ct)
    {
        IReadOnlyList<PlexMediaItem> episodes = [];

        await AnsiConsole.Status().StartAsync($"Loading {show.Title}...", async ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);
            episodes = await client.GetAllLeavesAsync(show.RatingKey, ct);
        });

        if (episodes.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No episodes found.[/]");
            return;
        }

        var choices = episodes
            .Concat([new PlexMediaItem("__back__", "← Back", null, "", "", null, null, null)])
            .ToList();

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<PlexMediaItem>()
                .Title($"[bold]{Markup.Escape(show.Title)}[/] [dim]({episodes.Count} episodes)[/]")
                .PageSize(15)
                .UseConverter(i => i.RatingKey == "__back__"
                    ? "← Back"
                    : $"{i.ParentTitle} – {i.Title}")
                .AddChoices(choices));

        if (selected.RatingKey == "__back__") return;

        await ShowItemPreviewAndPlayAsync(client, selected, useBraille, useBlocks,
            maxWidth, maxHeight, ffmpegPath, noAutoDownload, autoConfirm, ct);
    }

    // ─── Search mode ─────────────────────────────────────────────────────────

    private static async Task<int> SearchAndSelectAsync(PlexClient client, string query,
        bool useBraille, bool useBlocks, int maxWidth, int maxHeight,
        string? ffmpegPath, bool noAutoDownload, bool autoConfirm, CancellationToken ct)
    {
        IReadOnlyList<PlexMediaItem> results = [];

        await AnsiConsole.Status().StartAsync($"Searching for \"{Markup.Escape(query)}\"...", async ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);
            results = await client.SearchAsync(query, ct);
        });

        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No results for[/] [bold]{Markup.Escape(query)}[/]");
            return 0;
        }

        // Filter to types that make sense to display
        var playable = results.Where(r => r.Type is "movie" or "episode" or "track").ToList();
        var shows    = results.Where(r => r.Type is "show").ToList();
        var all      = playable.Concat(shows).ToList();

        if (all.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No playable results found.[/]");
            return 0;
        }

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<PlexMediaItem>()
                .Title($"[bold]{Markup.Escape(query)}[/] [dim]— {all.Count} results[/]")
                .PageSize(15)
                .UseConverter(i => $"{i.DisplayTitle}  [dim]{i.DetailLine}[/]")
                .AddChoices(all));

        if (selected.Type == "show")
        {
            await DrillIntoShowAsync(client, selected, useBraille, useBlocks,
                maxWidth, maxHeight, ffmpegPath, noAutoDownload, autoConfirm, ct);
            return 0;
        }

        await ShowItemPreviewAndPlayAsync(client, selected, useBraille, useBlocks,
            maxWidth, maxHeight, ffmpegPath, noAutoDownload, autoConfirm, ct);
        return 0;
    }

    // ─── Preview + play ──────────────────────────────────────────────────────

    private static async Task ShowItemPreviewAndPlayAsync(PlexClient client, PlexMediaItem item,
        bool useBraille, bool useBlocks, int maxWidth, int maxHeight,
        string? ffmpegPath, bool noAutoDownload, bool autoConfirm, CancellationToken ct)
    {
        // Render poster thumbnail
        await RenderThumbnailAsync(client, item, Math.Min(maxWidth, 60), Math.Min(maxHeight, 30), ct);

        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(item.DisplayTitle)}[/]  [dim]{item.DetailLine}[/]");

        if (!item.IsPlayable)
        {
            AnsiConsole.MarkupLine("[yellow]No direct stream available for this item.[/]");
            return;
        }

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .AddChoices("Play", "Cancel"));

        if (action == "Play")
            await PlayItemAsync(client, item, useBraille, useBlocks, maxWidth, maxHeight,
                ffmpegPath, noAutoDownload, autoConfirm, ct);
    }

    private static async Task RenderThumbnailAsync(PlexClient client, PlexMediaItem item,
        int maxWidth, int maxHeight, CancellationToken ct)
    {
        try
        {
            await using var stream = await client.GetThumbStreamAsync(item.ThumbPath, maxWidth * 2, maxHeight * 4, ct);
            if (stream == null) return;

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            ms.Position = 0;

            var opts = new RenderOptions
            {
                MaxWidth  = maxWidth,
                MaxHeight = maxHeight,
                UseColor  = true,
                UseParallelProcessing = true
            };

            using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(ms);

            using var renderer = new ColorBlockRenderer(opts);
            Console.WriteLine(renderer.RenderImage(image));
        }
        catch
        {
            // Thumbnail is best-effort — swallow errors
        }
    }

    // ─── Playback ─────────────────────────────────────────────────────────────

    private static async Task<int> PlayByIdAsync(PlexClient client, string ratingKey,
        bool useBraille, bool useBlocks, int maxWidth, int maxHeight,
        string? ffmpegPath, bool noAutoDownload, bool autoConfirm, CancellationToken ct)
    {
        PlexMediaItem? item = null;
        await AnsiConsole.Status().StartAsync("Resolving item...", async ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);
            // Try direct metadata first (works for movies and episodes)
            item = await client.GetMetadataAsync(ratingKey, ct);
            // If it's a show/season, get all leaves and play the first one
            if (item != null && !item.IsPlayable)
            {
                var leaves = await client.GetAllLeavesAsync(ratingKey, ct);
                item = leaves.FirstOrDefault(l => l.IsPlayable);
            }
        });

        if (item == null || !item.IsPlayable)
        {
            AnsiConsole.MarkupLine($"[red]No playable stream found for ID {Markup.Escape(ratingKey)}[/]");
            return 1;
        }

        return await PlayItemAsync(client, item, useBraille, useBlocks, maxWidth, maxHeight,
            ffmpegPath, noAutoDownload, autoConfirm, ct);
    }

    private static async Task<int> PlayItemAsync(PlexClient client, PlexMediaItem item,
        bool useBraille, bool useBlocks, int maxWidth, int maxHeight,
        string? ffmpegPath, bool noAutoDownload, bool autoConfirm, CancellationToken ct)
    {
        if (item.PartKey == null)
        {
            AnsiConsole.MarkupLine("[red]No stream URL available.[/]");
            return 1;
        }

        var streamUrl = client.StreamUrl(item.PartKey);
        AnsiConsole.MarkupLine($"[dim]Streaming:[/] {Markup.Escape(item.DisplayTitle)}");

        var opts = new VideoHandlerOptions
        {
            UseBraille       = useBraille || (!useBraille && !useBlocks), // braille default
            UseBlocks        = useBlocks,
            MaxWidth         = maxWidth,
            MaxHeight        = maxHeight,
            FfmpegPath       = ffmpegPath,
            NoAutoDownload   = noAutoDownload,
            AutoConfirmDownload = autoConfirm,
            Loop             = 1,
            Speed            = 1.0f,
            Contrast         = 2.5f,
            Gamma            = 0.5f,
            Buffer           = 3,
        };

        // VideoHandler.HandleAsync accepts HTTP URLs directly (FFmpeg can stream them).
        // FileInfo is only used for display purposes — use a safe sanitized name.
        var safeName = string.Join("_", item.DisplayTitle.Split(Path.GetInvalidFileNameChars()));
        return await VideoHandler.HandleAsync(streamUrl, new FileInfo(Path.Combine(Path.GetTempPath(), safeName + ".mkv")), opts, ct);
    }
}
