using System;
using System.IO;

namespace DeadSpaceTextureLauncher
{
    internal static class IntegrationHelper
    {
        public static bool TryRun(string[] args)
        {
            bool install = false;
            bool remove = false;
            string gameRoot = string.Empty;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--elevated-ea-install", StringComparison.OrdinalIgnoreCase)) install = true;
                else if (args[i].Equals("--elevated-ea-remove", StringComparison.OrdinalIgnoreCase)) remove = true;
                else if (args[i].Equals("--game-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) gameRoot = args[++i];
            }
            if (!install && !remove) return false;

            string errorPath = Path.Combine(AppInfo.DataDirectory, "integration-error.txt");
            try
            {
                Directory.CreateDirectory(AppInfo.DataDirectory);
                Log.Info("Elevated EA integration helper started.");
                if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
                    throw new LauncherException("The Dead Space game folder could not be verified.");
                if (install) NormalLaunchIntegration.RunElevatedEaInstall(Path.GetFullPath(gameRoot));
                else NormalLaunchIntegration.RunElevatedEaRemove(Path.GetFullPath(gameRoot));
                try { if (File.Exists(errorPath)) File.Delete(errorPath); } catch { }
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                try
                {
                    Directory.CreateDirectory(AppInfo.DataDirectory);
                    File.WriteAllText(errorPath, ex.Message + Environment.NewLine + ex);
                }
                catch { }
                Environment.ExitCode = 1;
            }
            return true;
        }
    }
}
