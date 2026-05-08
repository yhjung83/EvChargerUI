using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EvChargerUI.ViewModels;

namespace EvChargerUI.Views.Popup
{
    /// <summary>
    /// WrongPasswordPopupView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WrongPasswordPopupView : UserControl
    {
        public WrongPasswordPopupView()
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
            if (e.Key != Key.Enter && e.Key != Key.Return)
            {
                return;
            }

            MainViewModel vm = DataContext as MainViewModel;
            if (vm != null && vm.ReopenPasswordInputPopupCommand != null && vm.ReopenPasswordInputPopupCommand.CanExecute(null))
            {
                vm.ReopenPasswordInputPopupCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}

