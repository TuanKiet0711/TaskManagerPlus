namespace TaskManagerPlus
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem menuLanguage;
        private System.Windows.Forms.ToolStripMenuItem menuLangVI;
        private System.Windows.Forms.ToolStripMenuItem menuLangEN;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.menuLanguage = new System.Windows.Forms.ToolStripMenuItem();
            this.menuLangVI = new System.Windows.Forms.ToolStripMenuItem();
            this.menuLangEN = new System.Windows.Forms.ToolStripMenuItem();

            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabProcesses = new System.Windows.Forms.TabPage();
            this.tabPerformance = new System.Windows.Forms.TabPage();
            this.tabTemperature = new System.Windows.Forms.TabPage();
            this.tabBattery = new System.Windows.Forms.TabPage();
            this.tabStartup = new System.Windows.Forms.TabPage();
            this.tabAppHistory = new System.Windows.Forms.TabPage();

            this.panelTop = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();
            this.pictureBoxLogo = new System.Windows.Forms.PictureBox();
            this.btnToggleLanguage = new System.Windows.Forms.Button();

            this.panelBottom = new System.Windows.Forms.Panel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.numRefreshInterval = new System.Windows.Forms.NumericUpDown();
            this.lblRefreshInterval = new System.Windows.Forms.Label();
            this.chkAutoRefresh = new System.Windows.Forms.CheckBox();
            this.btnRefresh = new System.Windows.Forms.Button();

            this.timerRefresh = new System.Windows.Forms.Timer(this.components);

            this.menuStrip.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.panelTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLogo)).BeginInit();
            this.panelBottom.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numRefreshInterval)).BeginInit();
            this.SuspendLayout();

            // 
            // menuStrip
            // 
            this.menuStrip.BackColor = System.Drawing.Color.White;
            this.menuStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuLanguage});
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Padding = new System.Windows.Forms.Padding(7, 2, 0, 2);
            this.menuStrip.Size = new System.Drawing.Size(1284, 28);
            this.menuStrip.TabIndex = 100;
            this.menuStrip.Text = "menuStrip";

            // 
            // menuLanguage
            // 
            this.menuLanguage.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuLangVI,
            this.menuLangEN});
            this.menuLanguage.Name = "menuLanguage";
            this.menuLanguage.Size = new System.Drawing.Size(87, 24);
            this.menuLanguage.Text = "Language";

            // 
            // menuLangVI
            // 
            this.menuLangVI.Name = "menuLangVI";
            this.menuLangVI.Size = new System.Drawing.Size(152, 26);
            this.menuLangVI.Text = "Tiếng Việt";
            this.menuLangVI.Click += new System.EventHandler(this.menuLangVI_Click);

            // 
            // menuLangEN
            // 
            this.menuLangEN.Name = "menuLangEN";
            this.menuLangEN.Size = new System.Drawing.Size(152, 26);
            this.menuLangEN.Text = "English";
            this.menuLangEN.Click += new System.EventHandler(this.menuLangEN_Click);

            // 
            // tabControl
            // 
            this.tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl.Controls.Add(this.tabProcesses);
            this.tabControl.Controls.Add(this.tabPerformance);
            this.tabControl.Controls.Add(this.tabTemperature);
            this.tabControl.Controls.Add(this.tabBattery);
            this.tabControl.Controls.Add(this.tabStartup);
            this.tabControl.Controls.Add(this.tabAppHistory);
            this.tabControl.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.tabControl.ItemSize = new System.Drawing.Size(110, 32);
            this.tabControl.Location = new System.Drawing.Point(0, 93); // 28 menu + 65 top panel
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(1284, 638);
            this.tabControl.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            this.tabControl.TabIndex = 0;
            this.tabControl.SelectedIndexChanged += new System.EventHandler(this.tabControl_SelectedIndexChanged);

            // 
            // tabProcesses
            // 
            this.tabProcesses.BackColor = System.Drawing.Color.White;
            this.tabProcesses.Location = new System.Drawing.Point(4, 36);
            this.tabProcesses.Name = "tabProcesses";
            this.tabProcesses.Padding = new System.Windows.Forms.Padding(3);
            this.tabProcesses.Size = new System.Drawing.Size(1276, 598);
            this.tabProcesses.TabIndex = 0;
            this.tabProcesses.Text = "Processes";

            // 
            // tabPerformance
            // 
            this.tabPerformance.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(250)))));
            this.tabPerformance.Location = new System.Drawing.Point(4, 36);
            this.tabPerformance.Name = "tabPerformance";
            this.tabPerformance.Padding = new System.Windows.Forms.Padding(3);
            this.tabPerformance.Size = new System.Drawing.Size(1276, 598);
            this.tabPerformance.TabIndex = 1;
            this.tabPerformance.Text = "Performance";

            // 
            // tabTemperature
            // 
            this.tabTemperature.BackColor = System.Drawing.Color.White;
            this.tabTemperature.Location = new System.Drawing.Point(4, 36);
            this.tabTemperature.Name = "tabTemperature";
            this.tabTemperature.Size = new System.Drawing.Size(1276, 598);
            this.tabTemperature.TabIndex = 2;
            this.tabTemperature.Text = "Temperature";

            // 
            // tabBattery
            // 
            this.tabBattery.BackColor = System.Drawing.Color.White;
            this.tabBattery.Location = new System.Drawing.Point(4, 36);
            this.tabBattery.Name = "tabBattery";
            this.tabBattery.Size = new System.Drawing.Size(1276, 598);
            this.tabBattery.TabIndex = 3;
            this.tabBattery.Text = "Battery";

            // 
            // tabStartup
            // 
            this.tabStartup.BackColor = System.Drawing.Color.White;
            this.tabStartup.Location = new System.Drawing.Point(4, 36);
            this.tabStartup.Name = "tabStartup";
            this.tabStartup.Size = new System.Drawing.Size(1276, 598);
            this.tabStartup.TabIndex = 4;
            this.tabStartup.Text = "Startup";

            // 
            // tabAppHistory
            // 
            this.tabAppHistory.BackColor = System.Drawing.Color.White;
            this.tabAppHistory.Location = new System.Drawing.Point(4, 36);
            this.tabAppHistory.Name = "tabAppHistory";
            this.tabAppHistory.Size = new System.Drawing.Size(1276, 598);
            this.tabAppHistory.TabIndex = 5;
            this.tabAppHistory.Text = "App history";

            // 
            // panelTop
            // 
            this.panelTop.BackColor = System.Drawing.Color.White;
            this.panelTop.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelTop.Controls.Add(this.lblTitle);
            this.panelTop.Controls.Add(this.pictureBoxLogo);
            this.panelTop.Controls.Add(this.btnToggleLanguage);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 28); // dưới menu
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(1284, 65);
            this.panelTop.TabIndex = 1;

            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(58)))), ((int)(((byte)(64)))));
            this.lblTitle.Location = new System.Drawing.Point(65, 17);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(214, 37);
            this.lblTitle.TabIndex = 1;
            this.lblTitle.Text = "Task Manager+";

            // 
            // pictureBoxLogo
            // 
            this.pictureBoxLogo.Location = new System.Drawing.Point(15, 12);
            this.pictureBoxLogo.Name = "pictureBoxLogo";
            this.pictureBoxLogo.Size = new System.Drawing.Size(40, 40);
            this.pictureBoxLogo.TabIndex = 0;
            this.pictureBoxLogo.TabStop = false;

            // 
            // btnToggleLanguage
            // 
            this.btnToggleLanguage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnToggleLanguage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(108)))), ((int)(((byte)(117)))), ((int)(((byte)(125)))));
            this.btnToggleLanguage.FlatAppearance.BorderSize = 0;
            this.btnToggleLanguage.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToggleLanguage.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold);
            this.btnToggleLanguage.ForeColor = System.Drawing.Color.White;
            this.btnToggleLanguage.Location = new System.Drawing.Point(1120, 18);
            this.btnToggleLanguage.Name = "btnToggleLanguage";
            this.btnToggleLanguage.Size = new System.Drawing.Size(140, 30);
            this.btnToggleLanguage.TabIndex = 2;
            this.btnToggleLanguage.Text = "VI / EN";
            this.btnToggleLanguage.UseVisualStyleBackColor = false;
            this.btnToggleLanguage.Click += new System.EventHandler(this.btnToggleLanguage_Click);

            // 
            // panelBottom
            // 
            this.panelBottom.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(250)))));
            this.panelBottom.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelBottom.Controls.Add(this.lblStatus);
            this.panelBottom.Controls.Add(this.numRefreshInterval);
            this.panelBottom.Controls.Add(this.lblRefreshInterval);
            this.panelBottom.Controls.Add(this.chkAutoRefresh);
            this.panelBottom.Controls.Add(this.btnRefresh);
            this.panelBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBottom.Location = new System.Drawing.Point(0, 731);
            this.panelBottom.Name = "panelBottom";
            this.panelBottom.Size = new System.Drawing.Size(1284, 50);
            this.panelBottom.TabIndex = 2;

            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold);
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(25)))), ((int)(((byte)(135)))), ((int)(((byte)(84)))));
            this.lblStatus.Location = new System.Drawing.Point(1150, 17);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(121, 20);
            this.lblStatus.TabIndex = 4;
            this.lblStatus.Text = "Đang cập nhật...";
            this.lblStatus.Click += new System.EventHandler(this.lblStatus_Click);

            // 
            // numRefreshInterval
            // 
            this.numRefreshInterval.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.numRefreshInterval.Location = new System.Drawing.Point(405, 13);
            this.numRefreshInterval.Maximum = new decimal(new int[] { 60, 0, 0, 0 });
            this.numRefreshInterval.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numRefreshInterval.Name = "numRefreshInterval";
            this.numRefreshInterval.Size = new System.Drawing.Size(60, 27);
            this.numRefreshInterval.TabIndex = 3;
            this.numRefreshInterval.Value = new decimal(new int[] { 3, 0, 0, 0 });
            this.numRefreshInterval.ValueChanged += new System.EventHandler(this.numRefreshInterval_ValueChanged);

            // 
            // lblRefreshInterval
            // 
            this.lblRefreshInterval.AutoSize = true;
            this.lblRefreshInterval.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblRefreshInterval.Location = new System.Drawing.Point(285, 16);
            this.lblRefreshInterval.Name = "lblRefreshInterval";
            this.lblRefreshInterval.Size = new System.Drawing.Size(135, 20);
            this.lblRefreshInterval.TabIndex = 2;
            this.lblRefreshInterval.Text = "Update every (sec):";

            // 
            // chkAutoRefresh
            // 
            this.chkAutoRefresh.AutoSize = true;
            this.chkAutoRefresh.Checked = true;
            this.chkAutoRefresh.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkAutoRefresh.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.chkAutoRefresh.Location = new System.Drawing.Point(150, 15);
            this.chkAutoRefresh.Name = "chkAutoRefresh";
            this.chkAutoRefresh.Size = new System.Drawing.Size(149, 24);
            this.chkAutoRefresh.TabIndex = 1;
            this.chkAutoRefresh.Text = "Automatic refresh";
            this.chkAutoRefresh.UseVisualStyleBackColor = true;
            this.chkAutoRefresh.CheckedChanged += new System.EventHandler(this.chkAutoRefresh_CheckedChanged);

            // 
            // btnRefresh
            // 
            this.btnRefresh.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(13)))), ((int)(((byte)(110)))), ((int)(((byte)(253)))));
            this.btnRefresh.FlatAppearance.BorderSize = 0;
            this.btnRefresh.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRefresh.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold);
            this.btnRefresh.ForeColor = System.Drawing.Color.White;
            this.btnRefresh.Location = new System.Drawing.Point(15, 10);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(120, 30);
            this.btnRefresh.TabIndex = 0;
            this.btnRefresh.Text = "🔄 Refresh";
            this.btnRefresh.UseVisualStyleBackColor = false;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);

            // 
            // timerRefresh
            // 
            this.timerRefresh.Interval = 3000;
            this.timerRefresh.Tick += new System.EventHandler(this.timerRefresh_Tick);

            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1284, 781);

            this.Controls.Add(this.panelBottom);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.panelTop);
            this.Controls.Add(this.menuStrip);

            this.MainMenuStrip = this.menuStrip;

            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.MinimumSize = new System.Drawing.Size(1100, 750);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Task Manager Plus";
            this.Load += new System.EventHandler(this.Form1_Load);

            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.tabControl.ResumeLayout(false);
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLogo)).EndInit();
            this.panelBottom.ResumeLayout(false);
            this.panelBottom.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numRefreshInterval)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabProcesses;
        private System.Windows.Forms.TabPage tabPerformance;
        private System.Windows.Forms.TabPage tabTemperature;
        private System.Windows.Forms.TabPage tabBattery;
        private System.Windows.Forms.TabPage tabStartup;
        private System.Windows.Forms.TabPage tabAppHistory;
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.PictureBox pictureBoxLogo;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Button btnToggleLanguage;
        private System.Windows.Forms.Panel panelBottom;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.CheckBox chkAutoRefresh;
        private System.Windows.Forms.Label lblRefreshInterval;
        private System.Windows.Forms.NumericUpDown numRefreshInterval;
        private System.Windows.Forms.Timer timerRefresh;
        private System.Windows.Forms.Label lblStatus;
    }
}
