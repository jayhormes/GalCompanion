using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GalCompanion.Tests
{
    internal sealed class FakeRcloneRunner : IRcloneRunner
    {
        public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();

        public string ReadTextFile(string remotePath)
        {
            return Files.TryGetValue(remotePath, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;
        }

        public void UploadFile(string localPath, string remotePath)
        {
            Files[remotePath] = File.ReadAllBytes(localPath);
        }

        public void DownloadFile(string remotePath, string localPath)
        {
            if (!Files.TryGetValue(remotePath, out var bytes))
            {
                throw new InvalidOperationException("遠端不存在：" + remotePath);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(localPath));
            File.WriteAllBytes(localPath, bytes);
        }

        public List<string> ListFiles(string remoteDir)
        {
            var prefix = remoteDir.TrimEnd('/') + "/";
            var names = new List<string>();
            foreach (var key in Files.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal)
                    && key.IndexOf('/', prefix.Length) < 0)
                {
                    names.Add(key.Substring(prefix.Length));
                }
            }
            return names;
        }
    }
}
