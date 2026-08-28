using System;
using System.Collections.Generic;

namespace GalCompanion
{
    internal static class SavePathResolver
    {
        public static List<string> Resolve(IEnumerable<string> paths, string gameInstallDir)
        {
            var result = new List<string>();
            if (paths == null)
            {
                return result;
            }
            foreach (var raw in paths)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }
                var expanded = raw.Replace("{GameDir}", gameInstallDir ?? string.Empty);
                expanded = Environment.ExpandEnvironmentVariables(expanded).Trim();
                if (!string.IsNullOrWhiteSpace(expanded))
                {
                    result.Add(expanded);
                }
            }
            return result;
        }
    }
}
