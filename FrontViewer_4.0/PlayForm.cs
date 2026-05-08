using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;

namespace FrontViewer
{
    public partial class PlayForm : Form
    {
        private List<string> imagePaths;
        private int currentImageIndex = 0;
        private System.Windows.Forms.Timer imageTimer = new System.Windows.Forms.Timer();
        private Form parentForm;

        private int clickCount = 0;
        private DateTime firstClickTime;
        private int touchThreshold;

        public PlayForm(List<string> imagePaths, int interval, Form parentForm, int touchCount)
        {
            InitializeComponent();
            this.imagePaths = imagePaths;
            this.parentForm = parentForm;
            this.touchThreshold = touchCount;

            imageTimer.Interval = interval > 0 ? interval : 1000;
            imageTimer.Tick += ImageTimer_Tick;

            this.FormClosed += PlayForm_FormClosed;
            this.Load += PlayForm_Load;

            this.FormBorderStyle = FormBorderStyle.None;
            this.pictureBox1.MouseClick += PictureBox1_MouseClick;
        }

        private void PictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            // 우측 상단 100x100 영역만 터치 인식
            int regionSize = 100;
            bool isInTargetArea = e.X >= pictureBox1.Width - regionSize && e.Y <= regionSize;

            if (!isInTargetArea)
                return;

            if (clickCount == 0)
            {
                firstClickTime = DateTime.Now;
                clickCount = 1;
            }
            else
            {
                if ((DateTime.Now - firstClickTime).TotalSeconds < 5)
                {
                    clickCount++;
                    if (clickCount >= this.touchThreshold)
                    {
                        this.Close();
                    }
                }
                else
                {
                    clickCount = 1;
                    firstClickTime = DateTime.Now;
                }
            }
        }

        private void PlayForm_Load(object sender, EventArgs e)
        {
            if (imagePaths != null && imagePaths.Count > 0)
            {
                ShowImage(imagePaths[currentImageIndex]);
                imageTimer.Start();
            }
            else
            {
                MessageBox.Show("No images to display.");
                this.Close();
            }
        }

        private void ImageTimer_Tick(object sender, EventArgs e)
        {
            currentImageIndex = (currentImageIndex + 1) % imagePaths.Count;
            ShowImage(imagePaths[currentImageIndex]);
        }

        private void ShowImage(string imagePath)
        {
            try
            {
                Image image = Image.FromFile(imagePath);
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => 
                    {
                        this.ClientSize = image.Size;
                        pictureBox1.Image = image;
                    }));
                }
                else
                {
                    this.ClientSize = image.Size;
                    pictureBox1.Image = image;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading image: " + ex.Message);
            }
        }

        private void PlayForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            imageTimer.Stop();
            parentForm.Show();
        }
    }
}
