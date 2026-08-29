using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LunaImport
{
    /// <summary>
    /// LunaTranslator の userconfig を読む。
    /// - savegamedata_*.json : [ [uid,...], { uid: {gamepath, title, ...} }, ... ]
    /// - savegame.db         : gameinternalid_v2(gameinternalid, gameuid)
    ///                         trace_strict(gameinternalid, timestart, timestop)
    /// </summary>
    internal static class LunaReader
    {
        public static string FindUserConfig(string lunaRoot)
        {
            if (string.IsNullOrWhiteSpace(lunaRoot))
            {
                return null;
            }
            // ルートを渡されても userconfig 自体を渡されてもいいようにする
            var direct = Path.Combine(lunaRoot, "userconfig");
            if (Directory.Exists(direct))
            {
                return direct;
            }
            return Directory.Exists(lunaRoot) ? lunaRoot : null;
        }

        /// <summary>バージョンつきのファイル名なので、いちばん新しいものを選ぶ。</summary>
        internal static string PickGameListFile(IEnumerable<string> fileNames)
        {
            var news = fileNames.Where(f => f.StartsWith("savegamedata_", StringComparison.OrdinalIgnoreCase)
                                            && f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                .ToList();
            if (news.Count > 0)
            {
                return news[news.Count - 1];
            }

            var legacy = fileNames.Where(f => f.StartsWith("savehook_new_", StringComparison.OrdinalIgnoreCase)
                                              && f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                                  .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                  .ToList();
            return legacy.Count > 0 ? legacy[legacy.Count - 1] : null;
        }

        /// <summary>ゲーム一覧の JSON から uid → ゲーム を作る。旧形式はキーがパスなので uid を持たない。</summary>
        internal static Dictionary<string, LunaGame> ParseGameList(string json)
        {
            var games = new Dictionary<string, LunaGame>(StringComparer.Ordinal);
            var root = JToken.Parse(json);
            if (root.Type != JTokenType.Array || root.Count() < 2)
            {
                return games;
            }

            var data = root[1] as JObject;
            if (data == null)
            {
                return games;
            }

            foreach (var pair in data)
            {
                var entry = pair.Value as JObject;
                if (entry == null)
                {
                    continue;
                }

                // 新形式はキーが uid で gamepath は中にある。旧形式はキーがそのままパス
                var gamePath = (string)entry["gamepath"] ?? pair.Key;
                games[pair.Key] = new LunaGame
                {
                    Uid = pair.Key,
                    GamePath = gamePath,
                    Title = (string)entry["title"],
                };
            }
            return games;
        }

        /// <summary>db から uid ごとのセッションを読む。開始 ≥ 終了 の壊れた行は Luna 自身も捨てている。</summary>
        internal static Dictionary<string, List<PlaySession>> ReadSessions(Sqlite db)
        {
            var byUid = new Dictionary<string, List<PlaySession>>(StringComparer.Ordinal);
            var rows = db.Query(
                "SELECT gameinternalid_v2.gameuid, trace_strict.timestart, trace_strict.timestop "
                + "FROM gameinternalid_v2 "
                + "JOIN trace_strict ON gameinternalid_v2.gameinternalid = trace_strict.gameinternalid");

            foreach (var row in rows)
            {
                var uid = row[0] as string;
                if (string.IsNullOrEmpty(uid))
                {
                    continue;
                }
                var start = ToDouble(row[1]);
                var stop = ToDouble(row[2]);
                if (start == null || stop == null || stop.Value <= start.Value)
                {
                    continue;
                }

                List<PlaySession> list;
                if (!byUid.TryGetValue(uid, out list))
                {
                    list = new List<PlaySession>();
                    byUid[uid] = list;
                }
                list.Add(new PlaySession
                {
                    Start = FromUnix(start.Value),
                    End = FromUnix(stop.Value),
                });
            }
            return byUid;
        }

        private static double? ToDouble(object value)
        {
            if (value is double d) return d;
            if (value is long l) return l;
            if (value is string s)
            {
                double parsed;
                if (double.TryParse(s, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out parsed))
                {
                    return parsed;
                }
            }
            return null;
        }

        // Luna は time.time() をそのまま入れているので UTC 秒。表示は現地時間で見たいのでローカルに直す
        internal static DateTime FromUnix(double seconds)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddSeconds(seconds)
                .ToLocalTime();
        }

        public static List<LunaGame> Load(string userConfigDir)
        {
            var listFile = PickGameListFile(
                Directory.GetFiles(userConfigDir).Select(Path.GetFileName));
            if (listFile == null)
            {
                throw new InvalidOperationException(
                    $"{userConfigDir} に savegamedata_*.json が見つかりません");
            }

            var games = ParseGameList(File.ReadAllText(Path.Combine(userConfigDir, listFile)));

            var dbPath = Path.Combine(userConfigDir, "savegame.db");
            if (!File.Exists(dbPath))
            {
                throw new InvalidOperationException($"{dbPath} が見つかりません");
            }

            using (var db = new Sqlite(dbPath))
            {
                foreach (var pair in ReadSessions(db))
                {
                    LunaGame game;
                    if (games.TryGetValue(pair.Key, out game))
                    {
                        game.Sessions.AddRange(pair.Value);
                    }
                }
            }

            return games.Values.OrderByDescending(g => g.TotalSeconds).ToList();
        }
    }
}
