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

        // 只用來從 ETAPI 回應撈 id 欄位；Trilium 的 id 是英數字，不會踩跳脫
        public static string ExtractString(string json, string field)
        {
            var m = Regex.Match(json ?? string.Empty,
                "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        public static int? ExtractInt(string json, string field)
        {
            var m = Regex.Match(json ?? string.Empty,
                "\"" + Regex.Escape(field) + "\"\\s*:\\s*(-?\\d+)");
            return m.Success && int.TryParse(m.Groups[1].Value, out var value) ? value : (int?)null;
        }
    }
}
