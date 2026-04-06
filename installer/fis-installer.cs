// Feel It.Stream - Installer & Background Injector
// Single exe: GUI installer (default) or silent injector (--inject).
//
// Compile:
//   csc.exe /nologo /target:winexe /out:FISInstaller.exe fis-installer.cs
//
// Usage:
//   FISInstaller.exe            -> GUI installer
//   FISInstaller.exe --inject   -> Silent background injector

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// ============================================================
//  Entry Point
// ============================================================

class Program
{
    // System DPI aware — WinForms scales controls automatically
    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--inject")
            {
                Injector.Run();
                return;
            }
        }

        try { SetProcessDPIAware(); } catch { }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm());
    }
}

// ============================================================
//  Constants & Paths
// ============================================================

static class FISPaths
{
    public const string ENV_VAR_NAME = "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS";
    public const string ENV_VAR_VALUE = "--remote-debugging-port=9222";
    public const string BUNDLE_FILENAME = "feelit.bundle.js";
    public const string STREMIO_PROCESS = "stremio-shell-ng";
    public const string INJECTOR_MUTEX = "FISInjectorMutex_v1";
    public const string BUNDLE_URL = "https://feel-it.stream/stremio/feelit.bundle.js";
    public const string VERSION_URL = "https://feel-it.stream/stremio/version.json";
    public const int CDP_PORT = 9222;
    public const string VERSION = "1.0.0";

    public static string FisDir
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FIS");
        }
    }

    public static string BundlePath
    {
        get { return Path.Combine(FisDir, BUNDLE_FILENAME); }
    }

    public static string LogPath
    {
        get { return Path.Combine(FisDir, "fis.log"); }
    }

    public static string StremioDir
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Stremio");
        }
    }

    public static string StremioExe
    {
        get { return Path.Combine(StremioDir, "stremio-shell-ng.exe"); }
    }

    public static string StartupShortcut
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                "FIS Injector.lnk");
        }
    }

    public static string ThisExe
    {
        get { return Assembly.GetExecutingAssembly().Location; }
    }
}

// ============================================================
//  Logger
// ============================================================

static class Logger
{
    public static void Log(string msg)
    {
        string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + msg;
        try
        {
            string dir = Path.GetDirectoryName(FISPaths.LogPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.AppendAllText(FISPaths.LogPath, line + Environment.NewLine);
        }
        catch { }
    }
}

// ============================================================
//  Status Checker
// ============================================================

static class StatusChecker
{
    public static bool IsStremioInstalled()
    {
        return File.Exists(FISPaths.StremioExe);
    }

    public static bool IsBundleInstalled()
    {
        return File.Exists(FISPaths.BundlePath);
    }

    public static bool IsEnvVarSet()
    {
        // CDP is now managed per-process by the injector (not via global env var).
        // A global var actually causes conflicts with other WebView2 apps.
        // Return true if injector is running (it handles CDP), false otherwise.
        return IsInjectorRunning();
    }

    public static bool HasLegacyGlobalVar()
    {
        string val = Environment.GetEnvironmentVariable(
            FISPaths.ENV_VAR_NAME, EnvironmentVariableTarget.User);
        return val != null && val.Contains("--remote-debugging-port");
    }

    public static bool IsStartupShortcutPresent()
    {
        return File.Exists(FISPaths.StartupShortcut);
    }

    public static bool IsInjectorRunning()
    {
        bool created;
        try
        {
            Mutex m = new Mutex(false, FISPaths.INJECTOR_MUTEX, out created);
            if (created)
            {
                m.ReleaseMutex();
                m.Dispose();
                return false;
            }
            m.Dispose();
            return true;
        }
        catch { return false; }
    }

    public static bool IsStremioRunning()
    {
        try
        {
            Process[] procs = Process.GetProcessesByName(FISPaths.STREMIO_PROCESS);
            return procs.Length > 0;
        }
        catch { return false; }
    }

