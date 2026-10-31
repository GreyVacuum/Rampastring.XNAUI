using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI.FontManagement;

namespace Rampastring.XNAUI;

/// <summary>
/// A piece of text that shares the same formatting attributes.
/// Produced by <see cref="TextFormatting.Parse"/>.
/// </summary>
public sealed class FormattedTextRun
{
    public FormattedTextRun(string text, Color? color, bool bold, bool underlined, bool strikethrough, bool obfuscated)
    {
        Text = text;
        Color = color;
        Bold = bold;
        Underlined = underlined;
        Strikethrough = strikethrough;
        Obfuscated = obfuscated;
    }

    /// <summary>The visible text of the run. Formatting codes have been removed.</summary>
    public string Text { get; }

    /// <summary>The color set by a color code, or null when the run uses the base text color.</summary>
    public Color? Color { get; }

    public bool Bold { get; }
    public bool Underlined { get; }
    public bool Strikethrough { get; }
    public bool Obfuscated { get; }
}

/// <summary>
/// Parses Minecraft-style formatting codes that are embedded in text using the
/// section sign character (U+00A7, "§").
/// <para>
/// Color codes reset any formatting that was enabled before them, matching
/// Minecraft's behaviour. A reset code (§r) clears both color and formatting.
/// Both single-character codes (§c, §l, ...) and readable color names
/// (§red, §yellow, ...) are supported.
/// </para>
/// </summary>
public static class TextFormatting
{
    /// <summary>The character that introduces a formatting code.</summary>
    public const char CodeChar = '\u00A7';

    private enum CodeCommand
    {
        None,
        Color,
        Reset,
        Bold,
        Underline,
        Strikethrough,
        Obfuscated,
        Italic,
        Literal,
    }

    /// <summary>A resolved color code: the color and the legacy code letter used to reproduce it.</summary>
    private readonly struct ColorCode
    {
        public ColorCode(Color value, char legacyCode)
        {
            Value = value;
            LegacyCode = legacyCode;
        }

        public readonly Color Value;
        public readonly char LegacyCode;
    }

    private struct FormatState
    {
        public ColorCode? Color;
        public bool Bold;
        public bool Underlined;
        public bool Strikethrough;
        public bool Obfuscated;
        public bool Italic;

        public bool HasFormatting => Color.HasValue || Bold || Underlined || Strikethrough || Obfuscated || Italic;
    }

    private static readonly Dictionary<char, ColorCode> LegacyColorCodes = new()
    {
        ['0'] = new(new Color(0x00, 0x00, 0x00), '0'),
        ['1'] = new(new Color(0x00, 0x00, 0xAA), '1'),
        ['2'] = new(new Color(0x00, 0xAA, 0x00), '2'),
        ['3'] = new(new Color(0x00, 0xAA, 0xAA), '3'),
        ['4'] = new(new Color(0xAA, 0x00, 0x00), '4'),
        ['5'] = new(new Color(0xAA, 0x00, 0xAA), '5'),
        ['6'] = new(new Color(0xFF, 0xAA, 0x00), '6'),
        ['7'] = new(new Color(0xAA, 0xAA, 0xAA), '7'),
        ['8'] = new(new Color(0x55, 0x55, 0x55), '8'),
        ['9'] = new(new Color(0x55, 0x55, 0xFF), '9'),
        ['a'] = new(new Color(0x55, 0xFF, 0x55), 'a'),
        ['b'] = new(new Color(0x55, 0xFF, 0xFF), 'b'),
        ['c'] = new(new Color(0xFF, 0x55, 0x55), 'c'),
        ['d'] = new(new Color(0xFF, 0x55, 0xFF), 'd'),
        ['e'] = new(new Color(0xFF, 0xFF, 0x55), 'e'),
        ['f'] = new(new Color(0xFF, 0xFF, 0xFF), 'f'),
    };

