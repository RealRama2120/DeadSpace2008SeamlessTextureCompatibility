using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace DeadSpaceTextureLauncher
{
    internal sealed class GameCandidate
    {
        public string Path;
        public string Source;
        public int Score;
    }

    internal static class GameDetector
    {
        private static readonly string[] CommonRelativePaths = new[]
        {
            @"Program Files (x86)\Steam\steamapps\common\Dead Space\Dead Space.exe",
            @"Program Files\Steam\steamapps\common\Dead Space\Dead Space.exe",
            @"SteamLibrary\steamapps\common\Dead Space\Dead Space.exe",
            @"Steam\steamapps\common\Dead Space\Dead Space.exe",
            @"Program Files\EA Games\Dead Space\Dead Space.exe",
            @"Program Files (x86)\EA Games\Dead Space\Dead Space.exe",
            @"EA Games\Dead Space\Dead Space.exe",
            @"Program Files (x86)\Origin Games\Dead Space\Dead Space.exe",
            @"Program Files\Origin Games\Dead Space\Dead Space.exe",
            @"Origin Games\Dead Space\Dead Space.exe",
            @"Games\Dead Space\Dead Space.exe"
        };

        public static IList<GameCandidate> Find(Action<string> progress, CancellationToken cancellation)
        {
            Dictionary<string, GameCandidate> candidates = new Dictionary<string, GameCandidate>(StringComparer.OrdinalIgnoreCase);
            Report(progress, "Checking running game and saved store locations...");
            AddRunningProcess(candidates);
            AddRegistryLocations(candidates);
            AddSteamLibraries(candidates);
            AddCommonPathsOnEveryFixedDrive(candidates);
            cancellation.ThrowIfCancellationRequested();

            if (candidates.Count == 0)
            {
                Report(progress, "No standard install was found. Scanning every fixed drive (this can take a while)...");
                ScanEveryFixedDrive(candidates, progress, cancellation);
            }

            List<GameCandidate> result = new List<GameCandidate>(candidates.Values);
            result.Sort(delegate(GameCandidate a, GameCandidate b)
            {
                int score = b.Score.CompareTo(a.Score);
                return score != 0 ? score : string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
            });
            Report(progress, result.Count == 0 ? "Dead Space (2008) was not found automatically." : "Found " + result.Count + " valid Dead Space (2008) install" + (result.Count == 1 ? "." : "s."));
            return result;
        }

        private static void AddRunningProcess(Dictionary<string, GameCandidate> result)
        {
            try
            {
                foreach (Process process in Process.GetProcessesByName("Dead Space"))
                {
                    try { Add(result, process.MainModule.FileName, "running game", 100); }
                    catch { }
                    finally { process.Dispose(); }
                }
            }
            catch { }
        }

        private static void AddRegistryLocations(Dictionary<string, GameCandidate> result)
        {
            RegistryHive[] hives = new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
            RegistryView[] views = Environment.Is64BitOperatingSystem
                ? new[] { RegistryView.Registry64, RegistryView.Registry32 }
                : new[] { RegistryView.Registry32 };

            foreach (RegistryHive hive in hives)
            foreach (RegistryView view in views)
            {
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                    {
                        ReadDirectEaKeys(result, baseKey);
                        ReadUninstallKeys(result, baseKey);
                        ReadOriginGameKeys(result, baseKey);
                    }
                }
                catch { }
            }
        }

        private static void ReadDirectEaKeys(Dictionary<string, GameCandidate> result, RegistryKey baseKey)
        {
            string[] keys = new[]
            {
                @"SOFTWARE\EA Games\Dead Space",
                @"SOFTWARE\Electronic Arts\EA Games\Dead Space",
                @"SOFTWARE\WOW6432Node\EA Games\Dead Space",
                @"SOFTWARE\WOW6432Node\Electronic Arts\EA Games\Dead Space"
            };
            foreach (string keyName in keys)
            using (RegistryKey key = baseKey.OpenSubKey(keyName))
            {
                if (key == null) continue;
                AddDirectoryValues(result, key, "EA/Origin registry", 92);
            }
        }

        private static void ReadUninstallKeys(Dictionary<string, GameCandidate> result, RegistryKey baseKey)
        {
            string[] roots = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };
            foreach (string rootName in roots)
            using (RegistryKey root = baseKey.OpenSubKey(rootName))
            {
                if (root == null) continue;
                foreach (string childName in SafeSubKeyNames(root))
                {
                    try
                    {
                        using (RegistryKey child = root.OpenSubKey(childName))
                        {
                            if (child == null) continue;
                            string displayName = Convert.ToString(child.GetValue("DisplayName"));
                            if (!LooksLikeOriginalDeadSpace(displayName)) continue;
                            AddDirectoryValue(result, Convert.ToString(child.GetValue("InstallLocation")), "installed-app registry", 90);
                            AddDirectoryValue(result, Convert.ToString(child.GetValue("Install Dir")), "installed-app registry", 90);
                            AddPossibleCommandPath(result, Convert.ToString(child.GetValue("DisplayIcon")), "installed-app registry", 88);
                        }
                    }
                    catch { }
                }
            }
        }

        private static void ReadOriginGameKeys(Dictionary<string, GameCandidate> result, RegistryKey baseKey)
        {
            string[] roots = new[] { @"SOFTWARE\Origin Games", @"SOFTWARE\WOW6432Node\Origin Games" };
            foreach (string rootName in roots)
            using (RegistryKey root = baseKey.OpenSubKey(rootName))
            {
                if (root == null) continue;
                foreach (string childName in SafeSubKeyNames(root))
                {
                    try
                    {
                        using (RegistryKey child = root.OpenSubKey(childName))
                        {
                            if (child == null) continue;
                            string displayName = Convert.ToString(child.GetValue("DisplayName"));
                            string gameName = Convert.ToString(child.GetValue("GameName"));
                            if (!LooksLikeOriginalDeadSpace(displayName) && !LooksLikeOriginalDeadSpace(gameName)) continue;
                            AddDirectoryValues(result, child, "Origin registry", 91);
                        }
                    }
                    catch { }
                }
            }
        }

        private static void AddDirectoryValues(Dictionary<string, GameCandidate> result, RegistryKey key, string source, int score)
        {
            string[] names = new[] { "Install Dir", "InstallLocation", "InstallPath", "Path" };
            foreach (string name in names)
                AddDirectoryValue(result, Convert.ToString(key.GetValue(name)), source, score);
        }

        private static void AddPossibleCommandPath(Dictionary<string, GameCandidate> result, string value, string source, int score)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string cleaned = value.Trim().Trim('"');
            int comma = cleaned.LastIndexOf(',');
            if (comma > 2) cleaned = cleaned.Substring(0, comma).Trim('"');
            if (File.Exists(cleaned)) Add(result, cleaned, source, score);
            else AddDirectoryValue(result, Path.GetDirectoryName(cleaned), source, score);
        }

        private static void AddDirectoryValue(Dictionary<string, GameCandidate> result, string directory, string source, int score)
        {
            if (string.IsNullOrWhiteSpace(directory)) return;
            string cleaned = Environment.ExpandEnvironmentVariables(directory.Trim().Trim('"'));
            if (File.Exists(cleaned)) Add(result, cleaned, source, score);
            else
            {
                Add(result, Path.Combine(cleaned, "Dead Space.exe"), source, score);
                Add(result, Path.Combine(cleaned, "Dead Space", "Dead Space.exe"), source, score - 1);
            }
        }

        private static void AddSteamLibraries(Dictionary<string, GameCandidate> result)
        {
            HashSet<string> steamRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ReadSteamRoot(Registry.CurrentUser, @"SOFTWARE\Valve\Steam", steamRoots);
            ReadSteamRoot(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", steamRoots);
            ReadSteamRoot(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", steamRoots);

            foreach (string steamRoot in steamRoots)
            {
                AddSteamCandidate(result, steamRoot, 95);
                string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf)) continue;
                try
                {
                    string content = File.ReadAllText(vdf);
                    MatchCollection modern = Regex.Matches(content, "\\\"path\\\"\\s*\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase);
                    MatchCollection legacy = Regex.Matches(content, "\\\"\\d+\\\"\\s*\\\"([^\\\"]+)\\\"");
                    foreach (Match match in modern) AddSteamCandidate(result, UnescapeVdf(match.Groups[1].Value), 94);
                    foreach (Match match in legacy) AddSteamCandidate(result, UnescapeVdf(match.Groups[1].Value), 93);
                }
                catch { }
            }
        }

        private static void ReadSteamRoot(RegistryKey root, string subKey, HashSet<string> roots)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(subKey))
                {
                    if (key == null) return;
                    string path = Convert.ToString(key.GetValue("SteamPath"));
                    if (string.IsNullOrWhiteSpace(path)) path = Convert.ToString(key.GetValue("InstallPath"));
                    if (!string.IsNullOrWhiteSpace(path)) roots.Add(path.Replace('/', '\\'));
                }
            }
            catch { }
        }

        private static void AddSteamCandidate(Dictionary<string, GameCandidate> result, string libraryRoot, int score)
        {
            if (string.IsNullOrWhiteSpace(libraryRoot)) return;
            string steamApps = libraryRoot.EndsWith("steamapps", StringComparison.OrdinalIgnoreCase)
                ? libraryRoot
                : Path.Combine(libraryRoot, "steamapps");
            Add(result, Path.Combine(steamApps, "common", "Dead Space", "Dead Space.exe"), "Steam library", score);
        }

        private static string UnescapeVdf(string value)
        {
            return value.Replace("\\\\", "\\").Replace("/", "\\");
        }

        private static void AddCommonPathsOnEveryFixedDrive(Dictionary<string, GameCandidate> result)
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                    foreach (string relative in CommonRelativePaths)
                        Add(result, Path.Combine(drive.RootDirectory.FullName, relative), "common path on " + drive.Name, 80);
                }
                catch { }
            }
        }

        private static void ScanEveryFixedDrive(Dictionary<string, GameCandidate> result, Action<string> progress, CancellationToken cancellation)
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                cancellation.ThrowIfCancellationRequested();
                try
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                    Report(progress, "Scanning " + drive.Name + " for Dead Space.exe...");
                    ScanDirectoryTree(drive.RootDirectory.FullName, result, progress, cancellation);
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }
        }

        private static void ScanDirectoryTree(string root, Dictionary<string, GameCandidate> result, Action<string> progress, CancellationToken cancellation)
        {
            Stack<string> pending = new Stack<string>();
            pending.Push(root);
            int visited = 0;
            while (pending.Count > 0)
            {
                cancellation.ThrowIfCancellationRequested();
                string directory = pending.Pop();
                visited++;
                if (visited % 750 == 0) Report(progress, "Scanning " + Path.GetPathRoot(root) + " — " + visited.ToString("N0") + " folders checked...");
                try
                {
                    string candidate = Path.Combine(directory, "Dead Space.exe");
                    if (File.Exists(candidate)) Add(result, candidate, "full fixed-drive scan", 60);
                    if (result.Count >= 20) return;

                    foreach (string child in Directory.EnumerateDirectories(directory))
                    {
                        if (ShouldSkipDirectory(child)) continue;
                        pending.Push(child);
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
                catch (IOException) { }
                catch (System.Security.SecurityException) { }
            }
        }

        private static bool ShouldSkipDirectory(string path)
        {
            string name = Path.GetFileName(path);
            if (name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("WinSxS", StringComparison.OrdinalIgnoreCase))
                return true;
            try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
            catch { return true; }
        }

        private static bool LooksLikeOriginalDeadSpace(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string lowered = name.ToLowerInvariant();
            return lowered.Contains("dead space") && !lowered.Contains("remake") && !lowered.Contains("2023") &&
                   !lowered.Contains("dead space 2") && !lowered.Contains("dead space 3");
        }

        private static void Add(Dictionary<string, GameCandidate> result, string path, string source, int score)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string full;
            try { full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'))); }
            catch { return; }
            if (PeInspector.ValidateDeadSpace2008(full) != null) return;

            GameCandidate existing;
            if (result.TryGetValue(full, out existing))
            {
                if (score > existing.Score) { existing.Score = score; existing.Source = source; }
                return;
            }
            result.Add(full, new GameCandidate { Path = full, Source = source, Score = score });
        }

        private static string[] SafeSubKeyNames(RegistryKey key)
        {
            try { return key.GetSubKeyNames(); }
            catch { return new string[0]; }
        }

        private static void Report(Action<string> progress, string message)
        {
            Log.Info(message);
            if (progress != null) progress(message);
        }
    }
}
