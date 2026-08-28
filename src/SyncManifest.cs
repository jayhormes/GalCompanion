using System;
using System.Globalization;

namespace GalCompanion
{
    // NAS 端 manifest.json：timestamp = 存檔最新 mtime（不是推送時間），是 SyncPlanner 的比較基準
    internal sealed class SyncManifest
    {
        public DateTime TimestampUtc { get; set; }
        public string Device { get; set; }
        public int FileCount { get; set; }

        public string ToJson()
        {
            return "{\"timestamp\":\"" + TimestampUtc.ToString("o", CultureInfo.InvariantCulture) + "\"," +
                   "\"device\":\"" + JsonUtil.Escape(Device) + "\"," +
                   "\"fileCount\":" + FileCount.ToString(CultureInfo.InvariantCulture) + "}";
        }

        public static SyncManifest FromJson(string json)
        {
            var ts = JsonUtil.ExtractString(json, "timestamp");
            if (ts == null)
            {
                return null;
            }
            if (!DateTime.TryParse(ts, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return null;
            }
            return new SyncManifest
            {
                TimestampUtc = parsed.ToUniversalTime(),
                Device = JsonUtil.ExtractString(json, "device"),
                FileCount = JsonUtil.ExtractInt(json, "fileCount") ?? 0
            };
        }
    }
}
