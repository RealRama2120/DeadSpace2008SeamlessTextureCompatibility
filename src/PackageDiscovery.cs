using System;
using System.Collections.Generic;
using System.IO;

namespace DeadSpaceTextureLauncher
{
    internal static class PackageDiscovery
    {
        public static IList<string> Discover(LauncherSettings settings)
        {
            List<string> packages = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!VortexIntegration.IsDeploymentEnabled(settings))
            {
                Log.Info("The Vortex compatibility mod is disabled or purged; texture injection is bypassed.");
                return packages;
            }

            foreach (string package in settings.Packages)
                AddFile(packages, seen, package);

            if (!settings.AutoDiscoverPackages)
                return packages;

            List<string> folders = new List<string>();
            AddFolder(folders, settings.ManagedTextureFolder);
            AddFolder(folders, Path.Combine(AppInfo.ExecutableDirectory, "TexturePacks"));

            string gameRoot = settings.GameRoot;
            if (string.IsNullOrWhiteSpace(gameRoot) && !string.IsNullOrWhiteSpace(settings.GamePath))
                gameRoot = Path.GetDirectoryName(settings.GamePath);
            if (!string.IsNullOrWhiteSpace(gameRoot))
            {
                AddFolder(folders, Path.Combine(gameRoot, "TexturePacks"));
                AddFolder(folders, Path.Combine(gameRoot, "TexMod Packages"));
                AddFolder(folders, Path.Combine(gameRoot, "Mods"));
                AddTopLevelPackages(packages, seen, gameRoot);
            }

            foreach (string folder in settings.WatchedFolders)
                AddFolder(folders, folder);

            if (settings.ScanDownloads)
                AddFolder(folders, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));

            HashSet<string> scanned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in folders)
            {
                string full;
                try { full = Path.GetFullPath(folder); }
                catch { continue; }
                if (!scanned.Add(full)) continue;
                AddTreePackages(packages, seen, full);
            }

            Log.Info("Automatic package discovery found " + packages.Count + " unique .tpf file" + (packages.Count == 1 ? "." : "s."));
            return packages;
        }

        public static string EnsureManagedFolder(LauncherSettings settings)
        {
            string folder = settings.ManagedTextureFolder;
            if (string.IsNullOrWhiteSpace(folder))
                folder = Path.Combine(AppInfo.ExecutableDirectory, "TexturePacks");
            Directory.CreateDirectory(folder);
            settings.ManagedTextureFolder = Path.GetFullPath(folder);
            return settings.ManagedTextureFolder;
        }

        private static void AddTopLevelPackages(List<string> result, HashSet<string> seen, string folder)
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(folder, "*.tpf", SearchOption.TopDirectoryOnly))
                    AddFile(result, seen, file);
            }
            catch { }
        }

        private static void AddTreePackages(List<string> result, HashSet<string> seen, string folder)
        {
            if (!Directory.Exists(folder)) return;
            List<string> found = new List<string>();
            Stack<string> pending = new Stack<string>();
            pending.Push(folder);
            while (pending.Count > 0 && found.Count < 1000)
            {
                string current = pending.Pop();
                try
                {
                    foreach (string file in Directory.EnumerateFiles(current, "*.tpf", SearchOption.TopDirectoryOnly))
                        found.Add(file);
                    foreach (string child in Directory.EnumerateDirectories(current))
                    {
                        try
                        {
                            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                                pending.Push(child);
                        }
                        catch { }
                    }
                }
                catch { }
            }
            found.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string file in found) AddFile(result, seen, file);
        }

        private static void AddFile(List<string> result, HashSet<string> seen, string file)
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file) ||
                !Path.GetExtension(file).Equals(".tpf", StringComparison.OrdinalIgnoreCase)) return;
            string full;
            try { full = Path.GetFullPath(file); }
            catch { return; }
            if (seen.Add(full)) result.Add(full);
        }

        private static void AddFolder(List<string> folders, string folder)
        {
            if (!string.IsNullOrWhiteSpace(folder)) folders.Add(folder);
        }
    }
}
