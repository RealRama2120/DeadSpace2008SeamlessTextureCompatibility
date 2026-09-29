using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace DeadSpaceTextureLauncher
{
    internal static class IntegrationModes
    {
        public const string None = "None";
        public const string SteamLaunchOption = "SteamLaunchOption";
        public const string EaProxy = "EaProxy";
        public const string VortexBootstrap = "VortexBootstrap";
    }

    internal sealed class IntegrationInstallResult
    {
        public string Mode;
        public string InstalledLauncherPath;
        public string ManagedTextureFolder;
        public string GamePath;
        public string SteamLaunchOption;
    }

    internal static class NormalLaunchIntegration
    {
        public const string InstalledFolderName = "App";
        public const string OriginalBackupName = "Dead Space.original.exe";
        public const string ActiveProxyName = "DeadSpaceTextureProxy.active.exe";
        public const string ProxySourceName = "DeadSpaceTextureProxy.exe";
        private static readonly string[] LaaSidecarSuffixes =
        {
            ".deadspace-laa.bak",
            ".deadspace-laa.manifest"
        };

        public static bool LooksLikeSteam(string gamePath)
        {
            return (gamePath ?? string.Empty).IndexOf("\\steamapps\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static IntegrationInstallResult Install(string gamePath, bool useSteamLaunchOption)
        {
            string validation = PeInspector.ValidateDeadSpace2008(gamePath);
            if (validation != null) throw new LauncherException(validation);
            string gameRoot = Path.GetDirectoryName(Path.GetFullPath(gamePath));
            string installedDirectory = Path.Combine(AppInfo.DataDirectory, InstalledFolderName);
            string installedLauncher = Path.Combine(installedDirectory, "DeadSpaceTextureLauncher.exe");
            string installedProxy = Path.Combine(installedDirectory, ProxySourceName);
            string sourceLauncher = Process.GetCurrentProcess().MainModule.FileName;
            string sourceProxy = Path.Combine(AppInfo.ExecutableDirectory, ProxySourceName);
            if (!File.Exists(sourceProxy))
                throw new LauncherException(ProxySourceName + " is missing from the extracted release. Extract the complete ZIP and try again.");

            Directory.CreateDirectory(installedDirectory);
            CopyUnlessSame(sourceLauncher, installedLauncher);
            CopyUnlessSame(sourceProxy, installedProxy);
            string textureFolder = Path.Combine(AppInfo.DataDirectory, "TexturePacks");
            Directory.CreateDirectory(textureFolder);

            IntegrationInstallResult result = new IntegrationInstallResult();
            result.InstalledLauncherPath = installedLauncher;
            result.ManagedTextureFolder = textureFolder;

            if (useSteamLaunchOption)
            {
                result.Mode = IntegrationModes.SteamLaunchOption;
                result.GamePath = gamePath;
                result.SteamLaunchOption = "\"" + installedLauncher + "\" --steam %command%";
                Log.Info("Prepared Steam normal-launch integration at " + installedLauncher);
                return result;
            }

            RequestElevatedEaChange("--elevated-ea-install", gameRoot);
            result.Mode = IntegrationModes.EaProxy;
            result.GamePath = Path.Combine(gameRoot, OriginalBackupName);
            Log.Info("Installed EA normal-launch proxy in " + gameRoot);
            return result;
        }

        public static void RemoveEaProxy(string gameRoot)
        {
            if (string.IsNullOrWhiteSpace(gameRoot)) throw new LauncherException("The saved game folder is missing.");
            RequestElevatedEaChange("--elevated-ea-remove", gameRoot);
        }

        public static void RecoverEaState(string gameRoot)
        {
            string canonical = Path.Combine(gameRoot, "Dead Space.exe");
            string backup = Path.Combine(gameRoot, OriginalBackupName);
            string activeProxy = Path.Combine(gameRoot, ActiveProxyName);
            if (!File.Exists(activeProxy)) return;

            if (File.Exists(canonical) && !File.Exists(backup) && PeInspector.ValidateDeadSpace2008(canonical) == null)
            {
                MoveWithRetries(canonical, backup, 20);
                MoveWithRetries(activeProxy, canonical, 20);
                Log.Warn("Recovered EA integration after an interrupted prior launch.");
            }
            else if (!File.Exists(canonical) && File.Exists(backup))
            {
                MoveWithRetries(activeProxy, canonical, 20);
                Log.Warn("Recovered a missing EA proxy after an interrupted prior launch.");
            }
        }

        internal static void RunElevatedEaInstall(string gameRoot)
        {
            EnsureGameIsClosed();
            RecoverEaState(gameRoot);
            string canonical = Path.Combine(gameRoot, "Dead Space.exe");
            string backup = Path.Combine(gameRoot, OriginalBackupName);
            string installedProxy = Path.Combine(AppInfo.ExecutableDirectory, ProxySourceName);
            string gamePath = canonical;
            if (!File.Exists(installedProxy) || !PeInspector.IsRamaProxy(installedProxy))
                throw new LauncherException("The verified Rama2120 proxy is missing from the installed app folder.");

            if (PeInspector.IsRamaProxy(canonical) && PeInspector.ValidateDeadSpace2008(backup) == null)
            {
                MigrateMatchingLaaSidecars(canonical, backup);
                File.Copy(installedProxy, canonical, true);
                return;
            }

            if (!string.Equals(Path.GetFullPath(gamePath), canonical, StringComparison.OrdinalIgnoreCase))
                throw new LauncherException("EA integration requires the verified original game to currently be named Dead Space.exe. Remove or repair an older integration first.");
            if (File.Exists(backup))
                throw new LauncherException(OriginalBackupName + " already exists. It was not overwritten.");

            File.Move(canonical, backup);
            IList<KeyValuePair<string, string>> movedSidecars = null;
            try
            {
                movedSidecars = MoveLaaSidecars(canonical, backup);
                File.Copy(installedProxy, canonical, false);
                if (!PeInspector.IsRamaProxy(canonical)) throw new LauncherException("The copied proxy failed verification.");
            }
            catch
            {
                try
                {
                    if (File.Exists(canonical)) File.Delete(canonical);
                    RestoreMovedSidecars(movedSidecars);
                    if (File.Exists(backup)) File.Move(backup, canonical);
                }
                catch { }
                throw;
            }
        }

        internal static void RunElevatedEaRemove(string gameRoot)
        {
            EnsureGameIsClosed();
            RecoverEaState(gameRoot);
            string canonical = Path.Combine(gameRoot, "Dead Space.exe");
            string backup = Path.Combine(gameRoot, OriginalBackupName);
            if (!PeInspector.IsRamaProxy(canonical))
                throw new LauncherException("Dead Space.exe is not Rama2120's proxy, so it was left untouched.");
            string validation = PeInspector.ValidateDeadSpace2008(backup);
            if (validation != null) throw new LauncherException("The original-game backup could not be verified: " + validation);
            EnsureLaaSidecarDestinationsAvailable(backup, canonical);
            File.Delete(canonical);
            File.Move(backup, canonical);
            MoveLaaSidecars(backup, canonical);
            Log.Info("Removed EA normal-launch proxy and restored the original Dead Space.exe.");
        }

        private static void RequestElevatedEaChange(string operation, string gameRoot)
        {
            string installedLauncher = Path.Combine(AppInfo.DataDirectory, InstalledFolderName, "DeadSpaceTextureLauncher.exe");
            ProcessStartInfo start = new ProcessStartInfo(installedLauncher,
                operation + " --game-root \"" + gameRoot + "\"");
            start.UseShellExecute = true;
            start.Verb = "runas";
            start.WorkingDirectory = Path.GetDirectoryName(installedLauncher);
            Process helper;
            try { helper = Process.Start(start); }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new LauncherException("Administrator approval was cancelled or blocked. EA integration was not changed.", ex);
            }
            if (helper == null) throw new LauncherException("The elevated EA integration helper did not start.");
            using (helper)
            {
                helper.WaitForExit();
                if (helper.ExitCode != 0)
                {
                    string errorPath = Path.Combine(AppInfo.DataDirectory, "integration-error.txt");
                    string detail = File.Exists(errorPath) ? File.ReadAllText(errorPath) : "The elevated helper returned code " + helper.ExitCode + ".";
                    throw new LauncherException(detail.Trim());
                }
            }
        }

        private static void CopyUnlessSame(string source, string destination)
        {
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase)) return;
            File.Copy(source, destination, true);
        }

        private static IList<KeyValuePair<string, string>> MoveLaaSidecars(string sourceExecutable, string destinationExecutable)
        {
            EnsureLaaSidecarDestinationsAvailable(sourceExecutable, destinationExecutable);
            List<KeyValuePair<string, string>> moved = new List<KeyValuePair<string, string>>();
            try
            {
                foreach (string suffix in LaaSidecarSuffixes)
                {
                    string source = sourceExecutable + suffix;
                    if (!File.Exists(source)) continue;
                    string destination = destinationExecutable + suffix;
                    File.Move(source, destination);
                    moved.Add(new KeyValuePair<string, string>(source, destination));
                }
                return moved;
            }
            catch
            {
                RestoreMovedSidecars(moved);
                throw;
            }
        }

        private static void EnsureLaaSidecarDestinationsAvailable(string sourceExecutable, string destinationExecutable)
        {
            foreach (string suffix in LaaSidecarSuffixes)
            {
                if (File.Exists(sourceExecutable + suffix) && File.Exists(destinationExecutable + suffix))
                    throw new LauncherException("LAA integration has conflicting sidecar files for " + Path.GetFileName(destinationExecutable) + suffix + ". No files were overwritten.");
            }
        }

        private static void RestoreMovedSidecars(IList<KeyValuePair<string, string>> moved)
        {
            if (moved == null) return;
            for (int i = moved.Count - 1; i >= 0; i--)
            {
                string source = moved[i].Key;
                string destination = moved[i].Value;
                try
                {
                    if (File.Exists(destination) && !File.Exists(source)) File.Move(destination, source);
                }
                catch { }
            }
        }

        private static void MigrateMatchingLaaSidecars(string proxyPath, string originalPath)
        {
            string sourceBackup = proxyPath + LaaSidecarSuffixes[0];
            string sourceManifest = proxyPath + LaaSidecarSuffixes[1];
            if (!File.Exists(sourceBackup) && !File.Exists(sourceManifest)) return;
            if (!File.Exists(sourceBackup) || !File.Exists(sourceManifest))
                throw new LauncherException("The existing LAA sidecar set is incomplete. EA integration was not changed.");

            string originalHash = Sha256(originalPath);
            string manifest = File.ReadAllText(sourceManifest);
            if (manifest.IndexOf("OriginalSha256=" + originalHash, StringComparison.OrdinalIgnoreCase) < 0 &&
                manifest.IndexOf("PatchedSha256=" + originalHash, StringComparison.OrdinalIgnoreCase) < 0)
                return;

            MoveLaaSidecars(proxyPath, originalPath);
            Log.Info("Migrated existing Vortex LAA sidecars to " + Path.GetFileName(originalPath) + ".");
        }

        private static string Sha256(string filePath)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(filePath))
            {
                byte[] hash = algorithm.ComputeHash(stream);
                StringBuilder text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) text.Append(value.ToString("x2"));
                return text.ToString();
            }
        }

        private static void EnsureGameIsClosed()
        {
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    string name = process.ProcessName;
                    if (name.Equals("Dead Space", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("Dead Space.original", StringComparison.OrdinalIgnoreCase))
                        throw new LauncherException("Close Dead Space before installing normal-launch integration.");
                }
                catch (LauncherException) { throw; }
                catch { }
                finally { process.Dispose(); }
            }
        }

        internal static void MoveWithRetries(string source, string destination, int attempts)
        {
            Exception last = null;
            for (int i = 0; i < attempts; i++)
            {
                try { File.Move(source, destination); return; }
                catch (Exception ex) { last = ex; Thread.Sleep(250); }
            }
            throw new LauncherException("Could not move '" + source + "' to '" + destination + "'.", last);
        }
    }

    internal sealed class EaLaunchSwap : IDisposable
    {
        private readonly string gameRoot;
        private readonly string canonical;
        private readonly string backup;
        private readonly string activeProxy;
        private bool swapped;

        public string RuntimeGamePath { get { return canonical; } }

        private EaLaunchSwap(string gameRoot)
        {
            this.gameRoot = gameRoot;
            canonical = Path.Combine(gameRoot, "Dead Space.exe");
            backup = Path.Combine(gameRoot, NormalLaunchIntegration.OriginalBackupName);
            activeProxy = Path.Combine(gameRoot, NormalLaunchIntegration.ActiveProxyName);
        }

        public static EaLaunchSwap Begin(string gameRoot, int proxyProcessId)
        {
            EaLaunchSwap session = new EaLaunchSwap(gameRoot);
            session.Prepare(proxyProcessId);
            return session;
        }

        private void Prepare(int proxyProcessId)
        {
            if (proxyProcessId > 0)
            {
                try
                {
                    using (Process proxy = Process.GetProcessById(proxyProcessId))
                        proxy.WaitForExit(10000);
                }
                catch { }
            }

            NormalLaunchIntegration.RecoverEaState(gameRoot);
            if (!PeInspector.IsRamaProxy(canonical)) throw new LauncherException("EA launched a file that is not Rama2120's verified proxy. Run setup to repair integration.");
            string validation = PeInspector.ValidateDeadSpace2008(backup);
            if (validation != null) throw new LauncherException("The original Dead Space backup could not be verified: " + validation);

            StartWatchdog();
            NormalLaunchIntegration.MoveWithRetries(canonical, activeProxy, 20);
            try
            {
                NormalLaunchIntegration.MoveWithRetries(backup, canonical, 20);
                swapped = true;
                Log.Info("EA launch swap prepared; TexMod will target the original canonical executable.");
            }
            catch
            {
                try { NormalLaunchIntegration.MoveWithRetries(activeProxy, canonical, 20); } catch { }
                throw;
            }
        }

        private void StartWatchdog()
        {
            string watchdog = Path.Combine(AppInfo.DataDirectory, NormalLaunchIntegration.InstalledFolderName, NormalLaunchIntegration.ProxySourceName);
            if (!File.Exists(watchdog)) return;
            ProcessStartInfo start = new ProcessStartInfo(watchdog,
                "--watchdog " + Process.GetCurrentProcess().Id + " \"" + gameRoot + "\"");
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(start);
        }

        public void Dispose()
        {
            if (!swapped) return;
            try
            {
                if (File.Exists(canonical) && !File.Exists(backup))
                    NormalLaunchIntegration.MoveWithRetries(canonical, backup, 40);
                if (File.Exists(activeProxy) && !File.Exists(canonical))
                    NormalLaunchIntegration.MoveWithRetries(activeProxy, canonical, 40);
                swapped = false;
                Log.Info("EA launch swap restored; the normal Play button points to the proxy again.");
            }
            catch (Exception ex)
            {
                Log.Error("EA launch swap restoration failed: " + ex);
                throw;
            }
        }
    }
}
