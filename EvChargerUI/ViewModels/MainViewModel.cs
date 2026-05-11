using EvChargerUI.Commons.Controls;
using EvChargerUI.Commons.Enum;
using EvChargerUI.Commons.Settings;
using EvChargerUI.Commons.Util;
using EvChargerUI.Models;
using EvChargerUI.Services;
using EvChargerUI.Services.FaultHandling;
using EvChargerUI.Services.DspControl;
using EvChargerUI.ViewModels.Commons;
using EvChargerUI.Views;
using EvChargerUI.Views.InitView;
using EvChargerUI.Views.Popup;
using Newtonsoft.Json.Serialization;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EvChargerUI.ViewModels
{

    public class MainViewModel : BaseViewModel
    {
        private UserControl _leftChargerView;
        private UserControl _rightChargerView;
        private UserControl _popupView;
        private UserControl _initView;

        private DispatcherTimer _returnToNormalViewTimer;
        private DispatcherTimer _popupTimer;

        private bool _isDimmed;
        private PopupSizeType _popupType;

        private bool _showInitView;

        private int _chargerViewColSpan;

        private BitmapImage _reportQrCode;
        private BitmapImage _searchStationQrCode;
        
        // 관리자가 점검중 모달을 띄운 상태인지 추적
        private bool _isMaintenancePopupByAdmin = false;

        public BitmapImage ReportQrCode
        {
            get { return _reportQrCode; }
            set
            {
                _reportQrCode = value;
                OnPropertyChanged(nameof(ReportQrCode));
            }
        }

        public BitmapImage SearchStationQrCode
        {
            get { return _searchStationQrCode; }
            set
            {
                _searchStationQrCode = value;
                OnPropertyChanged(nameof(SearchStationQrCode));
            }
        }

        private DispatcherTimer _maintenanceCheckTimer;
        private bool _errorPopupShown = false;
        private UserControl _errorPopupView;
        private Charger _charger;
        private FaultHandlingManager _faultHandlingManager;
        // 충전 대기(PlugConnector) 상태에서 오류가 발생했을 때 InitializeCharger 호출이 반복되지 않도록 1회 보장
        private bool _maintenanceErrorPlugConnectorResetDone = false;
        private bool _alarmRaisedByErrorPopup = false;
        private string _lastPopupAlarmCode = "0000";
        private bool _keepErrorPopupUntilFaultClears = false;
        private string _maintenancePopupTitle = "점검중";
        private string _maintenancePopupMessage = "충전기 점검중입니다.";
        private readonly FileLogger _logger = ((App)Application.Current).AppLogger;

        private string _chargerFirmwareVersion;
        public string ChargerFirmwareVersion
        {
            get => _chargerFirmwareVersion;
            set
            {
                _chargerFirmwareVersion = value;
                OnPropertyChanged(nameof(ChargerFirmwareVersion));
            }
        }

        private string _appVersion;
        public string AppVersion
        {
            get => _appVersion;
            set
            {
                _appVersion = value;
                OnPropertyChanged(nameof(AppVersion));
            }
        }

        private string _errorCodeText;
        public string ErrorCodeText
        {
            get => _errorCodeText;
            set
            {
                _errorCodeText = value;
                OnPropertyChanged(nameof(ErrorCodeText));
            }
        }

        public string MaintenancePopupTitle
        {
            get => _maintenancePopupTitle;
            set
            {
                _maintenancePopupTitle = value;
                OnPropertyChanged(nameof(MaintenancePopupTitle));
            }
        }

        public string MaintenancePopupMessage
        {
            get => _maintenancePopupMessage;
            set
            {
                _maintenancePopupMessage = value;
                OnPropertyChanged(nameof(MaintenancePopupMessage));
            }
        }

        public ICommand HiddenBtnDbClickCommand { get; }
        private DateTime _lastClickTime = DateTime.MinValue;
        public ICommand PasswordPopupViewConfirmCommand { get;}
        public ICommand PasswordPopupViewCancelCommand { get; }
        public ICommand ReopenPasswordInputPopupCommand { get; }
        public ICommand OpenPasswordPopupCommand { get; }

        public ICommand OpenPopupHelpChargingCapacityCommand { get; }
        public ICommand OpenPopupReportQrCodeCommand { get; }
        public ICommand OpenPopupSearchStationQrCodeCommand { get; }

        public ICommand CloseCommonPopupCommand { get;  }

        public ICommand StartCommand { get; }

        public MainViewModel()
        {
                        InitView = new NormalView();

            HiddenBtnDbClickCommand = new RelayCommand(DbClickHiddenBtn);
            PasswordPopupViewCancelCommand = new RelayCommand(ClosePasswordPopupView);
            PasswordPopupViewConfirmCommand = new RelayCommand(ConfirmPasswordPopupView);
            ReopenPasswordInputPopupCommand = new RelayCommand(ReopenPasswordInputPopup);
            OpenPasswordPopupCommand = new RelayCommand(OpenPasswordPopup);

            OpenPopupHelpChargingCapacityCommand = new RelayCommand(OpenPopupHelpChargingCapacity);
            OpenPopupReportQrCodeCommand = new RelayCommand(OpenPopupReportQrCode);
            OpenPopupSearchStationQrCodeCommand = new RelayCommand(OpenPopupSearchStationQrCode);
            CloseCommonPopupCommand = new RelayCommand(CloseCommonPopup);

            StartCommand = new RelayCommand(Start);

            // 어셈블리 버전 초기화
            AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            Charger charger = ((App) Application.Current).Charger;
            _charger = charger;
            _faultHandlingManager = new FaultHandlingManager(_charger);


            if (!string.IsNullOrEmpty(AppSettingsManager.ChargerSettings.RightChannelChargerId))
            {
                LeftChargerView = new Views.DualChannel.ChargerView();
                var leftViewModel = new LeftChargerViewModel(0, this, charger);
                LeftChargerView.DataContext = leftViewModel;

                RightChargerView = new Views.DualChannel.ChargerView();
                var rightViewModel = new RightChargerViewModel(1, this, charger);
                RightChargerView.DataContext = rightViewModel;
                
                // 양쪽 ViewModel을 서로 참조하게 설정
                leftViewModel.SetOtherChargerViewModel(rightViewModel);
                rightViewModel.SetOtherChargerViewModel(leftViewModel);
                
                ChargerViewColSpan = 1;
            }
            else
            {
                LeftChargerView = new Views.SingleChannel.ChargerView();
                LeftChargerView.DataContext = new SingleChargerViewModel(0, this, charger);

                RightChargerView = null;
                ChargerViewColSpan = 3;
            }

            charger.EmergencyRaised += (s, e) =>
            {
                // 비상정지 모달 표시 (점검중 모달 위에 표시됨)
                this.EmergencyStopPopup();
            };
            charger.EmergencyCleared += (s, e) =>
            {
                // 비상정지 모달만 닫기 (점검중 모달은 유지)
                if (this.PopupView is EmergencyStopPopupView)
                {
                    // 비상정지 모달 닫기
                    this.PopupView = null;
                    
                    // 관리자가 띄운 점검중 모달이 있으면 다시 표시
                    if (_isMaintenancePopupByAdmin)
                    {
                        this.ShowMaintenancePopup(true);
                    }
                    // DSP 연결 문제 및 네트워크 연결 문제로 인해 오류 발생 모달이 있어야 하는 경우
                    else
                    {
                        CheckMaintenanceStatus();
                        if (this.PopupView == null)
                        {
                            // 추가 팝업이 없으면 dimmed 해제
                            this.IsDimmed = false;
                        }
                    }
                }
            };
            
            charger.DspConnectionLost += (s, e) =>
            {
                // 관리자가 띄운 모달이 아니면 DSP 연결 끊김으로 모달 표시
                if (!_isMaintenancePopupByAdmin)
                {
                    CheckMaintenanceStatus();
                }
            };
            charger.DspConnectionRestored += (s, e) =>
            {
                // 관리자가 띄운 모달이 아니면 DSP 연결 복구로 모달 닫기
                if (!_isMaintenancePopupByAdmin)
                {
                    CheckMaintenanceStatus();
                }
                // 관리자가 띄운 모달이면 그대로 유지
            };
            
            // EVSE_Status 변경 이벤트 구독
            charger.EvseStatusChanged += (evseStatus) =>
            {
                // 관리자가 점검중(1) 또는 중지(2)로 설정한 경우
                if (evseStatus == 1 || evseStatus == 2)
                {
                    StopChargingForMaintenanceMode();
                    _isMaintenancePopupByAdmin = true;
                    _errorPopupShown = false;
                    this.ShowMaintenancePopup(true);
                }
                // 관리자가 정상(0)으로 설정한 경우
                else if (evseStatus == 0)
                {
                    _isMaintenancePopupByAdmin = false;
                    CheckMaintenanceStatus();
                    // DSP 연결이 정상이고 fault가 없으면 모달 닫기
                    int dspStatus = AppSettingsManager.EvCommSettings.EVSE_DSP_Status;
                    if (dspStatus == 0)
                    {
                        this.ShowMaintenancePopup(false);
                    }
                }
            };

            // 초기 상태 확인
            int initialEvseStatus = AppSettingsManager.EvCommSettings.EVSE_Status;
            if (initialEvseStatus == 1 || initialEvseStatus == 2)
            {
                _isMaintenancePopupByAdmin = true;
            }
            
            // 초기 DSP 연결 상태 확인 (지연 실행하여 DSP 초기화 대기)
            Task.Delay(6000).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    int dspStatus = AppSettingsManager.EvCommSettings.EVSE_DSP_Status;
                    if (_isMaintenancePopupByAdmin)
                    {
                        // 관리자가 띄운 모달이면 표시
                        this.ShowMaintenancePopup(true);
                    }
                    else
                    {
                        CheckMaintenanceStatus();
                    }
                });
            });

            // 중단된 충전 복구 완료 이벤트 처리
            charger.InterruptedChargingRestored += (s, e) =>
            {
                _charger.StopChargingold2(e.ChannelNo); // DSP에게 충전 stop 명령 전송                
                ShowInterruptedChargingResult(e);
            };

            charger.PayYNStatusChanged += (isFree) =>
            {
                if (isFree)
                {
                    UnitCostText = "무료";
                }
                else
                {
                    float currentUnitCost = AppSettingsManager.ChargerOperationSettings.PriceForHour[DateTime.Now.Hour];
                    UnitCostText = $"{currentUnitCost:F1}원";
                }

                if (LeftChargerView.DataContext is ChargerViewModel leftVm)
                {
                    leftVm.SetPayYNVisibility(isFree);
                }
                if (RightChargerView != null && RightChargerView.DataContext is ChargerViewModel rightVm)
                {
                    rightVm.SetPayYNVisibility(isFree);
                }
            };

            charger.TestStatusChanged += (isVisible) =>
            {
                if (LeftChargerView.DataContext is ChargerViewModel leftVm)
                {
                    leftVm.SetTestVisibility(isVisible);
                }
                if (RightChargerView != null && RightChargerView.DataContext is ChargerViewModel rightVm)
                {
                    rightVm.SetTestVisibility(isVisible);
                }
            };

            // 복구 가능한 세션이 있으면 공지 페이지 스킵
            bool hasRestorableSessions = charger.HasRestorableSessions();
            if (hasRestorableSessions)
            {
                ShowInitView = false;
            }
            else
            {
                ShowInitView = true;                
                InitView.DataContext = this;
                SoundService.Instance.PlaySoundAsync("start_button.wav");
            }

            ReportQrCode = GetQrCode("https://ev.or.kr/nportal/partcptn/initInconfortReportAction.do");
            SearchStationQrCode = GetQrCode("https://ev.or.kr/nportal/monitor/evMap.do");

            // Check the initial state of PayYN
            string initialPayYN = AppSettingsManager.EvCommSettings.EVSE_PayYN;
            if (initialPayYN == "N") // If it's "N", it's free
            {
                UnitCostText = "무료";
            }
            else // It's "Y" or anything else, so show the price
            {
                float initialUnitCost = AppSettingsManager.ChargerOperationSettings.PriceForHour[DateTime.Now.Hour];
                UnitCostText = $"{initialUnitCost:F1}원";
            }
            
            _maintenanceCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _maintenanceCheckTimer.Tick += (s, e) => CheckMaintenanceStatus();
            _maintenanceCheckTimer.Start();

            // DSP 펌웨어 버전 초기화 (DSP 초기화 대기 후)
            Task.Delay(3000).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        string firmwareVersion = GetChargerFirmwareVersion(_charger);
                        ChargerFirmwareVersion = string.IsNullOrEmpty(firmwareVersion) ? "1.0.0" : firmwareVersion;
                    }
                    catch
                    {
                        ChargerFirmwareVersion = "1.0.0";
                    }
                });
            });
        }

        private void StopChargingForMaintenanceMode()
        {
            if (LeftChargerView?.DataContext is ChargerViewModel leftVm)
            {
                leftVm.CompleteChargingByMaintenance();
            }

            if (RightChargerView?.DataContext is ChargerViewModel rightVm)
            {
                rightVm.CompleteChargingByMaintenance();
            }
        }

        private void StopChargingForErrorPopup()
        {
            if (LeftChargerView?.DataContext is ChargerViewModel leftVm)
            {
                leftVm.CompleteChargingByErrorPopup();
            }

            if (RightChargerView?.DataContext is ChargerViewModel rightVm)
            {
                rightVm.CompleteChargingByErrorPopup();
            }
        }

        private string GetChargerFirmwareVersion(Charger charger)
        {
            if (charger != null)
            {
                return charger.GetChargerFirmwareVersion();
            }
            return "1.0.0";
        }

        private string NormalizeAlarmCode(string rawFaultCode)
        {
            if (string.IsNullOrWhiteSpace(rawFaultCode))
            {
                return "0000";
            }

            if (int.TryParse(rawFaultCode, out int code))
            {
                int normalized = Math.Abs(code) % 10000;
                return normalized.ToString("D4");
            }

            string digitsOnly = new string(rawFaultCode.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length == 0)
            {
                return "0000";
            }

            if (digitsOnly.Length > 4)
            {
                digitsOnly = digitsOnly.Substring(digitsOnly.Length - 4);
            }

            return digitsOnly.PadLeft(4, '0');
        }

        private void SendAlarmRaisedWhenErrorPopupShown(string errorCode)
        {
            if (_alarmRaisedByErrorPopup)
            {
                return;
            }

            string alarmCode = NormalizeAlarmCode(errorCode);
            if (alarmCode == "0000")
            {
                // 0 알람은 전송하지 않는다.
                return;
            }
            foreach (var channel in _charger.Channels)
            {
                if (channel == null) continue;
                _charger.EvCommService.SendAlarmHistory(channel.StationId, channel.ChargerId, "0", DateTime.Now.ToString("yyyyMMddHHmmss"), alarmCode);
            }

            _lastPopupAlarmCode = alarmCode;
            _alarmRaisedByErrorPopup = true;
        }

        private void SendAlarmClearedWhenErrorPopupHidden()
        {
            if (!_alarmRaisedByErrorPopup)
            {
                return;
            }

            if (_lastPopupAlarmCode == "0000")
            {
                _alarmRaisedByErrorPopup = false;
                _lastPopupAlarmCode = "0000";
                return;
            }
            foreach (var channel in _charger.Channels)
            {
                if (channel == null) continue;
                _charger.EvCommService.SendAlarmHistory(channel.StationId, channel.ChargerId, "1", DateTime.Now.ToString("yyyyMMddHHmmss"), _lastPopupAlarmCode);
            }

            _alarmRaisedByErrorPopup = false;
            _lastPopupAlarmCode = "0000";
        }

        private string _unitCostText;
        public string UnitCostText
        {
            get => _unitCostText;
            set
            {
                _unitCostText = value;
                OnPropertyChanged(nameof(UnitCostText));
            }
        }

        public string ChargingSpeedText
        {
            get
            {
                int speed = AppSettingsManager.ChargerSettings.ChargingSpeed;
                return $"{speed}kW";
            }
        }

        ~MainViewModel()
        {

        }

        public UserControl LeftChargerView
        {
            get { return _leftChargerView; }
            set
            {
                _leftChargerView = value;
                OnPropertyChanged(nameof(LeftChargerView));
            }
        }
        public UserControl RightChargerView { 
            get { return _rightChargerView; }
            set
            {
                _rightChargerView = value;
                OnPropertyChanged(nameof(RightChargerView));
            }    
        }

        public int ChargerViewColSpan
        {
            get => _chargerViewColSpan;
            set
            {
                _chargerViewColSpan = value;
                OnPropertyChanged(nameof(ChargerViewColSpan));
            }
        }

        public UserControl PopupView
        {

            get { return _popupView; }
            set
            {
                var previousPopupType = _popupView?.GetType().Name ?? "null";
                var nextPopupType = value?.GetType().Name ?? "null";

                _popupView = value;
                _logger.Info($"[UI] Popup changed: {previousPopupType} -> {nextPopupType}, IsDimmed={_isDimmed}, PopupType={_popupType}");
                OnPropertyChanged(nameof(PopupView));
                OnPropertyChanged(nameof(IsErrorPopupActive));
                OnPropertyChanged(nameof(IsBottomButtonsEnabled));
                OnPropertyChanged(nameof(IsAdminIconTapAllowed));
            }
        }

        public bool IsErrorPopupActive => _errorPopupShown;

        public bool IsAdminIconTapAllowed => _errorPopupShown && !(PopupView is InputPasswordPopupView);

        public bool IsBottomButtonsEnabled => !IsErrorPopupActive;

        public UserControl ErrorPopupView
        {
            get => _errorPopupView;
            set
            {
                _errorPopupView = value;
                OnPropertyChanged(nameof(ErrorPopupView));
            }
        }

        public bool IsDimmed
        {
            get => _isDimmed;
            set
            {
                _isDimmed = value;
                OnPropertyChanged(nameof(IsDimmed));
            }
        }
        
        public PopupSizeType PopupType
        {
            get => _popupType;
            set
            {
                _popupType = value;
                OnPropertyChanged(nameof(PopupType));
            }
        }

        public bool ShowInitView
        {
            get => _showInitView;
            set
            {
                _showInitView = value;
                OnPropertyChanged(nameof(ShowInitView));
            }
        }

        public UserControl InitView
        {

            get { return _initView; }
            set
            {
                _initView = value;
                OnPropertyChanged(nameof(InitView));
            }
        }

        public string StationName
        {
            get => AppSettingsManager.ChargerSettings.StationName;
        }

        public string StationId
        {
            get => AppSettingsManager.ChargerSettings.StationId;
        }


        public void PopupInsertICCard(ChargerViewModel caller)
        {
            PopupView = new InsertICCardPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.InsertICCardPopupViewTimer);
        }

        public void PopupPaymentLoading()
        {
            DisposePopupTimer();
            PopupView = new PaymentLoadingPopupView();
            PopupType = PopupSizeType.Small;
            IsDimmed = true;
        }

        public void PopupTagRFCard(ChargerViewModel caller)
        {
            PopupView = new TagRFCardPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.TagRFCardPopupViewTimer);
        }
        public void PopupTagSamsungpay(ChargerViewModel caller)
        {
            PopupView = new TagSamsungpayPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.TagSamsungpayPopupViewTimer);
        }

        public void PopupQrCode(ChargerViewModel caller)
        {
            PopupView = new QrCodePopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.QrCodePopupViewTimer);
        }

        public void PopupReserveCharger(ChargerViewModel caller)
        {

            InputPhoneNumberPopupViewModel vm = new InputPhoneNumberPopupViewModel("예약 알림을 받을", "휴대폰번호를 입력해 주세요.");
            vm.ConfirmCommand = caller.ConfirmReservationPhoneNumberCommand;
            vm.CancelCommand = caller.CancelReservationCommand;

            PopupView = new InputPhoneNumberPopupView();
            PopupView.DataContext = vm;
            

            PopupType = PopupSizeType.Large;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.InputPhoneNumberPopupViewTimer);
        }


        public void PopupReservationComplete(ChargerViewModel caller, bool isSuccess)
        {
            PopupView = new ReservationSuccessPopupView();
            PopupView.DataContext = this;

            PopupType = PopupSizeType.Alert;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.ReservationSuccessPopupViewTimer);
        }

        public void PopupReservationFail(ChargerViewModel caller)
        {
            PopupView = new ReservationFailPopupView();
            PopupView.DataContext = this;

            PopupType = PopupSizeType.Alert;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.ReservationSuccessPopupViewTimer);
        }

        public void PopupWrongPassword()
        {
            PopupView = new WrongPasswordPopupView();
            PopupView.DataContext = this;

            PopupType = PopupSizeType.Alert;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.InputPasswordPopupViewTimer);
        }

        public void PopupInputPassword()
        {
            InputPasswordPopupViewModel vm = new InputPasswordPopupViewModel();
            vm.ConfirmCommand = this.PasswordPopupViewConfirmCommand;
            vm.CancelCommand = this.PasswordPopupViewCancelCommand;

            PopupView = new InputPasswordPopupView();
            PopupView.DataContext = vm;

            PopupType = PopupSizeType.Large;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.InputPasswordPopupViewTimer);
        }

        public void PopupInputReservationNumber(ChargerViewModel caller, bool isCancelAction)
        {
            InputReservationNumberPopupViewModel vm = new InputReservationNumberPopupViewModel();

            if (!isCancelAction)
                vm.ConfirmCommand = caller.InputReservationNoCommand;
            else
                vm.ConfirmCommand = caller.CancelReservationNoCommand;

            vm.CancelCommand = caller.ClosePopupInputReservationNoCommand;


            PopupView = new InputReservationNumberPopupView();
            PopupView.DataContext = vm;

            PopupType = PopupSizeType.Large;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.InputReservationNumberPopupViewTimer);
        }

        public void PopupReservationDescription(ChargerViewModel caller)
        {
            PopupView = new ReservationDescriptionPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.ReservationDescriptionPopupViewTimer);
        }

        public void PopupWrongReservationNumber(ChargerViewModel caller)
        {
            PopupView = new WrongReservationNoPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Alert;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.WrongReservationNoPopupViewTimer);
        }
        public void PopupCancelReservation(ChargerViewModel caller)
        {
            PopupView = new ReservationCancelPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Alert;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.ReservationCancelPopupViewTimer);
        }

        public void PopupAutoCancelReservation(ChargerViewModel caller)
        {
            PopupView = new Views.Popup.AutoReservationCancelPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Alert;
            IsDimmed = true;
            // 30초 후 자동으로 홈화면으로 돌아가기
            StartPopupTimer(30);
        }

        public void PopupRegisterChargeEndAlarm(ChargerViewModel caller)
        {
            InputPhoneNumberPopupViewModel vm = new InputPhoneNumberPopupViewModel("종료 문자를 받을", "휴대폰번호를 입력해 주세요.");
            vm.ConfirmCommand = caller.ConfirmRegisterAlarmCommand;
            vm.CancelCommand = caller.CancelRegisterAlarmCommand;

            PopupView = new InputPhoneNumberPopupView();
            PopupView.DataContext = vm;


            PopupType = PopupSizeType.Large;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.InputPhoneNumberPopupViewTimer);
        }



        public void PopupInputChargeAmount(ChargerViewModel caller)
        {
            ChargeInputPopupViewModel vm = new ChargeInputPopupViewModel();
            vm.UnitCost = caller.CurrentUserUnitCost;
            vm.ConfirmCommand = caller.ConfirmChargeAmountCommand;
            vm.CancelCommand = caller.CancelChargeAmountCommand;

            PopupView = new ChargeInputPopupView();
            PopupView.DataContext = vm;

            PopupType = PopupSizeType.Large;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.ChargeInputPopupViewTimer);
        }
        public void WaitingPopup(ChargerViewModel caller)
        {
            PopupView = new WaitingPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Alert;
            IsDimmed = true;
        }

        public void WaitingChargeStartPopup(ChargerViewModel caller)
        {
            PopupView = new WaitingChargingStartPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.WaitingChargingStartPopupViewTimer);
        }

        public void AuthFailPopup(ChargerViewModel caller)
        {
            // 인증 실패 팝업: 시간이 지나면 확인 버튼(CommonClosePopupCommand)을 누른 것과 동일하게 동작
            PopupView = new AuthFailPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.AuthFailPopupViewTimer);
        }
        public void AuthSuccessPopup(ChargerViewModel caller)
        {
            // 인증 완료 팝업: 시간이 지나면 확인 버튼(StartChargingCommand)을 누른 것과 동일하게 동작
            PopupView = new AuthSuccessPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Alert;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.AuthSuccessPopupViewTimer);
        }

        public void PaymentFailPopup(ChargerViewModel caller)
        {
            // 결제 실패 팝업: 시간이 지나면 확인 버튼(CommonClosePopupCommand)을 누른 것과 동일하게 동작
            PopupView = new PaymentFailPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.PaymentFailPopupViewTimer);
        }
        public void PaymentSuccessPopup(ChargerViewModel caller)
        {
            // 결제 완료 팝업: 시간이 지나면 확인 버튼(StartChargingCommand)을 누른 것과 동일하게 동작
            PopupView = new PaymentSuccessPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Alert;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.PaymentSuccessPopupViewTimer);
        }

        public void ConnectorErrorPopup(ChargerViewModel caller)
        {
            // 커넥터 연결 오류 팝업: 확인 버튼 누르면 InitializeCharger 호출
            PopupView = new ConnectorErrorPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.ConnectorErrorPopupViewTimer);
        }

        public void PopupChargingEndConfirm(ChargerViewModel caller)
        {
            // 충전 종료 확인 팝업: 시간이 지나면 확인 버튼(ConfirmEndChargingCommand)을 누른 것과 동일하게 동작
            PopupView = new ChargingEndConfirmPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Alert;
            IsDimmed = true;
            StartPopupTimer(30); // 30초 타이머
        }

        public void PopupCreditCardReceipt(ChargerViewModel caller)
        {
            PopupView = new CreditCardReceiptPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Large;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.CreditCardReceiptPopupViewTimer);
        }

        public void EmergencyStopPopup()
        {
            // 비상정지 팝업은 타이머를 절대 시작하지 않음 (중요 상태 팝업)
            DisposePopupTimer(); // 기존 타이머가 있다면 명시적으로 중지
            PopupView = new EmergencyStopPopupView();

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
        }

        public void ShowErrorPopup(bool isVisible, bool dspStatus, bool networkStatus, bool pmsStatus, string errorCode)
        {
            if (isVisible)
            {
                // 비상정지 모달은 최우선
                if (PopupView is EmergencyStopPopupView)
                {
                    _errorPopupShown = false;
                    ErrorPopupView = null;
                    ErrorCodeText = string.Empty;
                    OnPropertyChanged(nameof(IsErrorPopupActive));
                    OnPropertyChanged(nameof(IsAdminIconTapAllowed));
                    UpdateDimmedState();
                    return;
                }

                _errorPopupShown = true;
                OnPropertyChanged(nameof(IsErrorPopupActive));
                OnPropertyChanged(nameof(IsAdminIconTapAllowed));

                string ErrcodeText = errorCode;

                if (ErrorPopupView == null)
                {
                    ErrorPopupView = new ErrorStatusPopupView();
                    ErrorPopupView.DataContext = this;
                }
                else if (ErrorPopupView.DataContext != this)
                {
                    ErrorPopupView.DataContext = this;
                }

                UpdateErrorPopupText(dspStatus, networkStatus, pmsStatus, ErrcodeText);

                if (!(PopupView is InputPasswordPopupView))
                {
                    DisposePopupTimer();
                }

                UpdateDimmedState();
            }
            else
            {
                _errorPopupShown = false;
                _keepErrorPopupUntilFaultClears = false;
                ErrorPopupView = null;
                ErrorCodeText = string.Empty;

                OnPropertyChanged(nameof(IsErrorPopupActive));
                OnPropertyChanged(nameof(IsAdminIconTapAllowed));

                UpdateDimmedState();
            }
        }

        private void UpdateErrorPopupText(bool dspStatus, bool networkStatus, bool pmsStatus, string errcodeText)
        {
            if (dspStatus)
            {
                if (errcodeText == "0501")
                    ErrorCodeText = $" ERROR CODE : 501 MCU 제어보드 통신 오류";
                else
                    ErrorCodeText = $" ERROR CODE : {errcodeText}";
            }
            else if (!networkStatus)
                ErrorCodeText = " ERROR : 서버와 연결이 되지 않습니다. ";
            else if (pmsStatus)
                ErrorCodeText = " ERROR CODE: 904 전력량계 통신 오류 ";
            else
                ErrorCodeText = $" Error code {errcodeText}";
        }

        private void UpdateMaintenancePopupText()
        {
            int evseStatus = AppSettingsManager.EvCommSettings.EVSE_Status;

            if (evseStatus == 2)
            {
                MaintenancePopupTitle = "운영중지";
                MaintenancePopupMessage = "충전기 운영중지입니다.";
            }
            else
            {
                MaintenancePopupTitle = "점검중";
                MaintenancePopupMessage = "충전기 점검중입니다.";
            }
        }

        public void ShowMaintenancePopup(bool isVisible)
        {
            if (isVisible)
            {
                UpdateMaintenancePopupText();

                // 이미 점검중 모달이 표시되어 있으면 텍스트만 갱신
                if (PopupView is EVSE_CheckView)
                {
                    if (PopupView.DataContext != this)
                    {
                        PopupView.DataContext = this;
                    }

                    return;
                }

                // 점검중 팝업은 타이머를 절대 시작하지 않음 (중요 상태 팝업)
                DisposePopupTimer(); // 기존 타이머가 있다면 명시적으로 중지
                PopupView = new EVSE_CheckView();
                PopupView.DataContext = this;
                PopupType = PopupSizeType.Small;
                IsDimmed = true;
            }
            else
            {
                // 관리자가 띄운 모달이 아니면 닫기
                if (!_isMaintenancePopupByAdmin)
                {
                    // 점검중 모달만 닫기 (비상정지 모달은 유지)
                    if (PopupView is EVSE_CheckView)
                    {
                        ClosePopup();
                    }
                }
            }
        }

        #region HELP POPUP
        public void HelpChargingCapacityPopup(ChargerViewModel caller)
        {
            PopupView = new HelpChargingCapacityPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.HelpPopupViewTimer);
        }

        public void HelpChargingSpeedPopup(ChargerViewModel caller)
        {
            PopupView = new HelpChargingSpeedPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.HelpPopupViewTimer);
        }

        public void HelpChargingFinishedNotificationPopup(ChargerViewModel caller)
        {
            PopupView = new HelpChargingFinishedNotificationPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.HelpPopupViewTimer);
        }

        public void HelpDCComboPopup(ChargerViewModel caller)
        {
            PopupView = new HelpDCComboPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.HelpPopupViewTimer);
        }

        public void HelpDCChademoPopup(ChargerViewModel caller)
        {
            // 인증 실패 팝업: 시간이 지나면 확인 버튼(CommonClosePopupCommand)을 누른 것과 동일하게 동작
            PopupView = new HelpDCChademoPopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.HelpPopupViewTimer);
        }

        public void HelpAC3Popup(ChargerViewModel caller)
        {
            // 인증 실패 팝업: 시간이 지나면 확인 버튼(CommonClosePopupCommand)을 누른 것과 동일하게 동작
            PopupView = new HelpAC3PopupView();
            PopupView.DataContext = caller;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.HelpPopupViewTimer);
        }
        #endregion


        public void ClosePopup()
        {
            ClosePopupInternal(); 
        }

        private void ClosePopupInternal()
        {
            DisposePopupTimer();
            PopupView = null;
            UpdateDimmedState();
        }

        private void UpdateDimmedState()
        {
            IsDimmed = PopupView != null || _errorPopupShown;
        }

        private void Start(object param)
        {
            ShowInitView = false;
            CheckForIdleState();
            SoundService.Instance.PlaySoundAsync("select_coupler.wav");
        }

        private void ConfirmPasswordPopupView(object param)
        {
            if (IsAnyChannelCharging())
            {
                ClosePopupInternal();
                return;
            }

            if (PopupView != null && PopupView.DataContext is InputPasswordPopupViewModel)
            {
                InputPasswordPopupViewModel vm = PopupView.DataContext as InputPasswordPopupViewModel;
                if (vm != null)
                {
#if true                    
                    // Compute the SHA256 hash of the input password
                    string hashedInputPassword = ComputeSha256Hash(vm.Input);
                    
                    if (hashedInputPassword.Equals("8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92"))
                    {
                        ClosePopupInternal();

                        var logger = ((App)Application.Current).AppLogger;
                        logger?.Info("Admin login");
                        
                       ((App)Application.Current).ShowAdminWindow();
                    }
                    else
                    {
                        // 비밀번호가 틀렸을 때 팝업 표시
                        ClosePopupInternal();
                        PopupWrongPassword();
                    }

#else
                    const string originalPassword = "123456";
                    string hashedOriginalPassword = ComputeSha256Hash(originalPassword);

                    // Compute the SHA256 hash of the input password
                    string hashedInputPassword = ComputeSha256Hash(vm.Input);

                   if (hashedInputPassword == hashedOriginalPassword)                   
                    {
                        ClosePopupInternal();

                        var logger = ((App)Application.Current).AppLogger;
                        logger?.Info("Admin login");

                        ((App)Application.Current).ShowAdminWindow();
                    }
                    else
                    {
                        // 비밀번호가 틀렸을 때 팝업 표시
                        ClosePopupInternal();
                        PopupWrongPassword();
                    }
#endif


                }

            }
        } 
        private void ClosePasswordPopupView(object param)
        {
            ClosePopupInternal();
        }

        private void ReopenPasswordInputPopup(object param)
        {
            ClosePopupInternal();
            PopupInputPassword();
        }

        private void DbClickHiddenBtn(object param)
        {
            try
            {
                var logger = ((App)Application.Current).AppLogger;
                var delta = (DateTime.Now - _lastClickTime).TotalMilliseconds;
                logger.Info($"[AdminIconClick] invoked. deltaMs={delta:0}");
            }
            catch
            {
                // 로깅 실패는 UI 동작에 영향 주지 않음
            }

            if (IsAnyChannelCharging())
            {
                return;
            }

            var now = DateTime.Now;
            if ((now - _lastClickTime).TotalMilliseconds < 200)
            {
                PopupInputPassword();
            }
            _lastClickTime = now;
        }

        private void OpenPasswordPopup(object param)
        {
            if (IsAnyChannelCharging())
            {
                return;
            }

            PopupInputPassword();
        }

        private bool IsAnyChannelCharging()
        {
            try
            {
                var leftVm = LeftChargerView?.DataContext as ChargerViewModel;
                if (leftVm?.CurrentChargeSequence == ChargeSequence.Charging)
                {
                    return true;
                }

                var rightVm = RightChargerView?.DataContext as ChargerViewModel;
                if (rightVm?.CurrentChargeSequence == ChargeSequence.Charging)
                {
                    return true;
                }
            }
            catch
            {
                // 상태 확인 실패 시에는 보수적으로 차단하지 않고 기존 동작 유지
            }

            return false;
        }

        private void OpenPopupHelpChargingCapacity(object param)
        {
            PopupView = new HelpChargingCapacityPopupView();
            PopupView.DataContext = this;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.HelpPopupViewTimer);
        }

        private void OpenPopupReportQrCode(object param)
        {
            PopupView = new ReportQrCodePopupView();
            PopupView.DataContext = this;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.ReportQrCodePopupViewTimer);
        }

        private void OpenPopupSearchStationQrCode(object param)
        {
            PopupView = new SearchStationQrCodePopupView();
            PopupView.DataContext = this;

            PopupType = PopupSizeType.Small;
            IsDimmed = true;
            StartPopupTimer(AppSettingsManager.ChargerTimerSettings.SearchStationQrCodePopupViewTimer);
        }
        

        private BitmapImage GetQrCode(string content)
        {
            using (var qrGenerator = new QRCodeGenerator())
            using (var qrData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new QRCode(qrData))
            using (var bitmap = qrCode.GetGraphic(20))
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = memory;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();

                return image;
            }
        }


        private void CloseCommonPopup(object param)
        {
            ClosePopupInternal();
        }

        private void DisposeReturnToNormalViewTimer()
        {
            if (_returnToNormalViewTimer != null && _returnToNormalViewTimer.IsEnabled)
            {
                _returnToNormalViewTimer.Stop();
                _returnToNormalViewTimer = null;
            }
        }

        private void DisposePopupTimer()
        {
            if (_popupTimer != null && _popupTimer.IsEnabled)
            {
                _popupTimer.Stop();
                _popupTimer.Tick -= PopupTimer_Tick;
                _popupTimer = null;
            }
        }

        private void PopupTimer_Tick(object sender, EventArgs e)
        {
            DisposePopupTimer();
            
            // 타이머 만료 시 확인 버튼의 Command를 실행 (확인 버튼을 누른 것과 동일하게 동작)
            if (PopupView != null && PopupView.DataContext is ChargerViewModel chargerVm)
            {
                // 팝업 타입에 따라 적절한 Command 실행
                if (PopupView is PaymentSuccessPopupView || PopupView is AuthSuccessPopupView)
                {
                    // 결제 완료/인증 완료: StartChargingCommand 실행
                    chargerVm.StartChargingCommand?.Execute(null);
                }
                else if (PopupView is PaymentFailPopupView || PopupView is AuthFailPopupView)
                {
                    // 결제 실패/인증 실패: CommonClosePopupCommand 실행
                    chargerVm.CommonClosePopupCommand?.Execute(null);
                }
                else if (PopupView is ChargingEndConfirmPopupView)
                {
                    // 충전 종료 확인: 타이머 만료 시 ContinueChargingCommand 실행 (모달만 닫고 충전 계속)
                    chargerVm.ContinueChargingCommand?.Execute(null);
                }
                else if (PopupView is AutoReservationCancelPopupView || PopupView is ReservationCancelPopupView)
                {
                    // 자동 예약 취소 팝업 또는 예약 취소 팝업: ConfirmReservationCancelCommand 실행
                    chargerVm.ConfirmReservationCancelCommand?.Execute(null);
                }
                else if (PopupView is WaitingChargingStartPopupView)
                {
                    // 타이머 만료돼도 아무 동작 하지 않음
                }
                else if (PopupView is CreditCardReceiptPopupView)
                {
                    // 신용카드 전표 팝업: 닫기 버튼(Command) 실행과 동일하게 동작
                    chargerVm.CloseCreditCardReceiptCommand?.Execute(null);
                }
                else
                {
                    // 기타 팝업은 기존처럼 닫기만 함
                    ClosePopupInternal();
                }
            }
            else if (PopupView is WrongPasswordPopupView)
            {
                // 비밀번호 틀렸을 때 팝업: 비밀번호 입력 팝업 다시 띄우기
                ReopenPasswordInputPopup(null);
            }
            else
            {
                ClosePopupInternal();
            }
        }

        private void StartPopupTimer(int seconds)
        {
            DisposePopupTimer();
            if (seconds > 0)
            {
                _popupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
                _popupTimer.Tick += PopupTimer_Tick;
                _popupTimer.Start();
            }
        }

        private void ReturnToNormalViewTimer_Tick(object sender, EventArgs e)
        {
            // 모달이 열려있으면 타이머 실행하지 않음
            if (PopupView != null || IsDimmed)
            {
                return;
            }

            DisposeReturnToNormalViewTimer();
            ShowInitView = true;
            SoundService.Instance.PlaySoundAsync("start_button.wav");
        }

        public void CheckForIdleState()
        {
            DisposeReturnToNormalViewTimer();

            if (ShowInitView) return;

            bool isIdle = false;
            var leftVm = LeftChargerView.DataContext as ChargerViewModel;

            if (RightChargerView != null)
            {
                var rightVm = RightChargerView.DataContext as ChargerViewModel;
                if (leftVm?.CurrentChargeSequence == ChargeSequence.SelectConnector && rightVm?.CurrentChargeSequence == ChargeSequence.SelectConnector)
                {
                    isIdle = true;
                }
            }
            else
            {
                if (leftVm?.CurrentChargeSequence == ChargeSequence.SelectConnector)
                {
                    isIdle = true;
                }
            }

            if (isIdle)
            {
                // 모달이 열려있으면 메인 타이머 시작하지 않음
                if (PopupView != null || IsDimmed)
                {
                    return;
                }

                Console.WriteLine($"AutoReturnToInitViewTimer: {AppSettingsManager.ChargerTimerSettings.AutoReturnToInitViewTimer}");
                _returnToNormalViewTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(AppSettingsManager.ChargerTimerSettings.AutoReturnToInitViewTimer) };
                _returnToNormalViewTimer.Tick += ReturnToNormalViewTimer_Tick;
                _returnToNormalViewTimer.Start();
            }
            else
            {
                // 한쪽이라도 SelectConnector가 아니면(충전중 등) 타이머 중지
                DisposeReturnToNormalViewTimer();
            }
        }


        private static string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256 hash from string
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        /// <summary>
        /// 중단된 충전 결과 화면 표시
        /// </summary>
        private void ShowInterruptedChargingResult(Models.InterruptedChargingRestoredEventArgs e)
        {
            try
            {
                var logger = ((App)Application.Current).AppLogger;
                logger.Info($"[ShowInterruptedChargingResult] Received event for channel {e.ChannelNo}");

                ChargerViewModel targetViewModel = null;

                // 채널 번호에 따라 ViewModel 찾기 (ChannelNo는 0 또는 1)
                if (e.ChannelNo == 0 && LeftChargerView?.DataContext is ChargerViewModel leftVm)
                {
                    targetViewModel = leftVm;
                    logger.Info($"[ShowInterruptedChargingResult] Found left ViewModel for channel {e.ChannelNo}");
                }
                else if (e.ChannelNo == 1 && RightChargerView?.DataContext is ChargerViewModel rightVm)
                {
                    targetViewModel = rightVm;
                    logger.Info($"[ShowInterruptedChargingResult] Found right ViewModel for channel {e.ChannelNo}");
                }
                else if (e.ChannelNo == 0 && RightChargerView == null && LeftChargerView?.DataContext is ChargerViewModel singleVm)
                {
                    // 단일 채널인 경우
                    targetViewModel = singleVm;
                    logger.Info($"[ShowInterruptedChargingResult] Found single ViewModel for channel {e.ChannelNo}");
                }

                if (targetViewModel != null)
                {
                    logger.Info($"[ShowInterruptedChargingResult] Calling ShowInterruptedChargingResult on ViewModel");
                    targetViewModel.ShowInterruptedChargingResult(
                        e.ChargePower,
                        e.ActualChargeAmount,
                        e.UserSetChargeAmount,
                        e.CancelChargeAmount,
                        e.StartTime,
                        e.EndTime,
                        e.ChargeTime
                    );
                    logger.Info($"[ShowInterruptedChargingResult] ViewModel method called successfully");
                }
                else
                {
                    // ViewModel을 찾을 수 없는 경우 로깅
                    logger.Error($"[ShowInterruptedChargingResult] ViewModel not found for channel {e.ChannelNo}");
                    logger.Error($"[ShowInterruptedChargingResult] LeftChargerView={LeftChargerView != null}, RightChargerView={RightChargerView != null}");
                }
            }
            catch (Exception ex)
            {
                var logger = ((App)Application.Current).AppLogger;
                logger.Error($"[ShowInterruptedChargingResult] Error: {ex.Message}");
                logger.Error($"[ShowInterruptedChargingResult] StackTrace: {ex.StackTrace}");
            }
        }

