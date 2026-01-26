using System.Diagnostics;
using System.Reflection;
using ConsoleVideo.Avalonia.Models;

namespace ConsoleVideo.Avalonia.Services;

/// <summary>
///     Service for launching console preview in external terminal.
/// </summary>
public class ConsolePreviewService
{
    /// <summary>
    ///     Get the path to consolevideo executable.
    /// </summary>
    private static string GetConsoleVideoPath()
    {
        // Try to find consolevideo relative to this assembly
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        if (assemblyDir != null)
        {
            // Check sibling directory (development layout)
            var devPath = Path.Combine(assemblyDir, "..", "..", "..", "..",
                "ConsoleImage.Video", "bin", "Debug", "net10.0", "consolevideo.exe");
            if (File.Exists(devPath))
                return Path.GetFullPath(devPath);

            // Check same directory (published layout)
            var localPath = Path.Combine(assemblyDir, "consolevideo.exe");
            if (File.Exists(localPath))
                return localPath;
        }

        // Fall back to PATH
        return "consolevideo";
    }

    /// <summary>
    ///     Launch consolevideo in an external terminal window.
    /// </summary>
    public void LaunchInTerminal(string videoPath, double startTime, double endTime, RenderMode mode)
    {
        var consolevideo = GetConsoleVideoPath();

        // Build consolevideo arguments
        var args = $"\"{videoPath}\" -ss {startTime:F2} -to {endTime:F2}";

        args += mode switch
        {
            RenderMode.Blocks => " --blocks",
            RenderMode.Braille => " --braille",
            _ => ""
        };

        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Windows: use cmd /c start to open in new window
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/c start \"ConsoleVideo Preview\" \"{consolevideo}\" {args}",
                    UseShellExecute = true,
                    CreateNoWindow = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                // macOS: use open -a Terminal
                var script = $"tell application \"Terminal\" to do script \"consolevideo {args}\"";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "osascript",
                    Arguments = $"-e '{script}'",
                    UseShellExecute = true
                });
            }
            else
            {
                // Linux: try common terminal emulators
                var terminals = new[]
                {
                    ("x-terminal-emulator", $"-e consolevideo {args}"),
                    ("gnome-terminal", $"-- consolevideo {args}"),
                    ("konsole", $"-e consolevideo {args}"),
                    ("xterm", $"-e consolevideo {args}")
                };

                foreach (var (terminal, termArgs) in terminals)
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = terminal,
                            Arguments = termArgs,
                            UseShellExecute = true
                        });
                        break;
                    }
                    catch
                    {
                        // Try next terminal
                    }
            }
        }
        catch (Exception ex)
        {
            // Log or handle error - terminal launch failed
            Debug.WriteLine($"Failed to launch terminal: {ex.Message}");
        }
    }

    /// <summary>
    ///     Launch a single frame preview (for testing render output).
    /// </summary>
    public void LaunchSingleFramePreview(string videoPath, double timestamp, RenderMode mode)
    {
        // Preview 3 seconds around the timestamp
        var start = Math.Max(0, timestamp - 1);
        var end = timestamp + 2;
        LaunchInTerminal(videoPath, start, end, mode);
    }
}