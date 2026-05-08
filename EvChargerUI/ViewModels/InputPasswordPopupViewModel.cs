using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Markup.Localizer;
using EvChargerUI.Commons.Enum;
using EvChargerUI.ViewModels.Commons;

namespace EvChargerUI.ViewModels
{
    public class InputPasswordPopupViewModel : BaseViewModel
    {

        private string _input;

        public InputPasswordPopupViewModel()
        {
            _input = "";

            NumberCommand = new RelayCommand(ClickNumber);
            BackspaceCommand = new RelayCommand(ClickBackspace);
        }

        public string Input => _input;

        public string Password
        {
            get
            {
                string result = null;

                for (int i = 0; i < _input.Length; i++)
                {
                    result += "*";
                }
                
                return result;
            }
        }
        public string PlaceHolder
        {
            get
            {
                string result = "";
                for (int i = 0; i < 6 - _input.Length; i++)
                {
                    result += "*";
                }

                return result;
            }
        }

        public bool CanConfirm => _input.Length == 6;

        public ICommand NumberCommand { get; }
        public ICommand BackspaceCommand { get; }

        public ICommand CancelCommand { get; set; }
        public ICommand ConfirmCommand { get; set; }

        private void ClickNumber(object param)
        {
            if (_input.Length < 6)
            {
                _input += param.ToString();
            }

            NotifyChanged();
        }

        private void ClickBackspace(object param)
        {
            if (_input.Length > 0)
            {
                _input = _input.Substring(0, _input.Length - 1);
            }

            NotifyChanged();
        }


        private void NotifyChanged()
        {
            OnPropertyChanged(nameof(Input));
            OnPropertyChanged(nameof(Password));
            OnPropertyChanged(nameof(PlaceHolder));
            OnPropertyChanged(nameof(CanConfirm));
        }

    }
}
