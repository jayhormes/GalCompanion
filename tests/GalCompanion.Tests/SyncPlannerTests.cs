using System;
using Xunit;

namespace GalCompanion.Tests
{
    public class SyncPlannerTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 8, 28, 12, 0, 0);
        private static readonly TimeSpan Tol = TimeSpan.FromSeconds(2);

        private static DateTime Min(int minutes) => T0.AddMinutes(minutes);

        [Fact]
        public void Nothing_anywhere_is_none()
        {
            Assert.Equal(SyncAction.None, SyncPlanner.Decide(null, null, null, Tol));
        }

        [Fact]
        public void Only_remote_pulls()
        {
            Assert.Equal(SyncAction.Pull, SyncPlanner.Decide(null, Min(0), null, Tol));
        }

        [Fact]
        public void Only_local_pushes()
        {
            Assert.Equal(SyncAction.Push, SyncPlanner.Decide(Min(0), null, null, Tol));
        }

        [Fact]
        public void Never_synced_with_close_timestamps_is_none()
        {
            Assert.Equal(SyncAction.None,
                SyncPlanner.Decide(Min(0), Min(0).AddSeconds(1), null, Tol));
        }

        [Fact]
        public void Never_synced_with_diverged_timestamps_is_conflict()
        {
            Assert.Equal(SyncAction.Conflict,
                SyncPlanner.Decide(Min(0), Min(30), null, Tol));
        }

        [Fact]
        public void Local_changed_only_pushes()
        {
            Assert.Equal(SyncAction.Push,
                SyncPlanner.Decide(Min(10), Min(0), Min(0), Tol));
        }

        [Fact]
        public void Remote_changed_only_pulls()
        {
            // 在另一台玩過並推上去：遠端比共同祖先新，本地沒動
            Assert.Equal(SyncAction.Pull,
                SyncPlanner.Decide(Min(0), Min(10), Min(0), Tol));
        }

        [Fact]
        public void Both_changed_is_conflict()
        {
            Assert.Equal(SyncAction.Conflict,
                SyncPlanner.Decide(Min(10), Min(20), Min(0), Tol));
        }

        [Fact]
        public void Neither_changed_is_none()
        {
            Assert.Equal(SyncAction.None,
                SyncPlanner.Decide(Min(0), Min(0), Min(0), Tol));
        }

        [Fact]
        public void Change_within_tolerance_does_not_count()
        {
            // mtime 與祖先差 1 秒（< 2 秒容差）視為沒變，不觸發 push
            Assert.Equal(SyncAction.None,
                SyncPlanner.Decide(Min(0).AddSeconds(1), Min(0), Min(0), Tol));
        }

        [Fact]
        public void Change_just_past_tolerance_counts()
        {
            Assert.Equal(SyncAction.Push,
                SyncPlanner.Decide(Min(0).AddSeconds(3), Min(0), Min(0), Tol));
        }
    }
}
