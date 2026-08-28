using System;

namespace GalCompanion
{
    internal enum SyncAction
    {
        None,
        Pull,
        Push,
        Conflict
    }

    // Phase 3 存檔同步的決策核心。時間戳語意：
    //   localMtime  本地存檔最新修改時間（null = 本地沒有存檔）
    //   remoteMtime NAS manifest 記的時間戳（null = 遠端沒有）
    //   lastSynced  本機上次成功 push/pull 後記下的共同祖先時間戳（null = 從未同步）
    // Conflict 一律不自動覆蓋，交給使用者選。
    internal static class SyncPlanner
    {
        public static SyncAction Decide(DateTime? localMtime, DateTime? remoteMtime, DateTime? lastSynced, TimeSpan tolerance)
        {
            if (localMtime == null && remoteMtime == null)
            {
                return SyncAction.None;
            }
            if (localMtime == null)
            {
                return SyncAction.Pull;
            }
            if (remoteMtime == null)
            {
                return SyncAction.Push;
            }

            if (lastSynced == null)
            {
                // 從未同步但兩邊都有存檔：時間戳接近視為同一份，否則不能自動選邊
                return Close(localMtime.Value, remoteMtime.Value, tolerance)
                    ? SyncAction.None
                    : SyncAction.Conflict;
            }

            var localChanged = localMtime.Value > lastSynced.Value + tolerance;
            var remoteChanged = remoteMtime.Value > lastSynced.Value + tolerance;

            if (localChanged && remoteChanged)
            {
                return Close(localMtime.Value, remoteMtime.Value, tolerance)
                    ? SyncAction.None
                    : SyncAction.Conflict;
            }
            if (localChanged)
            {
                return SyncAction.Push;
            }
            if (remoteChanged)
            {
                return SyncAction.Pull;
            }
            return SyncAction.None;
        }

        private static bool Close(DateTime a, DateTime b, TimeSpan tolerance)
        {
            return (a > b ? a - b : b - a) <= tolerance;
        }
    }
}
