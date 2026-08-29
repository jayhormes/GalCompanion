using System;
using System.Collections.Generic;

namespace LunaImport
{
    internal enum PlanAction
    {
        /// <summary>Playnite 側に対応するゲームが無い。</summary>
        Unmatched,

        /// <summary>Luna 側に記録が無い。</summary>
        NoSessions,

        /// <summary>Playnite に既に時間が入っていて、上書き指定も無い。</summary>
        KeepExisting,

        Write,
    }

    internal sealed class PlanEntry
    {
        public LunaGame Luna;
        public PlayniteGame Playnite;
        public MatchKind Kind;
        public PlanAction Action;

        public long LunaSeconds;
        public ulong CurrentPlaytime;
        public ulong NewPlaytime;
        public int SessionCount;
        public DateTime? FirstSession;
        public DateTime? LastSession;
    }

    internal static class ImportPlan
    {
        /// <summary>
        /// 何をどう書くかを先に全部決める。実際に書く前にこれを表で見せて確認できるようにするため。
        /// </summary>
        public static List<PlanEntry> Build(List<MatchResult> matches, bool overwrite)
        {
            var plan = new List<PlanEntry>();
            foreach (var match in matches)
            {
                var entry = new PlanEntry
                {
                    Luna = match.Luna,
                    Playnite = match.Playnite,
                    Kind = match.Kind,
                    LunaSeconds = match.Luna.TotalSeconds,
                    SessionCount = match.Luna.Sessions.Count,
                };

                foreach (var session in match.Luna.Sessions)
                {
                    if (entry.FirstSession == null || session.Start < entry.FirstSession.Value)
                    {
                        entry.FirstSession = session.Start;
                    }
                    if (entry.LastSession == null || session.End > entry.LastSession.Value)
                    {
                        entry.LastSession = session.End;
                    }
                }

                if (match.Playnite == null)
                {
                    entry.Action = PlanAction.Unmatched;
                    plan.Add(entry);
                    continue;
                }

                entry.CurrentPlaytime = match.Playnite.Playtime;
                if (entry.LunaSeconds <= 0)
                {
                    entry.Action = PlanAction.NoSessions;
                    entry.NewPlaytime = entry.CurrentPlaytime;
                    plan.Add(entry);
                    continue;
                }

                // 既に Playnite で遊んだ記録があるものを黙って潰さない
                if (entry.CurrentPlaytime > 0 && !overwrite)
                {
                    entry.Action = PlanAction.KeepExisting;
                    entry.NewPlaytime = entry.CurrentPlaytime;
                    plan.Add(entry);
                    continue;
                }

                entry.Action = PlanAction.Write;
                entry.NewPlaytime = (ulong)entry.LunaSeconds;
                plan.Add(entry);
            }
            return plan;
        }
    }
}
