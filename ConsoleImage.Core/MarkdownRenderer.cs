using System.Text;
using System.Text.RegularExpressions;

namespace ConsoleImage.Core;

/// <summary>
///     Renders ASCII/braille art to markdown-friendly formats.
/// </summary>
public static partial class MarkdownRenderer
{
    /// <summary>
    ///     Markdown output format options.
    /// </summary>
    public enum MarkdownFormat
    {
        /// <summary>Plain text in code block (monochrome, works everywhere)</summary>
        Plain,

        /// <summary>HTML spans with inline CSS colors (works in some renderers)</summary>
        Html,

        /// <summary>ANSI codes preserved (for terminals that render ANSI in markdown)</summary>
        Ansi,

        /// <summary>SVG with colored text (embeddable in markdown)</summary>
        Svg
    }

    // ANSI color code regex: \x1b[38;2;R;G;Bm or \x1b[0m
    [GeneratedRegex(@"\x1b\[(?:38;2;(\d+);(\d+);(\d+)|0)m")]
    private static partial Regex AnsiColorRegex();

    /// <summary>
    ///     Convert ANSI-colored content to markdown format.
    /// </summary>
    /// <param name="ansiContent">Content with ANSI escape codes</param>
    /// <param name="format">Target markdown format</param>
    /// <param name="title">Optional title for the output</param>
    /// <param name="fontFamily">Font family for SVG/HTML output</param>
    /// <returns>Markdown-formatted string</returns>
    public static string ToMarkdown(
        string ansiContent,
        MarkdownFormat format = MarkdownFormat.Plain,
        string? title = null,
        string fontFamily = "Consolas, 'Courier New', monospace")
    {
        return format switch
        {
            MarkdownFormat.Plain => ToPlainMarkdown(ansiContent, title),
            MarkdownFormat.Html => ToHtmlMarkdown(ansiContent, title, fontFamily),
            MarkdownFormat.Ansi => ToAnsiMarkdown(ansiContent, title),
            MarkdownFormat.Svg => ToSvgMarkdown(ansiContent, title, fontFamily),
            _ => ToPlainMarkdown(ansiContent, title)
        };
    }

