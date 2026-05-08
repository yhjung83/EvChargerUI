using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using EvChargerUI.ViewModels.Commons;

namespace EvChargerUI.ViewModels
{
    public class ChargeInputPopupViewModel : BaseViewModel
    {
        private static int _fullAmount = 20000;
        private int _input;
        private bool _isCustomMode;
        private ICommand _confirmInnerCommand;
        private ICommand _confirmCommand;
        private float _unitCost;


        public int Input
        {
            get => _input;
            set
            {
                _input = value;
                OnPropertyChanged(nameof(Input));
                OnPropertyChanged(nameof(CanConfirm));
                CommandManager.InvalidateRequerySuggested();
            }

        }

        public bool CanConfirm => Input >= 2000;

        /// <summary>
        /// 비회원 단가(원/kWh 등). GetNonMemberUnitCost 결과를 주입받아 표시용으로 사용.
        /// </summary>
        public float UnitCost
        {
            get => _unitCost;
            set
            {
                _unitCost = value;
                OnPropertyChanged(nameof(UnitCost));
                OnPropertyChanged(nameof(UnitCostText));
            }
        }

        public string UnitCostText
        {
            get
            {
                // 소수 첫째자리까지만 표시하되, .0 은 제거 (예: 374.0 -> 374원, 347.2 -> 347.2원)
                double v = Math.Round(UnitCost, 1, MidpointRounding.AwayFromZero);
                if (Math.Abs(v - Math.Round(v)) < 0.00001d)
                    return $"{v:N0}원";

                return $"{v:N1}원";
            }
        }

        public bool IsCustomMode
        {
            get => _isCustomMode;
            set
            {
                _isCustomMode = value;
                OnPropertyChanged(nameof(IsCustomMode));
            }
        }
        public ICommand NumberCommand { get; }
        public ICommand BackspaceCommand { get; }
        public ICommand ToggleModeCommand { get; }
        public ICommand AddChargeAmountCommand { get; }
        public ICommand ResetChargeAmountCommand { get;  }
        public ICommand SetFullChargeAmountCommand { get; }
        public ICommand CancelCommand { get; set; }
        public ICommand ConfirmCommand
        {
            get => _confirmCommand;
            set
            {
                // 외부에서 주입되는 실제 Confirm 커맨드(다음 단계로 넘어가는 로직)
                _confirmInnerCommand = value;

                // 최소 금액 미만이면 실행 불가하도록 래핑
                _confirmCommand = new RelayCommand(ExecuteConfirm, CanExecuteConfirm);
                OnPropertyChanged(nameof(ConfirmCommand));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ChargeInputPopupViewModel()
        {
            _input = 2000;
            _isCustomMode = false;

            NumberCommand = new RelayCommand(ClickNumber);
            BackspaceCommand = new RelayCommand(ClickBackspace);
            ToggleModeCommand = new RelayCommand(ToggleMode);
            AddChargeAmountCommand = new RelayCommand(AddChargeAmmount);
            ResetChargeAmountCommand = new RelayCommand(ResetChargeAmount);
            SetFullChargeAmountCommand = new RelayCommand(SetFullChargeAmount);
        }

        private bool CanExecuteConfirm(object param)
        {
            if (!CanConfirm) return false;
            return _confirmInnerCommand != null && _confirmInnerCommand.CanExecute(param);
        }

        private void ExecuteConfirm(object param)
        {
            if (!CanExecuteConfirm(param)) return;
            _confirmInnerCommand.Execute(param);
        }

        private void ClickNumber(object param)
        {
            int temp = _input * 10 + Int32.Parse(param.ToString());

            if (temp <= _fullAmount )
            {
                Input = temp;
            }
            else
            {
                Input = _fullAmount;
            }

        }

        private void ClickBackspace(object param)
        {
            Input = _input / 10;

        }

        private void ToggleMode(object param)
        {
            IsCustomMode = !IsCustomMode;
        }

        private void AddChargeAmmount(object param)
        {
            int temp = _input + Int32.Parse(param.ToString());

            if (temp <= _fullAmount)
            {
                Input = temp;
            }
            else
            {
                Input = _fullAmount;
            }
        }
        private void ResetChargeAmount(object param)
        {
            Input = 2000;
        }
        private void SetFullChargeAmount(object param)
        {
            Input = _fullAmount;
        }

        

    }
}
