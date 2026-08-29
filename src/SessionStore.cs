using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GalCompanion
{
    /// <summary>
    /// sessions.tsv の入出力。追記が主なので、終了のたびに全部書き直すことはしない。
    /// </summary>
    internal sealed class SessionStore
    {
        public const string FileName = "sessions.tsv";

        private readonly string path;
        private readonly object gate = new object();

        public SessionStore(string directory)
        {
            path = Path.Combine(directory, FileName);
        }

        public string Path_ => path;

        public List<PlaySession> Load()
        {
            lock (gate)
            {
                if (!File.Exists(path))
                {
                    return new List<PlaySession>();
                }
                return SessionLog.Parse(File.ReadAllText(path, Encoding.UTF8));
            }
        }

        public void Append(PlaySession session)
        {
            lock (gate)
            {
                var directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                var needsHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
                using (var writer = new StreamWriter(path, true, new UTF8Encoding(false)))
                {
                    if (needsHeader)
                    {
                        writer.Write(SessionLog.Header);
                        writer.Write('\n');
                    }
                    writer.Write(SessionLog.FormatLine(session));
                    writer.Write('\n');
                }
            }
        }

        /// <summary>合流のあとなど、全部書き直すとき。途中で落ちても元が残るよう一時ファイル経由。</summary>
        public void ReplaceAll(IEnumerable<PlaySession> sessions)
        {
            lock (gate)
            {
                var directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                var temp = path + ".tmp";
                File.WriteAllText(temp, SessionLog.Serialize(sessions), new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(temp, path);
            }
        }
    }
}
