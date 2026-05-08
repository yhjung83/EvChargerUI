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
    public class InputReservationNumberPopupViewModel : BaseViewModel
    {

        private string _input;

        public InputReservationNumberPopupViewModel()
        {
            _input = "";

            NumberCommand = new RelayCommand(ClickNumber);
            BackspaceCommand = new RelayCommand(ClickBackspace);
        }

        public string Input => _input;

        public string ReservationNumber
        {
            get
            {
                string result = "";

                for (int i = 0; i < _input.Length; i++)
                {
                    if (i > 0) result += "  ";
                    result += _input[i];
                }

                return result;
            }
            
        } 

        public string PlaceHolder
        {
            get
            {
                string result = "";
                for (int i = 0; i < 4 - _input.Length; i++)
                {
                    if (_input.Length + i > 0) result += "   ";
                    result += "\u25cf";

                }

                return result;
            }
        }

        public bool CanConfirm
        {
            get
            {
                return _input.Length == 4;
            }
        }

        public ICommand NumberCommand { get; }
        public ICommand BackspaceCommand { get; }

        public ICommand CancelCommand { get; set; }
        public ICommand ConfirmCommand { get; set; }

        private void ClickNumber(object param)
        {
            if (_input.Length < 4)
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
            OnPropertyChanged(nameof(ReservationNumber));
            OnPropertyChanged(nameof(PlaceHolder));
            OnPropertyChanged(nameof(CanConfirm));
        }

    }
}
