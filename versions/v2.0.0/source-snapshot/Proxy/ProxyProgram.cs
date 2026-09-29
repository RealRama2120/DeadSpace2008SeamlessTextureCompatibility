using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Dead Space Texture Proxy")]
[assembly: AssemblyDescription("Normal-launch bridge for Rama2120's Dead Space Texture Launcher")]
[assembly: AssemblyCompany("Rama2120")]
[assembly: AssemblyProduct("Dead Space Texture Proxy")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Rama2120")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]
[assembly: AssemblyInformationalVersion("2.0.0")]

internal static class ProxyProgram
{
    private const string InstalledFolderName = "App";
    private const string OriginalBackupName = "Dead Space.original.exe";
    private const string ActiveProxyName = "DeadSpaceTextureProxy.active.exe";

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length >= 3 && args[0].Equals("--watchdog", StringComparison.OrdinalIgnoreCase))
        {
            int processId;
            if (int.TryParse(args[1], out processId)) WatchAndRecover(processId, args[2]);
            return;
        }

        string gameRoot = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string launcher = Path.Combine(GetDataDirectory(), InstalledFolderName, "DeadSpaceTextureLauncher.exe");
        if (!File.Exists(launcher))
        {
            MessageBox.Show(
                "The installed texture launcher is missing. Run the Rama2120 setup again to repair normal-launch integration.",
                "Dead Space Texture Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        try
        {
            ProcessStartInfo start = new ProcessStartInfo(launcher,
                "--ea-proxy --proxy-pid " + Process.GetCurrentProcess().Id + " --game-root \"" + gameRoot + "\"");
            start.WorkingDirectory = Path.GetDirectoryName(launcher);
            start.UseShellExecute = true;
            Process.Start(start);
        }
        catch (Exception ex)
        {
            MessageBox.Show("The texture launcher could not be started:\n\n" + ex.Message,
                "Dead Space Texture Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void WatchAndRecover(int launcherProcessId, string gameRoot)
    {
        try
        {
            try
            {
                using (Process launcher = Process.GetProcessById(launcherProcessId))
                    launcher.WaitForExit();
            }
            catch { }
            Thread.Sleep(750);

            string canonical = Path.Combine(gameRoot, "Dead Space.exe");
            string backup = Path.Combine(gameRoot, OriginalBackupName);
            string active = Path.Combine(gameRoot, ActiveProxyName);
            if (!File.Exists(active)) return;

            if (File.Exists(canonical) && !File.Exists(backup))
            {
                MoveWithRetries(canonical, backup);
                MoveWithRetries(active, canonical);
                WriteLog(gameRoot, "Watchdog restored the normal-launch proxy after an interrupted launcher exit.");
            }
            else if (!File.Exists(canonical) && File.Exists(backup))
            {
                MoveWithRetries(active, canonical);
                WriteLog(gameRoot, "Watchdog restored a missing normal-launch proxy.");
            }
        }
        catch (Exception ex)
        {
            WriteLog(gameRoot, "Watchdog recovery failed: " + ex);
        }
    }

    private static void MoveWithRetries(string source, string destination)
    {
        Exception last = null;
        for (int i = 0; i < 80; i++)
        {
            try { File.Move(source, destination); return; }
            catch (Exception ex) { last = ex; Thread.Sleep(250); }
        }
        throw last ?? new IOException("File move failed.");
    }

    private static void WriteLog(string gameRoot, string message)
    {
        try
        {
            string directory = Path.Combine(GetDataDirectory(), InstalledFolderName);
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "watchdog.log"),
                "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message + Environment.NewLine);
        }
        catch { }
    }

    private static string GetDataDirectory()
    {
        string overrideDirectory = Environment.GetEnvironmentVariable("DSTL_CONFIG_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDirectory)) return Path.GetFullPath(overrideDirectory);
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Rama2120", "DeadSpaceTextureLauncher");
    }
}
