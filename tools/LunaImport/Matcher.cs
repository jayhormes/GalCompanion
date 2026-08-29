using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LunaImport
{
    /// <summary>
    /// LunaTranslator のゲームと Playnite のゲームを突き合わせる。
    /// Luna は exe のフルパスで管理しているので、まずパスで、駄目ならタイトルで照合する。
    /// </summary>
    internal static class Matcher
    {
        public static List<string> CandidatePaths(PlayniteGame game)
        {
            var paths = new List<string>();
            if (game == null)
            {
                return paths;
            }
            foreach (var raw in game.ActionPaths)
            {
                var resolved = PathUtil.Normalize(PathUtil.Resolve(raw, game.InstallDirectory));
                if (resolved.Length > 0 && !paths.Contains(resolved))
                {
                    paths.Add(resolved);
                }
            }
            return paths;
        }

        /// <summary>全角・空白・記号のゆれを潰す。「モザイクの天使 体験版」と「モザイクの天使体験版」を同じにする。</summary>
        public static string NormalizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }
            var wide = title.Normalize(NormalizationForm.FormKC);
            var sb = new StringBuilder(wide.Length);
            foreach (var c in wide)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }
            return sb.ToString();
        }

        public static List<MatchResult> Match(
            IEnumerable<LunaGame> lunaGames, IEnumerable<PlayniteGame> playniteGames)
        {
            var byPath = new Dictionary<string, PlayniteGame>(StringComparer.Ordinal);
            var byTitle = new Dictionary<string, PlayniteGame>(StringComparer.Ordinal);
            var ambiguousTitles = new HashSet<string>(StringComparer.Ordinal);

            foreach (var game in playniteGames)
            {
                foreach (var path in CandidatePaths(game))
                {
                    // 先に登録されたほうを残す。同じ exe を指すゲームが 2 つあるのは稀
                    if (!byPath.ContainsKey(path))
                    {
                        byPath[path] = game;
                    }
                }

                var title = NormalizeTitle(game.Name);
                if (title.Length == 0)
                {
                    continue;
                }
                if (byTitle.ContainsKey(title))
                {
                    // 同名が複数あるならタイトル照合は当てにならない
                    ambiguousTitles.Add(title);
                    continue;
                }
                byTitle[title] = game;
            }

            var results = new List<MatchResult>();
            foreach (var luna in lunaGames)
            {
                var result = new MatchResult { Luna = luna, Kind = MatchKind.None };

                PlayniteGame hit;
                var lunaPath = PathUtil.Normalize(luna.GamePath);
                if (lunaPath.Length > 0 && byPath.TryGetValue(lunaPath, out hit))
                {
                    result.Playnite = hit;
                    result.Kind = MatchKind.Path;
                }
                else
                {
                    var title = NormalizeTitle(luna.DisplayName);
                    if (title.Length > 0 && !ambiguousTitles.Contains(title)
                        && byTitle.TryGetValue(title, out hit))
                    {
                        result.Playnite = hit;
                        result.Kind = MatchKind.Title;
                    }
                }
                results.Add(result);
            }
            return results;
        }
    }
}
