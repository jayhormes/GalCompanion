using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace GalCompanion
{
    internal interface IRcloneRunner
    {
        // null = 遠端檔案不存在；其他失敗丟例外
        string ReadTextFile(string remotePath);

        void UploadFile(string localPath, string remotePath);

        void DownloadFile(string remotePath, string localPath);

        // 遠端目錄下的檔名（不遞迴）；目錄不存在回空清單
        List<string> ListFiles(string remoteDir);
    }

    internal sealed class RcloneRunner : IRcloneRunner
    {
        private const int TimeoutMs = 120000;
        private readonly string exe;

        public RcloneRunner(string exe)
        {
            this.exe = string.IsNullOrWhiteSpace(exe) ? "rclone" : exe;
        }

        public string ReadTextFile(string remotePath)
        {
            var result = Run("cat " + Quote(remotePath));
            if (result.ExitCode == 0)
            {
                return result.StdOut;
            }
            // rclone：3 = 目錄不存在、4 = 檔案不存在
            if (result.ExitCode == 3 || result.ExitCode == 4)
            {
                return null;
            }
            throw new InvalidOperationException($"rclone cat 失敗（exit {result.ExitCode}）：{result.StdErr}");
        }

        public void UploadFile(string localPath, string remotePath)
        {
            RunChecked("copyto " + Quote(localPath) + " " + Quote(remotePath));
        }

        public void DownloadFile(string remotePath, string localPath)
        {
            RunChecked("copyto " + Quote(remotePath) + " " + Quote(localPath));
        }

        public List<string> ListFiles(string remoteDir)
        {
            var result = Run("lsf --files-only " + Quote(remoteDir));
            var names = new List<string>();
            if (result.ExitCode == 3 || result.ExitCode == 4)
            {
                return names;
            }
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"rclone lsf 失敗（exit {result.ExitCode}）：{result.StdErr}");
            }
            foreach (var line in (result.StdOut ?? string.Empty).Split('\n'))
            {
                var name = line.Trim().TrimEnd('/');
                if (name.Length > 0)
                {
                    names.Add(name);
                }
            }
            return names;
        }

        internal static string Quote(string s)
        {
            return "\"" + s + "\"";
        }

        private void RunChecked(string args)
        {
            var result = Run(args);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"rclone {args} 失敗（exit {result.ExitCode}）：{result.StdErr}");
            }
        }

        private RunResult Run(string args)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                process.Start();
                var stderrTask = process.StandardError.ReadToEndAsync();
                var stdout = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(TimeoutMs))
                {
                    try { process.Kill(); } catch { /* 已結束 */ }
                    throw new TimeoutException($"rclone {args} 逾時（{TimeoutMs / 1000} 秒）");
                }
                return new RunResult
                {
                    ExitCode = process.ExitCode,
                    StdOut = stdout,
                    StdErr = stderrTask.Result
                };
            }
        }

        private sealed class RunResult
        {
            public int ExitCode;
            public string StdOut;
            public string StdErr;
        }
    }
}
