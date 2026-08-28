using System;
using System.Globalization;
using System.IO;

namespace GalCompanion
{
    // 每台機器自己的同步狀態（共同祖先時間戳），不隨存檔同步
    internal sealed class SyncStateStore
    {
        private const string Prefix = "lastSynced=";
        private readonly string dir;

        public SyncStateStore(string dir)
        {
            this.dir = dir;
        }

        public DateTime? GetLastSynced(string gameId)
        {
            var file = FileFor(gameId);
            if (!File.Exists(file))
            {
                return null;
            }
            foreach (var line in File.ReadAllLines(file))
            {
                if (!line.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    continue;
                }
                if (DateTime.TryParse(line.Substring(Prefix.Length), CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var parsed))
                {
                    return parsed.ToUniversalTime();
                }
            }
            return null;
        }

        public void SetLastSynced(string gameId, DateTime utc)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(FileFor(gameId),
                Prefix + utc.ToString("o", CultureInfo.InvariantCulture) + Environment.NewLine);
        }

        private string FileFor(string gameId)
        {
            return Path.Combine(dir, gameId + ".state");
        }
    }
}
