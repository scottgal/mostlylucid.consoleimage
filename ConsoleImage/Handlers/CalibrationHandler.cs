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
    /// Run calibration mode - display test pattern and optionally save.
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

        Console.WriteLine($"Aspect Ratio Calibration - {modeName} Mode (--char-aspect {calibrationAspect})");
        Console.WriteLine("The shape below should appear as a perfect CIRCLE.");
        Console.WriteLine("If stretched horizontally, decrease --char-aspect (try 0.45)");
        Console.WriteLine("If stretched vertically, increase --char-aspect (try 0.55)");
        Console.WriteLine();

        // Render calibration pattern using the core helper
        var calibrationOutput = CalibrationHelper.RenderCalibrationPattern(
            calibrationMode,
            calibrationAspect,
            !noColor);

        Console.WriteLine(calibrationOutput);
        Console.WriteLine();
        Console.WriteLine($"Current --char-aspect: {calibrationAspect} ({modeName} mode)");

        // Save calibration if requested
        if (saveCalibration)
        {
            // Start with existing settings or defaults
            var baseSettings = savedCalibration ?? new CalibrationSettings();
            // Update only the current mode's aspect ratio
            var settings = baseSettings.WithAspectRatio(calibrationMode, calibrationAspect);
            CalibrationHelper.Save(settings);
            Console.WriteLine($"Saved {modeName} calibration to: {CalibrationHelper.GetDefaultPath()}");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("Once the circle looks correct, run with --save to remember this setting:");
            var modeFlag = calibrationMode switch
            {
                RenderMode.Braille => " --braille",
                RenderMode.ColorBlocks => " --blocks",
                _ => ""
            };
            Console.WriteLine($"  consoleimage --calibrate{modeFlag} --char-aspect {calibrationAspect} --save");
        }

        return 0;
    }
}
