using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Rampastring.XNAUI.Extensions;

namespace Rampastring.XNAUI.XNAControls;

/// <summary>
/// A part of a potentially long line of text.
/// </summary>
public struct XNATextPart
{
    public XNATextPart(string text, int fontIndex, float scale, Color? color, bool underlined, bool includeSpace)
    {
        _text = text;
        _fontIndex = fontIndex;
        _scale = scale;
        _color = color;
        Underlined = underlined;
        IncludesSpace = includeSpace;
        Size = Point.Zero;
        UpdateSize();
    }

    public XNATextPart(string text, int fontIndex, float scale, Color? color, bool underlined) : this(text, fontIndex, scale, color, underlined, true)
    {
    }

    public XNATextPart(string text) : this(text, 0, 1.0f, null, false) { }

    public XNATextPart(string text, int fontIndex, Color? color) : this(text, fontIndex, 1.0f, color, false) { }

    private string _text;

    public string Text
    {
        get => _text;
        set { _text = value; UpdateSize(); }
    }

    private int _fontIndex;

    public int FontIndex
    {
        get => _fontIndex;
        set { _fontIndex = value; UpdateSize(); }
    }

    private float _scale;

    public float Scale
    {
        get => _scale;
        set { _scale = value; UpdateSize(); }
    }

    private Color? _color;

    public Color Color
    {
        get => _color ?? UISettings.ActiveSettings.TextColor;
        set => _color = value;
    }

    public bool Underlined { get; set; }

    /// <summary>
    /// The size of the text part, with scaling taken into account.
    /// </summary>
    public Point Size { get; private set; }

    /// <summary>
    /// Does this text part have a space at the end of it?
    /// </summary>
    public bool IncludesSpace { get; private set; }

    private void UpdateSize()
    {
        Vector2 size = Renderer.GetTextDimensions(_text, FontIndex) * Scale;
        Size = new Point((int)size.X, (int)size.Y);
    }

    public int Width => (int)(Size.X);

    public int Height => (int)(Size.Y);

    /// <summary>
    /// Defines the vertical draw offset of the text part.
    /// Calculated dynamically to center the text part within its line.
    /// </summary>
    internal int Y { get; set; }

    public void Draw(Point point)
    {
        Renderer.DrawStringWithShadow(Text, FontIndex, new Vector2(point.X, point.Y), Color, Scale);
        if (Underlined)
        {
            Renderer.DrawRectangle(new Rectangle(point.X, point.Y, (int)(Size.X * Scale), 1), Color, 1);
        }
    }
}

/// <summary>
/// A text line.
/// </summary>
internal struct XNATextLine
{
    public XNATextLine(List<XNATextPart> parts)
    {
        Parts = parts;
    }

    public List<XNATextPart> Parts { get; private set; }

    public int Width
    {
        get
        {
            int width = 0;
            Parts.ForEach(p => width += p.Width);
            return width;
        }
    }

    public int Height
    {
        get
        {
            int height = 0;
            Parts.ForEach(p => height = p.Height > height ? p.Height : height);
            return height;
        }
    }

    public void AddPart(XNATextPart part)
    {
        Parts.Add(part);
    }
}

/// <summary>
/// A text renderer, practically an enhanced label control.
/// Takes <see cref="XNATextPart"/>s, automatically formats them, applies line
/// breaks if necessary and renders them.
/// </summary>
public class XNATextRenderer : XNAControl
{
    public XNATextRenderer(WindowManager windowManager) : base(windowManager)
    {
    }

    public int Padding { get; set; } = 3;
    public int SpaceBetweenLines { get; set; } = 0;

    private List<XNATextPart> originalTextParts = new List<XNATextPart>();

    private List<XNATextLine> renderedTextLines = new List<XNATextLine>();

    public void AddTextPart(XNATextPart text)
    {
        originalTextParts.Add(text);
    }

    public void AddTextLine(XNATextPart text)
    {
        originalTextParts.Add(new XNATextPart(Environment.NewLine + text.Text,
            text.FontIndex, text.Scale, text.Color, text.Underlined));
    }

