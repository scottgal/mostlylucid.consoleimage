using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using ConsoleImage.Video.Core;
using ConsoleVideo.Avalonia.Models;
using ConsoleVideo.Avalonia.Services;
using ConsoleVideo.Avalonia.ViewModels;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Windowing;

namespace ConsoleVideo.Avalonia.Views;

public partial class MainWindow : AppWindow
{
    private WindowState _previousWindowState = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();

        // Enable drag-drop
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void VideoPreview_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only open file dialog when no video is loaded
        if (!ViewModel.HasVideo) OpenVideo_Click(sender, e);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            var file = files?.FirstOrDefault();

            if (file is IStorageFile storageFile)
            {
                var path = storageFile.Path.LocalPath;
                if (IsVideoFile(path))
                    await ViewModel.LoadVideoAsync(path);
                else if (IsImageFile(path)) await ViewModel.LoadImageAsync(path);
            }
        }
    }

    private async void OpenVideo_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Media File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("All Media")
                {
                    Patterns =
                    [
                        "*.mp4", "*.mkv", "*.avi", "*.mov", "*.webm", "*.wmv", "*.flv", "*.gif", "*.png", "*.jpg",
                        "*.jpeg", "*.bmp", "*.webp"
                    ]
                },
                new FilePickerFileType("Video Files")
                {
                    Patterns = ["*.mp4", "*.mkv", "*.avi", "*.mov", "*.webm", "*.wmv", "*.flv"]
                },
                new FilePickerFileType("Image Files")
                {
                    Patterns = ["*.gif", "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"]
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = ["*"]
                }
            ]
        });

        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            if (IsVideoFile(path))
                await ViewModel.LoadVideoAsync(path);
            else if (IsImageFile(path)) await ViewModel.LoadImageAsync(path);
        }
    }

    private async void SaveKeyframes_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Output Folder",
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            var path = folder[0].Path.LocalPath;
            await ViewModel.SaveKeyframesCommand.ExecuteAsync(path);
        }
    }

    private async void ExportManifest_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Output Folder for Manifest",
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            var path = folder[0].Path.LocalPath;
            await ExportManifestAsync(path);
        }
    }

    private async Task ExportManifestAsync(string outputFolder)
    {
        if (ViewModel.VideoPath == null || ViewModel.VideoInfo == null)
            return;

        if (ViewModel.Keyframes.Count == 0)
        {
            ViewModel.StatusText = "No keyframes to export. Extract keyframes first.";
            return;
        }

        var manifestService = new SceneManifestService();

        // Convert KeyframeViewModels to CoreKeyframes
        var coreKeyframes = ViewModel.Keyframes
            .Where(kf => kf.OriginalImage != null)
            .Select(kf => new ExtractedKeyframe
            {
                Index = kf.Index,
                Timestamp = kf.Timestamp,
                Image = kf.OriginalImage!,
                IsSceneBoundary = kf.IsSceneBoundary,
                Source = kf.Source
            })
            .ToList();

        // Create extraction settings from ViewModel state
        var settings = new ExtractionSettings
        {
            TargetKeyframeCount = ViewModel.TargetKeyframeCount,
            StartTime = ViewModel.RangeStart > 0 ? ViewModel.RangeStart : null,
            EndTime = ViewModel.RangeEnd < ViewModel.Duration ? ViewModel.RangeEnd : null,
            Strategy = ViewModel.SelectedStrategy,
            SceneThreshold = ViewModel.SceneThreshold
        };

        // Progress reporting
        var progress = new Progress<(string Status, double Progress)>(report =>
        {
            ViewModel.StatusText = report.Status;
            ViewModel.Progress = report.Progress * 100;
        });

        try
        {
            ViewModel.IsLoading = true;
            ViewModel.StatusText = "Exporting manifest...";

            await manifestService.ExportAsync(
                ViewModel.VideoPath,
                ViewModel.VideoInfo,
                coreKeyframes,
                settings,
                outputFolder,
                progress);

            ViewModel.StatusText = $"Manifest exported to: {outputFolder}";
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = $"Export failed: {ex.Message}";
        }
        finally
        {
            ViewModel.IsLoading = false;
            ViewModel.Progress = 0;
        }
    }

    private void PreviewInTerminal_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.VideoPath == null) return;

        var previewService = new ConsolePreviewService();
        previewService.LaunchInTerminal(
            ViewModel.VideoPath,
            ViewModel.RangeStart,
            ViewModel.RangeEnd,
            ViewModel.SelectedRenderMode);
    }

    private async void TimelineThumbnail_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is TimelineThumbnail thumb)
            await ViewModel.SeekToThumbnailAsync(thumb);
    }

    private void Keyframe_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is KeyframeViewModel kf)
        {
            ViewModel.SelectedKeyframe = kf;

            // Double-click to preview in terminal
            if (e.ClickCount == 2 && ViewModel.VideoPath != null)
            {
                var previewService = new ConsolePreviewService();
                var segmentStart = Math.Max(0, kf.Timestamp - 2);
                var segmentEnd = Math.Min(ViewModel.Duration, kf.Timestamp + 5);
                previewService.LaunchInTerminal(
                    ViewModel.VideoPath,
                    segmentStart,
                    segmentEnd,
                    ViewModel.SelectedRenderMode);
            }
        }
    }

    private void Exit_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
    }

    private void ToggleFullscreen_Click(object? sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void ToggleFullscreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            WindowState = _previousWindowState;
        }
        else
        {
            _previousWindowState = WindowState;
            WindowState = WindowState.FullScreen;
        }
    }

    private void SetWindowSize_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string sizeStr)
        {
            var parts = sizeStr.Split(',');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var width) &&
                int.TryParse(parts[1], out var height))
            {
                // Exit fullscreen first if needed
                if (WindowState == WindowState.FullScreen) WindowState = WindowState.Normal;

                Width = width;
                Height = height;

                // Center on screen
                if (Screens.Primary != null)
                {
                    var screen = Screens.Primary.WorkingArea;
                    Position = new PixelPoint(
                        screen.X + (screen.Width - width) / 2,
                        screen.Y + (screen.Height - height) / 2);
                }
            }
        }
    }

    private void Maximize_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void SetTheme_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string theme)
        {
            var faTheme = Application.Current?.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault();
            if (faTheme != null)
            {
                faTheme.PreferSystemTheme = false;
                Application.Current!.RequestedThemeVariant = theme == "Light"
                    ? ThemeVariant.Light
                    : ThemeVariant.Dark;
            }
        }
    }

    private void About_Click(object? sender, RoutedEventArgs e)
    {
        // Simple about dialog
        _ = ShowAboutDialogAsync();
    }

    private async Task ShowAboutDialogAsync()
    {
        var dialog = new Window
        {
            Title = "About ConsoleVideo",
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "ConsoleVideo",
                        FontSize = 24,
                        FontWeight = FontWeight.Bold
                    },
                    new TextBlock
                    {
                        Text =
                            "Video keyframe extraction and ASCII art preview tool.\n\nPart of the ConsoleImage suite.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Width = 100
                    }
                }
            }
        };

        if (dialog.Content is StackPanel panel && panel.Children.LastOrDefault() is Button okButton)
            okButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
    }

    private static bool IsVideoFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".wmv" or ".flv";
    }

    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".gif" or ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" or ".tiff" or ".tif";
    }

    private async void TranscribeSpeech_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.VideoPath == null) return;

        await ViewModel.TranscribeSpeechAsync();
    }
}