using System;

namespace GalCompanion
{
    internal static class PlaytimeApplier
    {
        /// <summary>
        /// セッションの合計と Playnite の現在値の大きいほうを採る。
        /// Steam などから取り込んだ、セッションの裏付けが無い時間を消さないため。
        /// </summary>
        public static ulong Resolve(ulong current, long sessionTotal)
        {
            if (sessionTotal <= 0)
            {
                return current;
            }
            var total = (ulong)sessionTotal;
            return total > current ? total : current;
        }

        public static bool NeedsUpdate(ulong current, long sessionTotal)
        {
            return Resolve(current, sessionTotal) != current;
        }
    }
}
