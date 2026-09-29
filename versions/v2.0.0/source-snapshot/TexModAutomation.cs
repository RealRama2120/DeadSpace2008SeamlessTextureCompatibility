using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace DeadSpaceTextureLauncher
{
    internal sealed class LaunchResult
    {
        public int GameProcessId;
        public TimeSpan TimeUntilWindow;
    }

    internal sealed class TexModAutomation
    {
        private readonly LauncherSettings settings;
        private readonly Action<string> report;
        private Process texModProcess;
        private IntPtr texModWindow;
        private bool movedOutOfSight;

        public TexModAutomation(LauncherSettings settings, Action<string> report)
        {
            this.settings = settings;
            this.report = report;
        }

        public LaunchResult Run(CancellationToken cancellation)
        {
            ValidateInputs();
            EnsureCleanStart();
            HashSet<int> existingGameProcesses = ExistingGameProcessIds();

            try
            {
                Status("Starting TexMod...");
                ProcessStartInfo start = new ProcessStartInfo(settings.TexModPath);
                start.WorkingDirectory = Path.GetDirectoryName(settings.TexModPath);
                start.UseShellExecute = false;
                texModProcess = Process.Start(start);
                if (texModProcess == null) throw new LauncherException("Windows could not start TexMod.");
                Log.Info("TexMod started. PID=" + texModProcess.Id);

                texModWindow = WaitForWindow((uint)texModProcess.Id, "tmlwndcls", TimeSpan.FromSeconds(AppInfo.TestMode ? 1 : 20), cancellation);
                if (texModWindow == IntPtr.Zero)
                    texModWindow = WaitForAnyMainWindow(texModProcess, TimeSpan.FromSeconds(4), cancellation);
                if (texModWindow == IntPtr.Zero)
                    throw new LauncherException("TexMod opened, but its main window was not found. Use TexMod 0.9b and try again.");

                Log.Info("TexMod window: class=" + NativeMethods.WindowClass(texModWindow) + ", title=" + NativeMethods.WindowText(texModWindow));
                Thread.Sleep(settings.ActionDelayMs);
                EnsurePackageMode();
                TexModControls controls = LocateControls();

                if (settings.KeepTexModOutOfSight)
                {
                    NativeMethods.SetWindowPos(texModWindow, IntPtr.Zero, -32000, -32000, 0, 0,
                        NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
                    movedOutOfSight = true;
                    Log.Info("TexMod was moved off-screen during automation.");
                }

                Status("Selecting Dead Space (2008)...");
                SelectFile(controls.TargetBrowse, settings.GamePath, "target game", cancellation);
                WaitUntil(delegate
                {
                    return NativeMethods.WindowText(texModWindow).IndexOf("Dead Space.exe", StringComparison.OrdinalIgnoreCase) >= 0;
                }, TimeSpan.FromSeconds(12), "TexMod to accept Dead Space.exe", cancellation);

                int initialCount = PackageCount(controls.PackageList);
                for (int i = 0; i < settings.Packages.Count; i++)
                {
                    string package = settings.Packages[i];
                    Status("Loading texture pack " + (i + 1) + " of " + settings.Packages.Count + ": " + Path.GetFileName(package));
                    SelectFile(controls.PackageBrowse, package, "texture pack", cancellation);
                    if (controls.PackageList != IntPtr.Zero)
                    {
                        int expected = initialCount + i + 1;
                        WaitUntil(delegate { return PackageCount(controls.PackageList) >= expected; },
                            TimeSpan.FromSeconds(20), "TexMod to list " + Path.GetFileName(package), cancellation);
                    }
                }

                Status("Launching Dead Space with textures...");
                NativeMethods.Click(controls.Run);
                Process game = WaitForNewGameProcess(existingGameProcesses, TimeSpan.FromMinutes(2), cancellation);
                Log.Info("Game process started. PID=" + game.Id);
                Status("Textures are loading. Large packs can leave the screen blank for several minutes — please wait.");

                Stopwatch windowTimer = Stopwatch.StartNew();
                IntPtr gameWindow = WaitForGameWindow(game, TimeSpan.FromMinutes(settings.GameWindowTimeoutMinutes), cancellation);
                windowTimer.Stop();
                if (gameWindow != IntPtr.Zero)
                {
                    NativeMethods.ForceForeground(gameWindow);
                    Status("Dead Space is ready. Have fun!");
                    Log.Info("Game window appeared after " + windowTimer.Elapsed.TotalSeconds.ToString("0.0") + " seconds.");
                }
                else
                {
                    Status("The game is still running, but no window appeared before the timeout. Check the log if it stays blank.");
                    Log.Warn("Game process is alive but no main window appeared before the configured timeout.");
                }

                int gameId = game.Id;
                WaitForGameExit(game, cancellation);
                if (settings.CloseTexModWithGame) CloseOwnedTexMod();
                return new LaunchResult { GameProcessId = gameId, TimeUntilWindow = windowTimer.Elapsed };
            }
            catch
            {
                RestoreTexModForManualFallback();
                throw;
            }
        }

        private void ValidateInputs()
        {
            string gameError = PeInspector.ValidateDeadSpace2008(settings.GamePath);
            if (gameError != null) throw new LauncherException(gameError);
            if (!File.Exists(settings.TexModPath)) throw new LauncherException("TexMod.exe was not found. Open Settings and choose your copy.");
            if (PeInspector.GetArchitecture(settings.TexModPath) == PeArchitecture.X64)
                throw new LauncherException("The selected TexMod is 64-bit. Dead Space (2008) needs the classic 32-bit TexMod 0.9b.");
            if (settings.Packages.Count == 0) throw new LauncherException("No .tpf texture packs are selected.");
            foreach (string package in settings.Packages)
            {
                if (!File.Exists(package)) throw new LauncherException("Texture pack not found: " + package);
                if (!Path.GetExtension(package).Equals(".tpf", StringComparison.OrdinalIgnoreCase))
                    throw new LauncherException("This is not a .tpf texture pack: " + package);
            }
        }

        private void EnsureCleanStart()
        {
            foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(settings.TexModPath)))
            {
                try
                {
                    string runningPath = process.MainModule.FileName;
                    if (string.Equals(Path.GetFullPath(runningPath), Path.GetFullPath(settings.TexModPath), StringComparison.OrdinalIgnoreCase))
                        throw new LauncherException("TexMod is already open. Close it once, then start the launcher again.");
                }
                catch (LauncherException) { throw; }
                catch { }
                finally { process.Dispose(); }
            }

            if (Process.GetProcessesByName("Dead Space").Length > 0)
                throw new LauncherException("Dead Space is already running. Close it before starting a texture-modded session.");
        }

        private void EnsurePackageMode()
        {
            if (NativeMethods.ChildById(texModWindow, 110, "SysListView32") != IntPtr.Zero) return;
            foreach (IntPtr button in NativeMethods.ChildWindows(texModWindow, "Button"))
            {
                string text = NativeMethods.WindowText(button);
                if (text.IndexOf("Package Mode", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    NativeMethods.Click(button);
                    Thread.Sleep(settings.ActionDelayMs);
                    break;
                }
            }
        }

        private TexModControls LocateControls()
        {
            TexModControls controls = new TexModControls();
            controls.TargetBrowse = NativeMethods.ChildById(texModWindow, 100, "Button");
            controls.Run = NativeMethods.ChildById(texModWindow, 103, "Button");
            controls.PackageList = NativeMethods.ChildById(texModWindow, 110, "SysListView32");
            controls.PackageBrowse = NativeMethods.ChildById(texModWindow, 111, "Button");

            List<IntPtr> buttons = NativeMethods.ChildWindows(texModWindow, "Button");
            foreach (IntPtr button in buttons)
            {
                Log.Info("TexMod control: class=Button id=" + NativeMethods.GetDlgCtrlID(button) + " text='" + NativeMethods.WindowText(button) + "'");
                if (controls.Run == IntPtr.Zero && NativeMethods.WindowText(button).Equals("Run", StringComparison.OrdinalIgnoreCase))
                    controls.Run = button;
            }

            // Classic TexMod 0.9b has stable dialog IDs. Some repacks strip those IDs,
            // so retain the observed z-order fallback while still verifying each action.
            if (controls.TargetBrowse == IntPtr.Zero && buttons.Count > 1) controls.TargetBrowse = buttons[1];
            if (controls.Run == IntPtr.Zero && buttons.Count > 10) controls.Run = buttons[10];
            if (controls.PackageBrowse == IntPtr.Zero && buttons.Count > 11) controls.PackageBrowse = buttons[11];

            if (controls.TargetBrowse == IntPtr.Zero || controls.Run == IntPtr.Zero || controls.PackageBrowse == IntPtr.Zero)
                throw new LauncherException("This TexMod window does not match classic TexMod 0.9b. The launcher left it open so you can inspect it.");
            if (controls.PackageList == IntPtr.Zero)
                Log.Warn("TexMod package list control was not found; package additions will be verified by dialog completion only.");
            return controls;
        }

        private void SelectFile(IntPtr browseButton, string path, string description, CancellationToken cancellation)
        {
            IntPtr dialog = IntPtr.Zero;
            for (int attempt = 0; attempt < 2 && dialog == IntPtr.Zero; attempt++)
            {
                NativeMethods.SetForegroundWindow(texModWindow);
                NativeMethods.Click(browseButton);
                Stopwatch timer = Stopwatch.StartNew();
                bool menuHandled = false;
                while (timer.Elapsed < TimeSpan.FromSeconds(10))
                {
                    cancellation.ThrowIfCancellationRequested();
                    dialog = NativeMethods.TopWindow((uint)texModProcess.Id, "#32770", true);
                    if (dialog != IntPtr.Zero) break;
                    if (!menuHandled)
                    {
                        IntPtr menu = NativeMethods.TopWindow((uint)texModProcess.Id, "#32768", true);
                        if (menu != IntPtr.Zero)
                        {
                            PostKey(texModWindow, 0x28); // Down to Browse...
                            PostKey(texModWindow, 0x0D); // Enter
                            menuHandled = true;
                        }
                    }
                    Thread.Sleep(100);
                }
            }

            if (dialog == IntPtr.Zero)
                throw new LauncherException("TexMod did not open the " + description + " file picker. Try increasing the UI delay in Settings.");

            Thread.Sleep(settings.ActionDelayMs);
            SetDialogFileName(dialog, path, description);
            IntPtr ok = NativeMethods.GetDlgItem(dialog, 1);
            if (ok == IntPtr.Zero) throw new LauncherException("Windows opened a file picker, but its Open button could not be controlled.");
            NativeMethods.Click(ok);
            WaitUntil(delegate { return !NativeMethods.IsWindow(dialog) || !NativeMethods.IsWindowVisible(dialog); },
                TimeSpan.FromSeconds(12), description + " file picker to close", cancellation);
            Thread.Sleep(settings.ActionDelayMs);
        }

        private static void SetDialogFileName(IntPtr dialog, string path, string description)
        {
            IntPtr field = NativeMethods.GetDlgItem(dialog, 1148);
            if (field != IntPtr.Zero)
            {
                List<IntPtr> nestedEdits = NativeMethods.ChildWindows(field, "Edit");
                if (nestedEdits.Count > 0) field = nestedEdits[0];
            }
            if (field == IntPtr.Zero) field = NativeMethods.GetDlgItem(dialog, 1152);
            if (field == IntPtr.Zero)
            {
                List<IntPtr> edits = NativeMethods.ChildWindows(dialog, "Edit");
                if (edits.Count > 0) field = edits[edits.Count - 1];
            }
            if (field == IntPtr.Zero)
                throw new LauncherException("The Windows file picker did not expose its filename field for " + description + ".");
            NativeMethods.SendMessage(field, NativeMethods.WM_SETTEXT, IntPtr.Zero, path);
            Log.Info("Set " + description + " picker to: " + path);
        }

        private static void PostKey(IntPtr window, int virtualKey)
        {
            NativeMethods.PostMessage(window, NativeMethods.WM_KEYDOWN, new IntPtr(virtualKey), IntPtr.Zero);
            Thread.Sleep(80);
            NativeMethods.PostMessage(window, NativeMethods.WM_KEYUP, new IntPtr(virtualKey), IntPtr.Zero);
            Thread.Sleep(80);
        }

        private static int PackageCount(IntPtr packageList)
        {
            if (packageList == IntPtr.Zero) return -1;
            return NativeMethods.SendMessage(packageList, NativeMethods.LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
        }

        private HashSet<int> ExistingGameProcessIds()
        {
            HashSet<int> ids = new HashSet<int>();
            foreach (Process process in Process.GetProcessesByName("Dead Space"))
            {
                ids.Add(process.Id);
                process.Dispose();
            }
            return ids;
        }

        private Process WaitForNewGameProcess(HashSet<int> existingIds, TimeSpan timeout, CancellationToken cancellation)
        {
            Process found = null;
            WaitUntil(delegate
            {
                if (texModProcess.HasExited)
                    throw new LauncherException("TexMod closed before it started Dead Space. Open the log for details.");
                foreach (Process process in Process.GetProcessesByName("Dead Space"))
                {
                    if (!existingIds.Contains(process.Id)) { found = process; return true; }
                    process.Dispose();
                }
                return false;
            }, timeout, "Dead Space.exe to start", cancellation);
            return found;
        }

        private IntPtr WaitForGameWindow(Process game, TimeSpan timeout, CancellationToken cancellation)
        {
            IntPtr handle = IntPtr.Zero;
            DateTime nextNotice = DateTime.UtcNow.AddSeconds(20);
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.Elapsed < timeout)
            {
                cancellation.ThrowIfCancellationRequested();
                game.Refresh();
                if (game.HasExited) throw new LauncherException("Dead Space exited before showing a window. Steam/EA may need to be open, or TexMod may have been blocked.");
                handle = game.MainWindowHandle;
                if (handle != IntPtr.Zero) return handle;
                if (DateTime.UtcNow >= nextNotice)
                {
                    Status("Still loading textures... " + timer.Elapsed.TotalSeconds.ToString("0") + " seconds");
                    nextNotice = DateTime.UtcNow.AddSeconds(20);
                }
                Thread.Sleep(500);
            }
            return handle;
        }

        private static void WaitForGameExit(Process game, CancellationToken cancellation)
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                game.Refresh();
                if (game.HasExited) return;
                Thread.Sleep(1000);
            }
        }

        private static IntPtr WaitForWindow(uint processId, string className, TimeSpan timeout, CancellationToken cancellation)
        {
            IntPtr result = IntPtr.Zero;
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.Elapsed < timeout)
            {
                cancellation.ThrowIfCancellationRequested();
                result = NativeMethods.TopWindow(processId, className, true);
                if (result != IntPtr.Zero) return result;
                Thread.Sleep(100);
            }
            return result;
        }

        private static IntPtr WaitForAnyMainWindow(Process process, TimeSpan timeout, CancellationToken cancellation)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.Elapsed < timeout)
            {
                cancellation.ThrowIfCancellationRequested();
                process.Refresh();
                if (process.HasExited) return IntPtr.Zero;
                if (process.MainWindowHandle != IntPtr.Zero) return process.MainWindowHandle;
                Thread.Sleep(100);
            }
            return IntPtr.Zero;
        }

        private static void WaitUntil(Func<bool> condition, TimeSpan timeout, string description, CancellationToken cancellation)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.Elapsed < timeout)
            {
                cancellation.ThrowIfCancellationRequested();
                if (condition()) return;
                Thread.Sleep(100);
            }
            throw new LauncherException("Timed out waiting for " + description + ". Try increasing the UI delay in Settings.");
        }

        private void CloseOwnedTexMod()
        {
            try
            {
                if (texModProcess == null || texModProcess.HasExited) return;
                Log.Info("Game closed; asking this launcher's TexMod process to close.");
                NativeMethods.PostMessage(texModWindow, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                if (!texModProcess.WaitForExit(4000))
                {
                    Log.Warn("TexMod did not close after WM_CLOSE; leaving it running rather than forcing termination.");
                    RestoreTexModForManualFallback();
                }
            }
            catch (Exception ex) { Log.Warn("Could not close TexMod cleanly: " + ex.Message); }
        }

        private void RestoreTexModForManualFallback()
        {
            try
            {
                if (!movedOutOfSight || texModProcess == null || texModProcess.HasExited || texModWindow == IntPtr.Zero) return;
                int offset = 0;
                foreach (IntPtr window in NativeMethods.TopWindows((uint)texModProcess.Id))
                {
                    NativeMethods.SetWindowPos(window, IntPtr.Zero, 80 + offset, 80 + offset, 0, 0,
                        NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
                    NativeMethods.ShowWindow(window, NativeMethods.SW_RESTORE);
                    offset += 24;
                }
                NativeMethods.ForceForeground(texModWindow);
                movedOutOfSight = false;
                Log.Info("TexMod was restored on-screen for manual fallback.");
            }
            catch { }
        }

        private void Status(string message)
        {
            Log.Info(message);
            if (report != null) report(message);
        }

        private sealed class TexModControls
        {
            public IntPtr TargetBrowse;
            public IntPtr Run;
            public IntPtr PackageBrowse;
            public IntPtr PackageList;
        }
    }
}
