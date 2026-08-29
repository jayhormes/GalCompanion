using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace GalCompanion
{
    /// <summary>
    /// 遊んだ記録の読み書き。JSON ではなく 1 行 1 セッションのタブ区切りにしてある：
    /// 終了のたびに 1 行足すだけで済み、二台ぶんの合流も行の和集合で終わるため。
    /// </summary>
    internal static class SessionLog
    {
        public const string Header = "#galcompanion-sessions\t1";

        public static string FormatLine(PlaySession session)
        {
            return string.Join("\t",
                session.GameId.ToString("D"),
                session.StartUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                session.Seconds.ToString(CultureInfo.InvariantCulture),
                Clean(session.Device),
                Clean(session.GameName));
        }

        /// <summary>壊れた行は捨てる。同期で切れたファイルが来ても全部落とさないため。</summary>
        public static PlaySession ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
            {
                return null;
            }

            var parts = line.Split('\t');
            if (parts.Length < 3)
            {
                return null;
            }

            Guid gameId;
            DateTime start;
            int seconds;
            if (!Guid.TryParse(parts[0], out gameId)
                || !DateTime.TryParse(parts[1], CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out start)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
            {
                return null;
            }
            if (seconds < 0)
            {
                return null;
            }

            return new PlaySession
            {
                GameId = gameId,
                StartUtc = Truncate(DateTime.SpecifyKind(start, DateTimeKind.Utc)),
                Seconds = seconds,
                Device = parts.Length > 3 ? parts[3] : string.Empty,
                GameName = parts.Length > 4 ? parts[4] : string.Empty,
            };
        }

        public static List<PlaySession> Parse(string text)
        {
            var sessions = new List<PlaySession>();
            if (string.IsNullOrEmpty(text))
            {
                return sessions;
            }
            foreach (var line in text.Split('\n'))
            {
                var session = ParseLine(line.TrimEnd('\r'));
                if (session != null)
                {
                    sessions.Add(session);
                }
            }
            return sessions;
        }

        public static string Serialize(IEnumerable<PlaySession> sessions)
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            foreach (var session in sessions.OrderBy(s => s.StartUtc).ThenBy(s => s.GameId))
            {
                sb.Append(FormatLine(session)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// 二台ぶんの記録を合流させる。累計と違って和集合で正しく合流できるのがこの形式の要点。
        /// 同じセッションが両方にあれば長いほうを採る（切れた側が短く記録されることがある）。
        /// </summary>
        public static List<PlaySession> Merge(
            IEnumerable<PlaySession> first, IEnumerable<PlaySession> second)
        {
            var byKey = new Dictionary<string, PlaySession>(StringComparer.Ordinal);
            foreach (var session in Concat(first, second))
            {
                PlaySession existing;
                if (!byKey.TryGetValue(session.Key, out existing))
                {
                    byKey[session.Key] = session;
                    continue;
                }
                if (session.Seconds > existing.Seconds)
                {
                    byKey[session.Key] = session;
                }
            }
            return byKey.Values.OrderBy(s => s.StartUtc).ThenBy(s => s.GameId).ToList();
        }

        public static Dictionary<Guid, long> TotalsByGame(IEnumerable<PlaySession> sessions)
        {
            var totals = new Dictionary<Guid, long>();
            foreach (var session in sessions)
            {
                long current;
                totals.TryGetValue(session.GameId, out current);
                totals[session.GameId] = current + session.Seconds;
            }
            return totals;
        }

        /// <summary>
        /// 日ごとの合計（現地時間）。日をまたいだセッションはまたいだ側に切って配る。
        /// カレンダー表示で「日付が 1 日ずれる」のを避けるため。
        /// </summary>
        public static Dictionary<DateTime, long> DailyTotals(IEnumerable<PlaySession> sessions)
        {
            var totals = new Dictionary<DateTime, long>();
            foreach (var session in sessions)
            {
                var cursor = session.StartUtc.ToLocalTime();
                var end = session.EndUtc.ToLocalTime();
                while (cursor < end)
                {
                    var dayEnd = cursor.Date.AddDays(1);
                    var slice = (end < dayEnd ? end : dayEnd) - cursor;
                    var seconds = (long)Math.Round(slice.TotalSeconds);
                    if (seconds > 0)
                    {
                        long current;
                        totals.TryGetValue(cursor.Date, out current);
                        totals[cursor.Date] = current + seconds;
                    }
                    cursor = dayEnd;
                }

                if (session.Seconds == 0)
                {
                    // 0 秒でも「その日遊んだ」痕跡は残す
                    var day = session.StartUtc.ToLocalTime().Date;
                    if (!totals.ContainsKey(day))
                    {
                        totals[day] = 0;
                    }
                }
            }
            return totals;
        }

        public static DateTime Truncate(DateTime value)
        {
            return new DateTime(
                value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), value.Kind);
        }

        private static IEnumerable<PlaySession> Concat(
            IEnumerable<PlaySession> first, IEnumerable<PlaySession> second)
        {
            if (first != null)
            {
                foreach (var session in first)
                {
                    yield return session;
                }
            }
            if (second != null)
            {
                foreach (var session in second)
                {
                    yield return session;
                }
            }
        }

        // 区切り文字が入ると行が壊れる
        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }
}
