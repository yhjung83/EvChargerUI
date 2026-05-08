namespace WinWatchDogApp
{
    partial class Watchdog
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.flowLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.button_Save = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.groupBox_Notice = new System.Windows.Forms.GroupBox();
            this.label_Notice = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.groupBox_Notice.SuspendLayout();
            this.SuspendLayout();
            // 
            // flowLayoutPanel
            // 
            this.flowLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanel.Name = "flowLayoutPanel";
            this.flowLayoutPanel.Size = new System.Drawing.Size(780, 368);
            this.flowLayoutPanel.TabIndex = 0;
            // 
            // button_Save
            // 
            this.button_Save.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.button_Save.Font = new System.Drawing.Font("굴림", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.button_Save.Location = new System.Drawing.Point(0, 368);
            this.button_Save.Name = "button_Save";
            this.button_Save.Size = new System.Drawing.Size(1080, 50);
            this.button_Save.TabIndex = 1;
            this.button_Save.Text = "SAVE";
            this.button_Save.UseVisualStyleBackColor = true;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.flowLayoutPanel);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.groupBox_Notice);
            this.splitContainer1.Size = new System.Drawing.Size(1080, 368);
            this.splitContainer1.SplitterDistance = 780;
            this.splitContainer1.TabIndex = 2;
            // 
            // groupBox_Notice
            // 
            this.groupBox_Notice.Controls.Add(this.label_Notice);
            this.groupBox_Notice.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox_Notice.Font = new System.Drawing.Font("굴림", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.groupBox_Notice.Location = new System.Drawing.Point(0, 0);
            this.groupBox_Notice.Name = "groupBox_Notice";
            this.groupBox_Notice.Size = new System.Drawing.Size(296, 368);
            this.groupBox_Notice.TabIndex = 0;
            this.groupBox_Notice.TabStop = false;
            this.groupBox_Notice.Text = "Notice";
            // 
            // label_Notice
            // 
            this.label_Notice.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label_Notice.Location = new System.Drawing.Point(3, 22);
            this.label_Notice.Name = "label_Notice";
            this.label_Notice.Size = new System.Drawing.Size(290, 343);
            this.label_Notice.TabIndex = 0;
            this.label_Notice.Text = "Notice content goes here.";
            // 
            // Watchdog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.GradientActiveCaption;
            this.ClientSize = new System.Drawing.Size(1080, 418);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.button_Save);
            this.Name = "Watchdog";
            this.Text = "WatchDog";
            this.ControlBox = true;
            this.MaximizeBox = false;            
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.groupBox_Notice.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel;
        private System.Windows.Forms.Button button_Save;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.GroupBox groupBox_Notice;
        private System.Windows.Forms.Label label_Notice;
    }
}