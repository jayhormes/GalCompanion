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
                    json = JObject.Parse(File.ReadAllText(file));
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
            var json = JObject.Parse(original);
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
