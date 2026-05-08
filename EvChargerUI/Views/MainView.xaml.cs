using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using EvChargerUI.ViewModels;

namespace EvChargerUI.Views
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainView : Window
    {
        public MainView()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();

            // 화면 크기 얻기
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            var screenWidth = SystemParameters.PrimaryScreenWidth;


            if(screenWidth >= screenHeight)
            {
                this.WindowState = WindowState.Maximized;
            }
            else
            {
                this.WindowState = WindowState.Normal;
                this.Width = screenWidth;
                this.Height = screenHeight / 2;
                this.Left = 0;
                this.Top = screenHeight / 2;

            }


        }
    }
}
