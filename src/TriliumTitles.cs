using System;
using System.Text;

namespace GalCompanion
{
    /// <summary>
    /// ノート標題のテンプレート。{game} を今遊んでいるタイトルに差し替える。
    /// 「XXX 遊戲心得」のようにゲームごとにノートを分けるための仕組みで、
    /// {game} を書かなければ従来どおり全タイトル共通の 1 枚になる。
    /// </summary>
    internal static class TriliumTitles
    {
        internal const string GamePlaceholder = "{game}";

        public const string DefaultImpressions = "{game} 遊戲心得";
        public const string DefaultTranslation = "翻譯問題";

        public static bool IsPerGame(string template)
        {
            return !string.IsNullOrEmpty(template)
                && template.IndexOf(GamePlaceholder, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// テンプレートを実際の標題にする。ゲーム名が取れないとき（Playnite 外で起動した等）は
        /// プレースホルダを消して詰めるので「遊戲心得」になる。
        /// </summary>
        public static string Format(string template, string gameName, string fallbackTemplate)
        {
            var result = Collapse(Substitute(template, gameName));
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
            return Collapse(Substitute(fallbackTemplate, gameName));
        }

        private static string Substitute(string template, string gameName)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }
            var name = (gameName ?? string.Empty).Trim();

            var sb = new StringBuilder();
            var i = 0;
            while (i < template.Length)
            {
                var hit = template.IndexOf(GamePlaceholder, i, StringComparison.OrdinalIgnoreCase);
                if (hit < 0)
                {
                    sb.Append(template, i, template.Length - i);
                    break;
                }
                sb.Append(template, i, hit - i).Append(name);
                i = hit + GamePlaceholder.Length;
            }
            return sb.ToString();
        }

        // 差し替えで空白が連続したり前後に残ったりするので畳む
        private static string Collapse(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            var sb = new StringBuilder(s.Length);
            var pendingSpace = false;
            foreach (var c in s)
            {
                if (char.IsWhiteSpace(c))
                {
                    pendingSpace = sb.Length > 0;
                    continue;
                }
                if (pendingSpace)
                {
                    sb.Append(' ');
                    pendingSpace = false;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
