using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LunaImport
{
    /// <summary>
    /// GameActivity 拡張のデータを書く。
    /// ExtensionsData\&lt;拡張の GUID&gt;\GameActivity\&lt;ゲームの GUID&gt;.json に
    /// { Id, Name, Items:[ {DateSession, ElapsedSeconds, ...} ] } の形で入っている。
    /// </summary>
    internal static class GameActivityWriter
    {
        /// <summary>GUID は決め打ちにせず、GameActivity という子フォルダを持つ拡張データを探す。</summary>
        public static string FindDataDir(string playniteRoot)
        {
            var extensionsData = Path.Combine(playniteRoot, "ExtensionsData");
            if (!Directory.Exists(extensionsData))
            {
                return null;
            }
            foreach (var dir in Directory.GetDirectories(extensionsData))
            {
                var candidate = Path.Combine(dir, "GameActivity");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        internal static JObject BuildActivity(LunaSession session)
        {
            return new JObject
            {
                ["SourceID"] = Guid.Empty.ToString(),
                ["PlatformIDs"] = new JArray(),
                ["GameActionName"] = "LunaTranslator",
                ["IdConfiguration"] = -1,
                ["DateSession"] = session.Start.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                ["ElapsedSeconds"] = session.Seconds,
            };
        }

        /// <summary>
        /// 既存ファイルがあれば Items に足す。同じ開始時刻のセッションは二重登録しない
        /// （二度流しても増えないように）。
        /// </summary>
        internal static string Merge(string existingJson, PlanEntry entry)
        {
            var root = string.IsNullOrWhiteSpace(existingJson)
                ? new JObject()
                : Json.Parse(existingJson);

            root["Id"] = entry.Playnite.Id.ToString();
            if (string.IsNullOrWhiteSpace((string)root["Name"]))
            {
                root["Name"] = entry.Playnite.Name;
            }

            var items = root["Items"] as JArray;
            if (items == null)
            {
                items = new JArray();
                root["Items"] = items;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items.OfType<JObject>())
            {
                var stamp = (string)item["DateSession"];
                if (!string.IsNullOrEmpty(stamp))
                {
                    seen.Add(NormalizeStamp(stamp));
                }
            }

            foreach (var session in entry.Luna.Sessions.OrderBy(s => s.Start))
            {
                var activity = BuildActivity(session);
                if (seen.Add(NormalizeStamp((string)activity["DateSession"])))
                {
                    items.Add(activity);
                }
            }

            return root.ToString(Formatting.Indented);
        }

        // 秒までで比べる。ミリ秒の丸めの違いで重複扱いを外さないため
        private static string NormalizeStamp(string stamp)
        {
            DateTime parsed;
            if (DateTime.TryParse(stamp, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out parsed))
            {
                return parsed.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss");
            }
            return stamp ?? string.Empty;
        }

        public static void Write(string dataDir, PlanEntry entry)
        {
            var path = Path.Combine(dataDir, entry.Playnite.Id.ToString() + ".json");
            var existing = File.Exists(path) ? File.ReadAllText(path) : null;
            File.WriteAllText(path, Merge(existing, entry));
        }

        public static string Backup(string dataDir, string outputDir, string stamp)
        {
            Directory.CreateDirectory(outputDir);
            var zipPath = Path.Combine(outputDir, $"gameactivity-{stamp}.zip");
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            System.IO.Compression.ZipFile.CreateFromDirectory(dataDir, zipPath);
            return zipPath;
        }
    }
}