    private static readonly Dictionary<string, ColorCode> NamedColorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = LegacyColorCodes['0'],
        ["blk"] = LegacyColorCodes['0'],
        ["darkblue"] = LegacyColorCodes['1'],
        ["dblu"] = LegacyColorCodes['1'],
        ["navy"] = LegacyColorCodes['1'],
        ["darkgreen"] = LegacyColorCodes['2'],
        ["dgrn"] = LegacyColorCodes['2'],
        ["darkaqua"] = LegacyColorCodes['3'],
        ["darkcyan"] = LegacyColorCodes['3'],
        ["daqua"] = LegacyColorCodes['3'],
        ["dcya"] = LegacyColorCodes['3'],
        ["darkred"] = LegacyColorCodes['4'],
        ["dred"] = LegacyColorCodes['4'],
        ["darkpurple"] = LegacyColorCodes['5'],
        ["darkmagenta"] = LegacyColorCodes['5'],
        ["dpur"] = LegacyColorCodes['5'],
        ["dmag"] = LegacyColorCodes['5'],
        ["gold"] = LegacyColorCodes['6'],
        ["orange"] = LegacyColorCodes['6'],
        ["org"] = LegacyColorCodes['6'],
        ["gray"] = LegacyColorCodes['7'],
        ["grey"] = LegacyColorCodes['7'],
        ["gry"] = LegacyColorCodes['7'],
        ["darkgray"] = LegacyColorCodes['8'],
        ["darkgrey"] = LegacyColorCodes['8'],
        ["dgry"] = LegacyColorCodes['8'],
        ["dgray"] = LegacyColorCodes['8'],
        ["blue"] = LegacyColorCodes['9'],
        ["blu"] = LegacyColorCodes['9'],
        ["green"] = LegacyColorCodes['a'],
        ["grn"] = LegacyColorCodes['a'],
        ["aqua"] = LegacyColorCodes['b'],
        ["cyan"] = LegacyColorCodes['b'],
        ["aqu"] = LegacyColorCodes['b'],
        ["cya"] = LegacyColorCodes['b'],
        ["red"] = LegacyColorCodes['c'],
        ["lightpurple"] = LegacyColorCodes['d'],
        ["lightmagenta"] = LegacyColorCodes['d'],
        ["lpurple"] = LegacyColorCodes['d'],
        ["lightpurp"] = LegacyColorCodes['d'],
        ["magenta"] = LegacyColorCodes['d'],
        ["purple"] = LegacyColorCodes['d'],
        ["pur"] = LegacyColorCodes['d'],
        ["pink"] = LegacyColorCodes['d'],
        ["yellow"] = LegacyColorCodes['e'],
        ["yel"] = LegacyColorCodes['e'],
        ["ylw"] = LegacyColorCodes['e'],
        ["white"] = LegacyColorCodes['f'],
        ["wht"] = LegacyColorCodes['f'],
        ["whi"] = LegacyColorCodes['f'],
    };

    /// <summary>
    /// Color names sorted by length so that the longest matching name wins
    /// (for example "darkgreen" is preferred over "green", and "red" over the
    /// single-character reset code 'r').
    /// </summary>
    private static readonly string[] NamedColorNames =
        NamedColorCodes.Keys.OrderByDescending(name => name.Length).ToArray();

    /// <summary>
    /// Returns true if the text contains at least one formatting code.
    /// </summary>
    public static bool ContainsFormatting(string text) =>
        !string.IsNullOrEmpty(text) && text.IndexOf(CodeChar) >= 0;

    /// <summary>
    /// Returns true if the text contains visible characters after its formatting
    /// codes have been removed. Whitespace counts as visible text.
    /// </summary>
    public static bool HasVisibleText(string text) =>
        !string.IsNullOrEmpty(text) && Strip(text).Length > 0;

    /// <summary>
    /// Removes all formatting codes from the given text.
    /// </summary>
    public static string Strip(string text)
    {
        if (!ContainsFormatting(text))
            return text;

        var builder = new StringBuilder(text.Length);

        for (int i = 0; i < text.Length;)
        {
            if (text[i] == CodeChar)
            {
                int length = TryReadCode(text, i, out _, out CodeCommand command);
                if (command == CodeCommand.Literal)
                {
                    builder.Append(CodeChar);
                    i += length;
                    continue;
                }

                if (length > 0)
                {
                    i += length;
                    continue;
                }
            }

            builder.Append(text[i]);
            i++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Parses the text into runs with concrete formatting attributes.
    /// </summary>
    public static List<FormattedTextRun> Parse(string text)
    {
        var runs = new List<FormattedTextRun>();
        if (string.IsNullOrEmpty(text))
            return runs;

        var builder = new StringBuilder(text.Length);
        FormatState state = default;

        for (int i = 0; i < text.Length;)
        {
            if (text[i] == CodeChar)
            {
                int length = TryReadCode(text, i, out ColorCode? color, out CodeCommand command);

                if (command == CodeCommand.Literal)
                {
                    builder.Append(CodeChar);
                    i += length;
                    continue;
                }

                if (length > 0)
                {
                    FlushRun(builder, state, runs);
                    ApplyCommand(ref state, command, color);
                    i += length;
                    continue;
                }
            }

            builder.Append(text[i]);
            i++;
        }

        FlushRun(builder, state, runs);
        return runs;
    }

    /// <summary>
    /// Scans the text and returns the formatting codes that reproduce the state
    /// that is active at the end of the text. The returned prefix can be prepended
    /// to a continued line so that its formatting is preserved after a line break.
    /// </summary>
    public static string GetActiveFormattingPrefix(string text)
    {
        if (!ContainsFormatting(text))
            return string.Empty;

        FormatState state = default;

        for (int i = 0; i < text.Length;)
        {
            if (text[i] == CodeChar)
            {
                int length = TryReadCode(text, i, out ColorCode? color, out CodeCommand command);
                if (length > 0)
                {
                    if (command != CodeCommand.Literal)
                        ApplyCommand(ref state, command, color);

                    i += length;
                    continue;
                }
            }

            i++;
        }

        if (!state.HasFormatting)
            return string.Empty;

        var builder = new StringBuilder();

        if (state.Color.HasValue)
            AppendColorPrefix(builder, state.Color.Value);

        if (state.Bold)
            builder.Append(CodeChar).Append('l');
        if (state.Underlined)
            builder.Append(CodeChar).Append('n');
        if (state.Strikethrough)
            builder.Append(CodeChar).Append('m');
        if (state.Obfuscated)
            builder.Append(CodeChar).Append('k');
        if (state.Italic)
            builder.Append(CodeChar).Append('o');

        return builder.ToString();
    }

    /// <summary>
    /// Measures the width of the text without counting formatting codes.
    /// </summary>
    public static Vector2 Measure(IFont font, string text) =>
        font.MeasureString(Strip(text));

    /// <summary>
    /// Returns a length that can be used to split the text starting at
    /// <paramref name="start"/> without cutting a formatting code in half.
    /// </summary>
    public static int GetSafeSplitLength(string text, int start, int length)
    {
        int end = start + length;
        if (end <= start)
            return 0;

        for (int i = end - 1; i >= start; i--)
        {
            if (text[i] != CodeChar)
                continue;

            int codeLength = TryReadCode(text, i, out _, out _);
            if (codeLength > 0 && i + codeLength > end)
                end = i;

            break;
        }

        return end - start;
    }

    /// <summary>
    /// Returns the longest prefix of the text whose visible width fits into
    /// <paramref name="maxWidth"/>. Formatting codes are preserved and never cut.
    /// </summary>
    public static string TruncateToWidth(IFont font, string text, int maxWidth)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        string plainText = Strip(text, out int[] visibleToRawIndex);
        if (font.MeasureString(plainText).X <= maxWidth)
            return text;

        int low = 0;
        int high = plainText.Length;

        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            if (font.MeasureString(plainText.Substring(0, mid)).X <= maxWidth)
                low = mid;
            else
                high = mid - 1;
        }

        return text.Substring(0, visibleToRawIndex[low]);
    }

    /// <summary>
    /// Removes formatting codes and returns a map from each visible character
    /// index (including the end index) back to its position in the original text.
    /// </summary>
    private static string Strip(string text, out int[] visibleToRawIndex)
    {
        if (!ContainsFormatting(text))
        {
            visibleToRawIndex = Enumerable.Range(0, text.Length + 1).ToArray();
            return text;
        }

        var builder = new StringBuilder(text.Length);
        var map = new List<int>(text.Length + 1);

        for (int i = 0; i < text.Length;)
        {
            if (text[i] == CodeChar)
            {
                int length = TryReadCode(text, i, out _, out CodeCommand command);
                if (command == CodeCommand.Literal)
                {
                    map.Add(i);
                    builder.Append(CodeChar);
                    i += length;
                    continue;
                }

                if (length > 0)
                {
                    i += length;
                    continue;
                }
            }

            map.Add(i);
            builder.Append(text[i]);
            i++;
        }

        map.Add(text.Length);
        visibleToRawIndex = map.ToArray();
        return builder.ToString();
    }

    private static void FlushRun(StringBuilder builder, FormatState state, List<FormattedTextRun> runs)
    {
        if (builder.Length == 0)
            return;

        runs.Add(new FormattedTextRun(builder.ToString(),
            state.Color.HasValue ? state.Color.Value.Value : null,
            state.Bold, state.Underlined, state.Strikethrough, state.Obfuscated));

        builder.Clear();
    }

    private static void ApplyCommand(ref FormatState state, CodeCommand command, ColorCode? color)
    {
        switch (command)
        {
            case CodeCommand.Color:
                // Color codes reset the formatting that was active before them.
                state = new FormatState { Color = color };
                break;

            case CodeCommand.Reset:
                state = default;
                break;

            case CodeCommand.Bold:
                state.Bold = true;
                break;

            case CodeCommand.Underline:
                state.Underlined = true;
                break;

            case CodeCommand.Strikethrough:
                state.Strikethrough = true;
                break;

            case CodeCommand.Obfuscated:
                state.Obfuscated = true;
                break;

            case CodeCommand.Italic:
                state.Italic = true;
                break;
        }
    }

    private static void AppendColorPrefix(StringBuilder builder, ColorCode color)
    {
        if (color.LegacyCode != '\0')
        {
            builder.Append(CodeChar).Append(color.LegacyCode);
            return;
        }

        // Arbitrary RGB value (the §x hexadecimal form).
        builder.Append(CodeChar).Append('x');
        uint rgb = ((uint)color.Value.R << 16) | ((uint)color.Value.G << 8) | color.Value.B;
        const string hexDigits = "0123456789abcdef";

        for (int shift = 20; shift >= 0; shift -= 4)
        {
            builder.Append(CodeChar)
                .Append(hexDigits[(int)((rgb >> shift) & 0xF)]);
        }
    }

    /// <summary>
    /// Reads a formatting code that starts at <paramref name="index"/>.
    /// Returns the number of characters the code occupies, or 0 if the text at
    /// the given index does not start with a valid code.
    /// </summary>
    private static int TryReadCode(string text, int index, out ColorCode? color, out CodeCommand command)
    {
        color = null;
        command = CodeCommand.None;

        if (index + 1 >= text.Length || text[index] != CodeChar)
            return 0;

        char next = text[index + 1];

        // "§§" renders a literal section sign.
        if (next == CodeChar)
        {
            command = CodeCommand.Literal;
            return 2;
        }

        int start = index + 1;

        // Named colors are matched first and longest-first, so that "§red"
        // resolves to red instead of the single-character reset code 'r'.
        foreach (string name in NamedColorNames)
        {
            if (text.Length - start < name.Length)
                continue;

            if (string.Compare(text, start, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                color = NamedColorCodes[name];
                command = CodeCommand.Color;
                return 1 + name.Length;
            }
        }

        // The §x form: §x§R§R§G§G§B§B
        if ((next == 'x' || next == 'X') && index + 13 < text.Length)
        {
            bool valid = true;
            var hex = new StringBuilder(6);

            for (int i = 0; i < 6; i++)
            {
                int codePosition = index + 2 + (i * 2);
                if (text[codePosition] != CodeChar || !Uri.IsHexDigit(text[codePosition + 1]))
                {
                    valid = false;
                    break;
                }

                hex.Append(text[codePosition + 1]);
            }

            if (valid)
            {
                int rgb = Convert.ToInt32(hex.ToString(), 16);
                color = new ColorCode(new Color(
                    (byte)((rgb >> 16) & 0xFF),
                    (byte)((rgb >> 8) & 0xFF),
                    (byte)(rgb & 0xFF)),
                    '\0');
                command = CodeCommand.Color;
                return 14;
            }
        }

        char lower = char.ToLowerInvariant(next);

        if (lower >= '0' && lower <= '9' || lower >= 'a' && lower <= 'f')
        {
            color = LegacyColorCodes[lower];
            command = CodeCommand.Color;
            return 2;
        }

        switch (lower)
        {
            case 'k':
                command = CodeCommand.Obfuscated;
                return 2;
            case 'l':
                command = CodeCommand.Bold;
                return 2;
            case 'm':
                command = CodeCommand.Strikethrough;
                return 2;
            case 'n':
                command = CodeCommand.Underline;
                return 2;
            case 'o':
                command = CodeCommand.Italic;
                return 2;
            case 'r':
                command = CodeCommand.Reset;
                return 2;
        }

        // Unknown codes are stripped but have no effect.
        command = CodeCommand.None;
        return 2;
    }
}