    public static string GetBundleVersion()
    {
        if (!IsBundleInstalled()) return null;
        try
        {
            FileInfo fi = new FileInfo(FISPaths.BundlePath);
            return (fi.Length / 1024) + " KB";
        }
        catch { return "?"; }
    }
}

// ============================================================
//  Installer Operations
// ============================================================

static class InstallerOps
{
    public static void Install(Action<string, bool> progress)
    {
        // 1. Check Stremio
        progress("Checking Stremio...", false);
        if (!StatusChecker.IsStremioInstalled())
        {
            progress("ERROR: Stremio is not installed!", true);
            return;
        }
        progress("  Stremio found", false);

        // 2. Create FIS directory
        progress("Creating FIS directory...", false);
        try
        {
            if (!Directory.Exists(FISPaths.FisDir))
                Directory.CreateDirectory(FISPaths.FisDir);
            progress("  " + FISPaths.FisDir, false);
        }
        catch (Exception ex)
        {
            progress("ERROR: " + ex.Message, true);
            return;
        }

        // 3. Download bundle from server (fallback: local file next to exe)
        progress("Downloading bundle...", false);
        bool bundleOk = false;
        try
        {
            // Force TLS 1.2 (.NET 4.x defaults to TLS 1.0 which modern servers reject)
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (WebClient wc = new WebClient())
            {
                wc.Proxy = null;
                wc.Headers.Add("User-Agent", "FISInstaller/" + FISPaths.VERSION);
                wc.DownloadFile(FISPaths.BUNDLE_URL, FISPaths.BundlePath);
            }
            long kb = new FileInfo(FISPaths.BundlePath).Length / 1024;
            progress("  Bundle downloaded (" + kb + " KB)", false);
            bundleOk = true;
        }
        catch (Exception ex)
        {
            progress("  Download failed: " + ex.Message, true);
        }

        if (!bundleOk)
        {
            // Fallback: local file next to exe
            progress("Searching for local bundle...", false);
            string localBundle = Path.Combine(
                Path.GetDirectoryName(FISPaths.ThisExe), FISPaths.BUNDLE_FILENAME);
            if (File.Exists(localBundle))
            {
                File.Copy(localBundle, FISPaths.BundlePath, true);
                long kb = new FileInfo(FISPaths.BundlePath).Length / 1024;
                progress("  Local bundle copied (" + kb + " KB)", false);
                bundleOk = true;
            }
            else if (StatusChecker.IsBundleInstalled())
            {
                progress("  Existing bundle kept", false);
                bundleOk = true;
            }
            else
            {
                progress("ERROR: Failed to retrieve the bundle!", true);
                progress("  Check your connection or place " + FISPaths.BUNDLE_FILENAME + " next to the exe.", true);
                return;
            }
        }

        // 5. Clean up global CDP variable (now handled per-process by injector)
        // Setting this globally causes port conflicts: ALL WebView2 apps (Windows widgets,
        // MSN feed, etc.) try to bind the same CDP port, blocking Stremio.
        progress("Configuring CDP...", false);
        try
        {
            string existing = Environment.GetEnvironmentVariable(
                FISPaths.ENV_VAR_NAME, EnvironmentVariableTarget.User);
            if (existing != null && existing.Contains("--remote-debugging-port"))
            {
                Environment.SetEnvironmentVariable(
                    FISPaths.ENV_VAR_NAME, null, EnvironmentVariableTarget.User);
                progress("  Removed global CDP variable (prevents conflicts)", false);
            }
            progress("  CDP managed per-process by injector", false);
        }
        catch (Exception ex)
        {
            progress("WARNING: " + ex.Message, false);
            // Non-critical — injector handles CDP per-process regardless
        }

        // 6. Create startup shortcut
        progress("Creating startup shortcut...", false);
        try
        {
            ShortcutHelper.Create(
                FISPaths.StartupShortcut,
                FISPaths.ThisExe,
                "--inject",
                "Feel It Stream - Background Injector");
            progress("  Auto-start enabled", false);
        }
        catch (Exception ex)
        {
            progress("WARNING shortcut: " + ex.Message, true);
            // Non-critical, continue
        }

        // 7. Kill existing injector and launch fresh
        progress("Starting injector...", false);
        KillInjector();
        Thread.Sleep(500);
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = FISPaths.ThisExe;
            psi.Arguments = "--inject";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            Process.Start(psi);
            progress("  Injector started in background", false);
        }
        catch (Exception ex)
        {
            progress("WARNING injector: " + ex.Message, true);
        }

        progress("", false);
        progress("Installation complete!", false);
    }

    public static void Uninstall(Action<string, bool> progress)
    {
        // 1. Kill injector
        progress("Stopping injector...", false);
        KillInjector();
        progress("  OK", false);

        // 2. Remove env var
        progress("Removing CDP variable...", false);
        try
        {
            Environment.SetEnvironmentVariable(
                FISPaths.ENV_VAR_NAME, null, EnvironmentVariableTarget.User);
            progress("  Variable removed", false);
        }
        catch (Exception ex)
        {
            progress("WARNING: " + ex.Message, true);
        }

        // 3. Remove startup shortcut
        progress("Removing startup shortcut...", false);
        try
        {
            if (File.Exists(FISPaths.StartupShortcut))
                File.Delete(FISPaths.StartupShortcut);
            progress("  Shortcut removed", false);
        }
        catch (Exception ex)
        {
            progress("WARNING: " + ex.Message, true);
        }

        // 4. Delete FIS directory
        progress("Cleaning up files...", false);
        try
        {
            if (Directory.Exists(FISPaths.FisDir))
                Directory.Delete(FISPaths.FisDir, true);
            progress("  FIS directory removed", false);
        }
        catch (Exception ex)
        {
            progress("WARNING: " + ex.Message, true);
            progress("  Some files may be locked", true);
        }

        progress("", false);
        progress("Uninstall complete!", false);
    }

    public static void KillInjector()
    {
        try
        {
            string thisName = Path.GetFileNameWithoutExtension(FISPaths.ThisExe);
            int myPid = Process.GetCurrentProcess().Id;
            Process[] procs = Process.GetProcessesByName(thisName);
            for (int i = 0; i < procs.Length; i++)
            {
                if (procs[i].Id != myPid)
                {
                    try { procs[i].Kill(); } catch { }
                }
            }
            // Also kill old fis-injector if still around
            Process[] old = Process.GetProcessesByName("fis-injector");
            for (int i = 0; i < old.Length; i++)
            {
                try { old[i].Kill(); } catch { }
            }
        }
        catch { }
    }
}

// ============================================================
//  Shortcut Helper (COM interop)
// ============================================================

static class ShortcutHelper
{
    public static void Create(string lnkPath, string target, string args, string description)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(shellType);
        object sc = shellType.InvokeMember("CreateShortcut",
            BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
        Type scType = sc.GetType();

        scType.InvokeMember("TargetPath",
            BindingFlags.SetProperty, null, sc, new object[] { target });
        scType.InvokeMember("Arguments",
            BindingFlags.SetProperty, null, sc, new object[] { args });
        scType.InvokeMember("WorkingDirectory",
            BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(target) });
        scType.InvokeMember("Description",
            BindingFlags.SetProperty, null, sc, new object[] { description });
        scType.InvokeMember("WindowStyle",
            BindingFlags.SetProperty, null, sc, new object[] { 7 }); // Minimized
        scType.InvokeMember("Save",
            BindingFlags.InvokeMethod, null, sc, null);
    }

    public static void Delete(string lnkPath)
    {
        if (File.Exists(lnkPath))
            File.Delete(lnkPath);
    }
}

