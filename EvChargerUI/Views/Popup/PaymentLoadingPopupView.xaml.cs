using System;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WpfAnimatedGif;

namespace EvChargerUI.Views.Popup
{
    /// <summary>
    /// PaymentLoadingPopupView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PaymentLoadingPopupView : UserControl
    {
        public PaymentLoadingPopupView()
        {
            InitializeComponent();

            var imagePath = "/Images/loading.gif";
            var uri = new Uri(imagePath, UriKind.Relative);
            var image = new BitmapImage(uri);
            ImageBehavior.SetAnimatedSource(LoadingImage, image);
        }
    }
}

