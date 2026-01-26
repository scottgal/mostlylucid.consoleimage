using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using ConsoleVideo.Avalonia.Models;
using FluentAvalonia.UI.Controls;

namespace ConsoleVideo.Avalonia.ViewModels;

/// <summary>
///     Static converter instances for enum bindings.
/// </summary>
public static class EnumConverters
{
    public static readonly IValueConverter RenderModeAscii = new EnumToBoolConverter<RenderMode>(RenderMode.Ascii);
    public static readonly IValueConverter RenderModeBlocks = new EnumToBoolConverter<RenderMode>(RenderMode.Blocks);
    public static readonly IValueConverter RenderModeBraille = new EnumToBoolConverter<RenderMode>(RenderMode.Braille);
    public static readonly IValueConverter PlayPauseIcon = new BoolToStringConverter("⏸", "▶");
    public static readonly IValueConverter PlayPauseSymbol = new BoolToSymbolConverter(Symbol.Pause, Symbol.Play);

    public static readonly IValueConverter HasVideoToCursor = new BoolToCursorConverter(
        Cursor.Default,
        new Cursor(StandardCursorType.Hand));
}

/// <summary>
///     Converter for boolean to string (e.g., play/pause icons).
/// </summary>
public class BoolToStringConverter : IValueConverter
{
    private readonly string _falseValue;
    private readonly string _trueValue;

    public BoolToStringConverter(string trueValue, string falseValue)
    {
        _trueValue = trueValue;
        _falseValue = falseValue;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? _trueValue : _falseValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}

/// <summary>
///     Converter for boolean to FluentAvalonia Symbol.
/// </summary>
public class BoolToSymbolConverter : IValueConverter
{
    private readonly Symbol _falseValue;
    private readonly Symbol _trueValue;

    public BoolToSymbolConverter(Symbol trueValue, Symbol falseValue)
    {
        _trueValue = trueValue;
        _falseValue = falseValue;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? _trueValue : _falseValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}

/// <summary>
///     Converter for binding enum values to radio buttons.
/// </summary>
public class EnumToBoolConverter<T> : IValueConverter where T : Enum
{
    private readonly T _targetValue;

    public EnumToBoolConverter(T targetValue)
    {
        _targetValue = targetValue;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is T enumValue) return enumValue.Equals(_targetValue);
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true) return _targetValue;
        return BindingOperations.DoNothing;
    }
}

/// <summary>
///     Converter for boolean to Cursor (e.g., show hand when clickable).
/// </summary>
public class BoolToCursorConverter : IValueConverter
{
    private readonly Cursor _falseValue;
    private readonly Cursor _trueValue;

    public BoolToCursorConverter(Cursor trueValue, Cursor falseValue)
    {
        _trueValue = trueValue;
        _falseValue = falseValue;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? _trueValue : _falseValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}