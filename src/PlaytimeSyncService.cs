using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GalCompanion
{
    /// <summary>
    /// 遊んだ記録を機械間で合流させる。
    /// 各機械は自分の名前のファイルにしか書かないので、書き込みが衝突しない。
    /// 読むときは全部のファイルを和集合するだけ。累計値だとこれができない。
    /// </summary>
    internal sealed class PlaytimeSyncService
    {
        private readonly IRcloneRunner rclone;
        private readonly SessionStore store;
        private readonly string remoteDir;
        private readonly string workDir;
        private readonly string fileName;
        private readonly object gate = new object();

        public PlaytimeSyncService(
            IRcloneRunner rclone, SessionStore store, string remoteRoot, string device, string workDir)
        {
            this.rclone = rclone;
            this.store = store;
            this.remoteDir = JoinRemote(remoteRoot, "playtime");
            this.workDir = workDir;
            this.fileName = SafeFileName(device) + ".tsv";
        }

        internal string RemoteFile => remoteDir + "/" + fileName;

        /// <summary>ファイル名に使えるようにする。機械名に全角や記号が入っていても壊れないように。</summary>
        internal static string SafeFileName(string device)
        {
            var sb = new StringBuilder();
            foreach (var c in device ?? string.Empty)
            {
                if (char.IsLetterOrDigit(c) && c < 128)
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
                else if (c == '-' || c == '_')
                {
                    sb.Append(c);
                }
            }
            return sb.Length > 0 ? sb.ToString() : "device";
        }

        internal static string JoinRemote(string root, string child)
        {
            return (root ?? string.Empty).TrimEnd('/') + "/" + child;
        }

        /// <summary>遠端の全機械ぶんを取り込んで、手元と合流させた結果を返す。</summary>
        public List<PlaySession> Pull()
        {
            lock (gate)
            {
                var merged = store.Load();
                foreach (var name in rclone.ListFiles(remoteDir))
                {
                    if (!name.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    var text = rclone.ReadTextFile(remoteDir + "/" + name);
                    if (text == null)
                    {
                        continue;
                    }
                    merged = SessionLog.Merge(merged, SessionLog.Parse(text));
                }
                store.ReplaceAll(merged);
                return merged;
            }
        }

        /// <summary>手元の全記録を自分のファイルとして置く。上書きしても他機械のファイルは無傷。</summary>
        public void Push()
        {
            lock (gate)
            {
                Directory.CreateDirectory(workDir);
                var temp = Path.Combine(workDir, fileName);
                File.WriteAllText(temp, SessionLog.Serialize(store.Load()), new UTF8Encoding(false));
                try
                {
                    rclone.UploadFile(temp, RemoteFile);
                }
                finally
                {
                    try { File.Delete(temp); } catch { /* 消せなくても次で上書きされる */ }
                }
            }
        }

        public List<PlaySession> Sync()
        {
            var merged = Pull();
            Push();
            return merged;
        }
    }
}
