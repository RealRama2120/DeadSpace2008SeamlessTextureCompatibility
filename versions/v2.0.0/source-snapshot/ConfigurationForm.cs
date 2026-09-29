using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeadSpaceTextureLauncher
{
    internal sealed class ConfigurationForm : Form
    {
        private readonly LauncherSettings working;
        private readonly bool firstRun;
        private readonly TextBox gamePath = new TextBox();
        private readonly TextBox texModPath = new TextBox();
        private readonly ListBox packages = new ListBox();
        private readonly Label detectionStatus = new Label();
        private readonly Button detectButton = new Button();
        private readonly Button saveButton = new Button();
        private readonly CheckBox keepOutOfSight = new CheckBox();
        private readonly CheckBox closeWithGame = new CheckBox();
        private readonly CheckBox autoDiscover = new CheckBox();
        private readonly CheckBox scanDownloads = new CheckBox();
        private readonly TextBox managedFolder = new TextBox();
        private readonly Label integrationStatus = new Label();
        private readonly Button integrationButton = new Button();
        private readonly Button downloadTexModButton = new Button();
        private readonly NumericUpDown delay = new NumericUpDown();
        private readonly NumericUpDown timeout = new NumericUpDown();
        private CancellationTokenSource detectionCancellation;

        public LauncherSettings Result { get; private set; }

        public ConfigurationForm(LauncherSettings initial, bool firstRun)
        {
            this.working = initial.Clone();
            this.firstRun = firstRun;
            Text = firstRun ? "First-time setup — " + AppInfo.Name : "Launcher settings";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(800, 760);
            ClientSize = new Size(880, 830);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(22, 25, 29);
            ForeColor = Color.WhiteSmoke;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            BuildInterface();
            LoadWorkingValues();
            Shown += OnShown;
            FormClosed += delegate { if (detectionCancellation != null) detectionCancellation.Cancel(); };
        }

        private void BuildInterface()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(22);
            root.ColumnCount = 1;
            root.RowCount = 10;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            Label heading = new Label();
            heading.Text = firstRun ? "Set it once, then launch with one click" : "Choose what the launcher should load";
            heading.Font = new Font("Segoe UI Semibold", 18F);
            heading.AutoSize = true;
            heading.Margin = new Padding(0, 0, 0, 5);
            root.Controls.Add(heading);

            Label explanation = new Label();
            explanation.Text = "Setup can fetch and verify the original TexMod 0.9b or use your own copy. Extracted .tpf packs are rediscovered every launch.";
            explanation.AutoSize = true;
            explanation.MaximumSize = new Size(750, 0);
            explanation.ForeColor = Color.Gainsboro;
            explanation.Margin = new Padding(0, 0, 0, 18);
            root.Controls.Add(explanation);

            TableLayoutPanel paths = new TableLayoutPanel();
            paths.Dock = DockStyle.Top;
            paths.AutoSize = true;
            paths.ColumnCount = 3;
            paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            AddPathRow(paths, 0, "Dead Space (2008)", gamePath, "Browse...", BrowseGame);
            AddPathRow(paths, 1, "TexMod 0.9b", texModPath, "Browse...", BrowseTexMod);
            paths.Margin = new Padding(0, 0, 0, 14);
            root.Controls.Add(paths);

            GroupBox packageGroup = new GroupBox();
            packageGroup.Text = "Pinned texture packs — top loads first (automatic folders are scanned every launch)";
            packageGroup.ForeColor = Color.WhiteSmoke;
            packageGroup.Dock = DockStyle.Fill;
            packageGroup.Padding = new Padding(12);
            root.Controls.Add(packageGroup);

            TableLayoutPanel packageLayout = new TableLayoutPanel();
            packageLayout.Dock = DockStyle.Fill;
            packageLayout.ColumnCount = 2;
            packageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            packageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            packageGroup.Controls.Add(packageLayout);
            packages.Dock = DockStyle.Fill;
            packages.HorizontalScrollbar = true;
            packages.BackColor = Color.FromArgb(36, 40, 46);
            packages.ForeColor = Color.WhiteSmoke;
            packages.BorderStyle = BorderStyle.FixedSingle;
            packageLayout.Controls.Add(packages, 0, 0);

            FlowLayoutPanel packageButtons = new FlowLayoutPanel();
            packageButtons.FlowDirection = FlowDirection.TopDown;
            packageButtons.WrapContents = false;
            packageButtons.Dock = DockStyle.Fill;
            packageLayout.Controls.Add(packageButtons, 1, 0);
            packageButtons.Controls.Add(ActionButton("Add .tpf...", AddPackages));
            packageButtons.Controls.Add(ActionButton("Remove", RemovePackage));
            packageButtons.Controls.Add(ActionButton("Move up", MovePackageUp));
            packageButtons.Controls.Add(ActionButton("Move down", MovePackageDown));
            packageButtons.Controls.Add(ActionButton("Open auto folder", OpenManagedFolder));
            packageButtons.Controls.Add(ActionButton("Watch folder...", AddWatchedFolder));

            FlowLayoutPanel detection = new FlowLayoutPanel();
            detection.Dock = DockStyle.Top;
            detection.AutoSize = true;
            detection.WrapContents = false;
            detection.Margin = new Padding(0, 14, 0, 10);
            detectButton.Text = "Find game automatically";
            detectButton.AutoSize = true;
            detectButton.Padding = new Padding(8, 4, 8, 4);
            detectButton.Click += delegate { StartDetection(); };
            detection.Controls.Add(detectButton);
            downloadTexModButton.Text = "Get verified TexMod 0.9b";
            downloadTexModButton.AutoSize = true;
            downloadTexModButton.Padding = new Padding(8, 4, 8, 4);
            downloadTexModButton.Click += DownloadTexMod;
            detection.Controls.Add(downloadTexModButton);
            detectionStatus.AutoSize = true;
            detectionStatus.ForeColor = Color.LightSteelBlue;
            detectionStatus.Padding = new Padding(10, 8, 0, 0);
            detection.Controls.Add(detectionStatus);
            root.Controls.Add(detection);

            GroupBox discovery = new GroupBox();
            discovery.Text = "Automatic Nexus/TPF discovery";
            discovery.ForeColor = Color.WhiteSmoke;
            discovery.Dock = DockStyle.Top;
            discovery.AutoSize = true;
            discovery.Padding = new Padding(12);
            root.Controls.Add(discovery);
            TableLayoutPanel discoveryLayout = new TableLayoutPanel();
            discoveryLayout.Dock = DockStyle.Top;
            discoveryLayout.AutoSize = true;
            discoveryLayout.ColumnCount = 3;
            discoveryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            discoveryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            discoveryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            discovery.Controls.Add(discoveryLayout);
            autoDiscover.Text = "Scan managed TexturePacks, game Mods/TexturePacks, game-root .tpf files, and watched folders every launch";
            autoDiscover.AutoSize = true;
            discoveryLayout.Controls.Add(autoDiscover, 0, 0);
            discoveryLayout.SetColumnSpan(autoDiscover, 3);
            scanDownloads.Text = "Also scan my Downloads folder for extracted .tpf files (disable if you use TexMod for other games)";
            scanDownloads.AutoSize = true;
            discoveryLayout.Controls.Add(scanDownloads, 0, 1);
            discoveryLayout.SetColumnSpan(scanDownloads, 3);
            discoveryLayout.Controls.Add(new Label { Text = "Managed folder", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, 0, 2);
            managedFolder.Dock = DockStyle.Fill;
            managedFolder.ReadOnly = true;
            discoveryLayout.Controls.Add(managedFolder, 1, 2);
            Button openFolder = new Button { Text = "Open", AutoSize = true };
            openFolder.Click += OpenManagedFolder;
            discoveryLayout.Controls.Add(openFolder, 2, 2);

            GroupBox options = new GroupBox();
            options.Text = "Options";
            options.ForeColor = Color.WhiteSmoke;
            options.Dock = DockStyle.Top;
            options.AutoSize = true;
            options.Padding = new Padding(12, 9, 12, 12);
            root.Controls.Add(options);
            TableLayoutPanel optionLayout = new TableLayoutPanel();
            optionLayout.Dock = DockStyle.Top;
            optionLayout.AutoSize = true;
            optionLayout.ColumnCount = 4;
            optionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            optionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            optionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
            optionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            options.Controls.Add(optionLayout);
            keepOutOfSight.Text = "Keep TexMod out of sight while loading";
            keepOutOfSight.AutoSize = true;
            closeWithGame.Text = "Close TexMod when the game closes";
            closeWithGame.AutoSize = true;
            optionLayout.Controls.Add(keepOutOfSight, 0, 0);
            optionLayout.Controls.Add(closeWithGame, 1, 0);
            optionLayout.Controls.Add(new Label { Text = "UI delay (milliseconds)", AutoSize = true, Padding = new Padding(0, 4, 0, 0) }, 2, 0);
            delay.Minimum = 150;
            delay.Maximum = 3000;
            delay.Increment = 50;
            optionLayout.Controls.Add(delay, 3, 0);
            optionLayout.Controls.Add(new Label { Text = "", AutoSize = true }, 0, 1);
            optionLayout.Controls.Add(new Label { Text = "", AutoSize = true }, 1, 1);
            optionLayout.Controls.Add(new Label { Text = "Game window timeout (minutes)", AutoSize = true, Padding = new Padding(0, 4, 0, 0) }, 2, 1);
            timeout.Minimum = 2;
            timeout.Maximum = 30;
            optionLayout.Controls.Add(timeout, 3, 1);

            GroupBox integration = new GroupBox();
            integration.Text = "Normal Play-button integration";
            integration.ForeColor = Color.WhiteSmoke;
            integration.Dock = DockStyle.Top;
            integration.AutoSize = true;
            integration.Padding = new Padding(12);
            root.Controls.Add(integration);
            TableLayoutPanel integrationLayout = new TableLayoutPanel();
            integrationLayout.Dock = DockStyle.Top;
            integrationLayout.AutoSize = true;
            integrationLayout.ColumnCount = 2;
            integrationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            integrationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            integration.Controls.Add(integrationLayout);
            integrationStatus.AutoSize = true;
            integrationStatus.MaximumSize = new Size(650, 0);
            integrationLayout.Controls.Add(integrationStatus, 0, 0);
            integrationButton.Text = "Install integration";
            integrationButton.AutoSize = true;
            integrationButton.Padding = new Padding(8, 4, 8, 4);
            integrationButton.Click += ConfigureIntegration;
            integrationLayout.Controls.Add(integrationButton, 1, 0);

            Label legal = new Label();
            legal.AutoSize = true;
            legal.MaximumSize = new Size(750, 0);
            legal.ForeColor = Color.DarkGray;
            legal.Text = "Nothing is injected by this launcher itself. TexMod does the texture injection. No controller or mouse input files are installed or changed.";
            legal.Margin = new Padding(0, 10, 0, 12);
            root.Controls.Add(legal);

            FlowLayoutPanel footer = new FlowLayoutPanel();
            footer.FlowDirection = FlowDirection.RightToLeft;
            footer.Dock = DockStyle.Top;
            footer.AutoSize = true;
            root.Controls.Add(footer);
            saveButton.Text = firstRun ? "Save setup" : "Save";
            saveButton.AutoSize = true;
            saveButton.Padding = new Padding(14, 6, 14, 6);
            saveButton.BackColor = Color.FromArgb(167, 44, 53);
            saveButton.ForeColor = Color.White;
            saveButton.FlatStyle = FlatStyle.Flat;
            saveButton.Click += SaveAndClose;
            footer.Controls.Add(saveButton);
            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.AutoSize = true;
            cancel.Padding = new Padding(10, 6, 10, 6);
            cancel.DialogResult = DialogResult.Cancel;
            footer.Controls.Add(cancel);
            AcceptButton = saveButton;
            CancelButton = cancel;
        }

        private void AddPathRow(TableLayoutPanel table, int row, string label, TextBox field, string buttonText, EventHandler click)
        {
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label name = new Label();
            name.Text = label;
            name.AutoSize = true;
            name.Padding = new Padding(0, 7, 0, 0);
            table.Controls.Add(name, 0, row);
            field.Dock = DockStyle.Fill;
            field.Margin = new Padding(4, 3, 8, 8);
            table.Controls.Add(field, 1, row);
            Button browse = new Button();
            browse.Text = buttonText;
            browse.AutoSize = true;
            browse.Click += click;
            table.Controls.Add(browse, 2, row);
        }

        private Button ActionButton(string text, EventHandler click)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = 92;
            button.Height = 30;
            button.Margin = new Padding(6, 3, 3, 5);
            button.Click += click;
            return button;
        }

        private void LoadWorkingValues()
        {
            gamePath.Text = working.GamePath;
            texModPath.Text = working.TexModPath;
            foreach (string package in working.Packages) packages.Items.Add(package);
            keepOutOfSight.Checked = working.KeepTexModOutOfSight;
            closeWithGame.Checked = working.CloseTexModWithGame;
            autoDiscover.Checked = working.AutoDiscoverPackages;
            scanDownloads.Checked = working.ScanDownloads;
            if (string.IsNullOrWhiteSpace(working.ManagedTextureFolder))
                working.ManagedTextureFolder = Path.Combine(AppInfo.ExecutableDirectory, "TexturePacks");
            managedFolder.Text = working.ManagedTextureFolder;
            delay.Value = Math.Max(delay.Minimum, Math.Min(delay.Maximum, working.ActionDelayMs));
            timeout.Value = Math.Max(timeout.Minimum, Math.Min(timeout.Maximum, working.GameWindowTimeoutMinutes));
            FindNearbyUserFiles();
            UpdateIntegrationStatus();
        }

        private void FindNearbyUserFiles()
        {
            if (string.IsNullOrWhiteSpace(texModPath.Text))
            {
                if (File.Exists(AppInfo.ManagedTexModPath)) texModPath.Text = AppInfo.ManagedTexModPath;
                else
                {
                    string nearbyTexMod = Path.Combine(AppInfo.ExecutableDirectory, "TexMod.exe");
                    if (File.Exists(nearbyTexMod)) texModPath.Text = nearbyTexMod;
                }
            }
        }

        private void OnShown(object sender, EventArgs e)
        {
            if (firstRun && PeInspector.ValidateDeadSpace2008(gamePath.Text) != null)
                BeginInvoke((MethodInvoker)delegate { StartDetection(); });
        }

        private void StartDetection()
        {
            if (detectionCancellation != null) detectionCancellation.Cancel();
            detectionCancellation = new CancellationTokenSource();
            CancellationToken token = detectionCancellation.Token;
            detectButton.Enabled = false;
            saveButton.Enabled = false;
            detectionStatus.Text = "Starting...";
            Task.Factory.StartNew(delegate
            {
                return GameDetector.Find(delegate(string message) { SafeStatus(message); }, token);
            }, token).ContinueWith(delegate(Task<IList<GameCandidate>> task)
            {
                if (IsDisposed) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    detectButton.Enabled = true;
                    saveButton.Enabled = true;
                    if (task.IsCanceled) { detectionStatus.Text = "Scan cancelled."; return; }
                    if (task.IsFaulted)
                    {
                        detectionStatus.Text = "Automatic detection hit an error; browse manually.";
                        Log.Warn("Detection failed: " + task.Exception.GetBaseException().Message);
                        return;
                    }
                    IList<GameCandidate> found = task.Result;
                    if (found.Count == 0)
                    {
                        detectionStatus.Text = "Not found. Use Browse to choose Dead Space.exe.";
                        return;
                    }
                    gamePath.Text = found[0].Path;
                    detectionStatus.Text = "Found via " + found[0].Source + ".";
                    if (found.Count > 1)
                    {
                        using (CandidatePicker picker = new CandidatePicker(found))
                        {
                            if (picker.ShowDialog(this) == DialogResult.OK) gamePath.Text = picker.SelectedPath;
                        }
                    }
                });
            });
        }

        private void SafeStatus(string message)
        {
            if (IsDisposed) return;
            try { BeginInvoke((MethodInvoker)delegate { detectionStatus.Text = message; }); }
            catch { }
        }

        private void BrowseGame(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose Dead Space (2008)";
                dialog.Filter = "Dead Space executable|Dead Space.exe|Programs|*.exe";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) == DialogResult.OK) gamePath.Text = dialog.FileName;
            }
        }

        private void BrowseTexMod(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose your copy of TexMod 0.9b";
                dialog.Filter = "TexMod executable|TexMod.exe|Programs|*.exe";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) == DialogResult.OK) texModPath.Text = dialog.FileName;
            }
        }

        private void AddPackages(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose one or more TexMod texture packs";
                dialog.Filter = "TexMod packages|*.tpf";
                dialog.Multiselect = true;
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                foreach (string file in dialog.FileNames)
                {
                    bool duplicate = false;
                    foreach (object item in packages.Items)
                        if (string.Equals(Convert.ToString(item), file, StringComparison.OrdinalIgnoreCase)) duplicate = true;
                    if (!duplicate) packages.Items.Add(file);
                }
            }
        }

        private void OpenManagedFolder(object sender, EventArgs e)
        {
            try
            {
                working.ManagedTextureFolder = string.IsNullOrWhiteSpace(managedFolder.Text)
                    ? Path.Combine(AppInfo.ExecutableDirectory, "TexturePacks")
                    : managedFolder.Text;
                string folder = PackageDiscovery.EnsureManagedFolder(working);
                managedFolder.Text = folder;
                Process.Start("explorer.exe", "\"" + folder + "\"");
            }
            catch (Exception ex) { ShowProblem("The texture folder could not be opened: " + ex.Message); }
        }

        private void AddWatchedFolder(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose a folder that should be scanned recursively for .tpf files every launch.";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string full = Path.GetFullPath(dialog.SelectedPath);
                bool exists = false;
                foreach (string folder in working.WatchedFolders)
                    if (string.Equals(folder, full, StringComparison.OrdinalIgnoreCase)) exists = true;
                if (!exists) working.WatchedFolders.Add(full);
                detectionStatus.Text = "Watching " + full;
            }
        }

        private void DownloadTexMod(object sender, EventArgs e)
        {
            DialogResult consent = MessageBox.Show(this,
                "This will download the original unsigned TexMod 0.9b archive from the archived Google Code project. " +
                "The launcher verifies the published archive SHA-1 and a pinned TexMod.exe SHA-256 before accepting it.\n\n" +
                "TexMod is an old packed injector and may be flagged by antivirus software. Continue?",
                "Get verified TexMod 0.9b", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (consent != DialogResult.Yes) return;

            downloadTexModButton.Enabled = false;
            saveButton.Enabled = false;
            Task.Factory.StartNew(delegate
            {
                return TexModAcquisition.DownloadVerified(delegate(string message) { SafeStatus(message); });
            }).ContinueWith(delegate(Task<string> task)
            {
                if (IsDisposed) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    downloadTexModButton.Enabled = true;
                    saveButton.Enabled = true;
                    if (task.IsFaulted)
                    {
                        Exception error = task.Exception.GetBaseException();
                        Log.Error(error.ToString());
                        ShowProblem("TexMod could not be downloaded and verified: " + error.Message + "\n\nYou can still browse to your own copy.");
                        return;
                    }
                    texModPath.Text = task.Result;
                    detectionStatus.Text = "Verified TexMod 0.9b is ready.";
                });
            });
        }

        private void ConfigureIntegration(object sender, EventArgs e)
        {
            string selectedGame = gamePath.Text.Trim();
            string validation = PeInspector.ValidateDeadSpace2008(selectedGame);
            if (validation != null) { ShowProblem(validation); return; }

            if (working.IntegrationMode == IntegrationModes.EaProxy)
            {
                if (MessageBox.Show(this,
                    "Remove EA normal-launch integration and restore the original Dead Space.exe? Texture packs will be kept.",
                    AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    NormalLaunchIntegration.RemoveEaProxy(working.GameRoot);
                    working.IntegrationMode = IntegrationModes.None;
                    working.GamePath = Path.Combine(working.GameRoot, "Dead Space.exe");
                    gamePath.Text = working.GamePath;
                    SettingsStore.Save(CollectSettings());
                    UpdateIntegrationStatus();
                }
                catch (Exception ex) { ShowProblem("Integration could not be removed safely: " + ex.Message); }
                return;
            }

            if (working.IntegrationMode == IntegrationModes.SteamLaunchOption)
            {
                MessageBox.Show(this,
                    "Remove the custom Launch Option from Dead Space's Steam Properties, then save these settings. The installed launcher and texture folder can remain.",
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                working.IntegrationMode = IntegrationModes.None;
                UpdateIntegrationStatus();
                return;
            }

            bool steam = NormalLaunchIntegration.LooksLikeSteam(selectedGame);
            string explanation = steam
                ? "The launcher will install a stable copy beside the game and generate Steam's %command% Launch Option. You will paste it into Steam Properties once. No game executable is renamed."
                : "EA App has no equivalent %command% option. Setup will preserve the verified original as Dead Space.original.exe and put Rama2120's small proxy at Dead Space.exe. Before every game launch it temporarily restores the original name, then safely swaps back on exit. A watchdog and repair path are included.";
            if (MessageBox.Show(this, explanation + "\n\nInstall normal Play-button integration now?",
                AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                IntegrationInstallResult installed = NormalLaunchIntegration.Install(selectedGame, steam);
                string priorTextureFolder = string.IsNullOrWhiteSpace(working.ManagedTextureFolder)
                    ? Path.Combine(AppInfo.ExecutableDirectory, "TexturePacks")
                    : working.ManagedTextureFolder;
                if (Directory.Exists(priorTextureFolder) &&
                    !string.Equals(Path.GetFullPath(priorTextureFolder), Path.GetFullPath(installed.ManagedTextureFolder), StringComparison.OrdinalIgnoreCase))
                {
                    bool alreadyWatched = false;
                    foreach (string watched in working.WatchedFolders)
                        if (string.Equals(Path.GetFullPath(watched), Path.GetFullPath(priorTextureFolder), StringComparison.OrdinalIgnoreCase)) alreadyWatched = true;
                    if (!alreadyWatched) working.WatchedFolders.Add(Path.GetFullPath(priorTextureFolder));
                }
                working.IntegrationMode = installed.Mode;
                working.GameRoot = Path.GetDirectoryName(selectedGame);
                working.InstalledLauncherPath = installed.InstalledLauncherPath;
                working.ManagedTextureFolder = installed.ManagedTextureFolder;
                working.GamePath = installed.GamePath;
                gamePath.Text = installed.GamePath;
                managedFolder.Text = installed.ManagedTextureFolder;
                LauncherSettings immediate = CollectSettings();
                SettingsStore.Save(immediate);
                CopyInto(working, immediate);

                if (steam)
                {
                    Clipboard.SetText(installed.SteamLaunchOption);
                    MessageBox.Show(this,
                        "Steam Launch Option copied to the clipboard:\n\n" + installed.SteamLaunchOption +
                        "\n\nIn Steam: right-click Dead Space → Properties → General → Launch Options, then paste it. After that, Steam's normal Play button runs the texture launcher automatically.",
                        "Steam integration — one paste required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this,
                        "EA normal-launch integration is installed. The verified original is preserved as " + NormalLaunchIntegration.OriginalBackupName +
                        ". From now on, use the normal Play button in EA App.",
                        "EA integration installed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                UpdateIntegrationStatus();
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowProblem("Normal-launch integration could not be installed safely: " + ex.Message);
            }
        }

        private void UpdateIntegrationStatus()
        {
            if (working.IntegrationMode == IntegrationModes.SteamLaunchOption)
            {
                integrationStatus.Text = "Steam integration prepared. Steam's Launch Options must contain the generated %command% line.";
                integrationButton.Text = "Removal help";
            }
            else if (working.IntegrationMode == IntegrationModes.EaProxy)
            {
                integrationStatus.Text = "EA integration installed. EA's normal Play button invokes the texture launcher and restores filenames after each session.";
                integrationButton.Text = "Remove integration";
            }
            else
            {
                integrationStatus.Text = "Not installed. The standalone Launch button works, but Steam/EA's normal Play button will bypass textures.";
                integrationButton.Text = "Install integration";
            }
        }

        private void RemovePackage(object sender, EventArgs e)
        {
            int index = packages.SelectedIndex;
            if (index < 0) return;
            packages.Items.RemoveAt(index);
            if (packages.Items.Count > 0) packages.SelectedIndex = Math.Min(index, packages.Items.Count - 1);
        }

        private void MovePackageUp(object sender, EventArgs e) { MovePackage(-1); }
        private void MovePackageDown(object sender, EventArgs e) { MovePackage(1); }

        private void MovePackage(int direction)
        {
            int index = packages.SelectedIndex;
            int target = index + direction;
            if (index < 0 || target < 0 || target >= packages.Items.Count) return;
            object value = packages.Items[index];
            packages.Items.RemoveAt(index);
            packages.Items.Insert(target, value);
            packages.SelectedIndex = target;
        }

        private void SaveAndClose(object sender, EventArgs e)
        {
            string gameError = PeInspector.ValidateDeadSpace2008(gamePath.Text.Trim());
            if (gameError != null) { ShowProblem(gameError); return; }
            if (!File.Exists(texModPath.Text.Trim())) { ShowProblem("Choose your user-supplied TexMod.exe."); return; }
            if (PeInspector.GetArchitecture(texModPath.Text.Trim()) == PeArchitecture.X64) { ShowProblem("Choose the classic 32-bit TexMod 0.9b executable."); return; }

            LauncherSettings saved;
            try { saved = CollectSettings(); }
            catch (Exception ex) { ShowProblem(ex.Message); return; }

            try { SettingsStore.Save(saved); }
            catch (Exception ex) { ShowProblem("Settings could not be saved: " + ex.Message); return; }
            Result = saved;
            DialogResult = DialogResult.OK;
            Close();
        }

        private LauncherSettings CollectSettings()
        {
            LauncherSettings saved = working.Clone();
            saved.SchemaVersion = 2;
            saved.GamePath = Path.GetFullPath(gamePath.Text.Trim());
            saved.TexModPath = string.IsNullOrWhiteSpace(texModPath.Text) ? string.Empty : Path.GetFullPath(texModPath.Text.Trim());
            saved.Packages.Clear();
            foreach (object item in packages.Items)
            {
                string package = Convert.ToString(item);
                if (!File.Exists(package)) throw new LauncherException("Texture pack not found: " + package);
                if (!Path.GetExtension(package).Equals(".tpf", StringComparison.OrdinalIgnoreCase))
                    throw new LauncherException("Only .tpf texture packs are supported: " + package);
                saved.Packages.Add(Path.GetFullPath(package));
            }
            saved.AutoDiscoverPackages = autoDiscover.Checked;
            saved.ScanDownloads = scanDownloads.Checked;
            saved.ManagedTextureFolder = string.IsNullOrWhiteSpace(managedFolder.Text)
                ? Path.Combine(AppInfo.ExecutableDirectory, "TexturePacks")
                : Path.GetFullPath(managedFolder.Text.Trim());
            saved.KeepTexModOutOfSight = keepOutOfSight.Checked;
            saved.CloseTexModWithGame = closeWithGame.Checked;
            saved.ActionDelayMs = (int)delay.Value;
            saved.GameWindowTimeoutMinutes = (int)timeout.Value;
            return saved;
        }

        private static void CopyInto(LauncherSettings target, LauncherSettings source)
        {
            target.SchemaVersion = source.SchemaVersion;
            target.GamePath = source.GamePath;
            target.TexModPath = source.TexModPath;
            target.Packages.Clear();
            target.Packages.AddRange(source.Packages);
            target.WatchedFolders.Clear();
            target.WatchedFolders.AddRange(source.WatchedFolders);
            target.AutoDiscoverPackages = source.AutoDiscoverPackages;
            target.ScanDownloads = source.ScanDownloads;
            target.ManagedTextureFolder = source.ManagedTextureFolder;
            target.IntegrationMode = source.IntegrationMode;
            target.GameRoot = source.GameRoot;
            target.InstalledLauncherPath = source.InstalledLauncherPath;
            target.KeepTexModOutOfSight = source.KeepTexModOutOfSight;
            target.CloseTexModWithGame = source.CloseTexModWithGame;
            target.ActionDelayMs = source.ActionDelayMs;
            target.GameWindowTimeoutMinutes = source.GameWindowTimeoutMinutes;
        }

        private void ShowProblem(string message)
        {
            MessageBox.Show(this, message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private sealed class CandidatePicker : Form
        {
            private readonly ListBox list = new ListBox();
            private readonly IList<GameCandidate> candidates;
            public string SelectedPath { get { return candidates[Math.Max(0, list.SelectedIndex)].Path; } }

            public CandidatePicker(IList<GameCandidate> candidates)
            {
                this.candidates = candidates;
                Text = "Choose the Dead Space install to use";
                StartPosition = FormStartPosition.CenterParent;
                ClientSize = new Size(680, 300);
                Font = new Font("Segoe UI", 9F);
                list.Dock = DockStyle.Fill;
                list.HorizontalScrollbar = true;
                foreach (GameCandidate candidate in candidates) list.Items.Add(candidate.Path + "   [" + candidate.Source + "]");
                list.SelectedIndex = 0;
                Controls.Add(list);
                FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
                Button use = new Button { Text = "Use selected", DialogResult = DialogResult.OK, AutoSize = true };
                Button cancel = new Button { Text = "Use best match", DialogResult = DialogResult.Cancel, AutoSize = true };
                footer.Controls.Add(use);
                footer.Controls.Add(cancel);
                Controls.Add(footer);
                AcceptButton = use;
                CancelButton = cancel;
            }
        }
    }
}
