namespace OlegZee.FractalBrowser.View
{
	partial class Form1
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.btnStart = new System.Windows.Forms.Button();
			this.listBoxLog = new System.Windows.Forms.ListBox();
			this.comboBoxRenderer = new System.Windows.Forms.ComboBox();
			this.label1 = new System.Windows.Forms.Label();
			this.buttonBenchmark = new System.Windows.Forms.Button();
			this.comboboxPicSize = new System.Windows.Forms.ComboBox();
			this.label2 = new System.Windows.Forms.Label();
			((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
			this.SuspendLayout();
			// 
			// pictureBox1
			// 
			this.pictureBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
						| System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.pictureBox1.BackColor = System.Drawing.Color.Black;
			this.pictureBox1.Location = new System.Drawing.Point(437, 22);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(512, 512);
			this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
			this.pictureBox1.TabIndex = 0;
			this.pictureBox1.TabStop = false;
			// 
			// btnStart
			// 
			this.btnStart.Location = new System.Drawing.Point(25, 34);
			this.btnStart.Name = "btnStart";
			this.btnStart.Size = new System.Drawing.Size(75, 23);
			this.btnStart.TabIndex = 0;
			this.btnStart.Text = "Start";
			this.btnStart.UseVisualStyleBackColor = true;
			this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
			// 
			// listBoxLog
			// 
			this.listBoxLog.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
						| System.Windows.Forms.AnchorStyles.Left)));
			this.listBoxLog.FormattingEnabled = true;
			this.listBoxLog.IntegralHeight = false;
			this.listBoxLog.Location = new System.Drawing.Point(12, 372);
			this.listBoxLog.Name = "listBoxLog";
			this.listBoxLog.Size = new System.Drawing.Size(403, 160);
			this.listBoxLog.TabIndex = 6;
			// 
			// comboBoxRenderer
			// 
			this.comboBoxRenderer.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBoxRenderer.FormattingEnabled = true;
			this.comboBoxRenderer.Location = new System.Drawing.Point(273, 36);
			this.comboBoxRenderer.Name = "comboBoxRenderer";
			this.comboBoxRenderer.Size = new System.Drawing.Size(142, 21);
			this.comboBoxRenderer.TabIndex = 3;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(213, 39);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(54, 13);
			this.label1.TabIndex = 2;
			this.label1.Text = "Renderer:";
			// 
			// buttonBenchmark
			// 
			this.buttonBenchmark.Location = new System.Drawing.Point(25, 318);
			this.buttonBenchmark.Name = "buttonBenchmark";
			this.buttonBenchmark.Size = new System.Drawing.Size(75, 23);
			this.buttonBenchmark.TabIndex = 1;
			this.buttonBenchmark.Text = "Benchmark";
			this.buttonBenchmark.UseVisualStyleBackColor = true;
			this.buttonBenchmark.Click += new System.EventHandler(this.buttonBenchmark_Click);
			// 
			// comboboxPicSize
			// 
			this.comboboxPicSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboboxPicSize.FormattingEnabled = true;
			this.comboboxPicSize.Location = new System.Drawing.Point(273, 64);
			this.comboboxPicSize.Name = "comboboxPicSize";
			this.comboboxPicSize.Size = new System.Drawing.Size(104, 21);
			this.comboboxPicSize.TabIndex = 5;
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(213, 67);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(30, 13);
			this.label2.TabIndex = 4;
			this.label2.Text = "Size:";
			// 
			// Form1
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(974, 564);
			this.Controls.Add(this.comboboxPicSize);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.comboBoxRenderer);
			this.Controls.Add(this.listBoxLog);
			this.Controls.Add(this.buttonBenchmark);
			this.Controls.Add(this.btnStart);
			this.Controls.Add(this.pictureBox1);
			this.Name = "Form1";
			this.Text = "Form1";
			this.Load += new System.EventHandler(this.Form1_Load);
			((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.PictureBox pictureBox1;
		private System.Windows.Forms.Button btnStart;
		private System.Windows.Forms.ListBox listBoxLog;
		private System.Windows.Forms.ComboBox comboBoxRenderer;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Button buttonBenchmark;
		private System.Windows.Forms.ComboBox comboboxPicSize;
		private System.Windows.Forms.Label label2;
	}
}

