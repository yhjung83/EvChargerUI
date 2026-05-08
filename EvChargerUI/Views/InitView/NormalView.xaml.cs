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

namespace EvChargerUI.Views.InitView
{
    /// <summary>
    /// NormalView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class NormalView : UserControl
    {
        public NormalView()
        {
            InitializeComponent();
        }

        private void OnRootTapped(object sender, MouseButtonEventArgs e)
        {
            if (IsFromStartButton(e.OriginalSource as DependencyObject))
            {
                // 버튼 클릭은 버튼이 자체적으로 Command를 실행하므로 여기서는 무시 (중복 실행 방지)
                return;
            }

            ExecuteStartCommand();
        }

        private void OnRootTouched(object sender, TouchEventArgs e)
        {
            if (IsFromStartButton(e.OriginalSource as DependencyObject))
            {
                // 터치가 버튼 영역이면 버튼이 처리하도록 둠 (중복 실행 방지)
                return;
            }

            ExecuteStartCommand();
        }

        private bool IsFromStartButton(DependencyObject source)
        {
            if (source == null || StartButton == null) return false;

            DependencyObject current = source;
            while (current != null)
            {
                if (ReferenceEquals(current, StartButton)) return true;
                current = GetParent(current);
            }

            return false;
        }

        private static DependencyObject GetParent(DependencyObject current)
        {
            // Run/TextElement 등은 Visual이 아니므로 VisualTreeHelper.GetParent를 쓰면 예외가 발생할 수 있음
            if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
            {
                return VisualTreeHelper.GetParent(current);
            }

            // TextElement/Inline/Run 등
            if (current is FrameworkContentElement fce)
            {
                return fce.Parent;
            }

            if (current is ContentElement ce)
            {
                return ContentOperations.GetParent(ce) ?? LogicalTreeHelper.GetParent(ce);
            }

            return LogicalTreeHelper.GetParent(current);
        }

        private void ExecuteStartCommand()
        {
            ICommand command = StartButton?.Command;
            object parameter = StartButton?.CommandParameter;

            if (command == null) return;
            if (!command.CanExecute(parameter)) return;

            command.Execute(parameter);
        }
    }
}