    public void ClearTextParts()
    {
        originalTextParts.Clear();
        renderedTextLines.Clear();
    }

    public int GetTextPartCount() => originalTextParts.Count;

    public XNATextPart GetTextPart(int index) => originalTextParts[index];

    private void AdjustHeight(ref XNATextLine line, ref XNATextPart currentOutputPart)
    {
        if (currentOutputPart.Height < line.Height)
        {
            // If this part is not as high as other parts in the line, center it
            currentOutputPart.Y = (line.Height - currentOutputPart.Height) / 2;
        }
        else if (currentOutputPart.Height > line.Height)
        {
            // If this part is higher than other parts in the line, center existing parts in the line
            for (int pIndex = 0; pIndex < line.Parts.Count; pIndex++)
            {
                line.Parts[pIndex] = line.Parts[pIndex] with { Y = currentOutputPart.Height - line.Parts[pIndex].Height };
            }
        }
    }

    /// <summary>
    /// Processes the text parts fed to this text renderer.
    /// </summary>
    public void PrepareTextParts()
    {
        renderedTextLines.Clear();

        var preparedTextLines = new List<XNATextLine>();

        // Create a clone of the original text parts.
        // Look for newlines and pre-process them by splitting parts
        // that contain newlines.
        var partsToProcess = new List<XNATextPart>(originalTextParts);
        for (int i = 0; i < partsToProcess.Count; i++)
        {
            var part = partsToProcess[i];

            int newLineIndex = part.Text.IndexOf(Environment.NewLine);
            if (newLineIndex == -1)
            {
                // There is no newline. Simply add this part to the existing line, or if
                // no line exists, create one.

                if (preparedTextLines.Count == 0)
                    preparedTextLines.Add(new XNATextLine(new List<XNATextPart>() { part }));
                else
                    preparedTextLines[preparedTextLines.Count - 1].AddPart(part);

                continue;
            }

            // There is a newline in this text part. Split this part from the index of the newline.

            string postNewlineText = part.Text.Substring(newLineIndex + Environment.NewLine.Length);
            string remainingTextForThisPart = part.Text.Substring(0, newLineIndex);

            // Process the part before the newline as part of an existing line. If no line exists, create one.
            var textPart = new XNATextPart(remainingTextForThisPart, part.FontIndex, part.Scale, part.Color, part.Underlined);
            if (preparedTextLines.Count == 0)
                preparedTextLines.Add(new XNATextLine(new List<XNATextPart>() { textPart }));
            else
                preparedTextLines[preparedTextLines.Count - 1].AddPart(textPart);

            // Keep the formatting that is active at the end of the previous line
            // so that colors are not lost across a line break.
            string newlineCarry = TextFormatting.GetActiveFormattingPrefix(remainingTextForThisPart);
            var newTextPart = new XNATextPart(newlineCarry + postNewlineText, part.FontIndex, part.Scale, part.Color, part.Underlined);
            partsToProcess.Insert(i + 1, newTextPart);
            preparedTextLines.Add(new XNATextLine(new List<XNATextPart>(1)));
        }

        // Look for lines that contain no text parts, or only empty text parts.
        // If ones exist, add a space to them so they still contain some height.
        // If there are empty text parts in a line that otherwise contains text,
        // remove the empty parts.
        for (int i = 0; i < preparedTextLines.Count; i++)
        {
            if (preparedTextLines[i].Parts.Count == 0)
            {
                preparedTextLines[i].AddPart(new XNATextPart(" "));
            }
            else if (preparedTextLines[i].Parts.TrueForAll(p => p.Text == ""))
            {
                var existingPart = preparedTextLines[i].Parts[0];
                preparedTextLines[i].Parts[0] = new XNATextPart(" ", existingPart.FontIndex, existingPart.Scale, existingPart.Color, existingPart.Underlined);
            }
            else
            {
                while (true)
                {
                    int emptyTextPartIndex = preparedTextLines[i].Parts.FindIndex(p => p.Text == string.Empty);
                    if (emptyTextPartIndex < 0)
                        break;

                    preparedTextLines[i].Parts.RemoveAt(emptyTextPartIndex);
                }
            }
        }

        // For each line, process their text parts.
        for (int i = 0; i < preparedTextLines.Count; i++)
        {
            var line = new XNATextLine(new List<XNATextPart>());
            renderedTextLines.Add(line);

            var lineOriginalTextParts = new List<XNATextPart>(preparedTextLines[i].Parts);
            int remainingWidth = Width - (Padding * 2);

            foreach (XNATextPart textPart in lineOriginalTextParts)
            {
                string remainingText = textPart.Text;
                var currentOutputPart = new XNATextPart("", textPart.FontIndex, textPart.Scale, textPart.Color, textPart.Underlined);
                int maxLineWidth = Width - (Padding * 2);

                while (true)
                {
                    var words = remainingText.Split([' '], StringSplitOptions.None);
                    for (int wIndex = 0; wIndex < words.Length; wIndex++)
                    {
                        string word = words[wIndex];

                        if (word == "")
                        {
                            currentOutputPart.Text += " ";
                            remainingWidth -= (int)Renderer.GetTextDimensions(" ", textPart.FontIndex).X;
                            continue;
                        }

                        bool includeSpace = textPart.IncludesSpace || wIndex < words.Length - 1;
                        string wordWithSpace = word + " ";
                        int wordWidth = (int)(Renderer.GetTextDimensions(word, textPart.FontIndex).X * textPart.Scale);
                        int wordWidthWithSpace = (int)(Renderer.GetTextDimensions(wordWithSpace, textPart.FontIndex).X * textPart.Scale);

                        if (wordWidth < remainingWidth)
                        {
                            remainingWidth -= includeSpace ? wordWidthWithSpace : wordWidth;
                            currentOutputPart.Text += includeSpace ? wordWithSpace : word;
                        }
                        else if (wordWidth <= maxLineWidth)
                        {
                            if (string.IsNullOrWhiteSpace(currentOutputPart.Text))
                            {
                                // Leading indentation (only whitespace accumulated so far):
                                // keep it attached to the wrapped word instead of orphaning it
                                // onto its own empty line.
                                line = new XNATextLine(new List<XNATextPart>());
                                renderedTextLines.Add(line);
                                currentOutputPart = new XNATextPart(currentOutputPart.Text + wordWithSpace, textPart.FontIndex, textPart.Scale, textPart.Color, textPart.Underlined);
                                remainingWidth = maxLineWidth - (int)(Renderer.GetTextDimensions(currentOutputPart.Text, textPart.FontIndex).X * textPart.Scale);
                            }
                            else
                            {
                                string wrapCarry = TextFormatting.GetActiveFormattingPrefix(currentOutputPart.Text);
                                AdjustHeight(ref line, ref currentOutputPart);
                                line.Parts.Add(currentOutputPart);
                                remainingWidth = maxLineWidth - wordWidthWithSpace;
                                currentOutputPart = new XNATextPart(wrapCarry + wordWithSpace, textPart.FontIndex, textPart.Scale, textPart.Color, textPart.Underlined);
                                line = new XNATextLine(new List<XNATextPart>());
                                renderedTextLines.Add(line);
                            }
                        }
                        else
                        {
                            // True only when the accumulated text so far is pure leading
                            // indentation (e.g. the spaces used to indent a paragraph).
                            bool leadingWhitespace = string.IsNullOrWhiteSpace(currentOutputPart.Text) && currentOutputPart.Text.Length > 0;
                            string chunkCarry = TextFormatting.GetActiveFormattingPrefix(currentOutputPart.Text);

                            if (!leadingWhitespace && currentOutputPart.Text.Length > 0)
                            {
                                AdjustHeight(ref line, ref currentOutputPart);
                                line.Parts.Add(currentOutputPart);
                                line = new XNATextLine(new List<XNATextPart>());
                                renderedTextLines.Add(line);
                                currentOutputPart = new XNATextPart("", textPart.FontIndex, textPart.Scale, textPart.Color, textPart.Underlined);
                            }

                            int start = 0;
                            bool firstChunk = true;
                            while (start < word.Length)
                            {
                                int remaining = word.Length - start;
                                int low = 0, high = remaining;
                                while (low < high)
                                {
                                    int mid = (low + high + 1) / 2;
                                    string candidate = word.SubstringSurrogateAware(start, mid);
                                    if (Renderer.GetTextDimensions(candidate, textPart.FontIndex).X * textPart.Scale <= maxLineWidth)
                                        low = mid;
                                    else
                                        high = mid - 1;
                                }

                                if (low >= remaining)
                                    break;

                                int safeLength = TextFormatting.GetSafeSplitLength(word, start, low);
                                if (safeLength <= 0)
                                    break;

                                string chunk = word.SubstringSurrogateAware(start, safeLength);
                                if (chunk.Length == 0)
                                    break;

                                // When carrying leading indentation, attach it to the first
                                // chunk so the indent stays on the same line as the start of
                                // the word instead of being orphaned onto its own empty line.
                                string chunkText = (firstChunk && leadingWhitespace) ? currentOutputPart.Text + chunk : chunkCarry + chunk;
                                var chunkPart = new XNATextPart(chunkText, textPart.FontIndex, textPart.Scale, textPart.Color, textPart.Underlined);
                                line.Parts.Add(chunkPart);
                                chunkCarry = TextFormatting.GetActiveFormattingPrefix(chunkText);
                                firstChunk = false;
                                line = new XNATextLine(new List<XNATextPart>());
                                renderedTextLines.Add(line);
                                start += safeLength;
                            }

                            if (start < word.Length)
                            {
                                string remainingWord = word.SubstringSurrogateAware(start, word.Length - start);
                                string prefix = (firstChunk && leadingWhitespace) ? currentOutputPart.Text : chunkCarry;
                                currentOutputPart = new XNATextPart(prefix + remainingWord + " ", textPart.FontIndex, textPart.Scale, textPart.Color, textPart.Underlined);
                                remainingWidth = maxLineWidth - (int)(Renderer.GetTextDimensions(currentOutputPart.Text, textPart.FontIndex).X * textPart.Scale);
                            }
                            else
                            {
                                // The entire word was laid out across chunks; the carried
                                // indentation has already been attached to the first chunk.
                                if (leadingWhitespace)
                                    currentOutputPart = new XNATextPart("", textPart.FontIndex, textPart.Scale, textPart.Color, textPart.Underlined);

                                remainingWidth = maxLineWidth;
                            }
                        }
                    }

                    AdjustHeight(ref line, ref currentOutputPart);
                    line.Parts.Add(currentOutputPart);
                    break;
                }
            }
        }

        ClientRectangleUpdated -= XNATextRenderer_ClientRectangleUpdated;
        Height = renderedTextLines.Sum(l => l.Height) + (Padding * 2);
        if (renderedTextLines.Count > 1)
            Height += (renderedTextLines.Count - 1) * SpaceBetweenLines;

        ClientRectangleUpdated += XNATextRenderer_ClientRectangleUpdated;
    }

    private void XNATextRenderer_ClientRectangleUpdated(object sender, EventArgs e)
    {
        PrepareTextParts();
    }

    public override void Draw(GameTime gameTime)
    {
        int y = Padding;

        foreach (XNATextLine line in renderedTextLines)
        {
            int x = Padding;

            foreach (XNATextPart part in line.Parts)
            {
                DrawStringWithShadow(part.Text, part.FontIndex, new Vector2(x, y + part.Y), part.Color, part.Scale);
                x += part.Width + 1;
            }

            y += line.Height + SpaceBetweenLines;
        }

        base.Draw(gameTime);
    }
}
