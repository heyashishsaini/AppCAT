using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppCAT.Services
{
   
    public enum InstallationType
    {
        DotNet,
        Java
    }

    
    public class InstallationProgress
    {
        public string Message { get; set; } = "";
        public int Percentage { get; set; }
        public bool IsError { get; set; }
    }

   
    public class InstallationService
    {
        // ── HttpClient ──
        private static readonly HttpClient _httpClient = new(
            new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10
            })
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        // ── CORRECT URLs ──
        private const string JavaAppcatZipUrl =
            "https://aka.ms/appcat/azure-migrate-appcat-for-java-cli-windows-amd64.zip";

        private const string Jdk17MsiUrl =
            "https://aka.ms/download-jdk/microsoft-jdk-17.0.18-windows-x64.msi";

        // ── Paths ──
        private const string JavaExtractRoot = @"C:\appcat-java";

        // Known .NET SDK locations
        private static readonly string[] DotNetSearchPaths =
        {
            @"C:\Program Files\dotnet",
            @"C:\Program Files (x86)\dotnet",
        };

        // Known JDK locations
        private static readonly string[] JdkSearchRoots =
        {
            @"C:\Program Files\Microsoft",
            @"C:\Program Files\Eclipse Adoptium",
            @"C:\Program Files\Java",
            @"C:\Program Files\OpenJDK",
        };


        public static async Task InstallDotNetAppcatAsync(
            IProgress<InstallationProgress> progress,
            CancellationToken ct = default)
        {
            // ── STEP 1: Check .NET SDK ──
            Report(progress, "Checking .NET SDK...", 2);

            bool sdkFound = IsDotNetSdkInstalled();

            if (sdkFound)
            {
                string ver = GetDotNetSdkVersion();
                string path = FindDotNetSdkPath();
                Report(progress,
                    $".NET SDK already installed: {ver}\n" +
                    $"   Path: {path}", 20);
            }
            else
            {
                Report(progress,
                    ".NET SDK not found — installing...", 5);
                await InstallDotNetSdkInternalAsync(progress, ct);

                // Verify after install
                if (!IsDotNetSdkInstalled())
                    throw new Exception(
                        ".NET SDK installation completed but " +
                        "'dotnet' is still not detected.\n" +
                        "Install manually from: " +
                        "https://dotnet.microsoft.com/download/dotnet/8.0\n" +
                        "Then restart this application.");

                Report(progress, ".NET SDK installed!", 45);
            }

            // ── STEP 2: Check .NET AppCAT ──
            Report(progress, "Checking .NET AppCAT tool...", 50);

            if (IsDotNetAppcatInstalled())
            {
                string path = FindDotNetAppcatPath();
                Report(progress,
                    $".NET AppCAT already installed!\n" +
                    $"   Path: {path}", 100);
                return;
            }

            // ── STEP 3: Install AppCAT ──
            Report(progress,
                "Installing dotnet-appcat global tool...", 55);

            await InstallDotNetAppcatToolInternalAsync(
                progress, ct);

            // Verify
            if (!IsDotNetAppcatInstalled())
                throw new Exception(
                    "dotnet-appcat command succeeded but tool " +
                    "is not visible. Restart this application.");

            string appcatPath = FindDotNetAppcatPath();
            Report(progress,
                $"AppCAT for .NET installed successfully!\n" +
                $"   Path: {appcatPath}", 100);
        }

        

        public static async Task InstallJavaAppcatAsync(
            IProgress<InstallationProgress> progress,
            CancellationToken ct = default)
        {
            // ── STEP 1: Check JDK ──
            Report(progress, "Checking Java JDK...", 2);

            bool jdkFound = IsJdkInstalled();

            if (jdkFound)
            {
                string ver = GetJdkVersion();
                string path = FindJdkPath();
                Report(progress,
                    $"JDK already installed: {ver}\n" +
                    $"   Path: {path}", 20);
            }
            else
            {
                Report(progress,
                    "JDK not found — installing JDK 17...", 5);
                await InstallJdk17InternalAsync(progress, ct);

                // Verify
                if (!IsJdkInstalled())
                    throw new Exception(
                        "JDK installation completed but 'java' " +
                        "is still not detected.\n" +
                        "Install manually from: " +
                        "https://learn.microsoft.com/java/openjdk/download\n" +
                        "Then restart this application.");

                Report(progress, "JDK 17 installed!", 35);
            }

            // ── STEP 2: Check Java AppCAT ──
            Report(progress,
                "Checking Java AppCAT CLI...", 38);

            if (IsJavaAppcatInstalled())
            {
                string path = FindJavaAppcatPath();
                Report(progress,
                    $"Java AppCAT already installed!\n" +
                    $"   Path: {path}", 100);
                return;
            }

            // ── STEP 3: Validate URL ──
            Report(progress,
                "Validating download URL...", 40);

            bool urlOk = await IsValidBinaryUrlAsync(
                JavaAppcatZipUrl, ct);

            if (!urlOk)
                throw new Exception(
                    "AppCAT download URL is not accessible.\n" +
                    $"URL: {JavaAppcatZipUrl}\n\n" +
                    "Download manually from:\n" +
                    "  https://azure.microsoft.com/products/app-cat\n" +
                    $"Extract to: {JavaExtractRoot}");

            // ── STEP 4: Download ZIP ──
            Report(progress,
                "Downloading AppCAT for Java...", 42);

            string zipPath = Path.Combine(Path.GetTempPath(),
                $"appcat-java-{Guid.NewGuid():N}.zip");

            try
            {
                await DownloadFileAsync(JavaAppcatZipUrl, zipPath,
                    "AppCAT for Java", 42, 72,
                    progress, ct);

                // ── STEP 5: Validate ZIP ──
                Report(progress, "Validating download...", 73);
                ValidateZipFile(zipPath);

                // ── STEP 6: Extract ──
                Report(progress, "Extracting...", 75);
                await ExtractZipAsync(zipPath, JavaExtractRoot,
                    progress, ct);
            }
            finally
            {
                SafeDelete(zipPath);
            }

            // ── STEP 7: Add to PATH ──
            Report(progress, "Configuring system PATH...", 90);

            string? appcatExe = FindJavaAppcatPath();
            if (!string.IsNullOrEmpty(appcatExe))
            {
                string? exeDir = Path.GetDirectoryName(appcatExe);
                if (!string.IsNullOrEmpty(exeDir))
                    await AddToPathAsync(exeDir);
            }

            await AddToPathAsync(JavaExtractRoot);

            string binDir = Path.Combine(JavaExtractRoot, "bin");
            if (Directory.Exists(binDir))
                await AddToPathAsync(binDir);

            await RefreshPathAsync();

            // ── STEP 8: Final verify ──
            if (!IsJavaAppcatInstalled())
            {
                string contents = ListDirectoryContents(
                    JavaExtractRoot);
                throw new Exception(
                    "Extraction completed but appcat " +
                    "executable not found.\n\n" +
                    $"Searched in: {JavaExtractRoot}\n" +
                    $"Contents:\n{contents}");
            }

            string finalPath = FindJavaAppcatPath();
            Report(progress,
                $"AppCAT for Java installed!\n" +
                $"   Path: {finalPath}", 100);
        }


        #region ── .NET SDK Detection ──

        private static bool IsDotNetSdkInstalled()
        {
            // METHOD 1: Check known directories
            foreach (string dir in DotNetSearchPaths)
            {
                string exe = Path.Combine(dir, "dotnet.exe");
                if (File.Exists(exe))
                    return true;
            }

            // METHOD 2: Try command
            try
            {
                var (exit, stdout, _) = RunQuick(
                    "dotnet", "--version");
                return exit == 0 &&
                       !string.IsNullOrWhiteSpace(stdout);
            }
            catch { return false; }
        }

        private static string GetDotNetSdkVersion()
        {
            try
            {
                var (exit, stdout, _) = RunQuick(
                    "dotnet", "--version");
                return exit == 0 ? stdout.Trim() : "unknown";
            }
            catch { return "unknown"; }
        }

        private static string FindDotNetSdkPath()
        {
            foreach (string dir in DotNetSearchPaths)
            {
                if (File.Exists(
                    Path.Combine(dir, "dotnet.exe")))
                    return dir;
            }
            return "C:\\Program Files\\dotnet";
        }

        #endregion

        #region ── .NET AppCAT Detection ──

        private static bool IsDotNetAppcatInstalled()
        {
            // METHOD 1: Check tools directory
            string toolExe = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                ".dotnet", "tools", "dotnet-appcat.exe");

            if (File.Exists(toolExe))
                return true;

            // METHOD 2: dotnet tool list
            try
            {
                var (exit, stdout, _) = RunQuick(
                    "dotnet", "tool list -g");
                return exit == 0 &&
                       stdout.Contains("dotnet-appcat",
                           StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static string FindDotNetAppcatPath()
        {
            string toolExe = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                ".dotnet", "tools", "dotnet-appcat.exe");

            return File.Exists(toolExe)
                ? toolExe
                : "dotnet-appcat";
        }

        #endregion

        #region ── JDK Detection (File System Search) ──

        private static bool IsJdkInstalled()
        {
            // ★ METHOD 1: Search file system FIRST ★
            // This is the key fix for re-download bug
            foreach (string root in JdkSearchRoots)
            {
                if (!Directory.Exists(root)) continue;

                try
                {
                    var jdkDirs = Directory.GetDirectories(
                        root, "jdk-*");

                    foreach (string jdkDir in jdkDirs)
                    {
                        string javaExe = Path.Combine(
                            jdkDir, "bin", "java.exe");
                        if (File.Exists(javaExe))
                            return true;
                    }
                }
                catch { }
            }

            // METHOD 2: Check JAVA_HOME
            string? javaHome = Environment
                .GetEnvironmentVariable("JAVA_HOME",
                    EnvironmentVariableTarget.Machine)
                ?? Environment.GetEnvironmentVariable(
                    "JAVA_HOME",
                    EnvironmentVariableTarget.User);

            if (!string.IsNullOrEmpty(javaHome) &&
                File.Exists(Path.Combine(
                    javaHome, "bin", "java.exe")))
                return true;

            // METHOD 3: Try java command
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                p.WaitForExit(15_000);
                return p.ExitCode == 0;
            }
            catch { return false; }
        }

        private static string GetJdkVersion()
        {
            // Try java -version (outputs to STDERR)
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null) return "unknown";
                p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError
                    .ReadToEnd().Trim();
                p.WaitForExit(10_000);
                if (p.ExitCode == 0 &&
                    !string.IsNullOrEmpty(stderr))
                    return stderr.Split('\n')[0].Trim();
            }
            catch { }

            // Fallback: find directory name
            string path = FindJdkPath();
            return !string.IsNullOrEmpty(path)
                ? Path.GetFileName(path)
                : "unknown";
        }

        private static string FindJdkPath()
        {
            foreach (string root in JdkSearchRoots)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    var dirs = Directory.GetDirectories(
                        root, "jdk-*");
                    foreach (string d in dirs)
                    {
                        if (File.Exists(
                            Path.Combine(d, "bin", "java.exe")))
                            return d;
                    }
                }
                catch { }
            }

            // Check JAVA_HOME
            string? jh = Environment.GetEnvironmentVariable(
                "JAVA_HOME",
                EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrEmpty(jh) &&
                Directory.Exists(jh))
                return jh;

            return "";
        }

        #endregion

        #region ── Java AppCAT Detection ──

        private static bool IsJavaAppcatInstalled()
        {
            string[] names =
            {
                "appcat.exe", "appcat.bat", "appcat"
            };

            string[] searchDirs =
            {
                JavaExtractRoot,
                Path.Combine(JavaExtractRoot, "bin"),
            };

            // Check known locations
            foreach (string dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (string name in names)
                {
                    if (File.Exists(Path.Combine(dir, name)))
                        return true;
                }
            }

            // Recursive search
            if (Directory.Exists(JavaExtractRoot))
            {
                try
                {
                    foreach (string name in names)
                    {
                        var found = Directory.GetFiles(
                            JavaExtractRoot, name,
                            SearchOption.AllDirectories);
                        if (found.Length > 0)
                            return true;
                    }
                }
                catch { }
            }

            return false;
        }

        private static string FindJavaAppcatPath()
        {
            if (!Directory.Exists(JavaExtractRoot))
                return "";

            string[] names =
            {
                "appcat.exe", "appcat.bat", "appcat"
            };

            try
            {
                foreach (string name in names)
                {
                    var found = Directory.GetFiles(
                        JavaExtractRoot, name,
                        SearchOption.AllDirectories);
                    if (found.Length > 0)
                        return found[0];
                }
            }
            catch { }

            return JavaExtractRoot;
        }

        #endregion


        #region ── .NET SDK Install ──

        private static async Task InstallDotNetSdkInternalAsync(
            IProgress<InstallationProgress> progress,
            CancellationToken ct)
        {
            // METHOD 1: winget
            Report(progress,
                "Trying winget install .NET SDK 8.0...", 8);

            bool ok = await RunProcessAsync("winget",
                "install Microsoft.DotNet.SDK.8 " +
                "--accept-source-agreements " +
                "--accept-package-agreements --silent",
                600_000, ct);

            if (ok)
            {
                await RefreshPathAsync();
                await Task.Delay(2_000, ct);
                if (IsDotNetSdkInstalled())
                {
                    Report(progress,
                        ".NET SDK installed via winget!", 40);
                    return;
                }
            }

            // METHOD 2: PowerShell script
            Report(progress,
                "Trying dotnet-install.ps1 script...", 20);

            string ps = @"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$tmp = Join-Path $env:TEMP 'dotnet-install.ps1'
Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $tmp -UseBasicParsing
& $tmp -Channel 8.0 -InstallDir 'C:\Program Files\dotnet'
Remove-Item $tmp -Force -ErrorAction SilentlyContinue
";
            await RunPowerShellAsync(ps, 600_000, ct, true);
            await RefreshPathAsync();
            await Task.Delay(3_000, ct);

            if (IsDotNetSdkInstalled())
            {
                Report(progress,
                    ".NET SDK installed via script!", 40);
                return;
            }

            throw new Exception(
                ".NET SDK 8.0 could not be installed.\n" +
                "Download: https://dotnet.microsoft.com/download/dotnet/8.0");
        }

        #endregion

        #region ── .NET AppCAT Tool Install ──

        private static async Task
            InstallDotNetAppcatToolInternalAsync(
            IProgress<InstallationProgress> progress,
            CancellationToken ct)
        {
            Report(progress,
                "Running: dotnet tool install -g dotnet-appcat...",
                60);

            var (exit, stdout, stderr) = await Task.Run(() =>
            {
                return RunQuick("dotnet",
                    "tool install -g " +
                    "--ignore-failed-sources dotnet-appcat",
                    300_000);
            }, ct);

            if (exit == 0)
            {
                await RefreshPathAsync();
                Report(progress,
                    "dotnet-appcat installed!", 90);
                return;
            }

            // Already installed?
            if (stderr.Contains("already installed",
                StringComparison.OrdinalIgnoreCase))
            {
                Report(progress,
                    "dotnet-appcat was already installed!", 90);
                return;
            }

            // Try update
            Report(progress,
                "Install failed — trying update...", 75);

            var (exit2, _, stderr2) = await Task.Run(() =>
            {
                return RunQuick("dotnet",
                    "tool update -g " +
                    "--ignore-failed-sources dotnet-appcat",
                    300_000);
            }, ct);

            if (exit2 == 0)
            {
                await RefreshPathAsync();
                Report(progress,
                    "dotnet-appcat updated!", 90);
                return;
            }

            throw new Exception(
                "dotnet-appcat install/update failed.\n" +
                $"Install error: {stderr}\n" +
                $"Update error: {stderr2}");
        }

        #endregion

        #region ── JDK 17 Install ──

        private static async Task InstallJdk17InternalAsync(
            IProgress<InstallationProgress> progress,
            CancellationToken ct)
        {
            // METHOD 1: winget
            Report(progress,
                "Trying winget install JDK 17...", 8);

            bool ok = await RunProcessAsync("winget",
                "install Microsoft.OpenJDK.17 " +
                "--accept-source-agreements " +
                "--accept-package-agreements --silent",
                600_000, ct);

            if (ok)
            {
                await RefreshPathAsync();
                await ConfigureJavaHomeAsync();
                await Task.Delay(3_000, ct);

                if (IsJdkInstalled())
                {
                    string path = FindJdkPath();
                    Report(progress,
                        $"JDK installed via winget!\n" +
                        $"   Path: {path}", 30);
                    return;
                }
            }

            // METHOD 2: Direct MSI download
            Report(progress,
                "Downloading JDK 17 MSI...", 12);

            string msiPath = Path.Combine(Path.GetTempPath(),
                $"ms-jdk17-{Guid.NewGuid():N}.msi");

            try
            {
                await DownloadFileAsync(Jdk17MsiUrl, msiPath,
                    "JDK 17 MSI", 12, 22,
                    progress, ct);

                // Validate size
                var fi = new FileInfo(msiPath);
                if (fi.Length < 5_000_000)
                    throw new Exception(
                        $"MSI too small ({fi.Length} bytes)");

                Report(progress,
                    "Installing JDK 17 (silent MSI)...", 24);

                await RunProcessAsync("msiexec.exe",
                    $"/i \"{msiPath}\" " +
                    "ADDLOCAL=FeatureMain,FeatureEnvironment," +
                    "FeatureJarFileRunWith,FeatureJavaHome " +
                    "INSTALLDIR=\"C:\\Program Files\\Microsoft\\jdk-17\" " +
                    "/quiet /norestart",
                    600_000, ct, asAdmin: true);

                await Task.Delay(3_000, ct);
                await RefreshPathAsync();
                await ConfigureJavaHomeAsync();

                // ★ Check file system — don't just rely on PATH
                if (IsJdkInstalled())
                {
                    string path = FindJdkPath();
                    Report(progress,
                        $"✅ JDK installed via MSI!\n" +
                        $"   Path: {path}", 30);
                    return;
                }

                // Force-search disk
                await ForceAddJdkToPathAsync();
                await RefreshPathAsync();

                if (IsJdkInstalled())
                {
                    Report(progress,
                        "✅ JDK installed (PATH fixed)!", 30);
                    return;
                }
            }
            finally
            {
                SafeDelete(msiPath);
            }

            throw new Exception(
                "JDK 17 could not be installed.\n" +
                "Download: https://learn.microsoft.com/java/openjdk/download");
        }

        #endregion

       

        #region ── URL Check ──

        private static async Task<bool> IsValidBinaryUrlAsync(
            string url, CancellationToken ct)
        {
            try
            {
                using var req = new HttpRequestMessage(
                    HttpMethod.Head, url);
                req.Headers.Add("User-Agent",
                    "AppCAT-Installer/2.0");

                using var resp = await _httpClient.SendAsync(
                    req, ct);

                if (!resp.IsSuccessStatusCode)
                    return false;

                // Reject Bing redirects
                string? finalUrl = resp.RequestMessage
                    ?.RequestUri?.ToString();
                if (finalUrl != null)
                {
                    string lower = finalUrl.ToLowerInvariant();
                    if (lower.Contains("bing.com") ||
                        lower.Contains("login.microsoftonline"))
                        return false;
                }

                // Reject HTML
                string ct_ = resp.Content.Headers
                    .ContentType?.MediaType ?? "";
                if (ct_.Contains("text/html",
                    StringComparison.OrdinalIgnoreCase))
                    return false;

                // Accept known binary types
                if (ct_.Contains("application/zip") ||
                    ct_.Contains("application/octet-stream"))
                    return true;

                // Check size
                long? len = resp.Content.Headers.ContentLength;
                return len.HasValue && len.Value > 1_000_000;
            }
            catch { return false; }
        }

        #endregion

      

        #region ── ZIP ──

        private static void ValidateZipFile(string path)
        {
            var fi = new FileInfo(path);

            if (fi.Length < 1_000)
            {
                string preview = File.ReadAllText(path)
                    [..Math.Min(500, (int)fi.Length)];
                throw new Exception(
                    $"File too small ({fi.Length} bytes).\n" +
                    $"Content:\n{preview}");
            }

            // ZIP magic bytes: 50 4B 03 04
            byte[] hdr = new byte[4];
            using (var fs = File.OpenRead(path))
                fs.Read(hdr, 0, 4);

            if (hdr[0] != 0x50 || hdr[1] != 0x4B ||
                hdr[2] != 0x03 || hdr[3] != 0x04)
            {
                string hex = string.Join(" ",
                    hdr.Select(b => $"0x{b:X2}"));

                if (hdr[0] == (byte)'<')
                {
                    string html = File.ReadAllText(path)
                        [..Math.Min(300, (int)fi.Length)];
                    throw new Exception(
                        $"Got HTML instead of ZIP:\n{html}");
                }

                throw new Exception(
                    $"Not a ZIP file. Header: {hex}");
            }

            // Verify integrity
            try
            {
                using var zip = ZipFile.OpenRead(path);
                if (zip.Entries.Count == 0)
                    throw new Exception("ZIP is empty.");
            }
            catch (InvalidDataException ex)
            {
                throw new Exception(
                    $"ZIP corrupt ({fi.Length / 1024} KB): " +
                    ex.Message, ex);
            }
        }

        private static Task ExtractZipAsync(
            string zipPath, string target,
            IProgress<InstallationProgress> progress,
            CancellationToken ct)
        {
            return Task.Run(() =>
            {
                if (Directory.Exists(target))
                    Directory.Delete(target, true);

                Directory.CreateDirectory(target);

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    int total = archive.Entries.Count;
                    int done = 0;

                    foreach (var entry in archive.Entries)
                    {
                        ct.ThrowIfCancellationRequested();

                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        string dest = Path.GetFullPath(
                            Path.Combine(target, entry.FullName));

                        // Zip-slip protection
                        if (!dest.StartsWith(
                            Path.GetFullPath(target),
                            StringComparison.OrdinalIgnoreCase))
                            continue;

                        string? dir = Path.GetDirectoryName(dest);
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);

                        entry.ExtractToFile(dest, true);

                        done++;
                        if (done % 50 == 0 || done == total)
                        {
                            int pct = 75 +
                                (int)(13.0 * done / total);
                            Report(progress,
                                $"Extracting {done}/{total} files...",
                                Math.Min(pct, 88));
                        }
                    }
                }

                FlattenSingleSubfolder(target);

            }, ct);
        }

        private static void FlattenSingleSubfolder(string root)
        {
            var dirs = Directory.GetDirectories(root);
            var files = Directory.GetFiles(root);

            if (dirs.Length != 1 || files.Length != 0)
                return;

            string inner = dirs[0];

            foreach (string f in Directory.GetFiles(inner))
                File.Move(f,
                    Path.Combine(root, Path.GetFileName(f)),
                    true);

            foreach (string d in Directory
                .GetDirectories(inner))
            {
                string dest = Path.Combine(
                    root, Path.GetFileName(d));
                if (Directory.Exists(dest))
                    Directory.Delete(dest, true);
                Directory.Move(d, dest);
            }

            Directory.Delete(inner, true);
            FlattenSingleSubfolder(root);
        }

        #endregion

       

        #region ── Download ──

        private static async Task DownloadFileAsync(
            string url, string dest, string label,
            int pctStart, int pctEnd,
            IProgress<InstallationProgress> progress,
            CancellationToken ct, int retries = 3)
        {
            Exception? last = null;

            for (int i = 1; i <= retries; i++)
            {
                try
                {
                    if (i > 1)
                    {
                        Report(progress,
                            $"{label} — retry {i}/{retries}...",
                            pctStart);
                        await Task.Delay(2_000 * i, ct);
                    }

                    using var req = new HttpRequestMessage(
                        HttpMethod.Get, url);
                    req.Headers.Add("User-Agent",
                        "AppCAT-Installer/2.0");

                    using var resp = await _httpClient.SendAsync(
                        req,
                        HttpCompletionOption.ResponseHeadersRead,
                        ct);

                    // Reject Bing redirect
                    string? finalUrl = resp.RequestMessage
                        ?.RequestUri?.ToString();
                    if (finalUrl?.Contains("bing.com") == true)
                        throw new Exception(
                            $"URL redirected to Bing: {finalUrl}");

                    // Reject HTML
                    string contentType = resp.Content.Headers
                        .ContentType?.MediaType ?? "";
                    if (contentType.Contains("text/html"))
                    {
                        string body = await resp.Content
                            .ReadAsStringAsync(ct);
                        throw new Exception(
                            $"Got HTML:\n" +
                            body[..Math.Min(300, body.Length)]);
                    }

                    resp.EnsureSuccessStatusCode();

                    long total = resp.Content.Headers
                        .ContentLength ?? -1;

                    await using var src = await resp.Content
                        .ReadAsStreamAsync(ct);
                    await using var dst = new FileStream(
                        dest, FileMode.Create,
                        FileAccess.Write, FileShare.None,
                        81_920);

                    byte[] buf = new byte[81_920];
                    long down = 0;
                    int read;
                    var tick = DateTime.MinValue;

                    while ((read = await src.ReadAsync(
                        buf.AsMemory(0, buf.Length), ct)) > 0)
                    {
                        await dst.WriteAsync(
                            buf.AsMemory(0, read), ct);
                        down += read;

                        if ((DateTime.Now - tick)
                            .TotalMilliseconds < 400)
                            continue;
                        tick = DateTime.Now;

                        int pct = total > 0
                            ? pctStart + (int)((pctEnd - pctStart)
                                * down / (double)total)
                            : (pctStart + pctEnd) / 2;

                        string info = total > 0
                            ? $"{down / 1_048_576}/{total / 1_048_576} MB"
                            : $"{down / 1_048_576} MB";

                        Report(progress,
                            $"{label}... {info}",
                            Math.Min(pct, pctEnd));
                    }

                    if (total > 0 && down < total)
                        throw new IOException(
                            $"Incomplete: {down}/{total}");

                    Report(progress,
                        $"{label} downloaded ✔",
                        pctEnd);
                    return;
                }
                catch (Exception ex) when (
                    (ex is HttpRequestException or IOException)
                    && i < retries)
                {
                    last = ex;
                    SafeDelete(dest);
                }
            }

            throw new Exception(
                $"{label} failed: {last?.Message}", last);
        }

        #endregion

        

        #region ── Helpers ──

        private static (int exit, string stdout, string stderr)
            RunQuick(string file, string args,
                int timeoutMs = 30_000)
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)
                ?? throw new Exception(
                    $"Cannot start {file}");
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit(timeoutMs);
            return (p.ExitCode, stdout, stderr);
        }

        private static async Task<bool> RunProcessAsync(
            string file, string args, int timeout,
            CancellationToken ct, bool asAdmin = false)
        {
            try
            {
                return await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = file,
                        Arguments = args,
                        CreateNoWindow = true
                    };

                    if (asAdmin)
                    {
                        psi.UseShellExecute = true;
                        psi.Verb = "runas";
                    }
                    else
                    {
                        psi.UseShellExecute = false;
                        psi.RedirectStandardOutput = true;
                        psi.RedirectStandardError = true;
                    }

                    using var p = Process.Start(psi);
                    if (p == null) return false;

                    if (!asAdmin)
                    {
                        p.StandardOutput.ReadToEnd();
                        p.StandardError.ReadToEnd();
                    }

                    bool exited = p.WaitForExit(timeout);
                    if (!exited)
                    {
                        try { p.Kill(true); } catch { }
                        return false;
                    }

                    return p.ExitCode is 0 or 3010;
                }, ct);
            }
            catch { return false; }
        }

        private static async Task<bool> RunPowerShellAsync(
            string script, int timeout,
            CancellationToken ct, bool admin)
        {
            string tmp = Path.Combine(Path.GetTempPath(),
                $"ps-{Guid.NewGuid():N}.ps1");
            await File.WriteAllTextAsync(tmp, script, ct);

            try
            {
                return await RunProcessAsync("powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass " +
                    $"-File \"{tmp}\"",
                    timeout, ct, admin);
            }
            finally { SafeDelete(tmp); }
        }

        private static Task RefreshPathAsync() => Task.Run(() =>
        {
            try
            {
                string m = Environment.GetEnvironmentVariable(
                    "Path",
                    EnvironmentVariableTarget.Machine) ?? "";
                string u = Environment.GetEnvironmentVariable(
                    "Path",
                    EnvironmentVariableTarget.User) ?? "";
                Environment.SetEnvironmentVariable("Path",
                    $"{m};{u}",
                    EnvironmentVariableTarget.Process);

                string? jh = Environment.GetEnvironmentVariable(
                    "JAVA_HOME",
                    EnvironmentVariableTarget.Machine);
                if (!string.IsNullOrEmpty(jh))
                    Environment.SetEnvironmentVariable(
                        "JAVA_HOME", jh,
                        EnvironmentVariableTarget.Process);
            }
            catch { }
        });

        private static Task ConfigureJavaHomeAsync() =>
            Task.Run(() =>
            {
                try
                {
                    string? current = Environment
                        .GetEnvironmentVariable("JAVA_HOME",
                            EnvironmentVariableTarget.Machine);
                    if (!string.IsNullOrEmpty(current) &&
                        Directory.Exists(current))
                        return;

                    foreach (string root in JdkSearchRoots)
                    {
                        if (!Directory.Exists(root)) continue;
                        var dirs = Directory.GetDirectories(
                            root, "jdk-17*");
                        foreach (string d in dirs)
                        {
                            if (!File.Exists(
                                Path.Combine(d, "bin", "java.exe")))
                                continue;

                            Environment.SetEnvironmentVariable(
                                "JAVA_HOME", d,
                                EnvironmentVariableTarget.Machine);

                            string bin = Path.Combine(d, "bin");
                            string path = Environment
                                .GetEnvironmentVariable("Path",
                                    EnvironmentVariableTarget.Machine)
                                ?? "";
                            if (!path.Contains(bin,
                                StringComparison.OrdinalIgnoreCase))
                                Environment.SetEnvironmentVariable(
                                    "Path", $"{path};{bin}",
                                    EnvironmentVariableTarget.Machine);
                            return;
                        }
                    }
                }
                catch { }
            });

        private static Task ForceAddJdkToPathAsync() =>
            Task.Run(() =>
            {
                try
                {
                    foreach (string root in JdkSearchRoots)
                    {
                        if (!Directory.Exists(root)) continue;
                        var dirs = Directory.GetDirectories(
                            root, "jdk-*");
                        foreach (string d in dirs)
                        {
                            string bin = Path.Combine(d, "bin");
                            if (!File.Exists(
                                Path.Combine(bin, "java.exe")))
                                continue;

                            Environment.SetEnvironmentVariable(
                                "JAVA_HOME", d,
                                EnvironmentVariableTarget.Machine);

                            string path = Environment
                                .GetEnvironmentVariable("Path",
                                    EnvironmentVariableTarget.Machine)
                                ?? "";
                            if (!path.Contains(bin,
                                StringComparison.OrdinalIgnoreCase))
                                Environment.SetEnvironmentVariable(
                                    "Path", $"{path};{bin}",
                                    EnvironmentVariableTarget.Machine);
                            return;
                        }
                    }
                }
                catch { }
            });

        private static Task AddToPathAsync(string dir) =>
            Task.Run(() =>
            {
                try
                {
                    string path = Environment
                        .GetEnvironmentVariable("Path",
                            EnvironmentVariableTarget.Machine)
                        ?? "";
                    if (!path.Contains(dir,
                        StringComparison.OrdinalIgnoreCase))
                        Environment.SetEnvironmentVariable("Path",
                            $"{path};{dir}",
                            EnvironmentVariableTarget.Machine);
                }
                catch { }
            });

        private static void SafeDelete(string p)
        {
            try
            {
                if (File.Exists(p)) File.Delete(p);
            }
            catch { }
        }

        private static void Report(
            IProgress<InstallationProgress> p,
            string msg, int pct)
        {
            p.Report(new InstallationProgress
            {
                Message = msg,
                Percentage = pct
            });
        }

        private static string ListDirectoryContents(
            string dir)
        {
            if (!Directory.Exists(dir))
                return "(directory not found)";

            try
            {
                var entries = Directory
                    .EnumerateFileSystemEntries(
                        dir, "*",
                        SearchOption.AllDirectories)
                    .Take(30);
                return string.Join("\n  ", entries);
            }
            catch
            {
                return "(could not list)";
            }
        }

        #endregion
    }
}