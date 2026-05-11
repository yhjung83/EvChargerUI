using System;
using System.Threading;
using System.Windows.Forms;

namespace FrontViewer
{
    internal static class Program
    {
#if !DEBUG
        private static Mutex appMutex;
#endif

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
#if !DEBUG
            bool createdNew;
            appMutex = new Mutex(true, "FrontViewer_4.0_SingleInstance", out createdNew);

            if (!createdNew)
            {
                return;
            }
#endif

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Main_Form());
#if !DEBUG
            appMutex.ReleaseMutex();
#endif
        }
    }
}
