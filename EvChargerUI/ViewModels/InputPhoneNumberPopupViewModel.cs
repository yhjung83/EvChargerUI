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
    public class InputPhoneNumberPopupViewModel : BaseViewModel
    {
        private static readonly string initNumber = "010";

        private string _input;

        public InputPhoneNumberPopupViewModel(string title1, string title2)
        {
            _input = initNumber;
            Title1 = title1;
            Title2 = title2;

            NumberCommand = new RelayCommand(ClickNumber);
            BackspaceCommand = new RelayCommand(ClickBackspace);
            ClearCommand = new RelayCommand(ClearPhoneNumber);
        }


        public string Title1 { get;  }
        public string Title2 { get; }

        public bool IsClearable => _input.Length > 3;

        public string Input => _input;

        public string PhoneNumber
        {
            get
            {
                string result = null;
                switch (_input.Length)
                {
                    case 2:
                        result = _input;
                        break;
                    case 3:
                        result = _input + "-";
                        break;
                    case 4:
                    case 5:
                        result = _input.Substring(0, 3) + "-" + _input.Substring(3, _input.Length - 3);
                        break;
                    case 6:
                        result = _input.Substring(0, 3) + "-" + _input.Substring(3, _input.Length - 3) + "-";
                        break;
                    case 7:
                    case 8:
                    case 9:
                    case 10:
                        result = _input.Substring(0, 3) + "-" + _input.Substring(3, 3) + "-" +
                                 _input.Substring(6, _input.Length - 6);
                        break;
                    case 11:
                        result = _input.Substring(0, 3) + "-" + _input.Substring(3, 4) + "-" +
                                 _input.Substring(7, _input.Length - 7);
                        break;

                }

                return result;
            }
        }
        public string PlaceHolder
        {
            get
            {
                string result = "";
                switch (_input.Length)
                {
                    case 2:
                        result = "0-0000-0000";
                        break;
                    case 3:
                        result = "0000-0000";
                        break;
                    case 4:
                        result = "    -0000";
                        break;
                    case 5:
                        result = "  -0000";
                        break;

                    case 6:
                        result = "0000";
                        break;
                    case 7:
                        result = "        ";
                        break;
                    case 8:
                        result = "      ";
                        break;
                    case 9:
                        result = "    ";
                        break;
                    case 10:
                        result = "  ";
                        break;
                    case 11:
                        result = "";

                        break;

                }
 
                return result;
            }
        }

        public bool CanConfirm
        {
            get
            {
                // 010 이후 8자리 = 총 11자리
                return _input.Length == 11;
            }
        }

        public ICommand NumberCommand { get; }
        public ICommand BackspaceCommand { get; }
        public ICommand ClearCommand { get; }

        public ICommand CancelCommand { get; set; }
        public ICommand ConfirmCommand { get; set; }

        private void ClickNumber(object param)
        {
            if (_input.Length < 11)
            {
                _input += param.ToString();
            }

            NotifyChanged();
        }

        private void ClickBackspace(object param)
        {
            if (_input.Length > 2)
            {
                _input = _input.Substring(0, _input.Length - 1);
            }

            NotifyChanged();
        }

        private void ClearPhoneNumber(object param)
        {
            _input = initNumber;
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            OnPropertyChanged(nameof(Input));
            OnPropertyChanged(nameof(PhoneNumber));
            OnPropertyChanged(nameof(PlaceHolder));
            OnPropertyChanged(nameof(IsClearable));
            OnPropertyChanged(nameof(CanConfirm));
        }

    }
}
