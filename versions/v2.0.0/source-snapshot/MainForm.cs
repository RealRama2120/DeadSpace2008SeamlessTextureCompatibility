using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeadSpaceTextureLauncher
{
    internal sealed class MainForm : Form
    {
        private LauncherSettings settings;
        private readonly LaunchInvocation invocation;
        private readonly Label status = new Label();
        private readonly Label details = new Label();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly Button launchButton = new Button();
        private readonly Button settingsButton = new Button();
        private bool launchRunning;
        private bool allowClose;

        public MainForm(LaunchInvocation invocation)
        {
            this.invocation = invocation;
            Text = AppInfo.Name + " " + AppInfo.Version;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(650, 355);
            MinimumSize = new Size(600, 380);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(18, 21, 25);
            ForeColor = Color.WhiteSmoke;
            MaximizeBox = false;
            BuildInterface();
            Shown += OnShown;
            FormClosing += OnFormClosing;
        }

        private void BuildInterface()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(28);
            root.ColumnCount = 1;
            root.RowCount = 7;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            Label heading = new Label();
            heading.Text = "DEAD SPACE (2008)";
            heading.Font = new Font("Segoe UI Semibold", 20F);
            heading.ForeColor = Color.FromArgb(231, 232, 234);
            heading.AutoSize = true;
            root.Controls.Add(heading);

            Label subheading = new Label();
            subheading.Text = "SEAMLESS TEXTURE LAUNCHER  •  by Rama2120";
            subheading.Font = new Font("Segoe UI Semibold", 9F);
            subheading.ForeColor = Color.FromArgb(195, 74, 81);
            subheading.AutoSize = true;
            subheading.Margin = new Padding(2, 0, 0, 18);
            root.Controls.Add(subheading);

            Panel statusPanel = new Panel();
            statusPanel.Dock = DockStyle.Fill;
            statusPanel.BackColor = Color.FromArgb(29, 33, 38);
            statusPanel.Padding = new Padding(18);
            root.Controls.Add(statusPanel);
            status.Dock = DockStyle.Top;
            status.Text = "Preparing...";
            status.Font = new Font("Segoe UI Semibold", 12F);
            status.Height = 32;
            statusPanel.Controls.Add(status);
            details.Dock = DockStyle.Fill;
            details.ForeColor = Color.Gainsboro;
            details.Padding = new Padding(0, 8, 0, 0);
            details.AutoEllipsis = true;
            statusPanel.Controls.Add(details);
            details.BringToFront();

            progress.Dock = DockStyle.Top;
            progress.Height = 5;
            progress.Style = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 25;
            progress.Visible = false;
            progress.Margin = new Padding(0, 14, 0, 12);
            root.Controls.Add(progress);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Top;
            buttons.AutoSize = true;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            root.Controls.Add(buttons);
            launchButton.Text = "Launch with textures";
            launchButton.AutoSize = true;
            launchButton.Padding = new Padding(14, 7, 14, 7);
            launchButton.BackColor = Color.FromArgb(167, 44, 53);
            launchButton.ForeColor = Color.White;
            launchButton.FlatStyle = FlatStyle.Flat;
            launchButton.Click += delegate { BeginLaunch(); };
            buttons.Controls.Add(launchButton);
            settingsButton.Text = "Settings";
            settingsButton.AutoSize = true;
            settingsButton.Padding = new Padding(10, 7, 10, 7);
            settingsButton.Click += OpenSettings;
            buttons.Controls.Add(settingsButton);
            Button logButton = new Button();
            logButton.Text = "Open log";
            logButton.AutoSize = true;
            logButton.Padding = new Padding(10, 7, 10, 7);
            logButton.Click += OpenLog;
            buttons.Controls.Add(logButton);

            Label hint = new Label();
            hint.Text = "Tip: hold Shift while opening the launcher to change settings without auto-launching.";
            hint.AutoSize = true;
            hint.ForeColor = Color.DarkGray;
            hint.Margin = new Padding(0, 15, 0, 0);
            root.Controls.Add(hint);
        }

        private void OnShown(object sender, EventArgs e)
        {
            settings = SettingsStore.Load();
            bool shiftHeld = (ModifierKeys & Keys.Shift) == Keys.Shift;
            bool configuredThisRun = false;
            if (!settings.IsComplete || settings.SchemaVersion < 2)
            {
                if (!RunConfiguration(settings.SchemaVersion < 2))
                {
                    allowClose = true;
                    Close();
                    return;
                }
                configuredThisRun = true;
            }

            UpdateSummary();
            if (invocation.ConfigureOnly || shiftHeld)
            {
                if (!configuredThisRun) RunConfiguration(false);
                UpdateSummary();
                return;
            }
            if (configuredThisRun)
            {
                status.Text = "Setup complete";
                details.Text = settings.IntegrationMode == IntegrationModes.EaProxy
                    ? "Close this window and use the normal Play button in EA App from now on."
                    : settings.IntegrationMode == IntegrationModes.SteamLaunchOption
                        ? "Paste the copied launch option into Steam once, then use Steam's normal Play button."
                        : "Setup is saved. Use Launch with textures, or return to Settings to install normal-launch integration.";
                return;
            }
            if (settings.IntegrationMode == IntegrationModes.EaProxy && !invocation.FromEaProxy)
            {
                status.Text = "Launch from EA App";
                details.Text = "Normal-launch integration is installed. Use Dead Space's regular Play button in EA App.";
                launchButton.Enabled = false;
                return;
            }
            BeginInvoke((MethodInvoker)delegate { BeginLaunch(); });
        }

        private bool RunConfiguration(bool firstRun)
        {
            using (ConfigurationForm form = new ConfigurationForm(settings ?? new LauncherSettings(), firstRun))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return false;
                settings = form.Result;
                return true;
            }
        }

        private void OpenSettings(object sender, EventArgs e)
        {
            if (launchRunning) return;
            RunConfiguration(false);
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            string validation = settings == null ? "Setup is incomplete." : PeInspector.ValidateDeadSpace2008(settings.GamePath);
            if (settings == null || !settings.IsComplete || validation != null)
            {
                status.Text = "Setup needs attention";
                details.Text = validation ?? "Open Settings and choose Dead Space plus a verified TexMod 0.9b copy. Texture packs can be added later.";
                launchButton.Enabled = false;
                return;
            }
            int discovered = PackageDiscovery.Discover(settings).Count;
            status.Text = "Ready";
            details.Text = Path.GetDirectoryName(settings.GamePath) + Environment.NewLine + discovered + " texture pack" + (discovered == 1 ? "" : "s") + " discovered for the next launch";
            launchButton.Enabled = true;
        }

        private void BeginLaunch()
        {
            if (launchRunning) return;
            launchRunning = true;
            launchButton.Enabled = false;
            settingsButton.Enabled = false;
            progress.Visible = true;
            status.Text = "Starting...";
            details.Text = "The launcher is taking care of TexMod.";
            CancellationTokenSource cancellation = new CancellationTokenSource();

            Task.Factory.StartNew(delegate
            {
                return RunEffectiveLaunch(cancellation.Token);
            }, cancellation.Token).ContinueWith(delegate(Task<LaunchResult> task)
            {
                if (IsDisposed) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    launchRunning = false;
                    progress.Visible = false;
                    settingsButton.Enabled = true;
                    if (task.IsFaulted)
                    {
                        Exception error = task.Exception.GetBaseException();
                        Log.Error(error.ToString());
                        status.Text = "Launch stopped";
                        details.Text = error.Message + Environment.NewLine + "TexMod was left visible when possible. Open the log for troubleshooting.";
                        launchButton.Enabled = true;
                        Show();
                        WindowState = FormWindowState.Normal;
                        Activate();
                        if (AppInfo.TestMode)
                        {
                            allowClose = true;
                            Close();
                        }
                        else
                        {
                            MessageBox.Show(this, details.Text, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        return;
                    }
                    if (task.IsCanceled)
                    {
                        status.Text = "Launch cancelled";
                        details.Text = "No changes were made to the game files.";
                        launchButton.Enabled = true;
                        return;
                    }
                    allowClose = true;
                    Close();
                });
            });
        }

        private LaunchResult RunEffectiveLaunch(CancellationToken cancellation)
        {
            LauncherSettings effective = settings.Clone();
            EaLaunchSwap eaSwap = null;
            try
            {
                if (invocation.FromEaProxy)
                {
                    string root = string.IsNullOrWhiteSpace(invocation.GameRoot) ? settings.GameRoot : invocation.GameRoot;
                    eaSwap = EaLaunchSwap.Begin(root, invocation.ProxyProcessId);
                    effective.GamePath = eaSwap.RuntimeGamePath;
                }
                else if (invocation.FromSteam && PeInspector.ValidateDeadSpace2008(invocation.ForwardedGamePath) == null)
                {
                    effective.GamePath = invocation.ForwardedGamePath;
                }

                effective.Packages.Clear();
                effective.Packages.AddRange(PackageDiscovery.Discover(settings));
                if (effective.Packages.Count == 0)
                    return RunWithoutTextures(effective.GamePath, cancellation);

                TexModAutomation automation = new TexModAutomation(effective, SetStatusSafe);
                return automation.Run(cancellation);
            }
            finally
            {
                if (eaSwap != null) eaSwap.Dispose();
            }
        }

        private LaunchResult RunWithoutTextures(string gamePath, CancellationToken cancellation)
        {
            string validation = PeInspector.ValidateDeadSpace2008(gamePath);
            if (validation != null) throw new LauncherException(validation);
            Log.Info("No .tpf files were discovered; launching the verified base game without TexMod: " + gamePath);
            SetStatusSafe("No .tpf files found — launching the base game normally.");
            ProcessStartInfo start = new ProcessStartInfo(gamePath);
            start.WorkingDirectory = Path.GetDirectoryName(gamePath);
            start.UseShellExecute = false;
            if (invocation.FromSteam) start.Arguments = invocation.ForwardedArguments;
            using (Process game = Process.Start(start))
            {
                if (game == null) throw new LauncherException("Windows could not start Dead Space.");
                while (!game.WaitForExit(500)) cancellation.ThrowIfCancellationRequested();
                Log.Info("Base game session ended normally. PID=" + game.Id);
                return new LaunchResult { GameProcessId = game.Id, TimeUntilWindow = TimeSpan.Zero };
            }
        }

        private void SetStatusSafe(string message)
        {
            if (IsDisposed) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    status.Text = message;
                    details.Text = "Do not start a second copy. Details are being written to latest.log.";
                    if (message.StartsWith("Dead Space is ready", StringComparison.OrdinalIgnoreCase))
                        Hide();
                });
            }
            catch { }
        }

        private void OpenLog(object sender, EventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppInfo.DataDirectory);
                if (!File.Exists(AppInfo.LogPath)) File.WriteAllText(AppInfo.LogPath, "No launch has been attempted yet.");
                Process.Start("notepad.exe", "\"" + AppInfo.LogPath + "\"");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (allowClose || !launchRunning) return;
            e.Cancel = true;
            Hide();
            Log.Info("Launcher window hidden while the active game session is monitored.");
        }
    }
}
