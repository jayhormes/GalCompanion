using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LunaImport
{
    internal static class Program
    {
        private const string Usage =
@"LunaImport — 把 LunaTranslator 的遊玩時間匯進 Playnite

用法:
  LunaImport.exe --luna <LunaTranslator 資料夾> [選項]

選項:
  --luna <路徑>       LunaTranslator 的安裝資料夾（或直接指到它的 userconfig）
  --playnite <路徑>   Playnite 的資料夾，預設 %AppData%\Playnite
  --apply             實際寫入。不加就只印報告，什麼都不動
  --overwrite         Playnite 已經有時數的也覆蓋（預設跳過，不動既有紀錄）
  --no-sessions       只寫總時數，不寫逐次遊玩紀錄
  --game-activity     逐次紀錄也寫一份進 GameActivity 擴充
  --backup <路徑>     備份 zip 放哪，預設 Playnite 資料夾下的 LunaImportBackup

寫入前 Playnite 必須關閉，否則記憶體裡的舊資料會蓋回去。";

        private static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("失敗：" + e.Message);
                return 1;
            }
        }

        private static int Run(string[] args)
        {
            var options = Options.Parse(args);
            if (options == null)
            {
                Console.WriteLine(Usage);
                return 2;
            }

            var userConfig = LunaReader.FindUserConfig(options.LunaRoot);
            if (userConfig == null)
            {
                Console.Error.WriteLine($"找不到 LunaTranslator 的 userconfig：{options.LunaRoot}");
                return 1;
            }

            List<string> tried;
            var gamesDir = PlayniteLibrary.FindGamesDir(options.PlayniteRoot, out tried);
            if (gamesDir == null)
            {
                Console.Error.WriteLine("找不到 Playnite 的遊戲庫。找過這些位置：");
                foreach (var path in tried)
                {
                    Console.Error.WriteLine("  " + path);
                }
                DumpFolder(options.PlayniteRoot);
                Console.Error.WriteLine();
                Console.Error.WriteLine("Playnite 的「設定 → 一般」會顯示資料庫位置。--playnite 請指到 library 的上一層，");
                Console.Error.WriteLine("或直接把 library 資料夾丟進來。不加 --playnite 的話會自己讀 config.json。");
                return 1;
            }
            if (!string.Equals(gamesDir, PlayniteLibrary.GamesDir(options.PlayniteRoot),
                    StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("遊戲庫：" + gamesDir);
            }

            if (options.Apply && IsPlayniteRunning())
            {
                Console.Error.WriteLine("Playnite 正在執行中。請先關掉再跑，否則寫進去的資料會被蓋回去。");
                return 1;
            }

            var lunaGames = LunaReader.Load(userConfig);
            var playniteGames = PlayniteLibrary.Load(gamesDir);
            Console.WriteLine($"LunaTranslator：{lunaGames.Count} 款　Playnite：{playniteGames.Count} 款");

            var plan = ImportPlan.Build(Matcher.Match(lunaGames, playniteGames), options.Overwrite);
            Report(plan);

            var writable = plan.Where(p => p.Action == PlanAction.Write).ToList();
            if (writable.Count == 0)
            {
                Console.WriteLine("沒有要寫入的項目。");
                return 0;
            }

            if (!options.Apply)
            {
                Console.WriteLine();
                Console.WriteLine($"以上是預演。確認沒問題後加 --apply 才會真的寫入（{writable.Count} 款）。");
                return 0;
            }

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupDir = options.BackupDir ?? Path.Combine(options.PlayniteRoot, "LunaImportBackup");
            Console.WriteLine("備份：" + PlayniteLibrary.Backup(gamesDir, backupDir, stamp));

            string activityDir = null;
            if (options.WriteSessions && options.WriteGameActivity)
            {
                activityDir = GameActivityWriter.FindDataDir(options.PlayniteRoot);
                if (activityDir == null)
                {
                    Console.WriteLine("沒有找到 GameActivity 的資料夾，跳過那一份。");
                }
                else
                {
                    Console.WriteLine("備份：" + GameActivityWriter.Backup(activityDir, backupDir, stamp));
                }
            }

            foreach (var entry in writable)
            {
                PlayniteLibrary.Write(entry);
                if (activityDir != null)
                {
                    GameActivityWriter.Write(activityDir, entry);
                }
            }

            if (options.WriteSessions)
            {
                var sessionBackup = GalCompanionWriter.Backup(options.PlayniteRoot, backupDir, stamp);
                if (sessionBackup != null)
                {
                    Console.WriteLine("備份：" + sessionBackup);
                }
                var added = GalCompanionWriter.Write(options.PlayniteRoot, writable);
                Console.WriteLine($"GalCompanion 遊玩紀錄新增 {added} 筆。");
            }

            Console.WriteLine($"已寫入 {writable.Count} 款。啟動 Playnite 確認。");
            return 0;
        }

        private static void DumpFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"（{path} 這個資料夾本身也不存在）");
                return;
            }
            Console.Error.WriteLine();
            Console.Error.WriteLine($"{path} 裡面有：");
            foreach (var dir in Directory.GetDirectories(path).Take(30))
            {
                Console.Error.WriteLine("  " + Path.GetFileName(dir) + Path.DirectorySeparatorChar);
            }
            foreach (var file in Directory.GetFiles(path).Take(30))
            {
                Console.Error.WriteLine("  " + Path.GetFileName(file));
            }
        }

        private static bool IsPlayniteRunning()
        {
            return Process.GetProcessesByName("Playnite.DesktopApp").Length > 0
                || Process.GetProcessesByName("Playnite.FullscreenApp").Length > 0;
        }

        private static void Report(List<PlanEntry> plan)
        {
            foreach (var group in plan.GroupBy(p => p.Action).OrderBy(g => (int)g.Key))
            {
                Console.WriteLine();
                Console.WriteLine($"--- {Describe(group.Key)}（{group.Count()}）");
                foreach (var entry in group.OrderByDescending(e => e.LunaSeconds))
                {
                    Console.WriteLine("  " + Line(entry));
                }
            }
        }

        private static string Describe(PlanAction action)
        {
            switch (action)
            {
                case PlanAction.Unmatched: return "Playnite 找不到對應的遊戲（略過）";
                case PlanAction.NoSessions: return "Luna 沒有遊玩紀錄（略過）";
                case PlanAction.KeepExisting: return "Playnite 已有時數（略過，要蓋請加 --overwrite）";
                default: return "會寫入";
            }
        }

        private static string Line(PlanEntry entry)
        {
            var name = entry.Luna.DisplayName;
            var hours = FormatHours(entry.LunaSeconds);
            var matched = entry.Playnite == null
                ? "—"
                : $"→ {entry.Playnite.Name} [{(entry.Kind == MatchKind.Path ? "路徑" : "標題")}]";
            var current = entry.Playnite == null
                ? string.Empty
                : $"（Playnite 目前 {FormatHours((long)entry.CurrentPlaytime)}）";
            return $"{name}  {hours} / {entry.SessionCount} 次  {matched} {current}".TrimEnd();
        }

        internal static string FormatHours(long seconds)
        {
            if (seconds <= 0)
            {
                return "0h";
            }
            var hours = seconds / 3600.0;
            return hours < 1
                ? $"{seconds / 60}m"
                : $"{hours.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}h";
        }

        internal sealed class Options
        {
            public string LunaRoot;
            public string PlayniteRoot = PlayniteLibrary.DefaultRoot();
            public string BackupDir;
            public bool Apply;
            public bool Overwrite;
            public bool WriteSessions = true;
            public bool WriteGameActivity;

            public static Options Parse(string[] args)
            {
                var options = new Options();
                for (var i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--luna":
                            if (++i >= args.Length) return null;
                            options.LunaRoot = args[i];
                            break;
                        case "--playnite":
                            if (++i >= args.Length) return null;
                            options.PlayniteRoot = args[i];
                            break;
                        case "--backup":
                            if (++i >= args.Length) return null;
                            options.BackupDir = args[i];
                            break;
                        case "--apply":
                            options.Apply = true;
                            break;
                        case "--overwrite":
                            options.Overwrite = true;
                            break;
                        case "--no-sessions":
                            options.WriteSessions = false;
                            break;
                        case "--game-activity":
                            options.WriteGameActivity = true;
                            break;
                        default:
                            return null;
                    }
                }
                return string.IsNullOrWhiteSpace(options.LunaRoot) ? null : options;
            }
        }
    }
}
