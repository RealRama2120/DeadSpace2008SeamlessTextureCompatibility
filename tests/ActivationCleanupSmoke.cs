using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace DeadSpaceTextureLauncher
{
    internal static class Log
    {
        public static void Info(string message) { Console.WriteLine("INFO " + message); }
        public static void Warn(string message) { Console.WriteLine("WARN " + message); }
    }

    internal static class ActivationCleanupSmoke
    {
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "helper")
            {
                Thread.Sleep(60000);
                return 0;
            }
            if (args.Length > 0 && args[0] == "parent")
            {
                Process good = StartHelper(Path.Combine(args[1], "Core", "ActivationUI.exe"));
                Process decoy = StartHelper(Path.Combine(args[1], "decoy", "ActivationUI.exe"));
                File.WriteAllText(Path.Combine(args[1], "children.txt"), good.Id + "," + decoy.Id);
                Thread.Sleep(300);
                return 0;
            }

            string root = Path.Combine(Path.GetDirectoryName(typeof(ActivationCleanupSmoke).Assembly.Location), "fixture");
            Directory.CreateDirectory(Path.Combine(root, "Core"));
            Directory.CreateDirectory(Path.Combine(root, "decoy"));
            string self = typeof(ActivationCleanupSmoke).Assembly.Location;
            File.Copy(self, Path.Combine(root, "Core", "ActivationUI.exe"), true);
            File.Copy(self, Path.Combine(root, "decoy", "ActivationUI.exe"), true);
            string marker = Path.Combine(root, "children.txt");
            if (File.Exists(marker)) File.Delete(marker);

            ProcessStartInfo parentStart = new ProcessStartInfo(self, "parent \"" + root + "\"");
            parentStart.UseShellExecute = false;
            Process parent = Process.Start(parentStart);
            for (int i = 0; i < 100 && !File.Exists(marker); i++) Thread.Sleep(50);
            if (!File.Exists(marker)) throw new Exception("Fixture parent did not report helper PIDs.");
            string[] ids = File.ReadAllText(marker).Split(',');
            int goodId = int.Parse(ids[0]);
            int decoyId = int.Parse(ids[1]);
            parent.WaitForExit();

            try
            {
                EaActivationCleanup cleanup = EaActivationCleanup.Capture(parent.Id, root);
                cleanup.StopAfterGameExit();
                bool goodExited;
                bool decoyAlive;
                try { using (Process good = Process.GetProcessById(goodId)) goodExited = good.WaitForExit(5000); }
                catch (ArgumentException) { goodExited = true; }
                using (Process decoy = Process.GetProcessById(decoyId)) decoyAlive = !decoy.HasExited;
                Console.WriteLine("expected helper exited=" + goodExited + " decoy preserved=" + decoyAlive);
                return goodExited && decoyAlive ? 0 : 1;
            }
            finally
            {
                foreach (int id in new[] { goodId, decoyId })
                {
                    try { using (Process process = Process.GetProcessById(id)) process.Kill(); }
                    catch (ArgumentException) { }
                }
            }
        }

        private static Process StartHelper(string path)
        {
            ProcessStartInfo start = new ProcessStartInfo(path, "helper");
            start.UseShellExecute = false;
            return Process.Start(start);
        }
    }
}