#if false
        private void CheckMaintenanceStatus()
        {
            int dspStatus = AppSettingsManager.EvCommSettings.EVSE_DSP_Status;
            int networkStatus = AppSettingsManager.EvCommSettings.EVSE_Network_Status;
            bool shouldShowError = dspStatus == 1 || networkStatus == 1;
            
            bool isAnyCharging = false;
            if (LeftChargerView?.DataContext is ChargerViewModel leftVm)
            {
                isAnyCharging |= leftVm.CurrentChargeSequence == ChargeSequence.Charging ||
                                leftVm.CurrentChargeSequence == ChargeSequence.PlugConnector;
            }
            if (RightChargerView?.DataContext is ChargerViewModel rightVm)
            {
                isAnyCharging |= rightVm.CurrentChargeSequence == ChargeSequence.Charging ||
                                rightVm.CurrentChargeSequence == ChargeSequence.PlugConnector;
            }

            if (shouldShowError && !isAnyCharging && !_isMaintenancePopupByAdmin)
            {
                if (!_errorPopupShown)
                {
                    _errorPopupShown = true;
                    ShowErrorPopup(true, dspStatus, networkStatus, false, "000");
                }
            }
            else
            {
                if (_errorPopupShown)
                {
                    _errorPopupShown = false;
                    ShowErrorPopup(false, false, false, false, "000");
                }
            }
        }
