using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskManagerPlus.Models;

namespace TaskManagerPlus.Controls
{
    public partial class StartupTab : UserControl
    {
        private readonly BindingList<StartupApp> startupApps;

        // Registry paths
        private const string RUN = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string RUN_ONCE = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";

        private const string APPROVED_RUN = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string APPROVED_RUN32 = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";

        public StartupTab()
        {
            InitializeComponent();
            startupApps = new BindingList<StartupApp>();
        }

        public void Initialize()
        {
            SetupDataGridView();
        }

        private void SetupDataGridView()
        {
            dataGridViewStartup.AutoGenerateColumns = false;
            dataGridViewStartup.Columns.Clear();

            // FIX ambiguous DoubleBuffered: no extension method
            SetDoubleBuffered(dataGridViewStartup, true);

            dataGridViewStartup.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewStartup.MultiSelect = false;
            dataGridViewStartup.AllowUserToAddRows = false;
            dataGridViewStartup.AllowUserToResizeRows = false;
            dataGridViewStartup.RowHeadersVisible = false;

            dataGridViewStartup.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Name",
                HeaderText = "Name",
                Width = 300
            });

            dataGridViewStartup.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Publisher",
                HeaderText = "Publisher",
                Width = 230
            });

            dataGridViewStartup.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Status",
                HeaderText = "Status",
                Width = 110
            });

            dataGridViewStartup.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "StartupImpact",
                HeaderText = "Startup impact",
                Width = 140
            });

            dataGridViewStartup.DataSource = startupApps;

            dataGridViewStartup.SelectionChanged += (s, e) => UpdateButtons();
            UpdateButtons();
        }

        private static void SetDoubleBuffered(DataGridView dgv, bool value)
        {
            try
            {
                var pi = typeof(DataGridView).GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (pi != null) pi.SetValue(dgv, value, null);
            }
            catch { }
        }

        private void UpdateButtons()
        {
            StartupApp app = GetSelected();
            if (app == null)
            {
                btnEnable.Enabled = false;
                btnDisable.Enabled = false;
                return;
            }

            btnEnable.Enabled = !app.IsEnabled;
            btnDisable.Enabled = app.IsEnabled;
        }

        private StartupApp GetSelected()
        {
            if (dataGridViewStartup.SelectedRows.Count == 0) return null;
            return dataGridViewStartup.SelectedRows[0].DataBoundItem as StartupApp;
        }

        public async Task LoadStartupAppsAsync()
        {
            try
            {
                List<StartupApp> apps = await Task.Run(() => GetStartupApplications());

                startupApps.RaiseListChangedEvents = false;
                startupApps.Clear();
                foreach (var a in apps) startupApps.Add(a);
                startupApps.RaiseListChangedEvents = true;
                startupApps.ResetBindings();

                UpdateButtons();
            }
            catch (Exception ex)
            {
                Console.WriteLine("LoadStartupAppsAsync error: " + ex.Message);
            }
        }

        // ============================================
        // MAIN: collect startup items from 4 sources
        // ============================================
        private List<StartupApp> GetStartupApplications()
        {
            Dictionary<string, StartupApp> map = new Dictionary<string, StartupApp>(StringComparer.OrdinalIgnoreCase);

            // 1) Run/RunOnce entries (HKCU/HKLM, 64/32)
            ReadRunEntries(RegistryHive.CurrentUser, RUN, map);
            ReadRunEntries(RegistryHive.CurrentUser, RUN_ONCE, map);
            ReadRunEntries(RegistryHive.LocalMachine, RUN, map);
            ReadRunEntries(RegistryHive.LocalMachine, RUN_ONCE, map);

            // 2) Apply StartupApproved state (HKCU/HKLM, Run/Run32, 64/32)
            ApplyApprovedState(RegistryHive.CurrentUser, APPROVED_RUN, map);
            ApplyApprovedState(RegistryHive.CurrentUser, APPROVED_RUN32, map);
            ApplyApprovedState(RegistryHive.LocalMachine, APPROVED_RUN, map);
            ApplyApprovedState(RegistryHive.LocalMachine, APPROVED_RUN32, map);

            // 3) Add Approved-only entries (still show)
            AddApprovedOnlyEntries(RegistryHive.CurrentUser, APPROVED_RUN, map);
            AddApprovedOnlyEntries(RegistryHive.CurrentUser, APPROVED_RUN32, map);
            AddApprovedOnlyEntries(RegistryHive.LocalMachine, APPROVED_RUN, map);
            AddApprovedOnlyEntries(RegistryHive.LocalMachine, APPROVED_RUN32, map);

            // 4) Startup folders (.lnk) like Task Manager
            ReadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), RegistryHive.CurrentUser, map);
            ReadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), RegistryHive.LocalMachine, map);

            // finalize
            foreach (var kv in map)
            {
                StartupApp app = kv.Value;

                if (string.IsNullOrWhiteSpace(app.StartupImpact))
                    app.StartupImpact = "Not measured";

                // If not assigned by StartupApproved -> assume Enabled
                if (string.IsNullOrWhiteSpace(app.Status))
                {
                    app.Status = "Enabled";
                    app.IsEnabled = true;
                }

                // Publisher
                if (string.IsNullOrWhiteSpace(app.Publisher))
                    app.Publisher = GetPublisher(app.Location);

                // Some common renames (optional small mapping)
                if (app.Name.Equals("SecurityHealth", StringComparison.OrdinalIgnoreCase) && app.Publisher == "Unknown")
                    app.Publisher = "Microsoft Corporation";
            }

            // Sort like Task Manager: Enabled first
            return map.Values
                .OrderBy(a => a.IsEnabled ? 0 : 1)
                .ThenBy(a => a.Name)
                .ToList();
        }

        // =======================
        // Read Run/RunOnce
        // =======================
        private void ReadRunEntries(RegistryHive hive, string runSubKey, Dictionary<string, StartupApp> map)
        {
            ReadRunEntriesView(hive, RegistryView.Registry64, runSubKey, map);
            ReadRunEntriesView(hive, RegistryView.Registry32, runSubKey, map);
        }

        private void ReadRunEntriesView(RegistryHive hive, RegistryView view, string runSubKey, Dictionary<string, StartupApp> map)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(runSubKey, false))
                {
                    if (key == null) return;

                    foreach (string valueName in key.GetValueNames())
                    {
                        if (string.IsNullOrWhiteSpace(valueName)) continue;

                        string cmd = key.GetValue(valueName) == null ? "" : key.GetValue(valueName).ToString();

                        StartupApp app;
                        if (!map.TryGetValue(valueName, out app))
                        {
                            app = new StartupApp
                            {
                                Name = valueName
                            };
                            map[valueName] = app;
                        }

                        if (!string.IsNullOrWhiteSpace(cmd))
                            app.Location = cmd;

                        app.Hive = hive;
                        app.RunSubKey = runSubKey;
                    }
                }
            }
            catch { }
        }

        // =======================
        // StartupApproved state
        // =======================
        private void ApplyApprovedState(RegistryHive hive, string approvedSubKey, Dictionary<string, StartupApp> map)
        {
            ApplyApprovedStateView(hive, RegistryView.Registry64, approvedSubKey, map);
            ApplyApprovedStateView(hive, RegistryView.Registry32, approvedSubKey, map);
        }

        private void ApplyApprovedStateView(RegistryHive hive, RegistryView view, string approvedSubKey, Dictionary<string, StartupApp> map)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(approvedSubKey, false))
                {
                    if (key == null) return;

                    foreach (string valueName in key.GetValueNames())
                    {
                        byte[] data = key.GetValue(valueName) as byte[];
                        if (data == null || data.Length == 0) continue;

                        bool enabled = IsApprovedEnabled(data);

                        StartupApp app;
                        if (map.TryGetValue(valueName, out app))
                        {
                            app.IsEnabled = enabled;
                            app.Status = enabled ? "Enabled" : "Disabled";
                            app.ApprovedSubKey = approvedSubKey;
                            if (app.Hive == 0) app.Hive = hive;
                        }
                    }
                }
            }
            catch { }
        }

        private void AddApprovedOnlyEntries(RegistryHive hive, string approvedSubKey, Dictionary<string, StartupApp> map)
        {
            AddApprovedOnlyEntriesView(hive, RegistryView.Registry64, approvedSubKey, map);
            AddApprovedOnlyEntriesView(hive, RegistryView.Registry32, approvedSubKey, map);
        }

        private void AddApprovedOnlyEntriesView(RegistryHive hive, RegistryView view, string approvedSubKey, Dictionary<string, StartupApp> map)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(approvedSubKey, false))
                {
                    if (key == null) return;

                    foreach (string valueName in key.GetValueNames())
                    {
                        if (map.ContainsKey(valueName)) continue;

                        byte[] data = key.GetValue(valueName) as byte[];
                        if (data == null || data.Length == 0) continue;

                        bool enabled = IsApprovedEnabled(data);

                        StartupApp app = new StartupApp
                        {
                            Name = valueName,
                            Status = enabled ? "Enabled" : "Disabled",
                            IsEnabled = enabled,
                            StartupImpact = "Not measured",
                            Location = "",
                            Hive = hive,
                            ApprovedSubKey = approvedSubKey,
                            Publisher = ""
                        };

                        map[valueName] = app;
                    }
                }
            }
            catch { }
        }

        private bool IsApprovedEnabled(byte[] data)
        {
            // 0x02 = enabled, 0x03/0x01 = disabled
            return data[0] == 0x02;
        }

        private byte[] BuildApprovedBytes(bool enabled)
        {
            // 12 bytes enough
            byte[] data = new byte[12];
            data[0] = enabled ? (byte)0x02 : (byte)0x03;
            return data;
        }

        // =======================
        // Startup Folder (.lnk)
        // =======================
        private void ReadStartupFolder(string folderPath, RegistryHive hive, Dictionary<string, StartupApp> map)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath)) return;
                if (!Directory.Exists(folderPath)) return;

                foreach (string file in Directory.GetFiles(folderPath, "*.lnk"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    StartupApp app;
                    if (!map.TryGetValue(name, out app))
                    {
                        app = new StartupApp
                        {
                            Name = name,
                            Location = file,
                            Hive = hive,
                            Status = "Enabled",
                            IsEnabled = true,
                            StartupImpact = "Not measured",
                            Publisher = "Unknown"
                        };
                        map[name] = app;
                    }
                    else
                    {
                        // If existing item had no location, store shortcut path
                        if (string.IsNullOrWhiteSpace(app.Location))
                            app.Location = file;
                    }
                }
            }
            catch { }
        }

        // =======================
        // Toggle Enable/Disable
        // =======================
        private void btnDisable_Click(object sender, EventArgs e)
        {
            StartupApp app = GetSelected();
            if (app == null)
            {
                MessageBox.Show("Please select a startup application.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                SetStartupEnabled(app, false);
                _ = LoadStartupAppsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not disable startup app: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnEnable_Click(object sender, EventArgs e)
        {
            StartupApp app = GetSelected();
            if (app == null)
            {
                MessageBox.Show("Please select a startup application.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                SetStartupEnabled(app, true);
                _ = LoadStartupAppsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not enable startup app: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetStartupEnabled(StartupApp app, bool enabled)
        {
            // Task Manager toggle primarily affects StartupApproved
            string approvedSubKey = string.IsNullOrWhiteSpace(app.ApprovedSubKey) ? APPROVED_RUN : app.ApprovedSubKey;
            RegistryHive hive = app.Hive == 0 ? RegistryHive.CurrentUser : app.Hive;

            byte[] bytes = BuildApprovedBytes(enabled);

            WriteApprovedValue(hive, RegistryView.Registry64, approvedSubKey, app.Name, bytes);
            WriteApprovedValue(hive, RegistryView.Registry32, approvedSubKey, app.Name, bytes);

            app.IsEnabled = enabled;
            app.Status = enabled ? "Enabled" : "Disabled";
        }

        private void WriteApprovedValue(RegistryHive hive, RegistryView view, string approvedSubKey, string name, byte[] bytes)
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
            using (RegistryKey key = baseKey.CreateSubKey(approvedSubKey))
            {
                if (key == null) throw new Exception("Cannot open StartupApproved key.");
                key.SetValue(name, bytes, RegistryValueKind.Binary);
            }
        }

        // =======================
        // Publisher helper
        // =======================
        private string GetPublisher(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                return "Unknown";

            try
            {
                // If it's a shortcut (.lnk), publisher can't be read directly here -> keep Unknown
                if (commandLine.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    return "Unknown";

                string filePath = ExtractExePath(commandLine);
                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                {
                    FileVersionInfo vi = FileVersionInfo.GetVersionInfo(filePath);
                    if (!string.IsNullOrWhiteSpace(vi.CompanyName))
                        return vi.CompanyName;
                }
            }
            catch { }

            return "Unknown";
        }

        private string ExtractExePath(string commandLine)
        {
            string s = commandLine.Trim();

            if (s.StartsWith("\""))
            {
                int end = s.IndexOf("\"", 1);
                if (end > 1) return s.Substring(1, end - 1);
            }

            int space = s.IndexOf(' ');
            if (space > 0) return s.Substring(0, space).Trim('"');

            return s.Trim('"');
        }
    }
}
