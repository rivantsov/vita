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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
      this.pnlTop = new System.Windows.Forms.Panel();
      this.btnGetToken = new System.Windows.Forms.Button();
      this.lblStatus = new System.Windows.Forms.Label();
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
      this.btnTestCall = new System.Windows.Forms.Button();
      this.pnlTop.SuspendLayout();
      this.SuspendLayout();
      // 
      // pnlTop
      // 
      this.pnlTop.Controls.Add(this.btnTestCall);
      this.pnlTop.Controls.Add(this.btnGetToken);
      this.pnlTop.Controls.Add(this.lblStatus);
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
      this.pnlTop.Size = new System.Drawing.Size(1718, 733);
      this.pnlTop.TabIndex = 1;
      // 
      // btnGetToken
      // 
      this.btnGetToken.Enabled = false;
      this.btnGetToken.Location = new System.Drawing.Point(417, 272);
      this.btnGetToken.Name = "btnGetToken";
      this.btnGetToken.Size = new System.Drawing.Size(304, 41);
      this.btnGetToken.TabIndex = 12;
      this.btnGetToken.Text = "Retrieve access token";
      this.btnGetToken.UseVisualStyleBackColor = true;
      this.btnGetToken.Click += new System.EventHandler(this.btnGetToken_Click);
      // 
      // lblStatus
      // 
      this.lblStatus.Location = new System.Drawing.Point(41, 439);
      this.lblStatus.Name = "lblStatus";
      this.lblStatus.Size = new System.Drawing.Size(1636, 253);
      this.lblStatus.TabIndex = 11;
      this.lblStatus.Text = "(status)";
      // 
      // label6
      // 
      this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.label6.Location = new System.Drawing.Point(985, 27);
      this.label6.Name = "label6";
      this.label6.Size = new System.Drawing.Size(704, 206);
      this.label6.TabIndex = 10;
      this.label6.Text = resources.GetString("label6.Text");
      // 
      // btnStart
      // 
      this.btnStart.Enabled = false;
      this.btnStart.Location = new System.Drawing.Point(418, 204);
      this.btnStart.Name = "btnStart";
      this.btnStart.Size = new System.Drawing.Size(304, 41);
      this.btnStart.TabIndex = 9;
      this.btnStart.Text = "Start OAuth process";
      this.btnStart.UseVisualStyleBackColor = true;
      this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
      // 
      // label5
      // 
      this.label5.AutoSize = true;
      this.label5.Location = new System.Drawing.Point(41, 208);
      this.label5.Name = "label5";
      this.label5.Size = new System.Drawing.Size(371, 25);
      this.label5.TabIndex = 8;
      this.label5.Text = "3. Start OAuth authorization process: ";
      // 
      // label4
      // 
      this.label4.AutoSize = true;
      this.label4.Location = new System.Drawing.Point(41, 69);
      this.label4.Name = "label4";
      this.label4.Size = new System.Drawing.Size(472, 25);
      this.label4.TabIndex = 7;
      this.label4.Text = "2. Set/update client app registration parameters:";
      // 
      // btnEditSave
      // 
      this.btnEditSave.Location = new System.Drawing.Point(605, 111);
      this.btnEditSave.Name = "btnEditSave";
      this.btnEditSave.Size = new System.Drawing.Size(116, 38);
      this.btnEditSave.TabIndex = 6;
      this.btnEditSave.Text = "Edit";
      this.btnEditSave.UseVisualStyleBackColor = true;
      this.btnEditSave.Click += new System.EventHandler(this.btnEditSave_Click);
      // 
      // txtClientSecret
      // 
      this.txtClientSecret.Location = new System.Drawing.Point(239, 151);
      this.txtClientSecret.Name = "txtClientSecret";
      this.txtClientSecret.ReadOnly = true;
      this.txtClientSecret.Size = new System.Drawing.Size(339, 31);
      this.txtClientSecret.TabIndex = 5;
      // 
      // label3
      // 
      this.label3.AutoSize = true;
      this.label3.Location = new System.Drawing.Point(89, 151);
      this.label3.Name = "label3";
      this.label3.Size = new System.Drawing.Size(138, 25);
      this.label3.TabIndex = 4;
      this.label3.Text = "Client secret:";
      // 
      // txtClientId
      // 
      this.txtClientId.Location = new System.Drawing.Point(239, 112);
      this.txtClientId.Name = "txtClientId";
      this.txtClientId.ReadOnly = true;
      this.txtClientId.Size = new System.Drawing.Size(339, 31);
      this.txtClientId.TabIndex = 3;
      // 
      // label2
      // 
      this.label2.AutoSize = true;
      this.label2.Location = new System.Drawing.Point(89, 109);
      this.label2.Name = "label2";
      this.label2.Size = new System.Drawing.Size(96, 25);
      this.label2.TabIndex = 2;
      this.label2.Text = "Client id:";
      // 
      // cboServers
      // 
      this.cboServers.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.cboServers.FormattingEnabled = true;
      this.cboServers.Location = new System.Drawing.Point(284, 19);
      this.cboServers.Name = "cboServers";
      this.cboServers.Size = new System.Drawing.Size(437, 33);
      this.cboServers.TabIndex = 1;
      this.cboServers.SelectedIndexChanged += new System.EventHandler(this.cboServers_SelectedIndexChanged);
      // 
      // label1
      // 
      this.label1.AutoSize = true;
      this.label1.Location = new System.Drawing.Point(41, 22);
      this.label1.Name = "label1";
      this.label1.Size = new System.Drawing.Size(237, 25);
      this.label1.TabIndex = 0;
      this.label1.Text = "1. Select OAuth Server:";
      // 
      // btnTestCall
      // 
      this.btnTestCall.Enabled = false;
      this.btnTestCall.Location = new System.Drawing.Point(418, 354);
      this.btnTestCall.Name = "btnTestCall";
      this.btnTestCall.Size = new System.Drawing.Size(304, 41);
      this.btnTestCall.TabIndex = 13;
      this.btnTestCall.Text = "Make test call";
      this.btnTestCall.UseVisualStyleBackColor = true;
      this.btnTestCall.Click += new System.EventHandler(this.btnTestCall_Click);
      // 
      // MainForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(1718, 1147);
      this.Controls.Add(this.pnlTop);
      this.Name = "MainForm";
      this.Text = "OAuthDemo";
      this.pnlTop.ResumeLayout(false);
      this.pnlTop.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion
    private System.Windows.Forms.Panel pnlTop;
    private System.Windows.Forms.ComboBox cboServers;
    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.Label label6;
    private System.Windows.Forms.Button btnStart;
    private System.Windows.Forms.Label label5;
    private System.Windows.Forms.Label label4;
    private System.Windows.Forms.Button btnEditSave;
    private System.Windows.Forms.TextBox txtClientSecret;
    private System.Windows.Forms.Label label3;
    private System.Windows.Forms.TextBox txtClientId;
    private System.Windows.Forms.Label label2;
    private System.Windows.Forms.Label lblStatus;
    private System.Windows.Forms.Button btnGetToken;
    private System.Windows.Forms.Button btnTestCall;
  }
}

