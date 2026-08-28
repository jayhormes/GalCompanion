using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace GalCompanion.Tests
{
    public class SaveSyncServiceTests
    {
        private const string GameId = "g1";
        private static readonly TimeSpan Tol = TimeSpan.FromSeconds(3);

        private static SaveSyncService Service(FakeRcloneRunner rclone, TempDir machine, string device)
        {
            return new SaveSyncService(
                rclone,
                new SyncStateStore(machine.Sub("state")),
                "nas:saves",
                device,
                Tol,
                machine.Sub("work"),
                keepHistory: true);
        }

        [Fact]
        public void Push_uploads_latest_history_manifest_and_records_state()
        {
            using (var machine = new TempDir())
            {
                var rclone = new FakeRcloneRunner();
                var mtime = new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc);
                machine.WriteFile("saves/a.sav", "data", mtime);
                var service = Service(rclone, machine, "PC");

                service.Push(GameId, new List<string> { machine.Sub("saves") });

                Assert.True(rclone.Files.ContainsKey("nas:saves/g1/latest.zip"));
                Assert.True(rclone.Files.Keys.Any(k => k.StartsWith("nas:saves/g1/history/")));
                var manifest = SyncManifest.FromJson(rclone.ReadTextFile("nas:saves/g1/manifest.json"));
                Assert.Equal(mtime, manifest.TimestampUtc);
                Assert.Equal("PC", manifest.Device);
                Assert.Equal(1, manifest.FileCount);

                var plan = service.Plan(GameId, new List<string> { machine.Sub("saves") });
                Assert.Equal(SyncAction.None, plan.Action);
            }
        }

        [Fact]
        public void Push_with_no_local_saves_does_nothing()
        {
            using (var machine = new TempDir())
            {
                var rclone = new FakeRcloneRunner();
                var service = Service(rclone, machine, "PC");
                service.Push(GameId, new List<string> { machine.Sub("empty") });
                Assert.Empty(rclone.Files);
            }
        }

        [Fact]
        public void Plan_pushes_when_remote_missing()
        {
            using (var machine = new TempDir())
            {
                var rclone = new FakeRcloneRunner();
                machine.WriteFile("saves/a.sav", "data");
                var service = Service(rclone, machine, "PC");

                var plan = service.Plan(GameId, new List<string> { machine.Sub("saves") });
                Assert.Equal(SyncAction.Push, plan.Action);
                Assert.Null(plan.Remote);
            }
        }

        [Fact]
        public void Two_device_flow_pull_restores_files_and_backs_up_local()
        {
            using (var deviceA = new TempDir())
            using (var deviceB = new TempDir())
            {
                var rclone = new FakeRcloneRunner();

                // A 機玩完推送
                var newer = new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);
                deviceA.WriteFile("saves/a.sav", "A 機進度", newer);
                Service(rclone, deviceA, "PC").Push(GameId, new List<string> { deviceA.Sub("saves") });

                // B 機本地是舊進度，且從未同步 → 時間戳差距大 → Conflict（保守）
                var older = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
                deviceB.WriteFile("saves/a.sav", "B 機舊進度", older);
                var serviceB = Service(rclone, deviceB, "Ally");
                var plan = serviceB.Plan(GameId, new List<string> { deviceB.Sub("saves") });
                Assert.Equal(SyncAction.Conflict, plan.Action);

                // 使用者選拉遠端：本地先備份、內容變 A 機進度、狀態記下祖先
                serviceB.Pull(GameId, new List<string> { deviceB.Sub("saves") }, plan.Remote);
                Assert.Equal("A 機進度", File.ReadAllText(deviceB.Sub("saves", "a.sav")));
                Assert.True(Directory.Exists(deviceB.Sub("work", "backup", GameId)));
                Assert.NotEmpty(Directory.GetFiles(deviceB.Sub("work", "backup", GameId)));

                // 之後再 Plan：兩邊一致 → None
                var planAfter = serviceB.Plan(GameId, new List<string> { deviceB.Sub("saves") });
                Assert.Equal(SyncAction.None, planAfter.Action);
            }
        }

        [Fact]
        public void Pull_without_remote_throws()
        {
            using (var machine = new TempDir())
            {
                var service = Service(new FakeRcloneRunner(), machine, "PC");
                Assert.Throws<InvalidOperationException>(
                    () => service.Pull(GameId, new List<string> { machine.Sub("saves") }, null));
            }
        }

        [Fact]
        public void SanitizeName_strips_invalid_filename_chars()
        {
            Assert.Equal("PC_Ally", SaveSyncService.SanitizeName("PC|Ally"));
            Assert.Equal("unknown", SaveSyncService.SanitizeName(""));
        }
    }
}
