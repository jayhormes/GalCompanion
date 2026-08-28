using System;
using System.Collections.Generic;
using System.IO;

namespace GalCompanion
{
    internal static class SaveScanner
    {
        // 所有規則路徑下最新的檔案修改時間（UTC）；一個檔案都沒有回 null
        public static DateTime? GetLatestWriteUtc(IEnumerable<string> resolvedPaths)
        {
            DateTime? latest = null;
            foreach (var path in resolvedPaths)
            {
                if (File.Exists(path))
                {
                    Consider(ref latest, File.GetLastWriteTimeUtc(path));
                }
                else if (Directory.Exists(path))
                {
                    foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        Consider(ref latest, File.GetLastWriteTimeUtc(file));
                    }
                }
            }
            return latest;
        }

        private static void Consider(ref DateTime? latest, DateTime candidate)
        {
            if (latest == null || candidate > latest.Value)
            {
                latest = candidate;
            }
        }
    }
}
