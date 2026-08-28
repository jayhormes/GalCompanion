using System;
using System.Collections.Generic;
using System.IO;

namespace GalCompanion
{
    internal sealed class SyncPlan
    {
        public SyncAction Action;
        public DateTime? LocalMtimeUtc;
        public SyncManifest Remote;
    }

    // 顯式 push/pull：OnGameStarting 判定拉、OnGameStopped 推、啟動時補推。
    // 遠端配置：{remote}/{gameId}/latest.zip + manifest.json + history/*.zip
    internal sealed class SaveSyncService
    {
        private readonly IRcloneRunner rclone;
        private readonly SyncStateStore state;
        private readonly string remoteRoot;
        private readonly string device;
        private readonly TimeSpan tolerance;
        private readonly string workDir;
        private readonly bool keepHistory;

        public SaveSyncService(IRcloneRunner rclone, SyncStateStore state, string remoteRoot,
            string device, TimeSpan tolerance, string workDir, bool keepHistory)
        {
            this.rclone = rclone;
            this.state = state;
            this.remoteRoot = (remoteRoot ?? string.Empty).TrimEnd('/');
            this.device = device;
            this.tolerance = tolerance;
            this.workDir = workDir;
            this.keepHistory = keepHistory;
        }

        public SyncPlan Plan(string gameId, IList<string> resolvedPaths)
        {
            var local = SaveScanner.GetLatestWriteUtc(resolvedPaths);
            var manifestJson = rclone.ReadTextFile(RemoteDir(gameId) + "/manifest.json");
            var remote = manifestJson == null ? null : SyncManifest.FromJson(manifestJson);
            var action = SyncPlanner.Decide(local, remote?.TimestampUtc, state.GetLastSynced(gameId), tolerance);
            return new SyncPlan { Action = action, LocalMtimeUtc = local, Remote = remote };
        }

        public void Push(string gameId, IList<string> resolvedPaths)
        {
            var local = SaveScanner.GetLatestWriteUtc(resolvedPaths);
            if (local == null)
            {
                return;
            }

            var zip = Path.Combine(workDir, "push", gameId + ".zip");
            var manifestPath = Path.Combine(workDir, "push", gameId + ".manifest.json");
            try
            {
                var count = SavePacker.Pack(resolvedPaths, zip);
                var remoteDir = RemoteDir(gameId);
                rclone.UploadFile(zip, remoteDir + "/latest.zip");
                if (keepHistory)
                {
                    var historyName = local.Value.ToString("yyyyMMdd_HHmmss") + "_" + SanitizeName(device) + ".zip";
                    rclone.UploadFile(zip, remoteDir + "/history/" + historyName);
                }
                // manifest 最後上傳，等同 commit 點
                var manifest = new SyncManifest { TimestampUtc = local.Value, Device = device, FileCount = count };
                File.WriteAllText(manifestPath, manifest.ToJson());
                rclone.UploadFile(manifestPath, remoteDir + "/manifest.json");
                state.SetLastSynced(gameId, local.Value);
            }
            finally
            {
                TryDelete(zip);
                TryDelete(manifestPath);
            }
        }

        // 呼叫前先 Plan 拿 remote；覆蓋本地前一律先備份到 workDir/backup
        public void Pull(string gameId, IList<string> resolvedPaths, SyncManifest remote)
        {
            if (remote == null)
            {
                throw new InvalidOperationException("遠端沒有存檔可拉");
            }

            var localBefore = SaveScanner.GetLatestWriteUtc(resolvedPaths);
            if (localBefore != null)
            {
                var backup = Path.Combine(workDir, "backup", gameId,
                    localBefore.Value.ToString("yyyyMMdd_HHmmss") + ".zip");
                SavePacker.Pack(resolvedPaths, backup);
            }

            var zip = Path.Combine(workDir, "pull", gameId + ".zip");
            try
            {
                rclone.DownloadFile(RemoteDir(gameId) + "/latest.zip", zip);
                SavePacker.Unpack(zip, resolvedPaths);
                state.SetLastSynced(gameId, remote.TimestampUtc);
            }
            finally
            {
                TryDelete(zip);
            }
        }

        private string RemoteDir(string gameId)
        {
            return remoteRoot + "/" + gameId;
        }

        internal static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "unknown";
            }
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(Path.GetInvalidFileNameChars(), chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 暫存檔清不掉不影響同步結果
            }
        }
    }
}