// ============================================================
//  Auto-Updater
// ============================================================

static class Updater
{
    /// Returns the download URL if a newer installer is available, null otherwise.
    public static string CheckForUpdate()
    {
        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (WebClient wc = new WebClient())
            {
                wc.Proxy = null;
                wc.Headers.Add("User-Agent", "FISInstaller/" + FISPaths.VERSION);
                string json = wc.DownloadString(FISPaths.VERSION_URL);
                string remoteVer = ExtractValue(json, "installer_version");
                if (remoteVer != null && IsNewer(remoteVer, FISPaths.VERSION))
                {
                    string url = ExtractValue(json, "installer_url");
                    return url;
                }
            }
        }
        catch { }
        return null;
    }

    /// Downloads the new exe and launches a batch script to replace the current one.
    public static void SelfUpdate(string downloadUrl, Action<string, bool> progress)
    {
        string tempExe = Path.Combine(Path.GetTempPath(), "FISInstaller_update.exe");
        string batchPath = Path.Combine(Path.GetTempPath(), "fis-update.cmd");
        string originalExe = FISPaths.ThisExe;

        progress("Downloading update...", false);
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        using (WebClient wc = new WebClient())
        {
            wc.Proxy = null;
            wc.Headers.Add("User-Agent", "FISInstaller/" + FISPaths.VERSION);
            wc.DownloadFile(downloadUrl, tempExe);
        }
        progress("  Downloaded", false);

        // Batch script waits for this process to exit, replaces the exe, relaunches
        string batch =
            "@echo off\r\n" +
            "timeout /t 2 /nobreak >nul\r\n" +
            "copy /Y \"" + tempExe + "\" \"" + originalExe + "\"\r\n" +
            "start \"\" \"" + originalExe + "\"\r\n" +
            "del \"" + tempExe + "\"\r\n" +
            "del \"%~f0\"\r\n";
        File.WriteAllText(batchPath, batch);

        progress("Restarting...", false);
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = batchPath;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        Process.Start(psi);
    }

    static bool IsNewer(string remote, string local)
    {
        string[] r = remote.Split('.');
        string[] l = local.Split('.');
        int len = Math.Max(r.Length, l.Length);
        for (int i = 0; i < len; i++)
        {
            int rv = i < r.Length ? int.Parse(r[i]) : 0;
            int lv = i < l.Length ? int.Parse(l[i]) : 0;
            if (rv > lv) return true;
            if (rv < lv) return false;
        }
        return false;
    }

    static string ExtractValue(string json, string key)
    {
        string search = "\"" + key + "\"";
        int idx = json.IndexOf(search);
        if (idx == -1) return null;
        int colon = json.IndexOf(':', idx + search.Length);
        if (colon == -1) return null;
        int start = json.IndexOf('"', colon + 1);
        if (start == -1) return null;
        int end = json.IndexOf('"', start + 1);
        if (end == -1) return null;
        return json.Substring(start + 1, end - start - 1);
    }
}

// ============================================================
//  Background Injector (--inject mode)
// ============================================================

static class Injector
{
    public static void Run()
    {
        Logger.Log("[FIS] Injector starting (background mode)...");

        bool created;
        Mutex mutex;
        try
        {
            mutex = new Mutex(true, FISPaths.INJECTOR_MUTEX, out created);
        }
        catch
        {
            Logger.Log("[FIS] Failed to acquire mutex, exiting.");
            return;
        }

        if (!created)
        {
            Logger.Log("[FIS] Another injector is already running, exiting.");
            return;
        }

        try
        {
            MonitorLoop();
        }
        finally
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }

    // The CDP port actually in use (may differ from CDP_PORT if 9222 is occupied)
    static int activeCdpPort = FISPaths.CDP_PORT;

