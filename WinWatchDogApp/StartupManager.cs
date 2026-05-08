using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WinWatchDogApp
{
    public class StartupManager
    {
        private const string AppName = "WinWatchDogApp";
        private const string RegistryPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

        public void AddToStartup()
        {
            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
                {
                    if (rk != null)
                    {
                        var currentValue = rk.GetValue(AppName) as string;
                        var newValue = $"\"{Application.ExecutablePath}\" /startup";
                        if (currentValue != newValue)
                        {
                            rk.SetValue(AppName, newValue);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding to startup: {ex.Message}", "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void RemoveFromStartup()
        {
            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
                {
                    if (rk != null && rk.GetValue(AppName) != null)
                    {
                        rk.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing from startup: {ex.Message}", "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public bool IsInStartup()
        {
            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                {
                    if (rk == null) return false;
                    return rk.GetValue(AppName) != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
