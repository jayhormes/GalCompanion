namespace GalCompanion
{
    internal static class LocaleEmulatorActions
    {
        public const string ActionName = "Locale Emulator";

        // 留空 GUID 用 LE 的預設 profile（與 Playnite 內建模擬器定義同款式），
        // 指定 GUID 走 -runas（LEGUI 每個 profile 的 GUID）
        public static string BuildArguments(string profileGuid, string exePath)
        {
            var quoted = "\"" + exePath + "\"";
            return string.IsNullOrWhiteSpace(profileGuid)
                ? quoted
                : "-runas " + profileGuid.Trim() + " " + quoted;
        }
    }
}