    static void MonitorLoop()
    {
        // Clean up any lingering global CDP env var from a previous crash
        CleanupGlobalCdpVar();

        int lastPid = -1;
        string lastPageId = null;
        bool waitingForRestart = false;
        int restartRetries = 0;

        while (true)
        {
            try
            {
                Process stremio = FindStremioProcess();

                if (stremio == null)
                {
                    if (lastPid != -1)
                    {
                        Logger.Log("[FIS] Stremio closed (was PID " + lastPid + ")");
                        lastPid = -1;
                        lastPageId = null;
                        waitingForRestart = false;
                        restartRetries = 0;
                    }
                    Thread.Sleep(3000);
                    continue;
                }

                if (stremio.Id != lastPid)
                {
                    lastPid = stremio.Id;
                    lastPageId = null;
                    Logger.Log("[FIS] Stremio detected (PID " + stremio.Id + ")");

                    string wsUrl = null;

                    if (waitingForRestart)
                    {
                        // We just restarted Stremio with CDP — wait on the chosen port
                        wsUrl = WaitForCDP(stremio, out lastPageId);
                    }
                    else
                    {
                        // Fresh detection — quick scan all ports (fast: ~1-2s)
                        Thread.Sleep(3000);
                        wsUrl = ScanForStremioCDP(out lastPageId);
                    }

                    if (wsUrl != null)
                    {
                        // CDP found — inject!
                        waitingForRestart = false;
                        restartRetries = 0;
                        CleanupGlobalCdpVar();
                        Thread.Sleep(2000);

                        if (!File.Exists(FISPaths.BundlePath))
                        {
                            Logger.Log("[FIS] Bundle not found: " + FISPaths.BundlePath);
                            Thread.Sleep(3000);
                            continue;
                        }

                        string bundle = File.ReadAllText(FISPaths.BundlePath, Encoding.UTF8);
                        Logger.Log("[FIS] Bundle loaded: " + (bundle.Length / 1024) + " KB");

                        bool ok = CdpClient.InjectBundle(wsUrl, bundle);
                        Logger.Log("[FIS] Injection: " + (ok ? "OK" : "FAILED"));
                    }
                    else if (restartRetries < 3)
                    {
                        // No CDP — restart Stremio with a free CDP port
                        restartRetries++;
                        waitingForRestart = true;
                        Logger.Log("[FIS] No Stremio CDP (attempt " + restartRetries + "/3), restarting...");
                        RestartStremioWithCDP();
                        lastPid = -1;
                        Thread.Sleep(5000);
                        continue;
                    }
                    else
                    {
                        Logger.Log("[FIS] CDP failed after 3 restarts, will retry later...");
                        waitingForRestart = false;
                        restartRetries = 0;
                        CleanupGlobalCdpVar();
                        lastPid = -1;
                        Thread.Sleep(30000);
                        continue;
                    }
                }
                else
                {
                    // Same PID — check for page changes (re-injection on navigation)
                    string currentPageId;
                    string currentWsUrl = CdpClient.FindStremioPage(activeCdpPort, out currentPageId);
                    if (currentWsUrl != null && currentPageId != null && currentPageId != lastPageId)
                    {
                        Logger.Log("[FIS] Page changed (" + lastPageId + " -> " + currentPageId + ")");
                        lastPageId = currentPageId;
                        Thread.Sleep(3000);

                        string bundle = File.ReadAllText(FISPaths.BundlePath, Encoding.UTF8);
                        bool ok = CdpClient.InjectBundle(currentWsUrl, bundle);
                        Logger.Log("[FIS] Re-injection: " + (ok ? "OK" : "FAILED"));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("[FIS] Monitor error: " + ex.Message);
            }

            Thread.Sleep(3000);
        }
    }

    /// Quick scan: try to find Stremio's CDP on any port in the range 9222-9231.
    /// Pre-filters with IsPortListening to skip closed ports instantly.
    static string ScanForStremioCDP(out string pageId)
    {
        pageId = null;
        for (int port = FISPaths.CDP_PORT; port < FISPaths.CDP_PORT + 10; port++)
        {
            if (!IsPortListening(port)) continue;

            string wsUrl = CdpClient.FindStremioPage(port, out pageId);
            if (wsUrl != null)
            {
                activeCdpPort = port;
                Logger.Log("[FIS] Found Stremio CDP on port " + port);
                return wsUrl;
            }
        }
        Logger.Log("[FIS] No Stremio CDP on ports " + FISPaths.CDP_PORT + "-" + (FISPaths.CDP_PORT + 9));
        return null;
    }

    static void RestartStremioWithCDP()
    {
        // Kill current Stremio (it launched without CDP or with port conflict)
        try
        {
            Process[] procs = Process.GetProcessesByName(FISPaths.STREMIO_PROCESS);
            for (int i = 0; i < procs.Length; i++)
            {
                try { procs[i].Kill(); } catch { }
            }
        }
        catch { }

        Thread.Sleep(2000);

        // Find a free port — avoids conflicts with Windows widgets / other WebView2 apps
        // that may have grabbed port 9222 via the old global env var.
        activeCdpPort = FindAvailablePort(FISPaths.CDP_PORT);
        string envValue = "--remote-debugging-port=" + activeCdpPort;

        // Set the env var at USER scope temporarily. This is necessary because
        // stremio-shell-ng may internally respawn itself (single-instance check,
        // auto-update), and the new process reads env from the user scope, not
        // from our process tree. We remove this var once CDP connects.
        try
        {
            Environment.SetEnvironmentVariable(FISPaths.ENV_VAR_NAME, envValue, EnvironmentVariableTarget.User);
            Logger.Log("[FIS] Temporary global CDP env var set (port " + activeCdpPort + ")");
        }
        catch (Exception ex)
        {
            Logger.Log("[FIS] WARNING: Could not set user env var: " + ex.Message);
        }

        // Also set in current process for direct child inheritance
        Environment.SetEnvironmentVariable(FISPaths.ENV_VAR_NAME, envValue);

        // Relaunch Stremio
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = FISPaths.StremioExe;
            psi.UseShellExecute = false;
            Process.Start(psi);
            Logger.Log("[FIS] Stremio relaunched with CDP on port " + activeCdpPort);
        }
        catch (Exception ex)
        {
            Logger.Log("[FIS] Failed to relaunch Stremio: " + ex.Message);
        }
    }

    /// Removes the temporary user-scope CDP env var to prevent other WebView2 apps
    /// from picking it up. Called after CDP connects or on injector startup.
    static void CleanupGlobalCdpVar()
    {
        try
        {
            string val = Environment.GetEnvironmentVariable(
                FISPaths.ENV_VAR_NAME, EnvironmentVariableTarget.User);
            if (val != null && val.Contains("--remote-debugging-port"))
            {
                Environment.SetEnvironmentVariable(
                    FISPaths.ENV_VAR_NAME, null, EnvironmentVariableTarget.User);
                Logger.Log("[FIS] Cleaned up global CDP env var");
            }
        }
        catch { }
    }

    /// Finds the first available port starting from the preferred port.
    /// Scans up to 10 ports to avoid conflicts with other WebView2 apps.
    static int FindAvailablePort(int preferred)
    {
        for (int port = preferred; port < preferred + 10; port++)
        {
            if (!IsPortListening(port))
                return port;
            Logger.Log("[FIS] Port " + port + " is occupied, trying next...");
        }
        // Extremely unlikely: all 10 ports taken. Fall back to preferred.
        Logger.Log("[FIS] WARNING: All ports " + preferred + "-" + (preferred + 9) + " occupied!");
        return preferred;
    }

    /// Checks if anything is listening on the given port (localhost only, fast).
    static bool IsPortListening(int port)
    {
        try
        {
            using (TcpClient client = new TcpClient())
            {
                IAsyncResult ar = client.BeginConnect("127.0.0.1", port, null, null);
                bool connected = ar.AsyncWaitHandle.WaitOne(500);
                if (connected)
                {
                    try { client.EndConnect(ar); } catch { }
                    return true;
                }
                return false;
            }
        }
        catch { return false; }
    }

    static Process FindStremioProcess()
    {
        try
        {
            Process[] procs = Process.GetProcessesByName(FISPaths.STREMIO_PROCESS);
            if (procs.Length > 0) return procs[0];
        }
        catch { }
        return null;
    }

    static string WaitForCDP(Process stremio, out string pageId)
    {
        pageId = null;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            if (stremio.HasExited) return null;
            Thread.Sleep(1000);

            // After 8 seconds, if nothing is listening on the CDP port, give up early.
            // This avoids wasting 30s when Stremio was started without the CDP env var.
            if (attempt == 8 && !IsPortListening(activeCdpPort))
            {
                Logger.Log("[FIS] CDP port " + activeCdpPort + " not listening after 8s — Stremio needs restart");
                return null;
            }

            string wsUrl = CdpClient.FindStremioPage(activeCdpPort, out pageId);
            if (wsUrl != null)
            {
                Logger.Log("[FIS] CDP connected on port " + activeCdpPort + ", page ID: " + pageId);
                return wsUrl;
            }
        }
        return null;
    }
}

