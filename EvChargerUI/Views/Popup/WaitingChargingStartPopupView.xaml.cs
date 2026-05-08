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
using WpfAnimatedGif;

namespace EvChargerUI.Views.Popup
{
    /// <summary>
    /// TagRFCardPopupView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WaitingChargingStartPopupView : UserControl
    {
        public WaitingChargingStartPopupView()
        {
            InitializeComponent();
            var imagePath = "/Images/loading.gif";
            var uri = new Uri(imagePath, UriKind.Relative);
            var image = new BitmapImage(uri);
            ImageBehavior.SetAnimatedSource(WaitingImage, image);
        }
    }
}
