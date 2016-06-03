namespace Vita.UnitTests.Common {
  partial class BrowserForm {
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
      this.Browser = new System.Windows.Forms.WebBrowser();
      this.Address = new System.Windows.Forms.TextBox();
      this.panel1 = new System.Windows.Forms.Panel();
      this.lblUrl = new System.Windows.Forms.Label();
      this.panel1.SuspendLayout();
      this.SuspendLayout();
      // 
      // Browser
      // 
      this.Browser.Dock = System.Windows.Forms.DockStyle.Fill;
      this.Browser.Location = new System.Drawing.Point(0, 68);
      this.Browser.MinimumSize = new System.Drawing.Size(20, 20);
      this.Browser.Name = "Browser";
      this.Browser.Size = new System.Drawing.Size(1906, 1279);
      this.Browser.TabIndex = 0;
      // 
      // Address
      // 
      this.Address.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
      this.Address.Location = new System.Drawing.Point(104, 19);
      this.Address.Name = "Address";
      this.Address.ReadOnly = true;
      this.Address.Size = new System.Drawing.Size(1790, 31);
      this.Address.TabIndex = 1;
      // 
      // panel1
      // 
      this.panel1.Controls.Add(this.lblUrl);
      this.panel1.Controls.Add(this.Address);
      this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
      this.panel1.Location = new System.Drawing.Point(0, 0);
      this.panel1.Name = "panel1";
      this.panel1.Size = new System.Drawing.Size(1906, 68);
      this.panel1.TabIndex = 2;
      // 
      // lblUrl
      // 
      this.lblUrl.AutoSize = true;
      this.lblUrl.Location = new System.Drawing.Point(23, 25);
      this.lblUrl.Name = "lblUrl";
      this.lblUrl.Size = new System.Drawing.Size(60, 25);
      this.lblUrl.TabIndex = 2;
      this.lblUrl.Text = "URL:";
      // 
      // BrowserForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(1906, 1347);
      this.Controls.Add(this.Browser);
      this.Controls.Add(this.panel1);
      this.Name = "BrowserForm";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      this.Text = "BrowserForm";
      this.panel1.ResumeLayout(false);
      this.panel1.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion

    public System.Windows.Forms.WebBrowser Browser;
    private System.Windows.Forms.Panel panel1;
    private System.Windows.Forms.Label lblUrl;
    public System.Windows.Forms.TextBox Address;
  }
}