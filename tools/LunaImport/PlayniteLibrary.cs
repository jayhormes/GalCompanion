using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace LunaImport
{
    /// <summary>
    /// Playnite のライブラリは library\games\&lt;guid&gt;.json の素の JSON。
    /// 読んだ JObject をそのまま書き戻すので、触っていない項目は原文のまま残る。
    /// </summary>
    internal static class PlayniteLibrary
    {
        public static string DefaultRoot()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Playnite");
        }

        public static string GamesDir(string playniteRoot)
        {
            return Path.Combine(playniteRoot, "library", "games");
        }

        /// <summary>
        /// library の場所は config.json の DatabasePath で動かせる（ポータブル版・別ドライブ）。
        /// 既定 → config.json → 渡されたパス自体が library か games、の順に見て最初に在るものを返す。
        /// </summary>
        public static string FindGamesDir(string playniteRoot, out List<string> tried)
        {
            tried = GamesDirCandidates(playniteRoot);
            foreach (var candidate in tried)
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
            return ScanForGamesDir(playniteRoot, 3) ?? ScanForGamesDir(DefaultRoot(), 3);
        }

        internal static List<string> GamesDirCandidates(string playniteRoot)
        {
            var candidates = new List<string>();
            Action<string> add = path =>
            {
                if (!string.IsNullOrWhiteSpace(path)
                    && !candidates.Any(c => string.Equals(c, path, StringComparison.OrdinalIgnoreCase)))
                {
                    candidates.Add(path);
                }
            };

            add(GamesDir(playniteRoot));
            // --playnite に library や games を直接渡された場合
            add(Path.Combine(playniteRoot, "games"));
            if (string.Equals(Path.GetFileName(playniteRoot.TrimEnd(Path.DirectorySeparatorChar)),
                    "games", StringComparison.OrdinalIgnoreCase))
            {
                add(playniteRoot);
            }

            // config.json は渡されたフォルダだけでなく既定の場所も見る。
            // --playnite に library を直接渡されると設定を読み損ねるため。
            foreach (var root in ConfigRoots(playniteRoot))
            {
                var configured = ReadConfiguredDatabasePath(root);
                if (configured != null)
                {
                    add(Path.Combine(ExpandPath(configured, root), "games"));
                }
            }
            return candidates;
        }

        private static IEnumerable<string> ConfigRoots(string playniteRoot)
        {
            yield return playniteRoot;
            var parent = Path.GetDirectoryName(playniteRoot.TrimEnd(Path.DirectorySeparatorChar));
            if (!string.IsNullOrEmpty(parent))
            {
                yield return parent;
            }
            yield return DefaultRoot();
        }

        /// <summary>
        /// それでも見つからないとき用。games という名前で、隣に platforms などが並んでいる
        /// フォルダを浅く探す。Playnite のライブラリはこの形しかない。
        /// </summary>
        internal static string ScanForGamesDir(string root, int maxDepth)
        {
            if (!Directory.Exists(root))
            {
                return null;
            }
            var level = new List<string> { root };
            for (var depth = 0; depth <= maxDepth && level.Count > 0; depth++)
            {
                var next = new List<string>();
                foreach (var dir in level)
                {
                    string[] children;
                    try
                    {
                        children = Directory.GetDirectories(dir);
                    }
                    catch (IOException) { continue; }
                    catch (UnauthorizedAccessException) { continue; }

                    foreach (var child in children)
                    {
                        if (LooksLikeGamesDir(child))
                        {
                            return child;
                        }
                        next.Add(child);
                    }
                }
                level = next;
            }
            return null;
        }

        private static readonly string[] siblingDirNames =
            { "platforms", "emulators", "genres", "tags", "companies", "filterpresets" };

        private static bool LooksLikeGamesDir(string path)
        {
            if (!string.Equals(Path.GetFileName(path), "games", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            try
            {
                if (Directory.GetFiles(path, "*.json").Length == 0)
                {
                    return false;
                }
                var parent = Path.GetDirectoryName(path);
                return parent != null
                    && siblingDirNames.Any(name => Directory.Exists(Path.Combine(parent, name)));
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        internal static string ReadConfiguredDatabasePath(string playniteRoot)
        {
            var configPath = Path.Combine(playniteRoot, "config.json");
            if (!File.Exists(configPath))
            {
                return null;
            }
            try
            {
                var value = (string)Json.Parse(File.ReadAllText(configPath))["DatabasePath"];
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>%AppData% などの環境変数と {PlayniteDir}、それに相対パスを解く。</summary>
        internal static string ExpandPath(string raw, string playniteRoot)
        {
            var path = Environment.ExpandEnvironmentVariables(raw.Trim())
                .Replace("{PlayniteDir}", playniteRoot);
            return Path.IsPathRooted(path) ? path : Path.Combine(playniteRoot, path);
        }

        internal static PlayniteGame ParseGame(JObject json, string filePath)
        {
            Guid id;
            if (!Guid.TryParse((string)json["Id"], out id))
            {
                return null;
            }

            var game = new PlayniteGame
            {
                Id = id,
                Name = (string)json["Name"] ?? string.Empty,
                InstallDirectory = (string)json["InstallDirectory"] ?? string.Empty,
                Playtime = (ulong?)json["Playtime"] ?? 0,
                PlayCount = (ulong?)json["PlayCount"] ?? 0,
                FilePath = filePath,
            };

            var last = json["LastActivity"];
            if (last != null && last.Type != JTokenType.Null)
            {
                DateTime parsed;
                if (DateTime.TryParse((string)last, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out parsed))
                {
                    game.LastActivity = parsed;
                }
            }

            var actions = json["GameActions"] as JArray;
            if (actions != null)
            {
                foreach (var action in actions.OfType<JObject>())
                {
                    var path = (string)action["Path"];
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        game.ActionPaths.Add(path);
                    }
                    // LE 経由だと Path は LEProc.exe で、本物は引数側にいる
                    foreach (var exe in PathUtil.ExtractExecutables((string)action["Arguments"]))
                    {
                        game.ActionPaths.Add(exe);
                    }
                }
            }
            return game;
        }

        public static List<PlayniteGame> Load(string gamesDir)
        {
            var games = new List<PlayniteGame>();
            foreach (var file in Directory.GetFiles(gamesDir, "*.json"))
            {
                JObject json;
                try
                {
                    json = Json.Parse(File.ReadAllText(file));
                }
                catch (JsonException)
                {
                    continue;
                }
                var game = ParseGame(json, file);
                if (game != null)
                {
                    games.Add(game);
                }
            }
            return games;
        }

        /// <summary>書き換える前に games フォルダをまるごと zip に取る。</summary>
        public static string Backup(string gamesDir, string outputDir, string stamp)
        {
            Directory.CreateDirectory(outputDir);
            var zipPath = Path.Combine(outputDir, $"playnite-games-{stamp}.zip");
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            ZipFile.CreateFromDirectory(gamesDir, zipPath);
            return zipPath;
        }

        /// <summary>Playtime と、必要なら最終プレイ日時・回数を書き戻す。</summary>
        internal static string Patch(string original, PlanEntry entry)
        {
            var json = Json.Parse(original);
            json["Playtime"] = entry.NewPlaytime;

            if (entry.SessionCount > 0)
            {
                var current = (ulong?)json["PlayCount"] ?? 0;
                if (current < (ulong)entry.SessionCount)
                {
                    json["PlayCount"] = (ulong)entry.SessionCount;
                }
            }

            if (entry.LastSession != null)
            {
                var existing = json["LastActivity"];
                DateTime parsed;
                var newer = existing == null || existing.Type == JTokenType.Null
                    || !DateTime.TryParse((string)existing, null,
                            System.Globalization.DateTimeStyles.RoundtripKind, out parsed)
                    || parsed < entry.LastSession.Value;
                if (newer)
                {
                    json["LastActivity"] = entry.LastSession.Value.ToUniversalTime()
                        .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                }
            }

            return json.ToString(Formatting.Indented);
        }

        public static void Write(PlanEntry entry)
        {
            var path = entry.Playnite.FilePath;
            File.WriteAllText(path, Patch(File.ReadAllText(path), entry));
        }
    }
}
