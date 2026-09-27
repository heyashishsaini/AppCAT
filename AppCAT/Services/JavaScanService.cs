using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AppCAT.Services
{
    public class JavaScanResult
    {
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public string ReportPath { get; set; } = "";
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
        public string Command { get; set; } = "";
    }

    public static class JavaScanService
    {
        // ── Java AppCAT executable path ──
        private const string APPCAT_JAVA_PATH =
            @"C:\appcat-java\appcat.exe";

        // ── Active process reference ──
        private static Process? _activeProcess;
        private static readonly object _lock = new();

        /// <summary>
        /// Build command string for Java AppCAT
        /// </summary>
        public static string BuildCommand(
            string inputPath,
            string outputPath,
            string format,
            string target,
            string capability,
            string os,
            bool overwrite)
        {
            // Base command
            string cmd = $"& \"{APPCAT_JAVA_PATH}\" analyze " +
                         $"--input \"{inputPath}\" " +
                         $"--output \"{outputPath}\" " +
                         $"--target {target}";

            // Add output format if specified
            if (!string.IsNullOrEmpty(format))
            {
                cmd += $" --output-format {format}";
            }

            if (!string.IsNullOrWhiteSpace(capability))
            {
                cmd += $" --capability {capability}";
            }

            if (!string.IsNullOrWhiteSpace(os))
            {
                cmd += $" --os {os}";
            }

            // Add overwrite flag 
            if (overwrite)
            {
                cmd += " --overwrite";
            }

            return cmd;
        }

        /// <summary>
        /// Kill the running process
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
        /// Run Java AppCAT scan
        /// </summary>
        public static async Task<JavaScanResult> RunScanAsync(
            string inputPath,
            string outputPath,
            string format,
            string target,
            string capability,
            string os,
            bool overwrite,
            CancellationToken cancellationToken = default)
        {
            string command = BuildCommand(
                inputPath, outputPath, format, target, capability, os, overwrite);

            var result = new JavaScanResult { Command = command };

            // Verify appcat.exe exists
            if (!File.Exists(APPCAT_JAVA_PATH))
            {
                result.Error =
                    $"Java AppCAT not found at: {APPCAT_JAVA_PATH}\n\n" +
                    "Please ensure Java AppCAT is installed at the expected location.";
                return result;
            }

            // Create output directory if it doesn't exist
            try
            {
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }
            }
            catch (Exception ex)
            {
                result.Error = $"Failed to create output directory: {ex.Message}";
                return result;
            }

            string scriptPath = Path.Combine(
                Path.GetTempPath(),
                $"appcat-java-scan-{Guid.NewGuid():N}.ps1");

            try
            {
                // Write command to PowerShell script
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
                    CreateNoWindow = true,
                    WorkingDirectory = inputPath
                };

                var process = Process.Start(psi);
                if (process == null)
                {
                    result.Error = "Could not start PowerShell.";
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
                    await process.WaitForExitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Token fired — kill the process
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(entireProcessTree: true);
                    }
                    catch { }

                    result.Cancelled = true;
                    result.Output = await outputTask;
                    result.Error = "Scan cancelled by user.";
                    return result;
                }

                result.Output = await outputTask;
                result.Error = await errorTask;
                result.Success = process.ExitCode == 0;

                // Find the report file
                if (Directory.Exists(outputPath))
                {
                    result.ReportPath = FindReport(outputPath, format);
                }
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

                // Clean up temp script
                try
                {
                    if (File.Exists(scriptPath))
                        File.Delete(scriptPath);
                }
                catch { }
            }

            return result;
        }

        /// <summary>
        /// Find the generated report file
        /// </summary>
        private static string FindReport(string dir, string format)
        {
            if (!Directory.Exists(dir)) return dir;

            try
            {
                // Determine patterns based on format
                string[] patterns;

                if (string.IsNullOrEmpty(format))
                {
                    // Default HTML format
                    patterns = new[] { "*.html", "index.html" };
                }
                else
                {
                    patterns = format.ToLower() switch
                    {
                        "json" => new[] { "*.json" },
                        "xml" => new[] { "*.xml" },
                        _ => new[] { "*.html", "*.json", "*.xml" }
                    };
                }

                foreach (string p in patterns)
                {
                    var files = Directory.GetFiles(
                        dir, p,
                        SearchOption.AllDirectories)
                        .OrderByDescending(f =>
                            new FileInfo(f).LastWriteTime)
                        .ToArray();

                    if (files.Length > 0)
                    {
                        // Prefer index.html if it exists
                        var indexFile = files.FirstOrDefault(
                            f => Path.GetFileName(f)
                                .Equals("index.html",
                                    StringComparison.OrdinalIgnoreCase));

                        return indexFile ?? files[0];
                    }
                }
            }
            catch { }

            return dir;
        }

        /// <summary>
        /// Open the report file or folder
        /// </summary>
        public static void OpenReport(string path)
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            else if (Directory.Exists(path))
            {
                OpenFolder(path);
            }
        }

        /// <summary>
        /// Open folder in Explorer
        /// </summary>
        public static void OpenFolder(string path)
        {
            if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", path);
            }
        }
    }
}