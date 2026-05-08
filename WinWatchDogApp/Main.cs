using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinWatchDogApp
{
    public partial class Watchdog : Form
    {
        private List<ProcessControl> processControls = new List<ProcessControl>();
        private ConfigManager configManager = new ConfigManager("WatchDog.ini");
        private ProcessMonitor processMonitor;
        private StartupManager startupManager = new StartupManager();
        private Queue<DateTime> clickTimes = new Queue<DateTime>();
        private CheckBox checkBox_AutoStart;

        public Watchdog(bool startMinimized) : this()
        {
            if (startMinimized)
            {
                this.WindowState = FormWindowState.Minimized;
            }
        }

        public Watchdog()
        {
            InitializeComponent();
            InitializeProcessControls(2);
            InitializeAutoStartCheckBox();
            LoadSettings();
            processMonitor = new ProcessMonitor(processControls);
            button_Save.Click += new EventHandler(button_Save_Click);
            label_Notice.Click += new EventHandler(label_Notice_Click);
            this.FormClosing += new FormClosingEventHandler(Watchdog_FormClosing);
        }

        private void InitializeAutoStartCheckBox()
        {
            checkBox_AutoStart = new CheckBox();
            checkBox_AutoStart.Text = "Start with Windows";
            checkBox_AutoStart.AutoSize = true;
            checkBox_AutoStart.Enabled = false;
            // Place it at a specific location
            // checkBox_AutoStart.Location = new Point(10, 280); 
            flowLayoutPanel.Controls.Add(checkBox_AutoStart);
            checkBox_AutoStart.CheckedChanged += new EventHandler(checkBox_AutoStart_CheckedChanged);
        }

        private void checkBox_AutoStart_CheckedChanged(object sender, EventArgs e)
        {
            bool isChecked = checkBox_AutoStart.Checked;
            configManager.SaveAutoStart(isChecked);
            if (isChecked)
            {
                startupManager.AddToStartup();
            }
            else
            {
                startupManager.RemoveFromStartup();
            }
        }
        
        private void Watchdog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
            }
        }

        private void InitializeProcessControls(int count)
        {
            for (int i = 0; i < count; i++)
            {
                ProcessControl pc = new ProcessControl();
                pc.Name = "processControl" + (i + 1);
                pc.button_Search.Click += new EventHandler(button_Search_Click);
                pc.button_Switch.Click += new EventHandler(button_Switch_Click);
                processControls.Add(pc);
                flowLayoutPanel.Controls.Add(pc);
            }
        }

        private void button_Search_Click(object sender, EventArgs e)
        {
            Button searchButton = sender as Button;
            ProcessControl parentControl = searchButton.Parent as ProcessControl;
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Executable Files (*.exe)|*.exe";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                parentControl.label_Name.Text = ofd.FileName;
            }
        }

        private void button_Switch_Click(object sender, EventArgs e)
        {
            Button switchButton = sender as Button;
            if (switchButton.Text == "OFF")
            {
                switchButton.Text = "ON";
                switchButton.BackColor = Color.Green;
            }
            else
            {
                switchButton.Text = "OFF";
                switchButton.BackColor = SystemColors.Control;
            }
        }

        private void button_Save_Click(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void LoadSettings()
        {
            configManager.Load(processControls);
            label_Notice.Text = configManager.LoadNotice();

            // Get the desired auto-start state from the INI file.
            bool shouldBeInStartup = configManager.LoadAutoStart();

            // Set the checkbox UI to reflect the desired state.
            checkBox_AutoStart.Checked = shouldBeInStartup;

            // Enforce the state from the config file.
            // If auto-start is enabled, this will ensure the registry has the correct path for the current executable location.
            // If disabled, this will ensure it's removed from startup.
            if (shouldBeInStartup)
            {
                startupManager.AddToStartup();
            }
            else
            {
                startupManager.RemoveFromStartup();
            }
        }

        private void SaveSettings()
        {
            configManager.Save(processControls);
            configManager.SaveNotice(label_Notice.Text);
            configManager.SaveAutoStart(checkBox_AutoStart.Checked);
            MessageBox.Show("Settings saved successfully!");
        }

        private void label_Notice_Click(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            clickTimes.Enqueue(now);

            while (clickTimes.Count > 0 && (now - clickTimes.Peek()).TotalSeconds > 5)
            {
                clickTimes.Dequeue();
            }

            if (clickTimes.Count >= 5)
            {
                Application.Exit();
            }
        }
    }

    public class ConfigManager
    {
        private string filePath;

        public ConfigManager(string filePath)
        {
            this.filePath = Path.Combine(Application.StartupPath, filePath);
        }

        private string GetRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath) || string.IsNullOrEmpty(toPath))
                return toPath;

            try
            {
                Uri fromUri = new Uri(fromPath + Path.DirectorySeparatorChar);
                Uri toUri = new Uri(toPath);

                if (fromUri.Scheme != toUri.Scheme)
                {
                    return toPath;
                }

                Uri relativeUri = fromUri.MakeRelativeUri(toUri);
                string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

                return relativePath.Replace('/', Path.DirectorySeparatorChar);
            }
            catch
            {
                return toPath;
            }
        }

        private string GetAbsolutePath(string basePath, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return relativePath;

            if (Path.IsPathRooted(relativePath))
            {
                return relativePath;
            }

            try
            {
                string combinedPath = Path.Combine(basePath, relativePath);
                return Path.GetFullPath(combinedPath);
            }
            catch
            {
                return relativePath;
            }
        }


        private Dictionary<string, Dictionary<string, string>> ParseIni()
        {
            var iniData = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(filePath)) return iniData;

            string currentSection = null;
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    if (!iniData.ContainsKey(currentSection))
                    {
                        iniData[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                }
                else if (currentSection != null && trimmedLine.Contains("="))
                {
                    var parts = trimmedLine.Split(new[] { '=' }, 2);
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    iniData[currentSection][key] = value;
                }
            }

            return iniData;
        }

        private void WriteIni(Dictionary<string, Dictionary<string, string>> iniData)
        {
            var lines = new List<string>();
            foreach (var section in iniData)
            {
                lines.Add($"[{section.Key}]");
                foreach (var entry in section.Value)
                {
                    lines.Add($"{entry.Key}={entry.Value}");
                }
                lines.Add("");
            }
            File.WriteAllLines(filePath, lines, Encoding.UTF8);
        }

        public void Save(List<ProcessControl> processControls)
        {
            var iniData = ParseIni();

            for (int i = 0; i < processControls.Count; i++)
            {
                string sectionName = "Process" + (i + 1);
                if (!iniData.ContainsKey(sectionName))
                {
                    iniData[sectionName] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                string relativePath = GetRelativePath(Application.StartupPath, processControls[i].label_Name.Text);
                iniData[sectionName]["Path"] = relativePath;
                iniData[sectionName]["Timer"] = processControls[i].textBox_Timer.Text;
                iniData[sectionName]["Enabled"] = (processControls[i].button_Switch.Text == "ON").ToString();
            }

            WriteIni(iniData);
        }

        public void Load(List<ProcessControl> processControls)
        {
            var iniData = ParseIni();

            for (int i = 0; i < processControls.Count; i++)
            {
                string sectionName = "Process" + (i + 1);
                if (iniData.TryGetValue(sectionName, out var section))
                {
                    section.TryGetValue("Path", out var path);
                    section.TryGetValue("Timer", out var timer);
                    section.TryGetValue("Enabled", out var enabled);
                    
                    string absolutePath = GetAbsolutePath(Application.StartupPath, path);
                    processControls[i].label_Name.Text = absolutePath ?? "Process Name";
                    processControls[i].textBox_Timer.Text = timer ?? "60";

                    if (bool.TryParse(enabled, out var isEnabled) && isEnabled)
                    {
                        processControls[i].button_Switch.Text = "ON";
                        processControls[i].button_Switch.BackColor = Color.Green;
                    }
                    else
                    {
                        processControls[i].button_Switch.Text = "OFF";
                        processControls[i].button_Switch.BackColor = SystemColors.Control;
                    }
                }
            }
        }

        public void SaveNotice(string notice)
        {
            var iniData = ParseIni();
            if (!iniData.ContainsKey("Notice"))
            {
                iniData["Notice"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            iniData["Notice"]["Content"] = notice;
            WriteIni(iniData);
        }

        public string LoadNotice()
        {
            var iniData = ParseIni();
            if (iniData.TryGetValue("Notice", out var section) && section.TryGetValue("Content", out var content))
            {
                return content;
            }
            return "Notice content goes here.";
        }

        public void SaveAutoStart(bool isEnabled)
        {
            var iniData = ParseIni();
            if (!iniData.ContainsKey("Setting"))
            {
                iniData["Setting"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            iniData["Setting"]["AutoStart"] = isEnabled.ToString();
            WriteIni(iniData);
        }

        public bool LoadAutoStart()
        {
            var iniData = ParseIni();
            if (iniData.TryGetValue("Setting", out var section) && section.TryGetValue("AutoStart", out var autoStartValue))
            {
                if (bool.TryParse(autoStartValue, out var isEnabled))
                {
                    return isEnabled;
                }
            }
            return false;
        }
    }

    public class ProcessMonitor
    {
        private List<ProcessControl> processControls;
        private Timer monitorTimer;
        private Dictionary<ProcessControl, int> countdowns = new Dictionary<ProcessControl, int>();
        private Dictionary<ProcessControl, int> originalTimers = new Dictionary<ProcessControl, int>();

        public ProcessMonitor(List<ProcessControl> processControls)
        {
            this.processControls = processControls;
            monitorTimer = new Timer();
            monitorTimer.Interval = 1000; // 1 second
            monitorTimer.Tick += new EventHandler(MonitorTimer_Tick);
            monitorTimer.Start();
        }

        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            foreach (ProcessControl pc in processControls)
            {
                if (pc.button_Switch.Text == "ON")
                {
                    string processName = Path.GetFileNameWithoutExtension(pc.label_Name.Text);
                    if (!IsProcessRunning(processName))
                    {
                        if (!countdowns.ContainsKey(pc))
                        {
                            int timerValue;
                            if (int.TryParse(pc.textBox_Timer.Text, out timerValue))
                            {
                                countdowns[pc] = timerValue;
                                originalTimers[pc] = timerValue;
                            }
                        }
                        else
                        {
                            countdowns[pc]--;
                            pc.textBox_Timer.Text = countdowns[pc].ToString();

                            if (countdowns[pc] <= 0)
                            {
                                countdowns[pc] = originalTimers[pc];
                                pc.textBox_Timer.Text = originalTimers[pc].ToString();

                                RestartProcess(pc.label_Name.Text);
                                //countdowns.Remove(pc);
                                //originalTimers.Remove(pc);
                                
                                Console.WriteLine($"Process restarted: {pc.label_Name.Text}");
                            }
                        }
                    }
                    else
                    {
                        if (countdowns.ContainsKey(pc))
                        {
                            pc.textBox_Timer.Text = originalTimers[pc].ToString();
                            countdowns.Remove(pc);
                            originalTimers.Remove(pc);
                        }
                    }
                }
                else
                {
                    if (countdowns.ContainsKey(pc))
                    {
                        pc.textBox_Timer.Text = originalTimers[pc].ToString();
                        countdowns.Remove(pc);
                        originalTimers.Remove(pc);
                    }
                }
            }
        }

        private bool IsProcessRunning(string processName)
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }

        private void RestartProcess(string path)
        {
            string directory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);

            if (!File.Exists(path))
            {
                if (string.Equals(fileName, "EvChargerUI.exe", StringComparison.OrdinalIgnoreCase))
                {
                    string backupPath = Path.Combine(directory, "EvChargerUI_.exe");
                    if (File.Exists(backupPath))
                    {
                        try
                        {
                            File.Copy(backupPath, path, true);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to copy backup file: {ex.Message}");
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            try
            {
                string logFilePath = Path.Combine(Application.StartupPath, "WatchDog.log");
                string message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Process restarted: {path}{Environment.NewLine}";
                File.AppendAllText(logFilePath, message, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to write to log file: {ex.Message}");
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = path;
                startInfo.WorkingDirectory = directory;
                startInfo.UseShellExecute = true; // ShellExecuteEx를 사용하여 UAC 권한 상승 허용
                Process.Start(startInfo);
            }
            catch (Win32Exception ex)
            {
                Debug.WriteLine($"Failed to start process (Win32: {ex.NativeErrorCode}): {ex.Message}");
            }
        }
    }
}

