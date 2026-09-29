using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DeadSpaceTextureLauncher
{
    // EA's initial game process can leave its ActivationUI child alive after
    // the texture handoff. EA then keeps Play disabled after the real game exits.
    internal sealed class EaActivationCleanup
    {
        private sealed class HelperIdentity
        {
            public int ProcessId;
            public DateTime StartTimeUtc;
        }

        private readonly string expectedPath;
        private readonly List<HelperIdentity> helpers = new List<HelperIdentity>();

        private EaActivationCleanup(string expectedPath)
        {
            this.expectedPath = expectedPath;
        }

        public static EaActivationCleanup Capture(int bootstrapProcessId, string gameRoot)
        {
            if (bootstrapProcessId <= 0 || string.IsNullOrWhiteSpace(gameRoot)) return null;
            string expectedPath = Path.GetFullPath(Path.Combine(gameRoot, "Core", "ActivationUI.exe"));
            EaActivationCleanup cleanup = new EaActivationCleanup(expectedPath);
            foreach (int processId in NativeMethods.ChildProcessIds(bootstrapProcessId, "ActivationUI.exe"))
            {
                try
                {
                    using (Process process = Process.GetProcessById(processId))
                    {
                        if (process.HasExited || !cleanup.IsExpectedPath(process)) continue;
                        cleanup.helpers.Add(new HelperIdentity
                        {
                            ProcessId = process.Id,
                            StartTimeUtc = process.StartTime.ToUniversalTime()
                        });
                        Log.Info("Tracking EA activation helper from bootstrap PID=" +
                            bootstrapProcessId + ": PID=" + process.Id);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("Could not inspect EA activation helper PID=" + processId + ": " + ex.Message);
                }
            }
            return cleanup;
        }

        public void StopAfterGameExit()
        {
            if (helpers.Count == 0) return;
            foreach (Process game in Process.GetProcessesByName("Dead Space"))
            {
                using (game)
                {
                    if (!game.HasExited)
                    {
                        Log.Warn("Leaving EA activation helper running because a Dead Space process is still active.");
                        return;
                    }
                }
            }
            foreach (HelperIdentity helper in helpers)
            {
                try
                {
                    using (Process process = Process.GetProcessById(helper.ProcessId))
                    {
                        if (process.HasExited || process.StartTime.ToUniversalTime() != helper.StartTimeUtc ||
                            !IsExpectedPath(process)) continue;
                        process.Kill();
                        if (process.WaitForExit(5000))
                            Log.Info("Closed orphan EA activation helper after game exit. PID=" + helper.ProcessId);
                        else
                            Log.Warn("EA activation helper did not exit after cleanup. PID=" + helper.ProcessId);
                    }
                }
                catch (ArgumentException) { } // The helper already exited.
                catch (Exception ex)
                {
                    Log.Warn("Could not close EA activation helper PID=" + helper.ProcessId + ": " + ex.Message);
                }
            }
        }

        private bool IsExpectedPath(Process process)
        {
            return string.Equals(Path.GetFullPath(process.MainModule.FileName), expectedPath,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
