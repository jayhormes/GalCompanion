using System;
using System.Collections.Generic;
using System.Text;

namespace LunaImport
{
    internal static class PathUtil
    {
        /// <summary>比較用に正規化する。大小・区切り・末尾のスラッシュ・引用符を無視する。</summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }
            var s = path.Trim().Trim('"');
            s = s.Replace('/', '\\');
            while (s.Length > 3 && s.EndsWith("\\", StringComparison.Ordinal))
            {
                s = s.Substring(0, s.Length - 1);
            }
            return s.ToLowerInvariant();
        }

        public static string FileName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }
            var s = path.Trim().Trim('"').Replace('/', '\\').TrimEnd('\\');
            var cut = s.LastIndexOf('\\');
            return cut < 0 ? s : s.Substring(cut + 1);
        }

        /// <summary>相対パスならインストール先を頭に付ける。Playnite のアクションは相対で入っていることがある。</summary>
        public static string Resolve(string path, string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }
            var s = path.Trim().Trim('"');
            if (IsRooted(s) || string.IsNullOrWhiteSpace(installDirectory))
            {
                return s;
            }
            return installDirectory.TrimEnd('\\', '/') + "\\" + s.TrimStart('\\', '/');
        }

        private static bool IsRooted(string s)
        {
            if (s.Length >= 2 && s[1] == ':')
            {
                return true;
            }
            return s.StartsWith("\\\\", StringComparison.Ordinal);
        }

        /// <summary>
        /// 引数の中の exe を拾う。Locale Emulator 経由だとアクションの Path は LEProc.exe で、
        /// 本物のゲームは引数側にいる。
        /// </summary>
        public static List<string> ExtractExecutables(string arguments)
        {
            var found = new List<string>();
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return found;
            }

            foreach (var token in Tokenize(arguments))
            {
                if (token.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(token);
                }
            }
            return found;
        }

        // 引用符つきのトークンを 1 つとして扱う素朴な分割
        internal static List<string> Tokenize(string s)
        {
            var tokens = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;

            foreach (var c in s)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }
                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                    continue;
                }
                sb.Append(c);
            }
            if (sb.Length > 0)
            {
                tokens.Add(sb.ToString());
            }
            return tokens;
        }
    }
}
