using System;
using System.IO;
using System.Text;

namespace DeadSpaceTextureLauncher
{
    internal static class VortexIntegration
    {
        public const string DeploymentMarkerName = "DeadSpaceTextureLauncher.vortex.json";
        public const string BootstrapName = "dinput8.dll";
        private const string MarkerId = "rama2120.dead-space-2008-texture-compatibility";

        public static bool SyncFromCurrentDeployment(LauncherSettings settings)
        {
            string marker = FindDeploymentMarker();
            if (marker == null) return false;

            string gameRoot = Path.GetDirectoryName(marker);
            string gamePath = ResolveOriginalGamePath(gameRoot);
            if (gamePath == null) return false;
            string bootstrap = Path.Combine(gameRoot, BootstrapName);
            if (!File.Exists(bootstrap))
                throw new LauncherException("The deployed compatibility mod is incomplete: dinput8.dll is missing. Redeploy this mod in Vortex.");

            bool firstVortexSync = !settings.VortexManaged;
            settings.VortexManaged = true;
            settings.VortexMarkerPath = marker;
            settings.GameRoot = gameRoot;
            settings.GamePath = gamePath;
            settings.ManagedTextureFolder = Path.Combine(gameRoot, "TexMod Packages");
            settings.AutoDiscoverPackages = true;
            settings.IntegrationMode = IntegrationModes.VortexBootstrap;
            settings.InstalledLauncherPath = ProcessExecutablePath();
            if (firstVortexSync) settings.ScanDownloads = false;

            Log.Info("Vortex deployment detected; TexMod Packages will follow Vortex enable, disable, deploy, and purge state.");
            return true;
        }

        public static bool IsDeploymentEnabled(LauncherSettings settings)
        {
            if (settings == null || !settings.VortexManaged) return true;
            return IsValidDeploymentMarker(settings.VortexMarkerPath);
        }

        public static void MarkSetupComplete(LauncherSettings settings)
        {
            if (settings == null || !settings.VortexManaged) return;
            string gameRoot = Path.GetDirectoryName(settings.VortexMarkerPath);
            if (string.IsNullOrWhiteSpace(gameRoot)) return;
            if (string.Equals(settings.IntegrationMode, IntegrationModes.None, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(settings.TexModPath) || !IsValidDeploymentMarker(settings.VortexMarkerPath))
                return;
            if (!File.Exists(Path.Combine(gameRoot, BootstrapName)))
                throw new LauncherException("The deployed compatibility mod is incomplete: dinput8.dll is missing. Redeploy this mod in Vortex.");
            Log.Info("Vortex first-run setup is complete. No unmanaged setup marker was written into the game folder.");
        }

        private static string FindDeploymentMarker()
        {
            string executableDirectory = Path.GetFullPath(AppInfo.ExecutableDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] roots = { executableDirectory, Path.GetDirectoryName(executableDirectory) };
            foreach (string root in roots)
            {
                if (string.IsNullOrWhiteSpace(root)) continue;
                string marker = Path.Combine(root, DeploymentMarkerName);
                if (IsValidDeploymentMarker(marker)) return marker;
            }
            return null;
        }

        private static bool IsValidDeploymentMarker(string marker)
        {
            if (string.IsNullOrWhiteSpace(marker) || !File.Exists(marker)) return false;
            try
            {
                string text = File.ReadAllText(marker, Encoding.UTF8);
                return text.IndexOf("\"id\"", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    text.IndexOf(MarkerId, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private static string ResolveOriginalGamePath(string gameRoot)
        {
            string canonical = Path.Combine(gameRoot, "Dead Space.exe");
            if (PeInspector.ValidateDeadSpace2008(canonical) == null) return canonical;
            string backup = Path.Combine(gameRoot, NormalLaunchIntegration.OriginalBackupName);
            return PeInspector.ValidateDeadSpace2008(backup) == null ? backup : null;
        }

        private static string ProcessExecutablePath()
        {
            return System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        }
    }
}
