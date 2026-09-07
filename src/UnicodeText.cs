using System.Globalization;

namespace CodexMonitor;

internal static class UnicodeText
{
    internal sealed record Run(string Text, bool Emoji);
    internal static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        var end = 0;
        foreach (var start in StringInfo.ParseCombiningCharacters(text))
        {
            if (start > maxLength) break;
            end = start;
        }
        return text[..end] + "…";
    }
    internal static Run[] Runs(string text)
    {
        var result = new List<Run>();
        var elements = StringInfo.GetTextElementEnumerator(text);
        var plain = new System.Text.StringBuilder();
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            var emoji = element.EnumerateRunes().Any(r => r.Value is >= 0x1F000 and <= 0x1FAFF or 0xFE0F or 0x20E3
                or >= 0x2600 and <= 0x27BF or 0x231A or 0x231B or 0x23E9 or 0x23EA or 0x23EB or 0x23EC or 0x23F0 or 0x23F3
                or 0x2B50 or 0x2B55);
            // Explicit text presentation should retain the user's chosen font.
            if (element.Contains('\uFE0E')) emoji = false;
            if (!emoji) { plain.Append(element); continue; }
            if (plain.Length > 0) { result.Add(new(plain.ToString(), false)); plain.Clear(); }
            result.Add(new(element, true));
        }
        if (plain.Length > 0) result.Add(new(plain.ToString(), false));
        return result.ToArray();
    }
}
