using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace GalCompanion.Tests
{
    public class PlaytimeSyncTests
    {
        private static readonly Guid A = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private const string Remote = "nas:playnite";

        private static PlaySession Session(string startUtc, int seconds, string device)
        {
            return new PlaySession
            {
                GameId = A,
                StartUtc = DateTime.Parse(startUtc, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal
                    | System.Globalization.DateTimeStyles.AssumeUniversal),
                Seconds = seconds,
                Device = device,
                GameName = "モザイクの天使",
            };
        }

        private static PlaytimeSyncService Sync(
            FakeRcloneRunner rclone, TempDir dir, string device, out SessionStore store)
        {
            store = new SessionStore(dir.Path);
            return new PlaytimeSyncService(rclone, store, Remote, device, Path.Combine(dir.Path, "work"));
        }

        [Theory]
        [InlineData("JAY-PC", "jay-pc")]
        [InlineData("ROG Ally", "rogally")]
        [InlineData("桌機", "device")]
        [InlineData("", "device")]
        [InlineData(null, "device")]
        public void Device_names_become_safe_file_names(string device, string expected)
        {
            Assert.Equal(expected, PlaytimeSyncService.SafeFileName(device));
        }

        [Fact]
        public void Each_machine_writes_only_its_own_file()
        {
            var rclone = new FakeRcloneRunner();
            using (var dir = new TempDir())
            {
                SessionStore store;
                var sync = Sync(rclone, dir, "JAY-PC", out store);
                store.Append(Session("2026-08-30T01:00:00Z", 60, "JAY-PC"));

                sync.Push();

                Assert.Equal("nas:playnite/playtime/jay-pc.tsv", sync.RemoteFile);
                Assert.True(rclone.Files.ContainsKey(sync.RemoteFile));
            }
        }

        [Fact]
        public void Pull_merges_every_machines_file()
        {
            var rclone = new FakeRcloneRunner();
            rclone.Files["nas:playnite/playtime/rogally.tsv"] = Encoding.UTF8.GetBytes(
                SessionLog.Serialize(new[] { Session("2026-08-31T01:00:00Z", 120, "ROGALLY") }));

            using (var dir = new TempDir())
            {
                SessionStore store;
                var sync = Sync(rclone, dir, "JAY-PC", out store);
                store.Append(Session("2026-08-30T01:00:00Z", 60, "JAY-PC"));

                var merged = sync.Pull();

                Assert.Equal(2, merged.Count);
                Assert.Equal(180, merged.Sum(s => s.Seconds));
                // 手元のファイルにも書き戻る
                Assert.Equal(2, store.Load().Count);
            }
        }

        [Fact]
        public void Pull_ignores_files_that_are_not_session_logs()
        {
            var rclone = new FakeRcloneRunner();
            rclone.Files["nas:playnite/playtime/readme.txt"] = Encoding.UTF8.GetBytes("ゴミ");

            using (var dir = new TempDir())
            {
                SessionStore store;
                var sync = Sync(rclone, dir, "JAY-PC", out store);

                Assert.Empty(sync.Pull());
            }
        }

        [Fact]
        public void Pull_with_nothing_on_the_remote_keeps_the_local_log()
        {
            var rclone = new FakeRcloneRunner();
            using (var dir = new TempDir())
            {
                SessionStore store;
                var sync = Sync(rclone, dir, "JAY-PC", out store);
                store.Append(Session("2026-08-30T01:00:00Z", 60, "JAY-PC"));

                Assert.Single(sync.Pull());
            }
        }

        [Fact]
        public void Two_machines_converge_after_a_round_trip_each()
        {
            var rclone = new FakeRcloneRunner();
            using (var pcDir = new TempDir())
            using (var allyDir = new TempDir())
            {
                SessionStore pcStore, allyStore;
                var pc = Sync(rclone, pcDir, "JAY-PC", out pcStore);
                var ally = Sync(rclone, allyDir, "ROG-ALLY", out allyStore);

                pcStore.Append(Session("2026-08-30T01:00:00Z", 60, "JAY-PC"));
                allyStore.Append(Session("2026-08-31T01:00:00Z", 120, "ROG-ALLY"));

                pc.Sync();
                ally.Sync();
                pc.Sync();

                Assert.Equal(180, pcStore.Load().Sum(s => s.Seconds));
                Assert.Equal(180, allyStore.Load().Sum(s => s.Seconds));
            }
        }

        [Fact]
        public void Syncing_repeatedly_does_not_grow_the_log()
        {
            var rclone = new FakeRcloneRunner();
            using (var dir = new TempDir())
            {
                SessionStore store;
                var sync = Sync(rclone, dir, "JAY-PC", out store);
                store.Append(Session("2026-08-30T01:00:00Z", 60, "JAY-PC"));

                sync.Sync();
                sync.Sync();
                sync.Sync();

                Assert.Single(store.Load());
            }
        }

        [Fact]
        public void A_wiped_machine_gets_its_history_back_from_the_other()
        {
            var rclone = new FakeRcloneRunner();
            using (var pcDir = new TempDir())
            using (var allyDir = new TempDir())
            using (var freshDir = new TempDir())
            {
                SessionStore pcStore, allyStore, freshStore;
                var pc = Sync(rclone, pcDir, "JAY-PC", out pcStore);
                var ally = Sync(rclone, allyDir, "ROG-ALLY", out allyStore);

                pcStore.Append(Session("2026-08-30T01:00:00Z", 60, "JAY-PC"));
                pc.Sync();
                ally.Sync();

                // PC を初期化して同じ機械名で戻ってくる
                var rebuilt = Sync(rclone, freshDir, "JAY-PC", out freshStore);
                rebuilt.Pull();

                Assert.Single(freshStore.Load());
            }
        }

        [Fact]
        public void Push_does_not_leave_the_temporary_file_behind()
        {
            var rclone = new FakeRcloneRunner();
            using (var dir = new TempDir())
            {
                SessionStore store;
                var sync = Sync(rclone, dir, "JAY-PC", out store);
                store.Append(Session("2026-08-30T01:00:00Z", 60, "JAY-PC"));

                sync.Push();

                Assert.Empty(Directory.GetFiles(Path.Combine(dir.Path, "work")));
            }
        }
    }

    public class PlaytimeApplierTests
    {
        [Fact]
        public void Takes_the_session_total_when_it_is_larger()
        {
            Assert.Equal(9000ul, PlaytimeApplier.Resolve(3600, 9000));
        }

        [Fact]
        public void Keeps_imported_playtime_that_has_no_sessions_behind_it()
        {
            // Steam から取り込んだ 100 時間をセッション 0 で潰さない
            Assert.Equal(360000ul, PlaytimeApplier.Resolve(360000, 0));
            Assert.Equal(360000ul, PlaytimeApplier.Resolve(360000, 3600));
        }

        [Fact]
        public void Reports_whether_anything_would_change()
        {
            Assert.True(PlaytimeApplier.NeedsUpdate(0, 60));
            Assert.False(PlaytimeApplier.NeedsUpdate(60, 60));
            Assert.False(PlaytimeApplier.NeedsUpdate(600, 60));
        }
    }
}
