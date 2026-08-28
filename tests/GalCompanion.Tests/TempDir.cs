using System;
using System.IO;

namespace GalCompanion.Tests
{
    internal sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "galcompanion-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Sub(params string[] parts)
        {
            var p = Path;
            foreach (var part in parts)
            {
                p = System.IO.Path.Combine(p, part);
            }
            return p;
        }

        public string WriteFile(string relative, string content, DateTime? mtimeUtc = null)
        {
            var file = System.IO.Path.Combine(Path, relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
            File.WriteAllText(file, content);
            if (mtimeUtc != null)
            {
                File.SetLastWriteTimeUtc(file, mtimeUtc.Value);
            }
            return file;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch
            {
                // 清不掉留給系統暫存清理
            }
        }
    }
}