#else
        private void CheckMaintenanceStatus()
        {
            bool dspStatus = _charger.IsDspDisconnected;
            bool networkStatus = _charger.EvCommService.IsServerConnected;
            bool pmsStatus = _charger.IsPmsDisconnected;
            string errorCode = "0";
            bool shouldShowError = dspStatus || !networkStatus || pmsStatus;
            bool isEvsis = string.Equals(
                AppSettingsManager.ChargerSettings.ChargerManufacturerCode,
                "evsis",
                StringComparison.OrdinalIgnoreCase);

            if (shouldShowError)
            {
                foreach (var channel in _charger.Channels)
                {
                    errorCode = _charger.GetFaultCode(channel.ChannelNo);
                }
            }

            // 요구사항: 충전 대기(PlugConnector) 상태에서 오류가 나면 팝업을 띄우고 InitializeCharger(Init + 결제 취소)를 수행해야 함.
            ChargerViewModel leftVm = LeftChargerView?.DataContext as ChargerViewModel;
            ChargerViewModel rightVm = RightChargerView?.DataContext as ChargerViewModel;

            bool isAnyCharging = false; // 팝업 억제 대상
            bool hasPlugConnector = false; // 충전 대기 대상

            if (leftVm != null)
            {
                if (leftVm.CurrentChargeSequence == ChargeSequence.PlugConnector)
                    hasPlugConnector = true;
                if (leftVm.CurrentChargeSequence == ChargeSequence.Charging ||
                    leftVm.CurrentChargeSequence == ChargeSequence.Completed)
                    isAnyCharging = true;
            }
            if (rightVm != null)
            {
                if (rightVm.CurrentChargeSequence == ChargeSequence.PlugConnector)
                    hasPlugConnector = true;
                if (rightVm.CurrentChargeSequence == ChargeSequence.Charging ||
                    rightVm.CurrentChargeSequence == ChargeSequence.Completed)
                    isAnyCharging = true;
            }

            bool shouldResetPlugStandbyForError =
                shouldShowError &&
                hasPlugConnector &&
                !isAnyCharging &&
                !_isMaintenancePopupByAdmin;

            if (shouldResetPlugStandbyForError)
            {
                if (!_maintenanceErrorPlugConnectorResetDone)
                {
                    // PlugConnector 채널에 대해 InitializeCharger를 실행하여 Init/결제취소를 포함한 초기 화면으로 복귀
                    if (leftVm?.CurrentChargeSequence == ChargeSequence.PlugConnector)
                        leftVm.InitializeChargerCommand.Execute(null);
                    if (rightVm?.CurrentChargeSequence == ChargeSequence.PlugConnector)
                        rightVm.InitializeChargerCommand.Execute(null);

                    _maintenanceErrorPlugConnectorResetDone = true;
                }
            }
            else
            {
                _maintenanceErrorPlugConnectorResetDone = false;
            }

            // 매니저는 "누리집 알람 히스토리(raise/clear)" 전송 정책만 담당하고,
            // 팝업 표시 여부는 반환값으로만 반영한다.
            // EVSIS는 충전 중 PMS(전력량계) 통신이 끊겨도 오류 팝업을 표시해야 한다.
            bool suppressErrorPopupByCharging = isAnyCharging && !(isEvsis && pmsStatus);
            bool shouldShowErrorPopup = _faultHandlingManager.HandleFaultState(
                dspStatus,
                networkStatus,
                pmsStatus,
                shouldShowError,
                errorCode,
                suppressErrorPopupByCharging,
                _isMaintenancePopupByAdmin);

            // 충전 중 오류 팝업으로 강제 종료를 시작한 경우, 오류가 해제되기 전까지 팝업 유지
            if (_keepErrorPopupUntilFaultClears && shouldShowError && !_isMaintenancePopupByAdmin)
            {
                shouldShowErrorPopup = true;
            }

            if (!shouldShowError)
            {
                _keepErrorPopupUntilFaultClears = false;
            }

            if (shouldShowErrorPopup)
            {
                if (!_errorPopupShown)
                {
                    _keepErrorPopupUntilFaultClears = true;
                    StopChargingForErrorPopup();
                    _errorPopupShown = true;
                }

                // 이미 팝업이 떠 있으면 동일 팝업에 잔존 오류 메시지로 갱신
                ShowErrorPopup(true, dspStatus, networkStatus, pmsStatus, errorCode);
            }
            else
            {
                if (_errorPopupShown)
                {
                    _errorPopupShown = false;
                    ShowErrorPopup(false, false, false, false, "0");
                }
            }
        }
#endif
    }
}
