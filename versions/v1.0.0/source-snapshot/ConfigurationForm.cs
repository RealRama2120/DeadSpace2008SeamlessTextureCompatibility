using System;
using System.Collections.Generic;
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
            MinimumSize = new Size(760, 650);
            ClientSize = new Size(820, 710);
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
            root.RowCount = 8;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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
            explanation.Text = "Bring your own TexMod 0.9b and .tpf packs. This launcher only remembers their locations and automates the normal TexMod buttons.";
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
            packageGroup.Text = "Texture packs — top loads first";
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
            detectionStatus.AutoSize = true;
            detectionStatus.ForeColor = Color.LightSteelBlue;
            detectionStatus.Padding = new Padding(10, 8, 0, 0);
            detection.Controls.Add(detectionStatus);
            root.Controls.Add(detection);

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
            saveButton.Text = firstRun ? "Save and launch" : "Save";
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
            delay.Value = Math.Max(delay.Minimum, Math.Min(delay.Maximum, working.ActionDelayMs));
            timeout.Value = Math.Max(timeout.Minimum, Math.Min(timeout.Maximum, working.GameWindowTimeoutMinutes));
            FindNearbyUserFiles();
        }

        private void FindNearbyUserFiles()
        {
            if (string.IsNullOrWhiteSpace(texModPath.Text))
            {
                string nearbyTexMod = Path.Combine(AppInfo.ExecutableDirectory, "TexMod.exe");
                if (File.Exists(nearbyTexMod)) texModPath.Text = nearbyTexMod;
            }
            if (packages.Items.Count == 0)
            {
                string folder = Path.Combine(AppInfo.ExecutableDirectory, "TexturePacks");
                if (Directory.Exists(folder))
                {
                    foreach (string package in Directory.GetFiles(folder, "*.tpf", SearchOption.TopDirectoryOnly))
                        packages.Items.Add(package);
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
            if (packages.Items.Count == 0) { ShowProblem("Add at least one .tpf texture pack."); return; }

            LauncherSettings saved = new LauncherSettings();
            saved.GamePath = Path.GetFullPath(gamePath.Text.Trim());
            saved.TexModPath = Path.GetFullPath(texModPath.Text.Trim());
            foreach (object item in packages.Items)
            {
                string package = Convert.ToString(item);
                if (!File.Exists(package)) { ShowProblem("Texture pack not found: " + package); return; }
                if (!Path.GetExtension(package).Equals(".tpf", StringComparison.OrdinalIgnoreCase)) { ShowProblem("Only .tpf texture packs are supported: " + package); return; }
                saved.Packages.Add(Path.GetFullPath(package));
            }
            saved.KeepTexModOutOfSight = keepOutOfSight.Checked;
            saved.CloseTexModWithGame = closeWithGame.Checked;
            saved.ActionDelayMs = (int)delay.Value;
            saved.GameWindowTimeoutMinutes = (int)timeout.Value;

            try { SettingsStore.Save(saved); }
            catch (Exception ex) { ShowProblem("Settings could not be saved: " + ex.Message); return; }
            Result = saved;
            DialogResult = DialogResult.OK;
            Close();
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