// ============================================================
//  CDP Client
// ============================================================

static class CdpClient
{
    public static string FindStremioPage(int port, out string pageId)
    {
        pageId = null;
        try
        {
            using (WebClient wc = new WebClient())
            {
                wc.Proxy = null;
                string json = wc.DownloadString("http://127.0.0.1:" + port + "/json");

                int searchPos = 0;
                while (true)
                {
                    int typeIdx = json.IndexOf("\"type\"", searchPos);
                    if (typeIdx == -1) break;

                    int objStart = json.LastIndexOf('{', typeIdx);
                    int objEnd = FindMatchingBrace(json, objStart);
                    if (objEnd == -1) break;

                    string obj = json.Substring(objStart, objEnd - objStart + 1);

                    if (obj.Contains("\"type\":\"page\"") || obj.Contains("\"type\": \"page\""))
                    {
                        if (obj.Contains("web.stremio.com") || obj.Contains("app.strem.io"))
                        {
                            string wsUrl = ExtractJsonValue(obj, "webSocketDebuggerUrl");
                            pageId = ExtractJsonValue(obj, "id");
                            return wsUrl;
                        }
                    }
                    searchPos = objEnd + 1;
                }
            }
        }
        catch { }
        return null;
    }

    public static bool InjectBundle(string wsUrl, string bundleContent)
    {
        ClientWebSocket ws = null;
        try
        {
            ws = new ClientWebSocket();
            CancellationTokenSource cts = new CancellationTokenSource(30000);

            try { ws.ConnectAsync(new Uri(wsUrl), cts.Token).Wait(); }
            catch (AggregateException ex)
            {
                Logger.Log("[FIS] WS connect failed: " + GetInner(ex));
                return false;
            }

            if (ws.State != WebSocketState.Open) return false;

            string escaped = EscapeForJson(bundleContent);
            string msg = "{\"id\":1,\"method\":\"Runtime.evaluate\",\"params\":{\"expression\":\"" + escaped + "\"}}";

            byte[] msgBytes = Encoding.UTF8.GetBytes(msg);
            try { ws.SendAsync(new ArraySegment<byte>(msgBytes), WebSocketMessageType.Text, true, cts.Token).Wait(); }
            catch (AggregateException ex)
            {
                Logger.Log("[FIS] WS send failed: " + GetInner(ex));
                return false;
            }

            byte[] buf = new byte[65536];
            StringBuilder resp = new StringBuilder();
            WebSocketReceiveResult result;
            int chunks = 0;
            do
            {
                try { result = ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token).Result; }
                catch { return false; }
                resp.Append(Encoding.UTF8.GetString(buf, 0, result.Count));
                chunks++;
                if (chunks >= 100) return false;
            } while (!result.EndOfMessage);

            try { ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait(); }
            catch { }

            return !resp.ToString().Contains("\"error\"");
        }
        catch (Exception ex)
        {
            Logger.Log("[FIS] Injection error: " + GetInner(ex));
            return false;
        }
        finally
        {
            if (ws != null) { try { ws.Dispose(); } catch { } }
        }
    }

    static string EscapeForJson(string s)
    {
        StringBuilder sb = new StringBuilder(s.Length + s.Length / 4);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\r': break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < (char)0x20)
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("x4"));
                    }
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    static int FindMatchingBrace(string json, int pos)
    {
        int depth = 0;
        bool inStr = false;
        bool esc = false;
        for (int i = pos; i < json.Length; i++)
        {
            char c = json[i];
            if (esc) { esc = false; continue; }
            if (c == '\\') { esc = true; continue; }
            if (c == '"') { inStr = !inStr; continue; }
            if (inStr) continue;
            if (c == '{') depth++;
            if (c == '}') { depth--; if (depth == 0) return i; }
        }
        return -1;
    }

    static string ExtractJsonValue(string json, string key)
    {
        string search = "\"" + key + "\"";
        int idx = json.IndexOf(search);
        if (idx == -1) return null;
        int colon = json.IndexOf(':', idx + search.Length);
        if (colon == -1) return null;
        int start = json.IndexOf('"', colon + 1);
        if (start == -1) return null;
        int end = json.IndexOf('"', start + 1);
        if (end == -1) return null;
        return json.Substring(start + 1, end - start - 1);
    }

    static string GetInner(Exception ex)
    {
        AggregateException agg = ex as AggregateException;
        if (agg != null && agg.InnerExceptions.Count > 0)
            return agg.InnerExceptions[0].Message;
        if (ex.InnerException != null)
            return ex.InnerException.Message;
        return ex.Message;
    }
}

