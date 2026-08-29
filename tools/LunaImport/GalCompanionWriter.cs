using GalCompanion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LunaImport
{
    /// <summary>
    /// GalCompanion の遊玩記録（sessions.tsv）へ流し込む。
    /// プラグイン側と同じ書式・同じ合流規則を使うので、二度流しても増えない。
    /// </summary>
    internal static class GalCompanionWriter
    {
        public const string PluginId = "80cdee03-e216-4df2-b247-a56056f61543";
        public const string Device = "LunaTranslator";

        public static string DataDir(string playniteRoot)
        {
            return Path.Combine(playniteRoot, "ExtensionsData", PluginId);
        }

        public static string LogPath(string playniteRoot)
        {
            return Path.Combine(DataDir(playniteRoot), "sessions.tsv");
        }

        internal static List<PlaySession> Convert(PlanEntry entry)
        {
            var sessions = new List<PlaySession>();
            foreach (var session in entry.Luna.Sessions)
            {
                var seconds = (int)Math.Min(session.Seconds, int.MaxValue);
                if (seconds <= 0)
                {
                    continue;
                }
                sessions.Add(new PlaySession
                {
                    GameId = entry.Playnite.Id,
                    StartUtc = SessionLog.Truncate(session.Start.ToUniversalTime()),
                    Seconds = seconds,
                    Device = Device,
                    GameName = entry.Playnite.Name,
                });
            }
            return sessions;
        }

        /// <summary>既存の記録と合流させた全文を返す。</summary>
        internal static string Merge(string existing, IEnumerable<PlanEntry> entries)
        {
            var incoming = new List<PlaySession>();
            foreach (var entry in entries)
            {
                incoming.AddRange(Convert(entry));
            }
            return SessionLog.Serialize(SessionLog.Merge(SessionLog.Parse(existing), incoming));
        }

        public static int Write(string playniteRoot, IEnumerable<PlanEntry> entries)
        {
            var path = LogPath(playniteRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var existing = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            var before = SessionLog.Parse(existing).Count;
            var merged = Merge(existing, entries);

            var temp = path + ".tmp";
            File.WriteAllText(temp, merged, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temp, path);

            return SessionLog.Parse(merged).Count - before;
        }

        public static string Backup(string playniteRoot, string outputDir, string stamp)
        {
            var path = LogPath(playniteRoot);
            if (!File.Exists(path))
            {
                return null;
            }
            Directory.CreateDirectory(outputDir);
            var copy = Path.Combine(outputDir, $"galcompanion-sessions-{stamp}.tsv");
            File.Copy(path, copy, true);
            return copy;
        }
    }
}
