using System;
using System.Windows.Forms;

namespace ToolCheckCmt {
    partial class Form1 {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
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
            this.dgvResult = new System.Windows.Forms.DataGridView();
            this.txtLinks = new System.Windows.Forms.RichTextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.lblLive = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblDie = new System.Windows.Forms.Label();
            this.btnCheck = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.rtbTokens = new System.Windows.Forms.RichTextBox();
            this.rtbKiotKeys = new System.Windows.Forms.RichTextBox();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResult)).BeginInit();
            this.SuspendLayout();
            // 
            // dgvResult
            // 
            this.dgvResult.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvResult.Location = new System.Drawing.Point(396, 12);
            this.dgvResult.Name = "dgvResult";
            this.dgvResult.Size = new System.Drawing.Size(359, 297);
            this.dgvResult.TabIndex = 0;
            this.dgvResult.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvResult_CellContentClick);
            // 
            // txtLinks
            // 
            this.txtLinks.Location = new System.Drawing.Point(49, 12);
            this.txtLinks.Name = "txtLinks";
            this.txtLinks.Size = new System.Drawing.Size(326, 297);
            this.txtLinks.TabIndex = 1;
            this.txtLinks.Text = "";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(16, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(27, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Link";
            // 
            // lblLive
            // 
            this.lblLive.AutoSize = true;
            this.lblLive.Location = new System.Drawing.Point(492, 366);
            this.lblLive.Name = "lblLive";
            this.lblLive.Size = new System.Drawing.Size(30, 13);
            this.lblLive.TabIndex = 3;
            this.lblLive.Text = "Alive";
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(684, 366);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(37, 13);
            this.lblStatus.TabIndex = 4;
            this.lblStatus.Text = "Status";
            // 
            // lblDie
            // 
            this.lblDie.AutoSize = true;
            this.lblDie.Location = new System.Drawing.Point(581, 366);
            this.lblDie.Name = "lblDie";
            this.lblDie.Size = new System.Drawing.Size(23, 13);
            this.lblDie.TabIndex = 5;
            this.lblDie.Text = "Die";
            // 
            // btnCheck
            // 
            this.btnCheck.Location = new System.Drawing.Point(529, 403);
            this.btnCheck.Name = "btnCheck";
            this.btnCheck.Size = new System.Drawing.Size(75, 23);
            this.btnCheck.TabIndex = 6;
            this.btnCheck.Text = "Start";
            this.btnCheck.UseVisualStyleBackColor = true;
            this.btnCheck.Click += new System.EventHandler(this.btnCheck_Click);
            // 
            // btnExport
            // 
            this.btnExport.Location = new System.Drawing.Point(646, 403);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(75, 23);
            this.btnExport.TabIndex = 7;
            this.btnExport.Text = "Export";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(5, 316);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(38, 13);
            this.label2.TabIndex = 9;
            this.label2.Text = "Token";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(10, 389);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(33, 13);
            this.label3.TabIndex = 10;
            this.label3.Text = "Proxy";
            this.label3.Click += new System.EventHandler(this.label3_Click);
            // 
            // rtbTokens
            // 
            this.rtbTokens.Location = new System.Drawing.Point(50, 327);
            this.rtbTokens.Name = "rtbTokens";
            this.rtbTokens.Size = new System.Drawing.Size(325, 52);
            this.rtbTokens.TabIndex = 11;
            this.rtbTokens.Text = "";
            // 
            // rtbKiotKeys
            // 
            this.rtbKiotKeys.Location = new System.Drawing.Point(49, 389);
            this.rtbKiotKeys.Name = "rtbKiotKeys";
            this.rtbKiotKeys.Size = new System.Drawing.Size(325, 52);
            this.rtbKiotKeys.TabIndex = 12;
            this.rtbKiotKeys.Text = "";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.rtbKiotKeys);
            this.Controls.Add(this.rtbTokens);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnExport);
            this.Controls.Add(this.btnCheck);
            this.Controls.Add(this.lblDie);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.lblLive);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtLinks);
            this.Controls.Add(this.dgvResult);
            this.Name = "Form1";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.dgvResult)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private void label3_Click(object sender, EventArgs e) {
            //throw new NotImplementedException();
        }

        private void Label3_Click(object sender, EventArgs e) {
            //throw new NotImplementedException();
        }

        private void dgvResult_CellContentClick(object sender, DataGridViewCellEventArgs e) {
            //throw new NotImplementedException();
        }

        #endregion

        private System.Windows.Forms.DataGridView dgvResult;
        private System.Windows.Forms.RichTextBox txtLinks;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lblLive;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblDie;
        private System.Windows.Forms.Button btnCheck;
        private System.Windows.Forms.Button btnExport;
        private Label label2;
        private Label label3;
        private RichTextBox rtbTokens;
        private RichTextBox rtbKiotKeys;
    }
}

