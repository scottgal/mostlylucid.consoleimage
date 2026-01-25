// Calibration handling for consoleimage CLI

using ConsoleImage.Cli.Utilities;
using ConsoleImage.Core;

namespace ConsoleImage.Cli.Handlers;

/// <summary>
/// Handles aspect ratio calibration mode.
/// </summary>
public static class CalibrationHandler
{
    /// <summary>
    /// Run calibration mode - interactive display with arrow key adjustment.
    /// </summary>
    public static int Handle(
        bool useBraille, bool useBlocks, bool useMatrix,
        float? charAspect, CalibrationSettings? savedCalibration,
        bool noColor, bool saveCalibration)
    {
        ConsoleHelper.EnableAnsiSupport();

        // Determine render mode for calibration
        var calibrationMode = useBraille ? RenderMode.Braille
            : useBlocks ? RenderMode.ColorBlocks
            : useMatrix ? RenderMode.Matrix
            : RenderMode.Ascii;

        // Get effective aspect ratio: explicit > saved for mode > default
        var calibrationAspect = charAspect
                                ?? savedCalibration?.GetAspectRatio(calibrationMode)
                                ?? 0.5f;

        var modeName = calibrationMode switch
        {
            RenderMode.Braille => "Braille",
            RenderMode.ColorBlocks => "Blocks",
            _ => "ASCII"
        };

        // If --save was passed with explicit aspect, just save and exit
        if (saveCalibration && charAspect.HasValue)
        {
            var baseSettings = savedCalibration ?? new CalibrationSettings();
            var settings = baseSettings.WithAspectRatio(calibrationMode, calibrationAspect);
            CalibrationHelper.Save(settings);
            Console.WriteLine($"Saved {modeName} calibration ({calibrationAspect}) to: {CalibrationHelper.GetDefaultPath()}");
            return 0;
        }

        // Interactive calibration mode
        Console.Write("\x1b[?25l"); // Hide cursor
        var running = true;
        var saved = false;

        while (running)
        {
            // Clear screen and render
            Console.Write("\x1b[2J\x1b[H");

            Console.WriteLine($"\x1b[1mAspect Ratio Calibration - {modeName} Mode\x1b[0m");
            Console.WriteLine();
            Console.WriteLine("Adjust until the shape below is a perfect CIRCLE:");
            Console.WriteLine();

            // Render calibration pattern
            var calibrationOutput = CalibrationHelper.RenderCalibrationPattern(
                calibrationMode,
                calibrationAspect,
                !noColor);

            Console.WriteLine(calibrationOutput);
            Console.WriteLine();
            Console.WriteLine($"\x1b[33m▲/▼ arrows\x1b[0m Adjust aspect ratio    \x1b[33mEnter\x1b[0m Save    \x1b[33mEsc\x1b[0m Cancel");
            Console.WriteLine();
            Console.WriteLine($"Character aspect ratio: \x1b[1;36m{calibrationAspect:F3}\x1b[0m");

            // Wait for key
            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    calibrationAspect = Math.Min(1.0f, calibrationAspect + 0.01f);
                    break;
                case ConsoleKey.DownArrow:
                    calibrationAspect = Math.Max(0.1f, calibrationAspect - 0.01f);
                    break;
                case ConsoleKey.PageUp:
                    calibrationAspect = Math.Min(1.0f, calibrationAspect + 0.05f);
                    break;
                case ConsoleKey.PageDown:
                    calibrationAspect = Math.Max(0.1f, calibrationAspect - 0.05f);
                    break;
                case ConsoleKey.Enter:
                    var baseSettings = savedCalibration ?? new CalibrationSettings();
                    var settings = baseSettings.WithAspectRatio(calibrationMode, calibrationAspect);
                    CalibrationHelper.Save(settings);
                    saved = true;
                    running = false;
                    break;
                case ConsoleKey.Escape:
                case ConsoleKey.Q:
                    running = false;
                    break;
            }
        }

        Console.Write("\x1b[?25h"); // Show cursor
        Console.Write("\x1b[2J\x1b[H"); // Clear screen

        if (saved)
        {
            Console.WriteLine($"\x1b[32m✓\x1b[0m Saved {modeName} calibration ({calibrationAspect:F3}) to: {CalibrationHelper.GetDefaultPath()}");
        }
        else
        {
            Console.WriteLine("Calibration cancelled. No changes saved.");
        }

        return 0;
    }
}
