using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EvChargerUI.ViewModels;

namespace EvChargerUI.Views.Popup
{
    /// <summary>
    /// InputPhoneNumberPopupView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class InputPasswordPopupView : UserControl
    {
        public InputPasswordPopupView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Focus();
            Keyboard.Focus(this);
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            InputPasswordPopupViewModel vm = DataContext as InputPasswordPopupViewModel;
            if (vm == null)
            {
                return;
            }

            string number = GetNumberFromKey(e.Key);
            if (number != null)
            {
                if (vm.NumberCommand != null && vm.NumberCommand.CanExecute(number))
                {
                    vm.NumberCommand.Execute(number);
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Back)
            {
                if (vm.BackspaceCommand != null && vm.BackspaceCommand.CanExecute(null))
                {
                    vm.BackspaceCommand.Execute(null);
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (vm.CancelCommand != null && vm.CancelCommand.CanExecute(null))
                {
                    vm.CancelCommand.Execute(null);
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                if (vm.CanConfirm && vm.ConfirmCommand != null && vm.ConfirmCommand.CanExecute(vm.Input))
                {
                    vm.ConfirmCommand.Execute(vm.Input);
                }

                e.Handled = true;
            }
        }

        private static string GetNumberFromKey(Key key)
        {
            switch (key)
            {
                case Key.D0:
                case Key.NumPad0:
                    return "0";
                case Key.D1:
                case Key.NumPad1:
                    return "1";
                case Key.D2:
                case Key.NumPad2:
                    return "2";
                case Key.D3:
                case Key.NumPad3:
                    return "3";
                case Key.D4:
                case Key.NumPad4:
                    return "4";
                case Key.D5:
                case Key.NumPad5:
                    return "5";
                case Key.D6:
                case Key.NumPad6:
                    return "6";
                case Key.D7:
                case Key.NumPad7:
                    return "7";
                case Key.D8:
                case Key.NumPad8:
                    return "8";
                case Key.D9:
                case Key.NumPad9:
                    return "9";
                default:
                    return null;
            }
        }
    }
}
