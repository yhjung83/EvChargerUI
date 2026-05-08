using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EvChargerUI.Views.SingleChannel
{
    /// <summary>
    /// ChaeviReadyToChargeUDLRView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ChaeviReadyToChargeUDLRView : UserControl
    {
        public ChaeviReadyToChargeUDLRView()
        {
            InitializeComponent();
        }

        private void UpButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.ChargerViewModel viewModel)
            {
                viewModel.MoveMotorUp();
            }
        }

        private void LeftButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.ChargerViewModel viewModel)
            {
                viewModel.MoveMotorLeft();
            }
        }

        private void RightButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.ChargerViewModel viewModel)
            {
                viewModel.MoveMotorRight();
            }
        }

        private void DownButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.ChargerViewModel viewModel)
            {
                viewModel.MoveMotorDown();
            }
        }

        private void Button_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.ChargerViewModel viewModel)
            {
                viewModel.MoveMotorEnd();
            }
        }
    }
}

