using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AppCAT.Services
{
    public class ScanResult
    {
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public string ReportPath { get; set; } = "";
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
        public string Command { get; set; } = "";
    }

    public static class ScanService
    {
        // ── Active process reference ──
        private static Process? _activeProcess;
        private static readonly object _lock = new();

        /// <summary>
        /// Build command string
        /// </summary>
        public static string BuildCommand(
            string projectPath,
            string reportPath,
            string format,
            string target,
            bool scanCode,
            bool scanBinaries,
            bool nonInteractive)
        {
            string cmd =
                $"appcat analyze \"{projectPath}\"" +
                $" --report \"{reportPath}\"" +
                $" --serializer \"{format}\"" +
                $" --target \"{target}\""; 

            if (scanCode) cmd += " --code";
            if (scanBinaries) cmd += " --binaries";
            if (nonInteractive) cmd += " --non-interactive";

            return cmd;
        }

        /// <summary>
        /// Kill the running PowerShell process
        /// </summary>
        public static void CancelScan()
        {
            lock (_lock)
            {
                try
                {
                    if (_activeProcess != null
                        && !_activeProcess.HasExited)
                    {
                        // Kill entire process tree
                        _activeProcess.Kill(
                            entireProcessTree: true);
                    }
                }
                catch { }
                finally
                {
                    _activeProcess = null;
                }
            }
        }

        /// <summary>
        /// Write command to .ps1 and run in PowerShell
        /// </summary>
        public static async Task<ScanResult> RunScanAsync(
            string projectPath,
            string reportPath,
            string format,
            string target,
            bool scanCode,
            bool scanBinaries,
            bool nonInteractive,
            CancellationToken cancellationToken = default)
        {
            string command = BuildCommand(
                projectPath, reportPath, format, target,
                scanCode, scanBinaries, nonInteractive);

            var result = new ScanResult { Command = command };

            string scriptPath = Path.Combine(
                Path.GetTempPath(),
                $"appcat-scan-{Guid.NewGuid():N}.ps1");

            try
            {
                await File.WriteAllTextAsync(
                    scriptPath, command + "\n",
                    cancellationToken);

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments =
                        $"-NoProfile " +
                        $"-ExecutionPolicy Bypass " +
                        $"-File \"{scriptPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(psi);
                if (process == null)
                {
                    result.Error =
                        "Could not start PowerShell.";
                    return result;
                }

                // Store reference for cancellation
                lock (_lock)
                {
                    _activeProcess = process;
                }

                // Read output concurrently
                var outputTask = process.StandardOutput
                    .ReadToEndAsync();
                var errorTask = process.StandardError
                    .ReadToEndAsync();

                try
                {
                    // Wait for exit with cancellation
                    await process.WaitForExitAsync(
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Token fired — kill the process
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(
                                entireProcessTree: true);
                    }
                    catch { }

                    result.Cancelled = true;
                    result.Output = await outputTask;
                    result.Error =
                        "Scan cancelled by user.";
                    return result;
                }

                result.Output = await outputTask;
                result.Error = await errorTask;

                result.Success = process.ExitCode == 0;

                if (Directory.Exists(reportPath))
                    result.ReportPath = FindReport(
                        reportPath, format);
            }
            catch (OperationCanceledException)
            {
                result.Cancelled = true;
                result.Error = "Scan cancelled by user.";
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }
            finally
            {
                // Clear active process
                lock (_lock)
                {
                    _activeProcess = null;
                }

                try
                {
                    if (File.Exists(scriptPath))
                        File.Delete(scriptPath);
                }
                catch { }
            }

            return result;
        }

        private static string FindReport(
            string dir, string format)
        {
            if (!Directory.Exists(dir)) return dir;

            try
            {
                string[] patterns =
                {
                    $"*.{format}", "*.html",
                    "*.json",      "*.csv"
                };

                foreach (string p in patterns)
                {
                    var files = Directory.GetFiles(
                        dir, p,
                        SearchOption.AllDirectories)
                        .OrderByDescending(f =>
                            new FileInfo(f).LastWriteTime)
                        .ToArray();

                    if (files.Length > 0)
                        return files[0];
                }
            }
            catch { }

            return dir;
        }

        public static void OpenReport(string path)
        {
            if (File.Exists(path))
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            else if (Directory.Exists(path))
                OpenFolder(path);
        }

        public static void OpenFolder(string path)
        {
            if (Directory.Exists(path))
                Process.Start("explorer.exe", path);
        }

       
    }
}