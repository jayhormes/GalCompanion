using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GalCompanion
{
    /// <summary>
    /// LunaTranslator の遊玩時間を取り込む。
    /// ライブラリの読み書きは Playnite の API に任せる —— 保存形式はバージョンで変わるので、
    /// library フォルダを直接触ると版が上がった途端に壊れる。
    /// </summary>
    internal sealed class LunaImportService
    {
        private readonly SessionStore store;

        public LunaImportService(SessionStore store)
        {
            this.store = store;
        }

        /// <summary>Playnite のゲームから照合用の情報だけ抜く。SDK 型をこの先に持ち込まないため。</summary>
        public static PlayniteGame Describe(
            Guid id, string name, string installDirectory,
            ulong playtime, ulong playCount, DateTime? lastActivity,
            IEnumerable<KeyValuePair<string, string>> playActions)
        {
            var game = new PlayniteGame
            {
                Id = id,
                Name = name ?? string.Empty,
                InstallDirectory = installDirectory ?? string.Empty,
                Playtime = playtime,
                PlayCount = playCount,
                LastActivity = lastActivity,
            };

            if (playActions != null)
            {
                foreach (var action in playActions)
                {
                    if (!string.IsNullOrWhiteSpace(action.Key))
                    {
                        game.ActionPaths.Add(action.Key);
                    }
                    // LE 経由だと Path は LEProc.exe で、本物は引数側にいる
                    foreach (var exe in PathUtil.ExtractExecutables(action.Value))
                    {
                        game.ActionPaths.Add(exe);
                    }
                }
            }
            return game;
        }

        public static List<PlanEntry> Plan(string lunaRoot, List<PlayniteGame> library, bool overwrite)
        {
            var userConfig = LunaReader.FindUserConfig(lunaRoot);
            if (userConfig == null)
            {
                throw new InvalidOperationException(
                    $"找不到 LunaTranslator 的 userconfig：{lunaRoot}");
            }
            return ImportPlan.Build(Matcher.Match(LunaReader.Load(userConfig), library), overwrite);
        }

        /// <summary>
        /// 書き込む前の控え。Playnite 側は API 経由なので壊しようがないが、
        /// sessions.tsv はこちらが全文を置き換えるので必ず取る。
        /// </summary>
        public string BackupSessions(string outputDir, string stamp)
        {
            if (!File.Exists(store.Path_))
            {
                return null;
            }
            Directory.CreateDirectory(outputDir);
            var copy = Path.Combine(outputDir, $"galcompanion-sessions-{stamp}.tsv");
            File.Copy(store.Path_, copy, true);
            return copy;
        }

        internal static List<PlaySession> ToSessions(PlanEntry entry)
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
                    Device = LunaDevice,
                    GameName = entry.Playnite.Name,
                });
            }
            return sessions;
        }

        public const string LunaDevice = "LunaTranslator";

        /// <summary>
        /// 逐次記録を流し込んで、増えた行数を返す。
        /// 二度流しても増えないのは SessionLog.Merge が「ゲーム＋開始時刻」で潰すため。
        /// </summary>
        public int WriteSessions(IEnumerable<PlanEntry> entries)
        {
            var incoming = new List<PlaySession>();
            foreach (var entry in entries)
            {
                incoming.AddRange(ToSessions(entry));
            }

            var existing = store.Load();
            var merged = SessionLog.Merge(existing, incoming);
            if (merged.Count == existing.Count)
            {
                return 0;
            }
            store.ReplaceAll(merged);
            return merged.Count - existing.Count;
        }

        /// <summary>報告用の 1 行。</summary>
        public static string Describe(PlanEntry entry)
        {
            var matched = entry.Playnite == null
                ? "—"
                : $"→ {entry.Playnite.Name} [{(entry.Kind == MatchKind.Path ? "路徑" : "標題")}]";
            var current = entry.Playnite == null
                ? string.Empty
                : $"（目前 {FormatHours((long)entry.CurrentPlaytime)}）";
            return $"{entry.Luna.DisplayName}　{FormatHours(entry.LunaSeconds)} / {entry.SessionCount} 次　{matched} {current}".TrimEnd();
        }

        public static string Describe(PlanAction action)
        {
            switch (action)
            {
                case PlanAction.Unmatched: return "Playnite 找不到對應的遊戲（略過）";
                case PlanAction.NoSessions: return "Luna 沒有遊玩紀錄（略過）";
                case PlanAction.KeepExisting: return "Playnite 已有時數（略過，勾「覆蓋」才會寫）";
                default: return "會寫入";
            }
        }

        public static string FormatHours(long seconds)
        {
            if (seconds <= 0)
            {
                return "0h";
            }
            var hours = seconds / 3600.0;
            return hours < 1
                ? $"{seconds / 60}m"
                : $"{hours.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}h";
        }
    }
}
