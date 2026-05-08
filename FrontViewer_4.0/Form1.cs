using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace FrontViewer
{
    public partial class Main_Form : Form
    {
        private Timer updateTimer;
        public Main_Form()
        {
            InitializeComponent();
            LoadSetting();
            
            // Initialize and start the 5-second polling timer
            updateTimer = new Timer();
            updateTimer.Interval = 5000; // 5 seconds
            updateTimer.Tick += new EventHandler(UpdateTimer_Tick);
            updateTimer.Start();

            this.Shown += new System.EventHandler(this.Main_Form_Shown);
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            string updateFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpdateFrontFile");
            if (Directory.Exists(updateFolderPath))
            {
                updateTimer.Stop(); // Stop polling during the update process.

                PlayForm openPlayForm = Application.OpenForms.OfType<PlayForm>().FirstOrDefault();

                if (openPlayForm != null)
                {
                    // A slideshow is running. We must wait for it to close before updating files.
                    // We create a one-time event handler for when the form is closed.
                    FormClosedEventHandler formClosedHandler = null;
                    formClosedHandler = (s, args) =>
                    {
                        // Now that the form is closed, we can safely update the files.
                        UpdateImagesFromFolder(updateFolderPath);

                        // Re-start the timer.
                        if (!updateTimer.Enabled)
                        {
                            updateTimer.Start();
                        }

                        // Detach the event handler to prevent memory leaks.
                        if (s is Form closedForm)
                        {
                            closedForm.FormClosed -= formClosedHandler;
                        }
                    };

                    openPlayForm.FormClosed += formClosedHandler;
                    openPlayForm.Close();
                }
                else
                {
                    // No slideshow is running, so we can update immediately.
                    UpdateImagesFromFolder(updateFolderPath);
                    if (!updateTimer.Enabled)
                    {
                        updateTimer.Start(); // Re-start the timer.
                    }
                }
            }
        }

        private void Main_Form_Shown(object sender, EventArgs e)
        {
            if (checkBox_View.Checked)
            {
                button_Play.PerformClick();
            }
        }

        private void LoadSetting()
        {
            string Version = "Ver: "; 
            label_Ver.Text = Version + "1.0.1" + "   " + "Frame 4.0";

            string basePath = AppDomain.CurrentDomain.BaseDirectory + "FViewerSetting.ini"; 
            //string settingPath = Path.Combine(basePath, "Set", "FViewerSetting.ini");
            IniFile ini = new IniFile(basePath);

            // UI에 적용
            // SET_VIEWER_VISIBLE 값은 설정화면 유무 
            bool setViewerVisible = ini.Read("SET", "SET_VIEWER_VISIBLE") == "TRUE";
            checkBox_View.Checked = setViewerVisible;
            checkBox_View_CheckedChanged(this, EventArgs.Empty);
            // SET_SUB_MONITER는 서브모니터 유무
            bool setSubMoniter = ini.Read("SET", "SET_SUB_MONITER") == "TRUE";
            checkBox_SubMoniter.Checked = setSubMoniter;
            checkBox_SubMoniter_CheckedChanged(this, EventArgs.Empty);
            // SELECTED_PATH 는 File PATH
            string selectedPath = ini.Read("SET", "SELECTED_PATH");
            if (string.IsNullOrEmpty(selectedPath) || selectedPath.Equals("NONE"))
            {
                string projectRoot = Path.GetFullPath(Path.Combine(basePath, "..", "..", ".."));
                selectedPath = Path.Combine(projectRoot, "ImageData", "Deafault");
            }
            label_SelectedPath.Text = selectedPath;
            LoadImageFiles(selectedPath);
            // SET_SWAP_TIME는 textBox_SwapTimer에 표시
            textBox_SwapTimer.Text = ini.Read("SET", "SET_SWAP_TIMER");

            // SET_TOUCH_TIMER는 textBox_TouchCount에 표시
            string touchCount = ini.Read("SET", "SET_TOUCH_COUNT");
            if (string.IsNullOrEmpty(touchCount))
            {
                touchCount = "10";
            }
            textBox_TouchCount.Text = touchCount;
        }

        private void LoadImageFiles(string path)
        {
            listView_List.Items.Clear();
            listView_List.Columns.Clear();
            listView_List.Columns.Add("File Name", 200);
            listView_List.Columns.Add("Resolution", 100);
            listView_List.Columns.Add("Modified Date", 150);

            if (!Directory.Exists(path))
            {
                return;
            }

            var imageExtensions = new string[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };

            var files = Directory.EnumerateFiles(path)
                                 .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()));

            foreach (var file in files)
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(file);
                    using (var img = Image.FromFile(file))
                    {
                        ListViewItem item = new ListViewItem(fileInfo.Name);
                        item.SubItems.Add(string.Format("{0} x {1}", img.Width, img.Height));
                        item.SubItems.Add(fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
                        listView_List.Items.Add(item);
                    }
                }
                catch
                {
                    FileInfo fileInfo = new FileInfo(file);
                    ListViewItem item = new ListViewItem(fileInfo.Name);
                    item.SubItems.Add("Invalid Image");
                    item.SubItems.Add(fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
                    listView_List.Items.Add(item);
                }
            }
        }

        public void SaveSetting()
        {
            //string basePath = AppDomain.CurrentDomain.BaseDirectory;
            //string settingPath = Path.Combine(basePath, "Set", "FViewerSetting.ini");
            //IniFile ini = new IniFile(settingPath);

            string basePath = AppDomain.CurrentDomain.BaseDirectory + "FViewerSetting.ini";
            //string settingPath = Path.Combine(basePath, "Set", "FViewerSetting.ini");
            IniFile ini = new IniFile(basePath);

            ini.Write("SET", "SET_VIEWER_VISIBLE", checkBox_View.Checked ? "TRUE" : "FALSE");
            ini.Write("SET", "SET_SUB_MONITER", checkBox_SubMoniter.Checked ? "TRUE" : "FALSE");
            ini.Write("SET", "SELECTED_PATH", label_SelectedPath.Text);
            ini.Write("SET", "SET_SWAP_TIMER", textBox_SwapTimer.Text);
            ini.Write("SET", "SET_TOUCH_COUNT", textBox_TouchCount.Text);
        }

        private void button_SAVE_Click(object sender, EventArgs e)
        {
            SaveSetting();
            MessageBox.Show("저장되었습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void button_Search_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                string selectedPath = folderBrowserDialog.SelectedPath;
                label_SelectedPath.Text = selectedPath;
                LoadImageFiles(selectedPath);
            }
        }

        private void button_Play_Click(object sender, EventArgs e)
        {
            List<string> imagePaths = new List<string>();
            string basePath = label_SelectedPath.Text;
            foreach (ListViewItem item in listView_List.Items)
            {
                imagePaths.Add(Path.Combine(basePath, item.Text));
            }

            int interval;
            int touchCount;
            if (int.TryParse(textBox_SwapTimer.Text, out interval) && int.TryParse(textBox_TouchCount.Text, out touchCount))
            {
                this.Hide();
                PlayForm playForm = new PlayForm(imagePaths, interval * 1000, this, touchCount);

                if (checkBox_SubMoniter.Checked)
                {
                    Screen[] screens = Screen.AllScreens;
                    if (screens.Length > 1)
                    {
                        Screen secondaryScreen = screens.FirstOrDefault(s => !s.Primary) ?? screens[1];
                        playForm.StartPosition = FormStartPosition.Manual;
                        playForm.Location = secondaryScreen.Bounds.Location;
                        //playForm.WindowState = FormWindowState.Maximized;
                    }
                }
                else
                {
                    playForm.StartPosition = FormStartPosition.Manual;
                    playForm.Location = new Point(0, 0);
                }

                playForm.Show();
            }
            else
            {
                MessageBox.Show("Please enter a valid number for the swap timer.");
            }
        }

        private void checkBox_View_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_View.Checked)
            {
                checkBox_View.BackColor = Color.LightGreen;
                checkBox_View.Text = "시작시 설정화면 OFF";
            }
            else
            {
                checkBox_View.BackColor = SystemColors.Control;
                checkBox_View.Text = "시작시 설정화면 ON";
            }
        }

        private void checkBox_SubMoniter_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_SubMoniter.Checked)
            {
                checkBox_SubMoniter.BackColor = Color.LightGreen;
                checkBox_SubMoniter.Text = "서브 모니터 사용 함";
            }
            else
            {
                checkBox_SubMoniter.BackColor = SystemColors.Control;
                checkBox_SubMoniter.Text = "서브 모니터 사용 안함";
            }
        }

        private void UpdateImagesFromFolder(string updateFolderPath)
        {
            try
            {
                // The calling method ensures PlayForm is closed.
                // Now, let's aggressively ask the GC to release underlying file handles.
                GC.Collect();
                GC.WaitForPendingFinalizers();

                string destinationPath = label_SelectedPath.Text;

                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                }

                // 1. Delete existing files in the destination path
                DirectoryInfo di = new DirectoryInfo(destinationPath);
                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }

                // 2. Copy new files from update folder
                DirectoryInfo sourceDi = new DirectoryInfo(updateFolderPath);
                foreach (FileInfo file in sourceDi.GetFiles())
                {
                    string temppath = Path.Combine(destinationPath, file.Name);
                    file.CopyTo(temppath, true);
                }

                // 3. Delete the update folder
                Directory.Delete(updateFolderPath, true);

                // 4. Refresh the list view
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => LoadImageFiles(destinationPath)));
                }
                else
                {
                    LoadImageFiles(destinationPath);
                }

                // 5. playform 재시작
                button_Play.PerformClick();

                // Log success message


                Console.WriteLine("Image files updated successfully.");
            }
            catch (Exception ex)
            {
                // Using Console.WriteLine to avoid halting the timer on repeated errors.
                Console.WriteLine("이미지 업데이트 중 오류가 발생했습니다: " + ex.Message);
            }
            
        }
    }

    internal class IniFile
    {
        private readonly string path;

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        public IniFile(string iniPath)
        {
            path = iniPath;
        }

        public void Write(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, path);
        }

        public string Read(string section, string key)
        {
            StringBuilder sb = new StringBuilder(255);
            GetPrivateProfileString(section, key, "", sb, 255, path);
            return sb.ToString();
        }
    }
}