    /// <summary>
    ///     Convert to plain text in a code block (strips ANSI codes).
    ///     Works in all markdown renderers.
    /// </summary>
    private static string ToPlainMarkdown(string ansiContent, string? title)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(title))
            sb.AppendLine($"## {title}").AppendLine();

        sb.AppendLine("```");
        sb.AppendLine(StripAnsiCodes(ansiContent));
        sb.AppendLine("```");

        return sb.ToString();
    }

    /// <summary>
    ///     Convert to HTML with inline CSS colors.
    ///     Works in markdown renderers that allow HTML (not GitHub).
    /// </summary>
    private static string ToHtmlMarkdown(string ansiContent, string? title, string fontFamily)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(title))
            sb.AppendLine($"## {title}").AppendLine();

        sb.AppendLine(
            $"<pre style=\"font-family: {fontFamily}; line-height: 1.0; background: #1e1e1e; padding: 10px; overflow-x: auto;\">");

        var lines = ansiContent.Split('\n');
        foreach (var line in lines) sb.AppendLine(ConvertAnsiLineToHtml(line));

        sb.AppendLine("</pre>");

        return sb.ToString();
    }

    /// <summary>
    ///     Preserve ANSI codes in a code block.
    ///     Only works in terminals that render ANSI codes in markdown.
    /// </summary>
    private static string ToAnsiMarkdown(string ansiContent, string? title)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(title))
            sb.AppendLine($"## {title}").AppendLine();

        // Use 'ansi' language hint (not widely supported but documents intent)
        sb.AppendLine("```ansi");
        sb.AppendLine(ansiContent);
        sb.AppendLine("```");

        return sb.ToString();
    }

    /// <summary>
    ///     Convert to SVG that can be embedded in markdown.
    ///     Full color support, works everywhere images are supported.
    /// </summary>
    private static string ToSvgMarkdown(string ansiContent, string? title, string fontFamily)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(title))
            sb.AppendLine($"## {title}").AppendLine();

        // Generate inline SVG
        var svg = GenerateSvg(ansiContent, fontFamily);
        sb.AppendLine(svg);

        return sb.ToString();
    }

    /// <summary>
    ///     Generate an SVG representation of ANSI-colored text.
    /// </summary>
    public static string GenerateSvg(string ansiContent, string fontFamily = "Consolas, monospace", int fontSize = 14)
    {
        var lines = ansiContent.ReplaceLineEndings("\n").Split('\n');
        var maxLineLength = lines.Max(l => StripAnsiCodes(l).TrimEnd().Length);

        // Estimate dimensions - braille/Unicode chars need ~0.65x width ratio
        var charWidth = fontSize * 0.65;
        var lineHeight = fontSize * 1.2;
        var padding = 20;
        var width = (int)(maxLineLength * charWidth) + padding * 2;
        var height = (int)(lines.Length * lineHeight) + padding;

        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\">");
        sb.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"#1e1e1e\"/>");
        sb.AppendLine($"  <text font-family=\"{fontFamily}\" font-size=\"{fontSize}\" fill=\"#d4d4d4\">");

        var y = (double)(fontSize + 5);
        foreach (var line in lines)
        {
            var cleanLine = line.TrimEnd();
            if (string.IsNullOrEmpty(cleanLine)) { y += lineHeight; continue; }
            var spans = ParseAnsiLine(cleanLine);
            var x = 10.0;

            sb.Append($"    <tspan x=\"{x}\" y=\"{y}\">");

            foreach (var (text, color) in spans)
            {
                var escapedText = EscapeXml(text);
                if (color != null)
                    sb.Append($"<tspan fill=\"{color}\">{escapedText}</tspan>");
                else
                    sb.Append(escapedText);
            }

            sb.AppendLine("</tspan>");
            y += lineHeight;
        }

        sb.AppendLine("  </text>");
        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    /// <summary>
    ///     Save ANSI content to a markdown file.
    /// </summary>
    public static async Task SaveMarkdownAsync(
        string ansiContent,
        string outputPath,
        MarkdownFormat format = MarkdownFormat.Plain,
        string? title = null,
        CancellationToken ct = default)
    {
        var markdown = ToMarkdown(ansiContent, format, title);
        await File.WriteAllTextAsync(outputPath, markdown, ct);
    }

    /// <summary>
    ///     Save ANSI content directly to SVG file.
    /// </summary>
    public static async Task SaveSvgAsync(
        string ansiContent,
        string outputPath,
        string fontFamily = "Consolas, monospace",
        int fontSize = 14,
        CancellationToken ct = default)
    {
        var svg = GenerateSvg(ansiContent, fontFamily, fontSize);
        await File.WriteAllTextAsync(outputPath, svg, ct);
    }

    /// <summary>
    ///     Strip all ANSI escape codes from content.
    /// </summary>
    public static string StripAnsiCodes(string content)
    {
        return AnsiColorRegex().Replace(content, "");
    }

    /// <summary>
    ///     Convert a single line with ANSI codes to HTML spans.
    /// </summary>
    private static string ConvertAnsiLineToHtml(string line)
    {
        var spans = ParseAnsiLine(line);
        var sb = new StringBuilder();

        foreach (var (text, color) in spans)
        {
            var escapedText = EscapeHtml(text);
            if (color != null)
                sb.Append($"<span style=\"color:{color}\">{escapedText}</span>");
            else
                sb.Append(escapedText);
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Parse a line and extract text segments with their colors.
    /// </summary>
    private static List<(string Text, string? Color)> ParseAnsiLine(string line)
    {
        var result = new List<(string Text, string? Color)>();
        string? currentColor = null;
        var lastEnd = 0;

        var matches = AnsiColorRegex().Matches(line);

        foreach (Match match in matches)
        {
            // Add text before this match
            if (match.Index > lastEnd)
            {
                var text = line.Substring(lastEnd, match.Index - lastEnd);
                if (!string.IsNullOrEmpty(text))
                    result.Add((text, currentColor));
            }

            // Update color
            if (match.Groups[1].Success)
            {
                // RGB color: \x1b[38;2;R;G;Bm
                var r = int.Parse(match.Groups[1].Value);
                var g = int.Parse(match.Groups[2].Value);
                var b = int.Parse(match.Groups[3].Value);
                currentColor = $"#{r:X2}{g:X2}{b:X2}";
            }
            else
            {
                // Reset: \x1b[0m
                currentColor = null;
            }

            lastEnd = match.Index + match.Length;
        }

        // Add remaining text
        if (lastEnd < line.Length)
        {
            var text = line.Substring(lastEnd);
            if (!string.IsNullOrEmpty(text))
                result.Add((text, currentColor));
        }

        return result;
    }

    private static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    private static string EscapeXml(string text)
    {
        return EscapeHtml(text)
            .Replace("'", "&apos;");
    }
}