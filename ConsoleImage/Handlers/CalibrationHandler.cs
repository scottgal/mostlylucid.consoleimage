// Calibration handling for consoleimage CLI

using ConsoleImage.Cli.Utilities;
using ConsoleImage.Core;

namespace ConsoleImage.Cli.Handlers;

/// <summary>
///     Calibration mode for adjusting aspect ratio and gamma per render mode.
/// </summary>
public enum CalibrationMode
{
    AspectRatio,
    Gamma
}

/// <summary>
///     Handles aspect ratio and color/gamma calibration mode.
/// </summary>
public static class CalibrationHandler
{
    /// <summary>
    ///     Run calibration mode - interactive display with adjustment controls.
    ///     Supports both aspect ratio (circle test) and gamma (color test card) calibration.
    /// </summary>
    public static int Handle(
        bool useBraille, bool useBlocks, bool useMatrix,
        float? charAspect, float? gamma, CalibrationSettings? savedCalibration,
        bool noColor, bool saveCalibration,
        int? width, int? height,
        bool colorCalibration)
    {
        ConsoleHelper.EnableAnsiSupport();

        // Determine render mode for calibration
        var renderMode = RenderHelpers.GetRenderMode(useBraille, useBlocks, useMatrix);

        // Get effective values: explicit > saved > default
        var calibrationAspect = RenderHelpers.GetEffectiveAspectRatio(charAspect, savedCalibration, renderMode);
        var calibrationGamma = RenderHelpers.GetEffectiveGamma(gamma, savedCalibration, renderMode);

        var modeName = RenderHelpers.GetRenderModeName(renderMode);

        // Get default dimensions from terminal or use reasonable defaults
        int consoleWidth = 80, consoleHeight = 30;
        try
        {
            if (Console.WindowWidth > 0) consoleWidth = Console.WindowWidth;
            if (Console.WindowHeight > 0) consoleHeight = Console.WindowHeight;
        }
        catch
        {
            /* Non-interactive console */
        }

        var defaultWidth = width ?? Math.Min(consoleWidth - 2, 80);
        var defaultHeight = height ?? Math.Min(consoleHeight - 10, 30);

        // If --save was passed with explicit values, just save and exit
        if (saveCalibration && (charAspect.HasValue || gamma.HasValue))
        {
            var baseSettings = savedCalibration ?? new CalibrationSettings();
            var settings = baseSettings;

            if (charAspect.HasValue)
                settings = settings.WithAspectRatio(renderMode, calibrationAspect);
            if (gamma.HasValue)
                settings = settings.WithGamma(renderMode, calibrationGamma);

            CalibrationHelper.Save(settings);

            var savedItems = new List<string>();
            if (charAspect.HasValue) savedItems.Add($"aspect={calibrationAspect:F3}");
            if (gamma.HasValue) savedItems.Add($"gamma={calibrationGamma:F2}");

            Console.WriteLine(
                $"Saved {modeName} calibration ({string.Join(", ", savedItems)}) to: {CalibrationHelper.GetDefaultPath()}");
            return 0;
        }

        // Start in appropriate calibration mode
        var currentMode = colorCalibration ? CalibrationMode.Gamma : CalibrationMode.AspectRatio;

        // Interactive calibration mode
        Console.Write("\x1b[?25l"); // Hide cursor
        var running = true;
        var saved = false;
        var aspectChanged = false;
        var gammaChanged = false;

        while (running)
        {
            // Clear screen and render
            Console.Write("\x1b[2J\x1b[H");

            if (currentMode == CalibrationMode.AspectRatio)
            {
                Console.WriteLine($"\x1b[1mAspect Ratio Calibration - {modeName} Mode\x1b[0m");
                Console.WriteLine();
                Console.WriteLine("Adjust until the shape below is a perfect CIRCLE:");
                Console.WriteLine();

                // Render calibration pattern
                var calibrationOutput = CalibrationHelper.RenderCalibrationPattern(
                    renderMode,
                    calibrationAspect,
                    !noColor,
                    defaultWidth,
                    defaultHeight);

                Console.WriteLine(calibrationOutput);
                Console.WriteLine();
                Console.WriteLine(
                    "\x001b[33m▲/▼\x001b[0m Adjust ratio   \x001b[33mPgUp/PgDn\x001b[0m Large steps   \x001b[33mTab\x001b[0m Color cal   \x001b[33mEnter\x001b[0m Save   \x001b[33mEsc\x001b[0m Cancel");
                Console.WriteLine();
                Console.WriteLine(
                    $"Character aspect ratio: \x1b[1;36m{calibrationAspect:F3}\x1b[0m{(aspectChanged ? " *" : "")}");
            }
            else // Gamma calibration
            {
                Console.WriteLine($"\x1b[1mColor/Gamma Calibration - {modeName} Mode\x1b[0m");
                Console.WriteLine();
                Console.WriteLine("Adjust gamma until colors look natural:");
                Console.WriteLine("  - PLUGE bars (left): Middle 2 bars should be barely distinguishable");
                Console.WriteLine("  - Grayscale ramp (right): Smooth gradient from black to white");
                Console.WriteLine();

                // Render color calibration pattern
                var calibrationOutput = CalibrationHelper.RenderColorCalibrationPattern(
                    renderMode,
                    calibrationAspect,
                    calibrationGamma,
                    !noColor,
                    defaultWidth,
                    defaultHeight);

                Console.WriteLine(calibrationOutput);
                Console.WriteLine();
                Console.WriteLine(
                    "\x001b[33m▲/▼\x001b[0m Adjust gamma   \x001b[33mPgUp/PgDn\x001b[0m Large steps   \x001b[33mTab\x001b[0m Aspect cal   \x001b[33mEnter\x001b[0m Save   \x001b[33mEsc\x001b[0m Cancel");
                Console.WriteLine();
                Console.WriteLine(
                    $"Gamma: \x1b[1;36m{calibrationGamma:F2}\x1b[0m{(gammaChanged ? " *" : "")}  (< 1.0 = brighter, > 1.0 = darker)");
            }

            // Wait for key
            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    if (currentMode == CalibrationMode.AspectRatio)
                    {
                        calibrationAspect = Math.Min(1.0f, calibrationAspect + 0.01f);
                        aspectChanged = true;
                    }
                    else
                    {
                        calibrationGamma = Math.Min(2.0f, calibrationGamma + 0.05f);
                        gammaChanged = true;
                    }

                    break;

                case ConsoleKey.DownArrow:
                    if (currentMode == CalibrationMode.AspectRatio)
                    {
                        calibrationAspect = Math.Max(0.1f, calibrationAspect - 0.01f);
                        aspectChanged = true;
                    }
                    else
                    {
                        calibrationGamma = Math.Max(0.2f, calibrationGamma - 0.05f);
                        gammaChanged = true;
                    }

                    break;

                case ConsoleKey.PageUp:
                    if (currentMode == CalibrationMode.AspectRatio)
                    {
                        calibrationAspect = Math.Min(1.0f, calibrationAspect + 0.05f);
                        aspectChanged = true;
                    }
                    else
                    {
                        calibrationGamma = Math.Min(2.0f, calibrationGamma + 0.1f);
                        gammaChanged = true;
                    }

                    break;

                case ConsoleKey.PageDown:
                    if (currentMode == CalibrationMode.AspectRatio)
                    {
                        calibrationAspect = Math.Max(0.1f, calibrationAspect - 0.05f);
                        aspectChanged = true;
                    }
                    else
                    {
                        calibrationGamma = Math.Max(0.2f, calibrationGamma - 0.1f);
                        gammaChanged = true;
                    }

                    break;

                case ConsoleKey.Tab:
                    // Switch between aspect ratio and gamma calibration
                    currentMode = currentMode == CalibrationMode.AspectRatio
                        ? CalibrationMode.Gamma
                        : CalibrationMode.AspectRatio;
                    break;

                case ConsoleKey.Enter:
                    // Always save current values (even if unchanged, to ensure file exists)
                    var baseSettings = savedCalibration ?? new CalibrationSettings();
                    var settings = baseSettings
                        .WithAspectRatio(renderMode, calibrationAspect)
                        .WithGamma(renderMode, calibrationGamma);

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
            Console.WriteLine(
                $"\x1b[32m+\x1b[0m Saved {modeName} calibration (aspect={calibrationAspect:F3}, gamma={calibrationGamma:F2}) to: {CalibrationHelper.GetDefaultPath()}");
        else
            Console.WriteLine("Calibration cancelled. No changes saved.");

        return 0;
    }
}