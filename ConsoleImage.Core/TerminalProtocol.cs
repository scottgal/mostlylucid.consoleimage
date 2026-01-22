namespace ConsoleImage.Core;

/// <summary>
///     Terminal image display protocols supported by ConsoleImage.
/// </summary>
public enum TerminalProtocol
{
    /// <summary>
    ///     ASCII art rendering using characters. Universally supported.
    /// </summary>
    Ascii,

    /// <summary>
    ///     Unicode half-block characters with ANSI colors. Requires 24-bit color support.
    /// </summary>
    ColorBlocks,

    /// <summary>
    ///     Unicode braille characters. Requires Unicode support.
    /// </summary>
    Braille,

    /// <summary>
    ///     Sixel graphics protocol. Supported by older terminals and some modern ones.
    /// </summary>
    Sixel,

    /// <summary>
    ///     iTerm2 inline images protocol. Supported by iTerm2 and compatible terminals.
    /// </summary>
    ITerm2,

    /// <summary>
    ///     Kitty graphics protocol. Supported by Kitty terminal and increasingly others.
    /// </summary>
    Kitty
}