using System;
using System.Text;

namespace GalCompanion
{
    /// <summary>
    /// ノート標題の組み立て。「XXX 遊戲心得」のようにゲームごとにノートを分けるためのもの。
    /// 既定はタイトルの頭にゲーム名を足すだけなので、既存の設定値をそのまま活かせる。
    /// 置き場所を自分で決めたいときは標題に {game} と書く。
    /// </summary>
    internal static class TriliumTitles
    {
        internal const string GamePlaceholder = "{game}";

        public const string DefaultImpressions = "遊戲心得";
        public const string DefaultTranslation = "翻譯問題";

        /// <summary>テンプレートが自分で置き場所を指定しているか。指定があればそちらを優先する。</summary>
        public static bool HasPlaceholder(string template)
        {
            return !string.IsNullOrEmpty(template)
                && template.IndexOf(GamePlaceholder, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>結果の標題にゲーム名が入るか。エントリ見出しの重複を避けるのに使う。</summary>
        public static bool CarriesGameName(string template, bool prefixWithGame)
        {
            return HasPlaceholder(template) || prefixWithGame;
        }

        /// <summary>
        /// テンプレートを実際の標題にする。
        /// {game} が書いてあればそこへ、無ければ prefixWithGame のときだけ頭に付ける。
        /// ゲーム名が取れないとき（Playnite 外で起動した等）はどちらもせず素の標題になる。
        /// </summary>
        public static string Format(
            string template, string gameName, string fallbackTemplate, bool prefixWithGame = false)
        {
            var name = (gameName ?? string.Empty).Trim();
            var body = string.IsNullOrWhiteSpace(template) ? fallbackTemplate : template;

            string result;
            if (HasPlaceholder(body))
            {
                result = Collapse(Substitute(body, name));
            }
            else
            {
                result = Collapse(body);
                if (prefixWithGame && name.Length > 0)
                {
                    result = Collapse(name + " " + result);
                }
            }

            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
            // {game} だけのテンプレートでゲーム名も無い、のような場合に空標題を作らせない
            return Collapse(Substitute(fallbackTemplate, name));
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
