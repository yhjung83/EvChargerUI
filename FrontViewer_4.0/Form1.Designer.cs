using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;

namespace FrontViewer
{
    partial class Main_Form
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label_Ver = new System.Windows.Forms.Label();
            this.checkBox_View = new System.Windows.Forms.CheckBox();
            this.listView_List = new System.Windows.Forms.ListView();
            this.label_SelectPATH = new System.Windows.Forms.Label();
            this.label_SelectedPath = new System.Windows.Forms.Label();
            this.button_SAVE = new System.Windows.Forms.Button();
            this.label_List = new System.Windows.Forms.Label();
            this.label_PageSwapTimer = new System.Windows.Forms.Label();
            this.textBox_SwapTimer = new System.Windows.Forms.TextBox();
            this.button_Search = new System.Windows.Forms.Button();
            this.checkBox_SubMoniter = new System.Windows.Forms.CheckBox();
            this.button_Play = new System.Windows.Forms.Button();
            this.textBox_TouchCount = new System.Windows.Forms.TextBox();
            this.label_ClickCount = new System.Windows.Forms.Label();
            this.label_ClickCountAddinfo = new System.Windows.Forms.Label();
            this.label_Notice = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label_Ver
            // 
            this.label_Ver.AutoSize = true;
            this.label_Ver.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.label_Ver.Location = new System.Drawing.Point(554, 396);
            this.label_Ver.Name = "label_Ver";
            this.label_Ver.Size = new System.Drawing.Size(49, 21);
            this.label_Ver.TabIndex = 1;
            this.label_Ver.Text = "ver : ";
            // 
            // checkBox_View
            // 
            this.checkBox_View.Appearance = System.Windows.Forms.Appearance.Button;
            this.checkBox_View.Font = new System.Drawing.Font("맑은 고딕", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.checkBox_View.Location = new System.Drawing.Point(118, 44);
            this.checkBox_View.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.checkBox_View.Name = "checkBox_View";
            this.checkBox_View.Size = new System.Drawing.Size(142, 53);
            this.checkBox_View.TabIndex = 2;
            this.checkBox_View.Text = "시작시 설정화면 On/Off";
            this.checkBox_View.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.checkBox_View.UseVisualStyleBackColor = true;
            this.checkBox_View.CheckedChanged += new System.EventHandler(this.checkBox_View_CheckedChanged);
            // 
            // listView_List
            // 
            this.listView_List.Font = new System.Drawing.Font("맑은 고딕", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.listView_List.HideSelection = false;
            this.listView_List.Location = new System.Drawing.Point(118, 102);
            this.listView_List.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.listView_List.Name = "listView_List";
            this.listView_List.Size = new System.Drawing.Size(522, 170);
            this.listView_List.TabIndex = 3;
            this.listView_List.UseCompatibleStateImageBehavior = false;
            this.listView_List.View = System.Windows.Forms.View.Details;
            // 
            // label_SelectPATH
            // 
            this.label_SelectPATH.AutoSize = true;
            this.label_SelectPATH.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.label_SelectPATH.Location = new System.Drawing.Point(12, 274);
            this.label_SelectPATH.Name = "label_SelectPATH";
            this.label_SelectPATH.Size = new System.Drawing.Size(150, 21);
            this.label_SelectPATH.TabIndex = 6;
            this.label_SelectPATH.Text = "적용된 파일 경로 : ";
            // 
            // label_SelectedPath
            // 
            this.label_SelectedPath.AutoSize = true;
            this.label_SelectedPath.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.label_SelectedPath.Location = new System.Drawing.Point(168, 274);
            this.label_SelectedPath.Name = "label_SelectedPath";
            this.label_SelectedPath.Size = new System.Drawing.Size(82, 21);
            this.label_SelectedPath.TabIndex = 7;
            this.label_SelectedPath.Text = "File PATH";
            // 
            // button_SAVE
            // 
            this.button_SAVE.Font = new System.Drawing.Font("맑은 고딕", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.button_SAVE.Location = new System.Drawing.Point(564, 329);
            this.button_SAVE.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.button_SAVE.Name = "button_SAVE";
            this.button_SAVE.Size = new System.Drawing.Size(96, 61);
            this.button_SAVE.TabIndex = 8;
            this.button_SAVE.Text = "저장";
            this.button_SAVE.UseVisualStyleBackColor = true;
            this.button_SAVE.Click += new System.EventHandler(this.button_SAVE_Click);
            // 
            // label_List
            // 
            this.label_List.AutoSize = true;
            this.label_List.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.label_List.Location = new System.Drawing.Point(12, 164);
            this.label_List.Name = "label_List";
            this.label_List.Size = new System.Drawing.Size(96, 21);
            this.label_List.TabIndex = 9;
            this.label_List.Text = "적용 파일 : ";
            // 
            // label_PageSwapTimer
            // 
            this.label_PageSwapTimer.AutoSize = true;
            this.label_PageSwapTimer.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.label_PageSwapTimer.Location = new System.Drawing.Point(12, 303);
            this.label_PageSwapTimer.Name = "label_PageSwapTimer";
            this.label_PageSwapTimer.Size = new System.Drawing.Size(164, 21);
            this.label_PageSwapTimer.TabIndex = 10;
            this.label_PageSwapTimer.Text = "화면전환 주기(Sec) : ";
            // 
            // textBox_SwapTimer
            // 
            this.textBox_SwapTimer.Font = new System.Drawing.Font("맑은 고딕", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.textBox_SwapTimer.Location = new System.Drawing.Point(168, 301);
            this.textBox_SwapTimer.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.textBox_SwapTimer.Name = "textBox_SwapTimer";
            this.textBox_SwapTimer.Size = new System.Drawing.Size(103, 29);
            this.textBox_SwapTimer.TabIndex = 11;
            this.textBox_SwapTimer.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // button_Search
            // 
            this.button_Search.Font = new System.Drawing.Font("맑은 고딕", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.button_Search.Location = new System.Drawing.Point(646, 102);
            this.button_Search.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.button_Search.Name = "button_Search";
            this.button_Search.Size = new System.Drawing.Size(113, 170);
            this.button_Search.TabIndex = 13;
            this.button_Search.Text = "찾기";
            this.button_Search.UseVisualStyleBackColor = true;
            this.button_Search.Click += new System.EventHandler(this.button_Search_Click);
            // 
            // checkBox_SubMoniter
            // 
            this.checkBox_SubMoniter.Appearance = System.Windows.Forms.Appearance.Button;
            this.checkBox_SubMoniter.Font = new System.Drawing.Font("맑은 고딕", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.checkBox_SubMoniter.Location = new System.Drawing.Point(266, 44);
            this.checkBox_SubMoniter.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.checkBox_SubMoniter.Name = "checkBox_SubMoniter";
            this.checkBox_SubMoniter.Size = new System.Drawing.Size(144, 53);
            this.checkBox_SubMoniter.TabIndex = 14;
            this.checkBox_SubMoniter.Text = "서브모니터 유/무";
            this.checkBox_SubMoniter.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.checkBox_SubMoniter.UseVisualStyleBackColor = true;
            this.checkBox_SubMoniter.CheckedChanged += new System.EventHandler(this.checkBox_SubMoniter_CheckedChanged);
            // 
            // button_Play
            // 
            this.button_Play.Font = new System.Drawing.Font("맑은 고딕", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.button_Play.Location = new System.Drawing.Point(663, 329);
            this.button_Play.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.button_Play.Name = "button_Play";
            this.button_Play.Size = new System.Drawing.Size(96, 61);
            this.button_Play.TabIndex = 15;
            this.button_Play.Text = "실행";
            this.button_Play.UseVisualStyleBackColor = true;
            this.button_Play.Click += new System.EventHandler(this.button_Play_Click);
            // 
            // textBox_TouchCount
            // 
            this.textBox_TouchCount.Font = new System.Drawing.Font("맑은 고딕", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.textBox_TouchCount.Location = new System.Drawing.Point(171, 339);
            this.textBox_TouchCount.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.textBox_TouchCount.Name = "textBox_TouchCount";
            this.textBox_TouchCount.Size = new System.Drawing.Size(103, 29);
            this.textBox_TouchCount.TabIndex = 17;
            this.textBox_TouchCount.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label_ClickCount
            // 
            this.label_ClickCount.AutoSize = true;
            this.label_ClickCount.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.label_ClickCount.Location = new System.Drawing.Point(12, 342);
            this.label_ClickCount.Name = "label_ClickCount";
            this.label_ClickCount.Size = new System.Drawing.Size(157, 21);
            this.label_ClickCount.TabIndex = 16;
            this.label_ClickCount.Text = "화면터치 횟수(5초) :"; 
            // 
            // label_ClickCountAddinfo
            // 
            this.label_ClickCountAddinfo.AutoSize = true;
            this.label_ClickCountAddinfo.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.label_ClickCountAddinfo.Location = new System.Drawing.Point(278, 343);
            this.label_ClickCountAddinfo.Name = "label_ClickCountAddinfo";
            this.label_ClickCountAddinfo.Size = new System.Drawing.Size(274, 21);
            this.label_ClickCountAddinfo.TabIndex = 18;
            this.label_ClickCountAddinfo.Text = "번 이상 터치하면 설정하면으로 이동";
            // 
            // label_Notice
            // 
            this.label_Notice.AutoSize = true;
            this.label_Notice.BackColor = System.Drawing.SystemColors.GradientActiveCaption;
            this.label_Notice.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.label_Notice.Location = new System.Drawing.Point(0, 0);
            this.label_Notice.Name = "label_Notice";
            this.label_Notice.Size = new System.Drawing.Size(550, 21);
            this.label_Notice.TabIndex = 19;
            this.label_Notice.Text = "이프로그램은 (주)모니트의 상용 소프트웨어 입니다. 무단 사용을 금합니다.";
            // 
            // Main_Form
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.GradientInactiveCaption;
            this.ClientSize = new System.Drawing.Size(762, 422);
            this.Controls.Add(this.label_Notice);
            this.Controls.Add(this.label_ClickCountAddinfo);
            this.Controls.Add(this.textBox_TouchCount);
            this.Controls.Add(this.label_ClickCount);
            this.Controls.Add(this.button_Play);
            this.Controls.Add(this.checkBox_SubMoniter);
            this.Controls.Add(this.button_Search);
            this.Controls.Add(this.textBox_SwapTimer);
            this.Controls.Add(this.label_PageSwapTimer);
            this.Controls.Add(this.label_List);
            this.Controls.Add(this.button_SAVE);
            this.Controls.Add(this.label_SelectedPath);
            this.Controls.Add(this.label_SelectPATH);
            this.Controls.Add(this.listView_List);
            this.Controls.Add(this.checkBox_View);
            this.Controls.Add(this.label_Ver);
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.Name = "Main_Form";
            this.Text = "FrontViewer";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Label label_Ver;
        private CheckBox checkBox_View;
        private ListView listView_List;
        private Label label_SelectPATH;
        private Label label_SelectedPath;
        private Button button_SAVE;
        private Label label_List;
        private Label label_PageSwapTimer;
        internal TextBox textBox_SwapTimer;
        private Button button_Search;
        private CheckBox checkBox_SubMoniter;
        private Button button_Play;
        internal TextBox textBox_TouchCount;
        private Label label_ClickCount;
        private Label label_ClickCountAddinfo;
        private Label label_Notice;
    }
}
