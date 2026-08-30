using System;
using System.Collections.Generic;

namespace GalCompanion
{
    /// <summary>LunaTranslator の 1 回の起動から終了まで。</summary>
    internal sealed class LunaSession
    {
        public DateTime Start;
        public DateTime End;

        public long Seconds => (long)Math.Round((End - Start).TotalSeconds);
    }

    internal sealed class LunaGame
    {
        public string Uid;
        public string GamePath;
        public string Title;
        public List<LunaSession> Sessions = new List<LunaSession>();

        public long TotalSeconds
        {
            get
            {
                long total = 0;
                foreach (var s in Sessions)
                {
                    total += s.Seconds;
                }
                return total;
            }
        }

        public string DisplayName =>
            !string.IsNullOrWhiteSpace(Title) ? Title.Trim() : PathUtil.FileName(GamePath);
    }

    internal sealed class PlayniteGame
    {
        public Guid Id;
        public string Name;
        public string InstallDirectory;
        public ulong Playtime;
        public ulong PlayCount;
        public DateTime? LastActivity;

        /// <summary>プレイアクションの Path と、引数に混ざっている exe。LE 経由だと後者にしか出ない。</summary>
        public List<string> ActionPaths = new List<string>();
    }

    internal enum MatchKind
    {
        None,
        Path,
        Title,
    }

    internal sealed class MatchResult
    {
        public LunaGame Luna;
        public PlayniteGame Playnite;
        public MatchKind Kind;
    }
}
