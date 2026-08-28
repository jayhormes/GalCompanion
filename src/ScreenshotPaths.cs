using System;
using System.IO;

namespace GalCompanion
{
    internal static class ScreenshotPaths
    {
        public static string GetDir(string configRoot, string defaultRoot, Guid? gameId)
        {
            var root = string.IsNullOrWhiteSpace(configRoot) ? defaultRoot : configRoot;
            return gameId == null
                ? Path.Combine(root, "screenshots", "unassigned")
                : Path.Combine(root, "games", gameId.Value.ToString(), "screenshots");
        }
    }
}