// ============================================================
//  GUI - Installer Form
// ============================================================

class InstallerForm : Form
{
    // Colors
    static readonly Color BG_DARK = Color.FromArgb(26, 26, 46);
    static readonly Color BG_PANEL = Color.FromArgb(22, 33, 62);
    static readonly Color ACCENT = Color.FromArgb(230, 57, 70);
    static readonly Color TEXT_PRIMARY = Color.FromArgb(255, 255, 255);
    static readonly Color TEXT_DIM = Color.FromArgb(139, 143, 163);
    static readonly Color SUCCESS = Color.FromArgb(76, 175, 80);
    static readonly Color BORDER = Color.FromArgb(42, 42, 74);

    // Status indicators
    Label lblStremio, lblBundle, lblEnvVar, lblStartup, lblInjector;
    // Log
    RichTextBox logBox;
    // Buttons
    Button btnInstall, btnUninstall, btnRefresh, btnLogs, btnUpdate;
    // State
    bool busy = false;
    string pendingUpdateUrl = null;

    public InstallerForm()
    {
        this.Text = "Feel It.Stream";
        this.AutoScaleMode = AutoScaleMode.Dpi;
        this.ClientSize = new Size(520, 520);
        this.MinimumSize = new Size(520, 460);
        this.FormBorderStyle = FormBorderStyle.Sizable;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = BG_DARK;
        this.ForeColor = TEXT_PRIMARY;
        this.Font = new Font("Segoe UI", 9.5f);
        this.Icon = CreateFISIcon();

        BuildUI();
        RefreshStatus();
        CheckForUpdateAsync();
    }

    // Generate a FIS icon: glowing red sphere on black background
    static Icon CreateFISIcon()
    {
        try
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Black);

                float cx = 16f, cy = 16f, mainR = 13f;

