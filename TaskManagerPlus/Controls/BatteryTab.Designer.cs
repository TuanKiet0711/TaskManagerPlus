namespace TaskManagerPlus.Controls
{
    partial class BatteryTab
    {
        private System.ComponentModel.IContainer components = null;

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.panelScroll = new System.Windows.Forms.Panel();
            this.pictureBoxBattery = new System.Windows.Forms.PictureBox();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblInfo = new System.Windows.Forms.Label();
            this.panelScroll.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxBattery)).BeginInit();
            this.SuspendLayout();
            // 
            // panelScroll
            // 
            this.panelScroll.AutoScroll = true;
            this.panelScroll.BackColor = System.Drawing.Color.White;
            this.panelScroll.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelScroll.Location = new System.Drawing.Point(0, 0);
            this.panelScroll.Name = "panelScroll";
            this.panelScroll.Size = new System.Drawing.Size(1000, 660);
            this.panelScroll.TabIndex = 3;
            // 
            // pictureBoxBattery
            // 
            this.pictureBoxBattery.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBoxBattery.BackColor = System.Drawing.Color.White;
            this.pictureBoxBattery.Location = new System.Drawing.Point(20, 80);
            this.pictureBoxBattery.Name = "pictureBoxBattery";
            this.pictureBoxBattery.Size = new System.Drawing.Size(960, 820);
            this.pictureBoxBattery.TabIndex = 0;
            this.pictureBoxBattery.TabStop = false;
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(58)))), ((int)(((byte)(64)))));
            this.lblTitle.Location = new System.Drawing.Point(20, 15);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(83, 30);
            this.lblTitle.TabIndex = 1;
            this.lblTitle.Text = "Battery";
            // 
            // lblInfo
            // 
            this.lblInfo.AutoSize = true;
            this.lblInfo.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblInfo.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(108)))), ((int)(((byte)(117)))), ((int)(((byte)(125)))));
            this.lblInfo.Location = new System.Drawing.Point(20, 50);
            this.lblInfo.Name = "lblInfo";
            this.lblInfo.Size = new System.Drawing.Size(350, 15);
            this.lblInfo.TabIndex = 2;
            this.lblInfo.Text = "Monitor battery status, charge level, and health information";
            // 
            // BatteryTab
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.panelScroll.Controls.Add(this.lblInfo);
            this.panelScroll.Controls.Add(this.lblTitle);
            this.panelScroll.Controls.Add(this.pictureBoxBattery);
            this.Controls.Add(this.panelScroll);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.Name = "BatteryTab";
            this.Size = new System.Drawing.Size(1000, 660);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxBattery)).EndInit();
            this.panelScroll.ResumeLayout(false);
            this.panelScroll.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelScroll;
        private System.Windows.Forms.PictureBox pictureBoxBattery;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblInfo;
    }
}
