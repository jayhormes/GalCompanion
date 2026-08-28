using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace GalCompanion
{
    // 打包/還原規則路徑。zip 內以 p{規則索引}/ 前綴對應回原路徑，
    // 保留檔案 mtime（zip 時間戳解析度 2 秒，同步容差須 ≥ 3 秒）。
    internal static class SavePacker
    {
        public static int Pack(IList<string> resolvedPaths, string zipPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath));
            var count = 0;
            using (var fs = new FileStream(zipPath, FileMode.Create))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                for (var i = 0; i < resolvedPaths.Count; i++)
                {
                    var path = resolvedPaths[i];
                    if (File.Exists(path))
                    {
                        AddFile(zip, path, $"p{i}/{Path.GetFileName(path)}");
                        count++;
                    }
                    else if (Directory.Exists(path))
                    {
                        var root = Path.GetFullPath(path);
                        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                        {
                            var relative = Path.GetFullPath(file)
                                .Substring(root.Length)
                                .TrimStart('\\', '/')
                                .Replace('\\', '/');
                            AddFile(zip, file, $"p{i}/{relative}");
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        private static void AddFile(ZipArchive zip, string file, string entryName)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            entry.LastWriteTime = File.GetLastWriteTime(file);
            using (var target = entry.Open())
            using (var source = File.OpenRead(file))
            {
                source.CopyTo(target);
            }
        }

        public static void Unpack(string zipPath, IList<string> resolvedPaths)
        {
            using (var fs = File.OpenRead(zipPath))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    if (entry.Name.Length == 0)
                    {
                        continue;
                    }
                    var target = MapEntry(entry.FullName, resolvedPaths);
                    if (target == null)
                    {
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var source = entry.Open())
                    using (var output = new FileStream(target, FileMode.Create))
                    {
                        source.CopyTo(output);
                    }
                    File.SetLastWriteTime(target, entry.LastWriteTime.DateTime);
                }
            }
        }

        // 拒絕 ../ 路徑穿越與未知前綴；規則指向單一檔案時 zip 內就是該檔名
        internal static string MapEntry(string entryFullName, IList<string> resolvedPaths)
        {
            var normalized = (entryFullName ?? string.Empty).Replace('\\', '/');
            var slash = normalized.IndexOf('/');
            if (slash <= 1 || normalized[0] != 'p')
            {
                return null;
            }
            if (!int.TryParse(normalized.Substring(1, slash - 1), out var index))
            {
                return null;
            }
            if (index < 0 || index >= resolvedPaths.Count)
            {
                return null;
            }
            var rest = normalized.Substring(slash + 1);
            if (rest.Length == 0)
            {
                return null;
            }
            var segments = rest.Split('/');
            foreach (var segment in segments)
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    return null;
                }
            }

            var basePath = resolvedPaths[index];
            if (segments.Length == 1
                && string.Equals(segments[0], Path.GetFileName(basePath), System.StringComparison.OrdinalIgnoreCase)
                && !Directory.Exists(basePath))
            {
                return basePath;
            }
            return Path.Combine(basePath, rest.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
