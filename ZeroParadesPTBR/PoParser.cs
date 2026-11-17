using System.Text;

namespace ZeroParadesPTBR;

public sealed record PoLoadResult(
    Dictionary<string, string> Translations,
    int TotalEntries,
    int UntranslatedEntries
);

public static class PoParser
{
    public static PoLoadResult Load(string poPath)
    {
        var translations = new Dictionary<string, string>(StringComparer.Ordinal);
        int total = 0;
        int untranslated = 0;

        string? msgctxt = null;
        string? msgid = null;
        string? msgstr = null;
        string? currentField = null;
        var currentBuffer = new StringBuilder();

        void FlushField()
        {
            if (currentField is null) return;
            var value = currentBuffer.ToString();
            switch (currentField)
            {
                case "msgctxt": msgctxt = value; break;
                case "msgid": msgid = value; break;
                case "msgstr": msgstr = value; break;
            }
            currentBuffer.Clear();
            currentField = null;
        }

        void FlushEntry()
        {
            FlushField();
            if (msgid is null)
            {
                msgctxt = null;
                msgstr = null;
                return;
            }

            // Skip PO header entry (empty msgid and no msgctxt)
            if (string.IsNullOrEmpty(msgctxt) && string.IsNullOrEmpty(msgid))
            {
                msgctxt = null;
                msgid = null;
                msgstr = null;
                return;
            }

            if (!string.IsNullOrEmpty(msgctxt))
            {
                total++;
                if (!string.IsNullOrEmpty(msgstr))
                {
                    translations[msgctxt] = msgstr;
                }
                else
                {
                    translations[msgctxt] = msgid;
                    untranslated++;
                }
            }

            msgctxt = null;
            msgid = null;
            msgstr = null;
        }

        foreach (var rawLine in File.ReadLines(poPath, Encoding.UTF8))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                FlushEntry();
                continue;
            }

            if (line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("msgctxt "))
            {
                FlushField();
                currentField = "msgctxt";
                currentBuffer.Append(Unquote(line.AsSpan(8)));
            }
            else if (line.StartsWith("msgid "))
            {
                FlushField();
                currentField = "msgid";
                currentBuffer.Append(Unquote(line.AsSpan(6)));
            }
            else if (line.StartsWith("msgstr "))
            {
                FlushField();
                currentField = "msgstr";
                currentBuffer.Append(Unquote(line.AsSpan(7)));
            }
            else if (line.StartsWith('"'))
            {
                currentBuffer.Append(Unquote(line.AsSpan()));
            }
        }

        FlushEntry();
        return new PoLoadResult(translations, total, untranslated);
    }

    private static string Unquote(ReadOnlySpan<char> span)
    {
        span = span.Trim();
        if (span.Length >= 2 && span[0] == '"' && span[^1] == '"')
        {
            span = span[1..^1];
        }

        var sb = new StringBuilder(span.Length);
        for (int i = 0; i < span.Length; i++)
        {
            char c = span[i];
            if (c == '\\' && i + 1 < span.Length)
            {
                char next = span[++i];
                sb.Append(next switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '\\' => '\\',
                    '"' => '"',
                    _ => next
                });
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