                // Outer glow
                float glowR = mainR * 1.35f;
                RectangleF glowRect = new RectangleF(cx - glowR, cy - glowR, glowR * 2, glowR * 2);
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(glowRect);
                    using (PathGradientBrush pgb = new PathGradientBrush(gp))
                    {
                        pgb.CenterColor = Color.FromArgb(140, 200, 0, 0);
                        pgb.SurroundColors = new Color[] { Color.FromArgb(0, 0, 0, 0) };
                        pgb.CenterPoint = new PointF(cx, cy);
                        g.FillEllipse(pgb, glowRect);
                    }
                }

                // Main sphere
                RectangleF sphereRect = new RectangleF(cx - mainR, cy - mainR, mainR * 2, mainR * 2);
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(sphereRect);
                    using (PathGradientBrush pgb = new PathGradientBrush(gp))
                    {
                        pgb.CenterColor = Color.FromArgb(255, 255, 50, 35);
                        pgb.SurroundColors = new Color[] { Color.FromArgb(255, 90, 0, 0) };
                        pgb.CenterPoint = new PointF(cx, cy);
                        g.FillEllipse(pgb, sphereRect);
                    }
                }

                // Bright core
                float hiR = mainR * 0.45f;
                RectangleF hiRect = new RectangleF(cx - hiR, cy - hiR, hiR * 2, hiR * 2);
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(hiRect);
                    using (PathGradientBrush pgb = new PathGradientBrush(gp))
                    {
                        pgb.CenterColor = Color.FromArgb(90, 255, 140, 100);
                        pgb.SurroundColors = new Color[] { Color.FromArgb(0, 255, 40, 20) };
                        pgb.CenterPoint = new PointF(cx, cy);
                        g.FillEllipse(pgb, hiRect);
                    }
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    void BuildUI()
    {
        int y = 20;
        int pad = 24;
        int w = this.ClientSize.Width - pad * 2;

        // ── Header: "FEEL IT" + "." (red) + "STREAM" + version ──
        Font brandFont = new Font("Segoe UI", 16f, FontStyle.Bold);
        Label lbl1 = new Label();
        lbl1.Text = "FEEL IT";
        lbl1.Font = brandFont;
        lbl1.ForeColor = TEXT_PRIMARY;
        lbl1.AutoSize = true;
        lbl1.Location = new Point(pad, y);
        this.Controls.Add(lbl1);

        Label lblDot = new Label();
        lblDot.Text = ".";
        lblDot.Font = brandFont;
        lblDot.ForeColor = ACCENT;
        lblDot.AutoSize = true;
        // Position right after "FEEL IT"
        int dotX = pad + TextRenderer.MeasureText("FEEL IT", brandFont).Width - 8;
        lblDot.Location = new Point(dotX, y);
        this.Controls.Add(lblDot);

        Label lbl2 = new Label();
        lbl2.Text = "STREAM";
        lbl2.Font = brandFont;
        lbl2.ForeColor = TEXT_PRIMARY;
        lbl2.AutoSize = true;
        int streamX = dotX + TextRenderer.MeasureText(".", brandFont).Width - 6;
        lbl2.Location = new Point(streamX, y);
        this.Controls.Add(lbl2);

        Label ver = new Label();
        ver.Text = "v" + FISPaths.VERSION;
        ver.Font = new Font("Segoe UI", 9f);
        ver.ForeColor = ACCENT;
        ver.AutoSize = true;
        int verX = streamX + TextRenderer.MeasureText("STREAM", brandFont).Width;
        ver.Location = new Point(verX, y + 6);
        this.Controls.Add(ver);
        y += 32;

        Label subtitle = new Label();
        subtitle.Text = "Mod Installer for Stremio Desktop";
        subtitle.ForeColor = TEXT_DIM;
        subtitle.Location = new Point(pad, y);
        subtitle.AutoSize = true;
        this.Controls.Add(subtitle);
        y += 30;

        // ── Separator ──
        Panel sep1 = new Panel();
        sep1.BackColor = BORDER;
        sep1.Location = new Point(pad, y);
        sep1.Size = new Size(w, 1);
        sep1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.Controls.Add(sep1);
        y += 16;

        // ── Status section ──
        Label statusTitle = new Label();
        statusTitle.Text = "STATUS";
        statusTitle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        statusTitle.ForeColor = TEXT_DIM;
        statusTitle.Location = new Point(pad, y);
        statusTitle.AutoSize = true;
        this.Controls.Add(statusTitle);
        y += 24;

        lblStremio = AddStatusRow(ref y, "Stremio Desktop");
        lblBundle = AddStatusRow(ref y, "FIS Bundle");
        lblEnvVar = AddStatusRow(ref y, "CDP Port");
        lblStartup = AddStatusRow(ref y, "Auto-start");
        lblInjector = AddStatusRow(ref y, "Injector");
        y += 8;

        // ── Separator ──
        Panel sep2 = new Panel();
        sep2.BackColor = BORDER;
        sep2.Location = new Point(pad, y);
        sep2.Size = new Size(w, 1);
        sep2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.Controls.Add(sep2);
        y += 16;

        // ── Log area ──
        logBox = new RichTextBox();
        logBox.Location = new Point(pad, y);
        logBox.Size = new Size(w, 140);
        logBox.BackColor = Color.FromArgb(14, 14, 28);
        logBox.ForeColor = TEXT_DIM;
        logBox.Font = new Font("Consolas", 9f);
        logBox.ReadOnly = true;
        logBox.BorderStyle = BorderStyle.None;
        logBox.ScrollBars = RichTextBoxScrollBars.Vertical;
        logBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        this.Controls.Add(logBox);
        y += 150;

        // ── Buttons (auto-sized to text) ──
        int gap = 10;
        int bx = pad;

        btnInstall = MakeButton("INSTALL", bx, y, ACCENT);
        btnInstall.Click += OnInstallClick;
        btnInstall.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this.Controls.Add(btnInstall);
        bx += btnInstall.Width + gap;

        btnUninstall = MakeButton("UNINSTALL", bx, y, BORDER);
        btnUninstall.Click += OnUninstallClick;
        btnUninstall.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this.Controls.Add(btnUninstall);
        bx += btnUninstall.Width + gap;

        btnRefresh = MakeButton("REFRESH", bx, y, BORDER);
        btnRefresh.Click += OnRefreshClick;
        btnRefresh.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this.Controls.Add(btnRefresh);

        // Logs link (auto-sized)
        y += btnInstall.Height + 8;
        Font logsFont = new Font("Segoe UI", 8.5f, FontStyle.Underline);
        Size logsSize = TextRenderer.MeasureText("Open logs", logsFont);
        btnLogs = new Button();
        btnLogs.Text = "Open logs";
        btnLogs.FlatStyle = FlatStyle.Flat;
        btnLogs.FlatAppearance.BorderSize = 0;
        btnLogs.BackColor = BG_DARK;
        btnLogs.ForeColor = TEXT_DIM;
        btnLogs.Font = logsFont;
        btnLogs.Location = new Point(pad, y);
        btnLogs.Size = new Size(logsSize.Width + 16, logsSize.Height + 8);
        btnLogs.Cursor = Cursors.Hand;
        btnLogs.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnLogs.Click += OnLogsClick;
        this.Controls.Add(btnLogs);

        // Update button (hidden until update is detected)
        btnUpdate = MakeButton("UPDATE AVAILABLE", pad + logsSize.Width + 24, y - 2, Color.FromArgb(30, 130, 76));
        btnUpdate.Visible = false;
        btnUpdate.Click += OnUpdateClick;
        btnUpdate.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this.Controls.Add(btnUpdate);
    }

    Label AddStatusRow(ref int y, string label)
    {
        int pad = 24;

        Label name = new Label();
        name.Text = label;
        name.ForeColor = TEXT_PRIMARY;
        name.Location = new Point(pad + 20, y);
        name.Size = new Size(200, 22);
        this.Controls.Add(name);

        Label status = new Label();
        status.Text = "...";
        status.ForeColor = TEXT_DIM;
        status.TextAlign = ContentAlignment.MiddleRight;
        status.Location = new Point(this.ClientSize.Width - 24 - 160, y);
        status.Size = new Size(160, 22);
        status.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.Controls.Add(status);

        y += 26;
        return status;
    }

    Button MakeButton(string text, int x, int y, Color bg)
    {
        Font btnFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        Size textSize = TextRenderer.MeasureText(text, btnFont);
        Button btn = new Button();
        btn.Text = text;
        btn.Font = btnFont;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = bg;
        btn.ForeColor = TEXT_PRIMARY;
        btn.Location = new Point(x, y);
        btn.Size = new Size(textSize.Width + 24, textSize.Height + 16);
        btn.Cursor = Cursors.Hand;
        return btn;
    }

    void SetStatus(Label lbl, bool ok, string text)
    {
        if (lbl.InvokeRequired)
        {
            lbl.Invoke(new Action<Label, bool, string>(SetStatus), lbl, ok, text);
            return;
        }
        lbl.Text = text;
        lbl.ForeColor = ok ? SUCCESS : ACCENT;
    }

    void RefreshStatus()
    {
        SetStatus(lblStremio, StatusChecker.IsStremioInstalled(),
            StatusChecker.IsStremioInstalled() ? "Installed" : "Not found");

        bool bundleOk = StatusChecker.IsBundleInstalled();
        string bundleInfo = bundleOk ? StatusChecker.GetBundleVersion() : "Not installed";
        SetStatus(lblBundle, bundleOk, bundleInfo);

        // CDP status: warn if legacy global var still present
        bool injectorUp = StatusChecker.IsInjectorRunning();
        bool legacyVar = StatusChecker.HasLegacyGlobalVar();
        if (legacyVar)
            SetStatus(lblEnvVar, false, "Global (may conflict)");
        else if (injectorUp)
            SetStatus(lblEnvVar, true, "Per-process");
        else
            SetStatus(lblEnvVar, false, "Not active");

        SetStatus(lblStartup, StatusChecker.IsStartupShortcutPresent(),
            StatusChecker.IsStartupShortcutPresent() ? "Enabled" : "Disabled");

        SetStatus(lblInjector, injectorUp,
            injectorUp ? "Active" : "Inactive");
    }

    void LogLine(string text, bool isError)
    {
        if (logBox.InvokeRequired)
        {
            logBox.Invoke(new Action<string, bool>(LogLine), text, isError);
            return;
        }
        Color c = isError ? ACCENT : TEXT_DIM;
        logBox.SelectionStart = logBox.TextLength;
        logBox.SelectionLength = 0;
        logBox.SelectionColor = c;
        if (logBox.TextLength > 0) logBox.AppendText("\n");
        logBox.AppendText("> " + text);
        logBox.ScrollToCaret();
    }

    void SetBusy(bool b)
    {
        if (btnInstall.InvokeRequired)
        {
            btnInstall.Invoke(new Action<bool>(SetBusy), b);
            return;
        }
        busy = b;
        btnInstall.Enabled = !b;
        btnUninstall.Enabled = !b;
        btnRefresh.Enabled = !b;
    }

    void OnInstallClick(object sender, EventArgs e)
    {
        if (busy) return;
        SetBusy(true);
        logBox.Clear();
        ThreadPool.QueueUserWorkItem(delegate
        {
            InstallerOps.Install(LogLine);
            Thread.Sleep(500);
            RefreshStatusSafe();
            SetBusy(false);
        });
    }

    void OnUninstallClick(object sender, EventArgs e)
    {
        if (busy) return;
        DialogResult dr = MessageBox.Show(
            "Uninstall Feel It Stream?\n\nThis will remove the bundle, CDP variable and auto-start.",
            "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (dr != DialogResult.Yes) return;

        SetBusy(true);
        logBox.Clear();
        ThreadPool.QueueUserWorkItem(delegate
        {
            InstallerOps.Uninstall(LogLine);
            Thread.Sleep(500);
            RefreshStatusSafe();
            SetBusy(false);
        });
    }

    void OnRefreshClick(object sender, EventArgs e)
    {
        if (busy) return;
        RefreshStatus();
    }

    void OnLogsClick(object sender, EventArgs e)
    {
        if (File.Exists(FISPaths.LogPath))
        {
            Process.Start("notepad.exe", FISPaths.LogPath);
        }
        else
        {
            MessageBox.Show("No log file found.", "Logs",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    void RefreshStatusSafe()
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(RefreshStatus));
        }
        else
        {
            RefreshStatus();
        }
    }

    void CheckForUpdateAsync()
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            string url = Updater.CheckForUpdate();
            if (url != null)
            {
                pendingUpdateUrl = url;
                ShowUpdateButton();
            }
        });
    }

    void ShowUpdateButton()
    {
        if (btnUpdate.InvokeRequired)
        {
            btnUpdate.Invoke(new Action(ShowUpdateButton));
            return;
        }
        btnUpdate.Visible = true;
    }

    void OnUpdateClick(object sender, EventArgs e)
    {
        if (busy || pendingUpdateUrl == null) return;

        DialogResult dr = MessageBox.Show(
            "A new version of FIS Installer is available.\n\nThe installer will download the update, restart, and relaunch automatically.\n\nUpdate now?",
            "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (dr != DialogResult.Yes) return;

        SetBusy(true);
        logBox.Clear();
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                Updater.SelfUpdate(pendingUpdateUrl, LogLine);
                Thread.Sleep(500);
                // Exit so the batch script can replace the exe
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                LogLine("Update failed: " + ex.Message, true);
                SetBusy(false);
            }
        });
    }
}
