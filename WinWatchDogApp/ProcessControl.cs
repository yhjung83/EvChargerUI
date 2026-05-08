using System;
using System.Windows.Forms;

namespace WinWatchDogApp
{
    public class ProcessControl : UserControl
    {
        public Label label_Name;
        public Button button_Switch;
        public TextBox textBox_Timer;
        public Button button_Search;

        public ProcessControl()
        {
            this.label_Name = new System.Windows.Forms.Label();
            this.button_Switch = new System.Windows.Forms.Button();
            this.textBox_Timer = new System.Windows.Forms.TextBox();
            this.button_Search = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label_Name
            // 
            this.label_Name.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.label_Name.Font = new System.Drawing.Font("Gulim", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.label_Name.Location = new System.Drawing.Point(3, 4);
            this.label_Name.Name = "label_Name";
            this.label_Name.Size = new System.Drawing.Size(400, 50);
            this.label_Name.TabIndex = 0;
            this.label_Name.Text = "Process Name";
            this.label_Name.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // button_Switch
            // 
            this.button_Switch.Font = new System.Drawing.Font("Gulim", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.button_Switch.Location = new System.Drawing.Point(409, 4);
            this.button_Switch.Name = "button_Switch";
            this.button_Switch.Size = new System.Drawing.Size(100, 50);
            this.button_Switch.TabIndex = 1;
            this.button_Switch.Text = "OFF";
            this.button_Switch.UseVisualStyleBackColor = true;
            // 
            // textBox_Timer
            // 
            this.textBox_Timer.Font = new System.Drawing.Font("Gulim", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.textBox_Timer.Location = new System.Drawing.Point(515, 4);
            this.textBox_Timer.Multiline = true;
            this.textBox_Timer.Name = "textBox_Timer";
            this.textBox_Timer.Size = new System.Drawing.Size(100, 50);
            this.textBox_Timer.TabIndex = 2;
            this.textBox_Timer.Text = "60";
            this.textBox_Timer.TextAlign = HorizontalAlignment.Center;
            // 
            // button_Search
            // 
            this.button_Search.Font = new System.Drawing.Font("Gulim", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.button_Search.Location = new System.Drawing.Point(621, 4);
            this.button_Search.Name = "button_Search";
            this.button_Search.Size = new System.Drawing.Size(100, 50);
            this.button_Search.TabIndex = 3;
            this.button_Search.Text = "Search";
            this.button_Search.UseVisualStyleBackColor = true;
            // 
            // ProcessControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.button_Search);
            this.Controls.Add(this.textBox_Timer);
            this.Controls.Add(this.button_Switch);
            this.Controls.Add(this.label_Name);
            this.Name = "ProcessControl";
            this.Size = new System.Drawing.Size(730, 60);
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
