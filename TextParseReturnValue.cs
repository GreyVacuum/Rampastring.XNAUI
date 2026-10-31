using System;
using System.Collections.Generic;
using Rampastring.XNAUI.Extensions;
using Rampastring.XNAUI.FontManagement;

namespace Rampastring.XNAUI;

public class TextParseReturnValue
{
    public int LineCount;
    public string Text;

    public TextParseReturnValue(string text, int lineCount)
    {
        Text = text;
        LineCount = lineCount;
    }

    public static TextParseReturnValue FixText(IFont font, int width, string text)
    {
        string line = string.Empty;
        int lineCount = 0;
        string processedText = string.Empty;
        string[] wordArray = text.Split(' ');

        foreach (string word in wordArray)
        {
            string wordWithSpace = word + " ";

            if (TextFormatting.Measure(font, line + wordWithSpace).X > width)
            {
                if (TextFormatting.Measure(font, word).X > width)
                {
                    if (TextFormatting.HasVisibleText(line))
                    {
                        string carry = TextFormatting.GetActiveFormattingPrefix(line);
                        processedText = processedText + line + Environment.NewLine;
                        lineCount++;
                        line = carry;
                    }

                    int start = 0;
                    while (start < word.Length)
                    {
                        int remaining = word.Length - start;
                        int low = 0, high = remaining;
                        while (low < high)
                        {
                            int mid = (low + high + 1) / 2;
                            if (TextFormatting.Measure(font, word.SubstringSurrogateAware(start, mid)).X <= width)
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

                        if (TextFormatting.HasVisibleText(line))
                        {
                            string carry = TextFormatting.GetActiveFormattingPrefix(line);
                            processedText = processedText + line + Environment.NewLine;
                            lineCount++;
                            line = carry;
                        }

                        line = line + chunk + " ";
                        start += safeLength;
                    }

                    if (start < word.Length)
                    {
                        string remainingWord = word.SubstringSurrogateAware(start, word.Length - start);
                        if (TextFormatting.HasVisibleText(line))
                        {
                            string carry = TextFormatting.GetActiveFormattingPrefix(line);
                            processedText = processedText + line + Environment.NewLine;
                            lineCount++;
                            line = carry;
                        }
                        line = line + remainingWord + " ";
                    }
                    else if (TextFormatting.HasVisibleText(line))
                    {
                        line = line.TrimEnd() + " ";
                    }
                    continue;
                }

                string lineCarry = TextFormatting.GetActiveFormattingPrefix(line);
                processedText = processedText + line + Environment.NewLine;
                lineCount++;
                line = lineCarry;
            }

            line = line + wordWithSpace;
        }

        processedText = processedText + line;
        return new TextParseReturnValue(processedText, lineCount);
    }

    public static List<string> GetFixedTextLines(IFont font, int width, string text, bool splitWords = true, bool keepBlankLines = false)
    {
        if (string.IsNullOrEmpty(text))
            return new List<string>(0);

        var returnValue = new List<string>();

        // Remove '\r' characters so Windows newlines aren't counted twice
        string[] lineArray = text.Replace("\r", "").Split(new char[] { '\n' }, StringSplitOptions.None);

        foreach (string originalTextLine in lineArray)
        {
            if (keepBlankLines && originalTextLine == string.Empty)
                returnValue.Add(string.Empty);

            string line = string.Empty;

            string[] wordArray = originalTextLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in wordArray)
            {
                if (TextFormatting.Measure(font, line + word).X > width)
                {
                    string carry = TextFormatting.GetActiveFormattingPrefix(line);

                    if (TextFormatting.HasVisibleText(line))
                    {
                        returnValue.Add(line.Remove(line.Length - 1));
                    }

                    // Split individual words that are longer than the allowed width
                    if (splitWords && TextFormatting.Measure(font, word).X > width)
                    {
                        int start = 0;
                        while (start < word.Length)
                        {
                            int remaining = word.Length - start;
                            int low = 0, high = remaining;
                            while (low < high)
                            {
                                int mid = (low + high + 1) / 2;
                                if (TextFormatting.Measure(font, word.SubstringSurrogateAware(start, mid)).X <= width)
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

                            string chunkText = carry + chunk;
                            returnValue.Add(chunkText);
                            carry = TextFormatting.GetActiveFormattingPrefix(chunkText);
                            start += safeLength;
                        }

                        line = start < word.Length
                            ? carry + word.SubstringSurrogateAware(start, word.Length - start) + " "
                            : string.Empty;
                        continue;
                    }

                    line = carry + word + " ";
                    continue;
                }

                line = line + word + " ";
            }

            if (!string.IsNullOrEmpty(line) && line.Length > 1)
                returnValue.Add(line.TrimEnd());
        }

        return returnValue;
    }
}
