using System.Globalization;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace GalCompanion
{
    // Playnite 執行時才有 JSON 程式庫，外掛與測試共用的部分自己處理，避免版本綁定
    internal static class JsonUtil
    {
        public static string Escape(string s)
        {
            if (s == null)
            {
                return string.Empty;
            }
            var sb = new StringBuilder(s.Length + 8);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        public static string ExtractString(string json, string field)
        {
            var m = Regex.Match(json ?? string.Empty,
                "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? Unescape(m.Groups[1].Value) : null;
        }

        public static string Unescape(string s)
        {
            if (s == null || s.IndexOf('\\') < 0)
            {
                return s;
            }
            var sb = new StringBuilder(s.Length);
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c != '\\' || i == s.Length - 1)
                {
                    sb.Append(c);
                    continue;
                }
                i++;
                var next = s[i];
                switch (next)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 < s.Length && int.TryParse(s.Substring(i + 1, 4),
                                NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                        {
                            sb.Append((char)code);
                            i += 4;
                        }
                        else
                        {
                            sb.Append(next);
                        }
                        break;
                    default: sb.Append(next); break;
                }
            }
            return sb.ToString();
        }

        // ["a","b"] 形式の配列を取り出す（childNoteIds 用）
        public static List<string> ExtractStringArray(string json, string field)
        {
            var items = new List<string>();
            var m = Regex.Match(json ?? string.Empty,
                "\"" + Regex.Escape(field) + "\"\\s*:\\s*\\[([^\\]]*)\\]");
            if (!m.Success)
            {
                return items;
            }
            foreach (Match item in Regex.Matches(m.Groups[1].Value, "\"((?:[^\"\\\\]|\\\\.)*)\""))
            {
                items.Add(Unescape(item.Groups[1].Value));
            }
            return items;
        }

        public static int? ExtractInt(string json, string field)
        {
            var m = Regex.Match(json ?? string.Empty,
                "\"" + Regex.Escape(field) + "\"\\s*:\\s*(-?\\d+)");
            return m.Success && int.TryParse(m.Groups[1].Value, out var value) ? value : (int?)null;
        }
    }
}
