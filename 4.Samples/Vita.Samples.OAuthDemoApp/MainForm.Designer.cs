namespace Vita.Samples.OAuthDemoApp {
  partial class MainForm {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing) {
      if(disposing && (components != null)) {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent() {
      this.pnlTop = new System.Windows.Forms.Panel();
      this.label7 = new System.Windows.Forms.Label();
      this.linkSpecialCases = new System.Windows.Forms.LinkLabel();
      this.linkDocs = new System.Windows.Forms.LinkLabel();
      this.btnCancel = new System.Windows.Forms.Button();
      this.label6 = new System.Windows.Forms.Label();
      this.btnStart = new System.Windows.Forms.Button();
      this.label5 = new System.Windows.Forms.Label();
      this.label4 = new System.Windows.Forms.Label();
      this.btnEditSave = new System.Windows.Forms.Button();
      this.txtClientSecret = new System.Windows.Forms.TextBox();
      this.label3 = new System.Windows.Forms.Label();
      this.txtClientId = new System.Windows.Forms.TextBox();
      this.label2 = new System.Windows.Forms.Label();
      this.cboServers = new System.Windows.Forms.ComboBox();
      this.label1 = new System.Windows.Forms.Label();
      this.groupBox1 = new System.Windows.Forms.GroupBox();
      this.txtLog = new System.Windows.Forms.TextBox();
      this.pnlTop.SuspendLayout();
      this.groupBox1.SuspendLayout();
      this.SuspendLayout();
      // 
      // pnlTop
      // 
      this.pnlTop.Controls.Add(this.label7);
      this.pnlTop.Controls.Add(this.linkSpecialCases);
      this.pnlTop.Controls.Add(this.linkDocs);
      this.pnlTop.Controls.Add(this.btnCancel);
      this.pnlTop.Controls.Add(this.label6);
      this.pnlTop.Controls.Add(this.btnStart);
      this.pnlTop.Controls.Add(this.label5);
      this.pnlTop.Controls.Add(this.label4);
      this.pnlTop.Controls.Add(this.btnEditSave);
      this.pnlTop.Controls.Add(this.txtClientSecret);
      this.pnlTop.Controls.Add(this.label3);
      this.pnlTop.Controls.Add(this.txtClientId);
      this.pnlTop.Controls.Add(this.label2);
      this.pnlTop.Controls.Add(this.cboServers);
      this.pnlTop.Controls.Add(this.label1);
      this.pnlTop.Dock = System.Windows.Forms.DockStyle.Top;
      this.pnlTop.Location = new System.Drawing.Point(0, 0);
      this.pnlTop.Name = "pnlTop";
      this.pnlTop.Size = new System.Drawing.Size(1520, 370);
      this.pnlTop.TabIndex = 1;
      // 
      // label7
      // 
      this.label7.AutoSize = true;
      this.label7.Location = new System.Drawing.Point(935, 213);
      this.label7.Name = "label7";
      this.label7.Size = new System.Drawing.Size(234, 25);
      this.label7.TabIndex = 17;
      this.label7.Text = "About registering apps:";
      // 
      // linkSpecialCases
      // 
      this.linkSpecialCases.AutoSize = true;
      this.linkSpecialCases.Location = new System.Drawing.Point(935, 249);
      this.linkSpecialCases.Name = "linkSpecialCases";
      this.linkSpecialCases.Size = new System.Drawing.Size(248, 25);
      this.linkSpecialCases.TabIndex = 16;
      this.linkSpecialCases.TabStop = true;
      this.linkSpecialCases.Text = "RegisteringOAuthApp.txt";
      this.linkSpecialCases.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkSpecialCases_LinkClicked);
      // 
      // linkDocs
      // 
      this.linkDocs.AutoSize = true;
      this.linkDocs.Location = new System.Drawing.Point(116, 260);
      this.linkDocs.Name = "linkDocs";
      this.linkDocs.Size = new System.Drawing.Size(100, 25);
      this.linkDocs.TabIndex = 15;
      this.linkDocs.TabStop = true;
      this.linkDocs.Text = "(doc link)";
      this.linkDocs.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkDocs_LinkClicked);
      // 
      // btnCancel
      // 
      this.btnCancel.Enabled = false;
      this.btnCancel.Location = new System.Drawing.Point(724, 301);
      this.btnCancel.Name = "btnCancel";
      this.btnCancel.Size = new System.Drawing.Size(146, 41);
      this.btnCancel.TabIndex = 13;
      this.btnCancel.Text = "Cancel";
      this.btnCancel.UseVisualStyleBackColor = true;
      this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
      // 
      // label6
      // 
      this.label6.Location = new System.Drawing.Point(116, 204);
      this.label6.Name = "label6";
      this.label6.Size = new System.Drawing.Size(730, 56);
      this.label6.TabIndex = 12;
      this.label6.Text = "Visit OAuth server site, signup and register a new OAuth Client app, and copy cli" +
    "ent Id and Secret into the boxes above. Documentation:";
      // 
      // btnStart
      // 
      this.btnStart.Enabled = false;
      this.btnStart.Location = new System.Drawing.Point(559, 301);
      this.btnStart.Name = "btnStart";
      this.btnStart.Size = new System.Drawing.Size(146, 41);
      this.btnStart.TabIndex = 9;
      this.btnStart.Text = "Start";
      this.btnStart.UseVisualStyleBackColor = true;
      this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
      // 
      // label5
      // 
      this.label5.AutoSize = true;
      this.label5.Location = new System.Drawing.Point(25, 307);
      this.label5.Name = "label5";
      this.label5.Size = new System.Drawing.Size(371, 25);
      this.label5.TabIndex = 8;
      this.label5.Text = "3. Start OAuth authorization process: ";
      // 
      // label4
      // 
      this.label4.AutoSize = true;
      this.label4.Location = new System.Drawing.Point(25, 69);
      this.label4.Name = "label4";
      this.label4.Size = new System.Drawing.Size(472, 25);
      this.label4.TabIndex = 7;
      this.label4.Text = "2. Set/update client app registration parameters.";
      // 
      // btnEditSave
      // 
      this.btnEditSave.Location = new System.Drawing.Point(724, 117);
      this.btnEditSave.Name = "btnEditSave";
      this.btnEditSave.Size = new System.Drawing.Size(146, 38);
      this.btnEditSave.TabIndex = 6;
      this.btnEditSave.Text = "Edit";
      this.btnEditSave.UseVisualStyleBackColor = true;
      this.btnEditSave.Click += new System.EventHandler(this.btnEditSave_Click);
      // 
      // txtClientSecret
      // 
      this.txtClientSecret.Location = new System.Drawing.Point(268, 156);
      this.txtClientSecret.Name = "txtClientSecret";
      this.txtClientSecret.ReadOnly = true;
      this.txtClientSecret.Size = new System.Drawing.Size(437, 31);
      this.txtClientSecret.TabIndex = 5;
      // 
      // label3
      // 
      this.label3.AutoSize = true;
      this.label3.Location = new System.Drawing.Point(73, 158);
      this.label3.Name = "label3";
      this.label3.Size = new System.Drawing.Size(138, 25);
      this.label3.TabIndex = 4;
      this.label3.Text = "Client secret:";
      // 
      // txtClientId
      // 
      this.txtClientId.Location = new System.Drawing.Point(268, 117);
      this.txtClientId.Name = "txtClientId";
      this.txtClientId.ReadOnly = true;
      this.txtClientId.Size = new System.Drawing.Size(437, 31);
      this.txtClientId.TabIndex = 3;
      // 
      // label2
      // 
      this.label2.AutoSize = true;
      this.label2.Location = new System.Drawing.Point(74, 116);
      this.label2.Name = "label2";
      this.label2.Size = new System.Drawing.Size(96, 25);
      this.label2.TabIndex = 2;
      this.label2.Text = "Client id:";
      // 
      // cboServers
      // 
      this.cboServers.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.cboServers.FormattingEnabled = true;
      this.cboServers.Location = new System.Drawing.Point(268, 19);
      this.cboServers.Name = "cboServers";
      this.cboServers.Size = new System.Drawing.Size(437, 33);
      this.cboServers.TabIndex = 1;
      this.cboServers.SelectedIndexChanged += new System.EventHandler(this.cboServers_SelectedIndexChanged);
      // 
      // label1
      // 
      this.label1.AutoSize = true;
      this.label1.Location = new System.Drawing.Point(25, 22);
      this.label1.Name = "label1";
      this.label1.Size = new System.Drawing.Size(237, 25);
      this.label1.TabIndex = 0;
      this.label1.Text = "1. Select OAuth Server:";
      // 
      // groupBox1
      // 
      this.groupBox1.Controls.Add(this.txtLog);
      this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
      this.groupBox1.Location = new System.Drawing.Point(0, 370);
      this.groupBox1.Name = "groupBox1";
      this.groupBox1.Size = new System.Drawing.Size(1520, 506);
      this.groupBox1.TabIndex = 2;
      this.groupBox1.TabStop = false;
      this.groupBox1.Text = "OAuth Process Log";
      // 
      // txtLog
      // 
      this.txtLog.Dock = System.Windows.Forms.DockStyle.Fill;
      this.txtLog.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
      this.txtLog.HideSelection = false;
      this.txtLog.Location = new System.Drawing.Point(3, 27);
      this.txtLog.Multiline = true;
      this.txtLog.Name = "txtLog";
      this.txtLog.ReadOnly = true;
      this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
      this.txtLog.Size = new System.Drawing.Size(1514, 476);
      this.txtLog.TabIndex = 0;
      // 
      // MainForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(1520, 876);
      this.Controls.Add(this.groupBox1);
      this.Controls.Add(this.pnlTop);
      this.Name = "MainForm";
      this.Text = "OAuth Demo App";
      this.pnlTop.ResumeLayout(false);
      this.pnlTop.PerformLayout();
      this.groupBox1.ResumeLayout(false);
      this.groupBox1.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion
    private System.Windows.Forms.Panel pnlTop;
    private System.Windows.Forms.ComboBox cboServers;
    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.Button btnStart;
    private System.Windows.Forms.Label label5;
    private System.Windows.Forms.Label label4;
    private System.Windows.Forms.Button btnEditSave;
    private System.Windows.Forms.TextBox txtClientSecret;
    private System.Windows.Forms.Label label3;
    private System.Windows.Forms.TextBox txtClientId;
    private System.Windows.Forms.Label label2;
    private System.Windows.Forms.Label label6;
    private System.Windows.Forms.GroupBox groupBox1;
    private System.Windows.Forms.TextBox txtLog;
    private System.Windows.Forms.Button btnCancel;
    private System.Windows.Forms.LinkLabel linkDocs;
    private System.Windows.Forms.Label label7;
    private System.Windows.Forms.LinkLabel linkSpecialCases;
  }
}

