using EvChargerUI.Commons.Enum;
using EvChargerUI.Commons.Settings;
using EvChargerUI.Commons.Util;
using EvChargerUI.Domains;
using EvChargerUI.Models;
using EvChargerUI.Services;
using EvChargerUI.Services.DspControl;
using EvChargerUI.Services.EvComm;
using EvChargerUI.Services.EvComm.HttpJsonRequest;
using PaymentInfo = EvChargerUI.Domains.PaymentInfo;
using EvChargerUI.ViewModels.Commons;
using EvChargerUI.Views;
using EvChargerUI.Views.DualChannel;
using Newtonsoft.Json.Linq;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EvChargerUI.ViewModels
{
    public class ChargerViewModel : BaseViewModel
    {
        protected readonly MainViewModel _parentViewModel;
        protected readonly Charger _charger;
        protected readonly ChargerChannel _chargerChannel;
        protected readonly IEvCommService _evCommService;
        protected JSonParser jSonParser = new JSonParser();

        // 비회원 단가 조회(GetNonMemberUnitCost) 결과가 반영되는 현재 단가
        public float CurrentUserUnitCost => _chargerChannel?.CurrentUserUnitCost ?? ChargerChannel.DefaultUnitCost;

        protected UserControl _currentView;

        protected UserControl _reservationWaitView;
        protected UserControl _chargerSelectTypeView;
        protected UserControl _tripleChargerSelectTypeView;
        protected UserControl _paymentMethodSelectView;
        protected UserControl _readyToChargingView;
        protected UserControl _chaeviReadyToChargeLRView;
        protected UserControl _chaeviReadyToChargeUDLRView;
        protected UserControl _waitingOtherSideView;
        protected UserControl _progressView;
        protected UserControl _calChargeAmountView;
        protected UserControl _completeView;
        protected UserControl _chargingReceiptView;

        protected ChargeSequence _currentChargeSequence;
        protected ChargerViewModel _otherChargerViewModel;

        // ── 듀얼 채널 동시 클릭 경쟁 방지 ─────────────────────────────────────────
        // 두 채널이 거의 동시에 SelectPaymentMethod를 호출할 때 하나만 진입 허용
        private static readonly object _dualChannelSelectSync = new object();
        private static int _dualChannelSelectOwner = -1; // -1=소유자 없음, 0/1=채널 번호
        private bool _isSelectPaymentMethodInProgress = false;

        // 결제 카드 대기 팝업(IC/삼성페이)에서 사용자가 취소 버튼을 누른 경우 플래그
        // true일 때는 PayCost 실패 응답이 와도 결제 실패 팝업을 띄우지 않고 그냥 닫음
        private bool _isPaymentCancelledByUser = false;

        // RF 카드 대기 팝업에서 사용자가 취소 버튼을 누른 경우 플래그
        // true일 때는 ReadRfCard 실패 응답이 와도 결제 실패 팝업을 띄우지 않음
        private bool _isRfCardCancelledByUser = false;

        // Home 버튼 연속 클릭 방지 (UI 경로 전용)
        private bool _isHomeInitializeClickLocked = false;
        private DateTime _lastHomeInitializeAt = DateTime.MinValue;
        private static readonly TimeSpan _homeInitializeDebounce = TimeSpan.FromMilliseconds(700);
        private int _initializeVersion = 0;

        /// <summary>
        /// 듀얼 채널 선택 소유권 획득 시도.
        /// 이미 다른 채널이 점유 중이면 false를 반환한다.
        /// </summary>
        private bool TryAcquireDualChannelSelectOwnership()
        {
            lock (_dualChannelSelectSync)
            {
                // 이미 이 채널이 소유 중 → 재진입 허용
                if (_dualChannelSelectOwner == _chargerChannel.ChannelNo)
                    return true;

                // 다른 채널이 점유 중 → 차단
                if (_dualChannelSelectOwner != -1)
                {
                    _logger.Warn($"[DualChannel][CH{_chargerChannel.ChannelNo}] SelectPaymentMethod 진입 차단 — 채널 {_dualChannelSelectOwner} 이 이미 결제/커넥터 단계 점유 중");
                    return false;
                }

                // 상대방 ViewModel 상태 이중 검사 (lock 외부의 상태 변경 방어)
                if (_otherChargerViewModel != null)
                {
                    var otherSeq = _otherChargerViewModel.CurrentChargeSequence;
                    if (otherSeq == ChargeSequence.SelectPaymentMethod || otherSeq == ChargeSequence.PlugConnector)
                    {
                        _logger.Warn($"[DualChannel][CH{_chargerChannel.ChannelNo}] SelectPaymentMethod 진입 차단 — 상대방 채널 상태: {otherSeq}");
                        return false;
                    }
                }

                _dualChannelSelectOwner = _chargerChannel.ChannelNo;
                _logger.Info($"[DualChannel][CH{_chargerChannel.ChannelNo}] 듀얼 채널 선택 소유권 획득 (owner={_dualChannelSelectOwner})");
                return true;
            }
        }

        /// <summary>
        /// 현재 채널이 소유 중인 경우에만 소유권을 해제한다.
        /// </summary>
        private void ReleaseDualChannelSelectOwnership()
        {
            lock (_dualChannelSelectSync)
            {
                if (_dualChannelSelectOwner == _chargerChannel.ChannelNo)
                {
                    _logger.Info($"[DualChannel][CH{_chargerChannel.ChannelNo}] 듀얼 채널 선택 소유권 해제 (owner: {_dualChannelSelectOwner} → -1)");
                    _dualChannelSelectOwner = -1;
                }
            }
        }
        // ─────────────────────────────────────────────────────────────────────────

        protected bool _isWaitingOtherSide;
        public bool IsWaitingOtherSide
        {
            get => _isWaitingOtherSide;
            set
            {
                _isWaitingOtherSide = value;
                OnPropertyChanged(nameof(IsWaitingOtherSide));
            }
        }

        protected string _reservationCallbackPhoneNumber;

        protected string _chargeEndCallbackPhoneNumber;

        protected int _progress;
        protected double _speed;
        protected string _chargeTime;
        protected double _powerMeter;
        protected int _chargingCost;
        protected int _userSetCost;
        protected int _cancelCost;

        protected int _waitingChargeRemainSeconds;
        protected int _waitingChargeDisplaySeconds;

        protected int _reservationCount;
        public int ReservationCount
        {
            get => _reservationCount;
            set
            {
                _reservationCount = value;
                OnPropertyChanged(nameof(ReservationCount));
                OnPropertyChanged(nameof(ReservationCountText));
                OnPropertyChanged(nameof(HasReservation));
            }
        }

        /// <summary>
        /// 예약 대기 사용자 전화번호 뒤 4자리
        /// (_chargerChannel.ReservationPhoneNo 기반)
        /// </summary>
        public string ReservationWaitingPhoneLast4
        {
            get
            {
                var phone = _chargerChannel?.ReservationPhoneNo;
                if (string.IsNullOrWhiteSpace(phone))
                    return "";

                // 숫자만 추출 후 뒤 4자리
                var digits = new string(phone.Where(char.IsDigit).ToArray());
                if (digits.Length <= 4)
                    return digits;

                return digits.Substring(digits.Length - 4, 4);
            }
        }

        /// <summary>
        /// 예약 대기 화면 타이틀 표시 문구
        /// 예) "0000 예약 대기중입니다."
        /// </summary>
        public string ReservationWaitingText
        {
            get
            {
                var last4 = ReservationWaitingPhoneLast4;
                return string.IsNullOrEmpty(last4)
                    ? "예약 대기중입니다."
                    : $"{last4} 예약 대기중입니다.";
            }
        }

        public string ReservationCountText
        {
            get
            {
                if (_reservationCount <= 0)
                    return "";
                if (_reservationCount > 99)
                    return "99+";
                return _reservationCount.ToString();
            }
        }

        public bool HasReservation
        {
            get => _reservationCount > 0;
        }

        protected FileLogger _logger = ((App)Application.Current).AppLogger;

        protected DispatcherTimer _waitingTimer;
        private EventHandler _waitingTimerTickHandler;
        private int _waitingCountdownSessionId = 0;
        private bool _isStartChargingInProgress = false;
        protected DispatcherTimer _evsisDspTimer;

        protected DispatcherTimer _progressTimer;
        protected DispatcherTimer _progressSendTimer;
        protected DispatcherTimer _paymentMethodSelectTimer;
        protected DispatcherTimer _reservationWaitingTimer;
        protected DispatcherTimer _readyToChargingTimer;
        protected Stopwatch _chargeStopwatch;
        protected DispatcherTimer _chargeTimeDisplayTimer;  // 충전시간 UI 표시 전용 타이머
        protected DispatcherTimer _cehckIdleStautTimer;
        protected bool _isEnterCouplerPage = false;
        protected bool _isRefreshing = false;
        protected bool _isProgressChargingEnded = false;
        protected bool _isChargingEndConfirmPopupShown = false;
        protected bool _evsisRequestStopCharging = false;
        protected bool _evsisRequestStartCharging = false;
        protected bool _evsisRequestStopChargingFinished = false;
        protected bool _evsisRunCompleteCharging = false;
        protected int _evsisRequestStopChargingCheckCount = 0;

        protected bool _isHomeButtonEnabled;
        public bool IsHomeButtonEnabled
        {
            get => _isHomeButtonEnabled;
            set
            {
                _isHomeButtonEnabled = value;
                OnPropertyChanged(nameof(IsHomeButtonEnabled));
            }
        }

        // 결제 카드 대기 팝업(IC/삼성페이)의 취소 버튼 활성화 상태
        // 사용자가 취소 누른 직후 REQ_STOP 응답이 올 때까지 false로 두어 버튼만 비활성화
        protected bool _isPaymentCancelButtonEnabled = true;
        public bool IsPaymentCancelButtonEnabled
        {
            get => _isPaymentCancelButtonEnabled;
            set
            {
                _isPaymentCancelButtonEnabled = value;
                OnPropertyChanged(nameof(IsPaymentCancelButtonEnabled));
            }
        }

        protected BitmapImage _qrCodeImage;
        public BitmapImage QrCodeImage
        {
            get => _qrCodeImage;
            set
            {
                _qrCodeImage = value;
                OnPropertyChanged(nameof(QrCodeImage));
            }
        }
        public string PaymentFailTitle { get; set; } = "결제가 실패했습니다";
        public string PaymentFailMessage { get; set; } = "결제가 실패했습니다.\n다시 시도해 주세요.";

        public UserControl CurrentView { get { return _currentView; } 
            set { 
                var previousViewType = _currentView?.GetType().Name ?? "null";
                var nextViewType = value?.GetType().Name ?? "null";

                _currentView = value;
                _logger.Info($"[UI] View changed. CH={_chargerChannel?.ChannelNo}, Sequence={CurrentChargeSequence}, {previousViewType} -> {nextViewType}");
                OnPropertyChanged(nameof(CurrentView));
            }
        }
        
        public UserControl WaitingOtherSideView { get { return _waitingOtherSideView; } }

        public int WaitingChargeRemainSeconds
        {
            get => _waitingChargeRemainSeconds;
            set
            {
                _waitingChargeRemainSeconds = value;
                OnPropertyChanged(nameof(WaitingChargeRemainSeconds));
            }
        }

        public int WaitingChargeDisplaySeconds
        {
            get => _waitingChargeDisplaySeconds;
            set
            {
                _waitingChargeDisplaySeconds = value;
                OnPropertyChanged(nameof(WaitingChargeDisplaySeconds));
            }
        }

        public string ConnectorImage { get; set; }
        public string UsableButtonImage { get; set; }
        public string UsableButtonImage2 { get; set; }
        public string UsableButtonImage3 { get; set; }

        /// <summary>
        /// 커넥터 타입 아이콘
        /// </summary>
        public string ConnectorTypeIcon
        {
            get
            {
                switch (_chargerChannel.ChargingSelect)
                {
                    case 0: // AC3상
                        return "/Images/ic_type_charger_ac3.png";
                    case 1: // DC콤보 (DC Combo)
                        return "/Images/ic_type_charger_dc.png";
                    case 2: // 차데모 (CHAdeMO)
                        return "/Images/ic_type_charger_chademo.png";
                    default:
                        return "/Images/ic_type_charger_ac3.png";
                }
            }
        }

        /// <summary>
        /// 커넥터 타입 텍스트
        /// </summary>
        public string ConnectorTypeText
        {
            get
            {
                switch (_chargerChannel.ChargingSelect)
                {
                    case 0:
                        return "AC3상";
                    case 1:
                        return "DC콤보";
                    case 2:
                        return "차데모";
                    default:
                        return "AC3상";
                }
            }
        }

        public Visibility PayYNVisibility { get; set; } = Visibility.Visible;
        public Visibility TestVisibility { get; set; } = Visibility.Visible;

        public string UsableButtonColumnIndex { get; set; }

        /// <summary>
        /// 충전 시간 제한 값 (분)
        /// </summary>
        public int ChargeLimitTime
        {
            get => AppSettingsManager.ChargerOperationSettings.ChargeLimitTime;
        }

        /// <summary>
        /// 설정 변경 이벤트 핸들러
        /// </summary>
        private void OnSettingsChanged(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(ChargeLimitTime));
            });
        }

        /// <summary>
        /// 트리플 커넥터 선택 화면용 속성들
        /// </summary>
        private int _selectedConnectorType = 0;
        public bool IsComboSelected => _selectedConnectorType == 1;
        public bool IsChademoSelected => _selectedConnectorType == 2;
        public bool IsAc3Selected => _selectedConnectorType == 0;

        /// <summary>
        /// 초기화 시 현재 커넥터 타입 설정
        /// </summary>
        public void InitializeTripleConnectorSelection()
        {
            _selectedConnectorType = _chargerChannel.ChargingSelect;
            OnPropertyChanged(nameof(IsComboSelected));
            OnPropertyChanged(nameof(IsChademoSelected));
            OnPropertyChanged(nameof(IsAc3Selected));
        }

        public string ReservationCallbackPhoneNumber
        {
            get
            {
                return _reservationCallbackPhoneNumber.Substring(0, 3) + "-"
                                                                       + _reservationCallbackPhoneNumber.Substring(3,
                                                                           _reservationCallbackPhoneNumber.Length - 7) +
                                                                       "-"
                                                                       + _reservationCallbackPhoneNumber.Substring(
                                                                           _reservationCallbackPhoneNumber.Length - 4,
                                                                           4);
            }
        }

        public void SetPayYNVisibility(bool isVisible)
        {
            PayYNVisibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            OnPropertyChanged(nameof(PayYNVisibility));
        }

        public void SetTestVisibility(bool isVisible)
        {
            TestVisibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            OnPropertyChanged(nameof(TestVisibility));
        }


        public string ChargeEndCallbackPhoneNumber
        {
            get => _chargeEndCallbackPhoneNumber;
            set
            {
                _chargeEndCallbackPhoneNumber = value;
                OnPropertyChanged(nameof(ChargeEndCallbackPhoneNumber));
            }
        }

        public ChargeSequence CurrentChargeSequence
        {
            get => _currentChargeSequence;
            set
            {
                _currentChargeSequence = value;
                _chargerChannel.CurrentSequence = value;
                RefreshChargerSequence();
                OnPropertyChanged(nameof(CurrentChargeSequence));
                _parentViewModel.CheckForIdleState();
            }
        }

        public int Progress
        {
            get => _progress;
            set
            {
                if (_progress != value)
                {
                    _progress = value;
                    OnPropertyChanged(nameof(Progress));
                }
            }
        }

        public double Speed
        {
            get => _speed;
            set
            {
                _speed = value;
                OnPropertyChanged(nameof(Speed));
            }
        }

        public string ChargeTime
        {
            get => _chargeTime;
            set
            {
                _chargeTime = value;
                OnPropertyChanged(nameof(ChargeTime));
                OnPropertyChanged(nameof(ChargeTimeFormatted));

            }
        }

        public string ChargeTimeFormatted
        {
            get
            {
                if (string.IsNullOrEmpty(_chargeTime))
                    return "00분 00초";
                
                // "mm:ss" 형식을 "mm분 ss초" 형식으로 변환
                var parts = _chargeTime.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int minutes) && int.TryParse(parts[1], out int seconds))
                {
                    return $"{minutes}분 {seconds}초";
                }
                
                return _chargeTime;
            }
        }

        public double PowerMeter
        {
            get => _powerMeter;
            set
            {
                Debug.WriteLine($"[PowerMeter Set] Raw value: {value}");
                double roundedValue = ChargingAmountUtil.ToRoundedDisplayKwh(value);

                // 구간별 과금 기준값(CurrentSegmentStartPowerMeter)은 "현재 구간의 시작 delta(kWh)".
                // 시작 시 0이 들어올 수 있으므로, 다음 tick에 유효값(>0)이 들어오면 그 값으로 1회 덮어써서
                // (value - CurrentSegmentStartPowerMeter) 가 0부터 시작하도록 맞춘다.
                if (_chargerChannel.CurrentSegmentStartPowerMeter <= 0 && roundedValue > 0)
                {
                    _chargerChannel.CurrentSegmentStartPowerMeter = roundedValue;
                }

                _powerMeter = roundedValue;

                // ========== 변경: 구간별 과금 계산 ==========
                // 원단위 절삭: 1원 자리 버림(=10원 단위로 내림)
                // 이전 구간들의 누적 금액 + (현재 전력량 - 현재 구간 시작 전력량) × 현재 단가
                int currentSegmentCost = MoneyUtil.TruncateWonUnit(
                    (int)((roundedValue - _chargerChannel.CurrentSegmentStartPowerMeter) * _chargerChannel.CurrentUserUnitCost)
                );
                _chargingCost = _chargerChannel.AccumulatedCostBeforeCurrentSegment + currentSegmentCost;
                // ========== 구간별 과금 계산 끝 ==========

                OnPropertyChanged(nameof(PowerMeter));
                OnPropertyChanged(nameof(ChargingCost));

                //_logger.Info($"------------ [구간별 과금] ChargingCost: {ChargingCost} / PowerMeter: {_powerMeter} / " +
                //             $"CurrentSegmentStart: {_chargerChannel.CurrentSegmentStartPowerMeter} / " +
                //             $"AccumulatedBefore: {_chargerChannel.AccumulatedCostBeforeCurrentSegment} / " +
                //             $"CurrentSegmentCost: {currentSegmentCost} / UnitCost: {_chargerChannel.CurrentUserUnitCost}");
            }
        }

        public int ChargingCost
        {
            get
            {
                // 선결제 금액이 있고 실제 충전 금액이 선결제 금액을 초과하면 선결제 금액 반환 (UI 표시용)
                if (_chargerChannel.UserSetChargeAmount > 0 && _chargingCost > _chargerChannel.UserSetChargeAmount)
                {
                    return _chargerChannel.UserSetChargeAmount;
                }
                return _chargingCost;
            }
        }

        public int UserSetCost
        {
            get => _userSetCost < 0 ? 0 : _userSetCost;
            set
            {
                _userSetCost = value;
                OnPropertyChanged(nameof(UserSetCost));
            }
        }

        public int CancelCost
        {
            get => _cancelCost;
            set
            {
                _cancelCost = value;
                OnPropertyChanged(nameof(CancelCost));
            }
        }

        public int DepositAmount
        {
            get => _chargerChannel.UserSetChargeAmount > 0 ? _chargerChannel.UserSetChargeAmount : 0;
        }

        public int FinalChargeAmount
        {
            get => _chargerChannel.ChargeAmount;
        }

        public string BatteryAfterCharging
        {
            get => $"{Progress}%";
        }

        // 신용카드 전표 프로퍼티
        public string MerchantName
        {
            get
            {
                if (_chargerChannel.PrePaymentInfo != null && !string.IsNullOrEmpty(_chargerChannel.PrePaymentInfo.MerchantName))
                {
                    return _chargerChannel.PrePaymentInfo.MerchantName;
                }
                return "한국자동차환경협회(사)";
            }
        }
        public string MerchantNumber
        {
            get
            {
                if (_chargerChannel.PrePaymentInfo != null && !string.IsNullOrEmpty(_chargerChannel.PrePaymentInfo.MerchantId))
                {
                    return _chargerChannel.PrePaymentInfo.MerchantId;
                }
                return "000000000";
            }
        }
        public string CardNumber
        {
            get
            {
                if (_chargerChannel.PrePaymentInfo != null && !string.IsNullOrEmpty(_chargerChannel.PrePaymentInfo.MaskedCardNumber))
                {
                    return _chargerChannel.PrePaymentInfo.MaskedCardNumber;
                }
                return "0000*********";
            }
        }

        public string CardCompanyName
        {
            get
            {
                if (_chargerChannel.PrePaymentInfo != null && !string.IsNullOrEmpty(_chargerChannel.PrePaymentInfo.CardIssuerName))
                {
                    return _chargerChannel.PrePaymentInfo.CardIssuerName;
                }
                return "카드사";
            }
        }

        public string AcquirerName
        {
            get
            {
                if (_chargerChannel.PrePaymentInfo != null && !string.IsNullOrEmpty(_chargerChannel.PrePaymentInfo.CardAcquirerName))
                {
                    return _chargerChannel.PrePaymentInfo.CardAcquirerName;
                }
                return "";
            }
        }
        public string InstallmentMonths
        {
            get
            {
                if (_chargerChannel.PrePaymentInfo != null && !string.IsNullOrEmpty(_chargerChannel.PrePaymentInfo.InstallmentMonths))
                {
                    return _chargerChannel.PrePaymentInfo.InstallmentMonths;
                }
                return "-";
            }
        }

        public int TransactionAmount
        {
            get
            {
                if (_chargerChannel.PrePaymentInfo != null && int.TryParse(_chargerChannel.PrePaymentInfo.TotalCost, out int total))
                {
                    // 부가세 제외한 거래 금액
                    return (int)(total / 1.1);
                }
                return 0;
            }
        }

        public int Vat
        {
            get
            {
                if (_chargerChannel.PrePaymentInfo != null && int.TryParse(_chargerChannel.PrePaymentInfo.TotalCost, out int total))
                {
                    // 부가세 = 총액 - 거래금액
                    return total - TransactionAmount;
                }
                return 0;
            }
        }

        public int PaymentAmount
        {
            get
            {
                if (_chargerChannel.PrePaymentInfo != null && int.TryParse(_chargerChannel.PrePaymentInfo.TotalCost, out int total))
                {
                    return total;
                }
                return 0;
            }
        }

        public string ApprovalNumber
        {
            get
            {
                return _chargerChannel.PrePaymentInfo?.AuthNum ?? "";
            }
        }

        public string ApprovalDateTime
        {
            get
            {
                if (_chargerChannel.PrePaymentInfo != null)
                {
                    string date = _chargerChannel.PrePaymentInfo.PayDate ?? "";
                    string time = _chargerChannel.PrePaymentInfo.PayTime ?? "";
                    return date + " " + time;
                }
                return "";
            }
        }

        public string TerminalNumber
        {
            get
            {
                return _chargerChannel.PrePaymentInfo?.PgNum ?? "";
            }
        }

        public double ChargeAmount => PowerMeter;

        public ICommand CloseCreditCardReceiptCommand { get; set; }

        public ICommand SelectPaymentMethodCommand { get; set; }
        public ICommand SelectConnectorTypeCommand { get; set; }
        public ICommand OpenConnectorHelpCommand { get; set; }
        public ICommand OpenChargingSpeedHelpCommand { get; set; }
        public ICommand StartChargingCommand { get; set; }
        public ICommand CompleteChargingCommand { get; set; }
        public ICommand InitializeChargerCommand { get; set; }
        public ICommand HomeInitializeChargerCommand { get; set; }

        public ICommand SelectICCardCommand { get; set; }
        public ICommand SelectRFCardCommand { get; set; }
        public ICommand SelectSamsungpayCommand { get; set; }
        public ICommand SelectQRAuthCommand { get; set; }

        public ICommand ClosePopupInsertICCardCommand { get; set; }
        public ICommand ClosePopupTagRFCardCommand { get; set; }
        public ICommand ClosePopupTagSamsungpayCommand { get; set; }
        public ICommand ClosePopupQrCodeCommand { get; set; }

        public ICommand ReserveChargerCommand { get; set; }
        public ICommand ConfirmReservationPhoneNumberCommand { get; set; }
        public ICommand CancelReservationCommand { get; set; }
        public ICommand ClosePopupReservationCommand { get; set; }


        public ICommand SelectUseReservationCommand { get; set; }
        public ICommand SelectCancelReservationCommand { get; set; }
        public ICommand ShowReservationDescriptionCommand { get; set; }
        public ICommand InputReservationNoCommand { get; set; }
        public ICommand CancelReservationNoCommand { get; set; }
        public ICommand ClosePopupInputReservationNoCommand { get; set; }
        public ICommand RePopupInputReservationNumberCommand { get; set; }
        public ICommand ConfirmReservationCancelCommand {  get; set; }

        public ICommand OpenRegisterChargeEndAlarmHelpPopupCommand { get; set; }
        public ICommand RegisterChargeEndAlarmCommand {  get; set; }
        public ICommand ConfirmRegisterAlarmCommand {  get; set; }
        public ICommand CancelRegisterAlarmCommand {  get; set; }

        public ICommand ReceiptViewCommand { get; set; }
        public ICommand BackToChargingCompleteCommand { get; set; }

        public ICommand ConfirmChargeAmountCommand { get; set; }
        public ICommand CancelChargeAmountCommand { get; set; }

        public ICommand ConfrimCalcChargeAmountCommand { get; set; }

        public ICommand CommonClosePopupCommand { get; set; }
        public ICommand ClosePopupReportQrCodeCommand { get; set; }
        public ICommand CancleWaitForConnectorPlugInCommand { get; set; }

        public ICommand ContinueChargingCommand { get; set; }
        public ICommand ConfirmEndChargingCommand { get; set; }
        public ICommand StartChargeCommand { get; set; }
        public ICommand CloseCommonPopupCommand => _parentViewModel.CloseCommonPopupCommand;

        public ChargerViewModel(int channelNo, MainViewModel parentViewModel, Charger charger)
        {
            _parentViewModel = parentViewModel;
            _charger = charger;
            _chargerChannel = _charger.Channels[channelNo];
            _evCommService = _charger.EvCommService;

            // 설정 변경 이벤트 구독
            AppSettingsManager.SettingsChanged += OnSettingsChanged;

            InitViews();

            // QR 이벤트 구독 추가
            _charger.QrChargingStarted += OnQrChargingStarted;
            _charger.QrChargingEnded += OnQrChargingEnded;

            InitRelayCommands();

            InitializeCharger(null);

            GenerateQr(_chargerChannel.QrCode);

            // Evsis DSP 주기적으로 통신 (200ms)
            if (AppSettingsManager.ChargerSettings.ChargerManufacturerCode == "evsis")
            {
                _evsisDspTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                _evsisDspTimer.Tick += async (s, e) =>
                {
                    switch (CurrentChargeSequence)
                    {
                        // 예약페이지 및 커넥터 선택 페이지에서는 Standby 상태로 설정
                        case ChargeSequence.WaitReservation:
                        case ChargeSequence.SelectConnector:
                            _charger.InitStandby(_chargerChannel.ChannelNo);
                            break;
                        case ChargeSequence.SelectPaymentMethod:
                            _charger.SetChargeReadyForEvsis(_chargerChannel.ChannelNo);
                            break;
                        case ChargeSequence.PlugConnector:
                            if (AppSettingsManager.ChargerSettings.IsTriple == "Y")
                            {
                                if (_evsisRequestStartCharging)
                                {
                                    _charger.SetWaitForConnectorPlugInForEvsis(_chargerChannel.ChannelNo);
                                }
                                else
                                {
                                    _charger.SetChargeReadyForEvsis(_chargerChannel.ChannelNo);
                                }
                            }
                            else
                            {
                                // 취소 플래그가 올라간 상태에서는 StartCharging이 다시 나가지 않도록 가드
                                if (_evsisRequestStartCharging && !_chargerChannel.IsWaitForConnectorPlugInCancelled)
                                {
                                    _charger.SetWaitForConnectorPlugInForEvsis(_chargerChannel.ChannelNo);
                                }
                                else
                                {
                                    _charger.SetChargeReadyForEvsis(_chargerChannel.ChannelNo);
                                }
                            }
                            break;
                        case ChargeSequence.Charging:
                            // 충전 종료 요청 시
                            if (_evsisRequestStopCharging)
                            {
                                if (!_charger.CheckChargingFinishStatus(_chargerChannel.ChannelNo))
                                {
                                    _charger.StopChargingold(_chargerChannel.ChannelNo);
                                    _evsisRequestStopChargingCheckCount++;
                                    if (_evsisRequestStopChargingCheckCount > 10)
                                    {
                                        _evsisRequestStopChargingFinished = true;
                                        _evsisRequestStopChargingCheckCount = 0;
                                    }
                                }
                                else
                                {
                                    _charger.StopChargingold(_chargerChannel.ChannelNo);
                                    _evsisRequestStopChargingFinished = true;
                                    _evsisRequestStopChargingCheckCount = 0;
                                }
                                if (_evsisRequestStopChargingFinished)
                                {
                                    if (!_evsisRunCompleteCharging) 
                                    {
                                        _evsisRunCompleteCharging = true;
                                        CompleteCharging(null);
                                    }
                                }
                            }
                            // 충전 중인 경우
                            else
                            {
                                _charger.CheckChargingRun(_chargerChannel.ChannelNo);
                            }
                            break;
                        case ChargeSequence.Completed:
                            _evsisRequestStopCharging = false;
                            _evsisRequestStopChargingFinished = false;
                            _evsisRequestStopChargingCheckCount = 0;
                            _evsisRunCompleteCharging = false;
                            _charger.StopChargingold(_chargerChannel.ChannelNo);
                            // 세션 삭제
                            try
                            {
                                ChargingSessionManager.DeleteSession(_chargerChannel.ChannelNo);
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"[InitializeCharger] Channel {_chargerChannel.ChannelNo}: Error deleting session: {ex.Message}");
                            }
                            break;
                        default:
                            break;
                    }
                };
                _evsisDspTimer.Start();
            }
        }


        protected virtual void InitViews()
        {
            // 커넥터 타입 속성 초기화 알림
            OnPropertyChanged(nameof(ConnectorTypeIcon));
            OnPropertyChanged(nameof(ConnectorTypeText));
            
            // 트리플 커넥터 선택 초기화
            InitializeTripleConnectorSelection();
        }

        public void SetOtherChargerViewModel(ChargerViewModel other)
        {
            _otherChargerViewModel = other;
        }

        /// <summary>
        /// 중단된 충전 결과 표시
        /// </summary>
        public void ShowInterruptedChargingResult(double chargePower, int actualChargeAmount, int userSetChargeAmount, int cancelChargeAmount, DateTime startTime, DateTime endTime, int chargeTimeSeconds)
        {
            try
            {
                // 충전 정보 설정
                _chargerChannel.ChargingStartTime = startTime;
                _chargerChannel.ChargingEndTime = endTime;
                _chargerChannel.ChargeAmount = actualChargeAmount;
                _chargerChannel.UserSetChargeAmount = userSetChargeAmount;
                _chargerChannel.CancelChargeAmount = cancelChargeAmount;

                // 전력량 및 비용 설정
                PowerMeter = chargePower;
                CancelCost = cancelChargeAmount;
                UserSetCost = userSetChargeAmount;

                // 충전 시간 설정
                TimeSpan chargeTimeSpan = TimeSpan.FromSeconds(chargeTimeSeconds);
                ChargeTime = $"{(int)chargeTimeSpan.TotalMinutes:D2}:{chargeTimeSpan.Seconds:D2}";

                // Progress는 마지막 전력량 기반으로 계산 (실제로는 SOC를 사용하지만 여기서는 간단히)
                // 실제로는 세션에 저장된 SOC 값을 사용해야 할 수도 있음
                OnPropertyChanged(nameof(DepositAmount));
                OnPropertyChanged(nameof(FinalChargeAmount));
                OnPropertyChanged(nameof(BatteryAfterCharging));
                OnPropertyChanged(nameof(ChargeTimeFormatted));

                // 충전 완료 상태로 설정
                CurrentChargeSequence = ChargeSequence.Completed;
            }
            catch (Exception ex)
            {
                _logger.Error($"[ShowInterruptedChargingResult] Channel {_chargerChannel.ChannelNo}: Error: {ex.Message}");
            }
        }
        private void InitRelayCommands()
        {
            SelectPaymentMethodCommand = new RelayCommand(SelectPaymentMethod);
            SelectConnectorTypeCommand = new RelayCommand(SelectConnectorType);
            OpenConnectorHelpCommand = new RelayCommand(OpenConnectorHelp);
            OpenChargingSpeedHelpCommand = new RelayCommand(OpenChargingSpeedHelp);
            StartChargingCommand = new RelayCommand(StartCharging);
            CompleteChargingCommand = new RelayCommand((param) => CompleteCharging("user"));
            HomeInitializeChargerCommand = new RelayCommand(HomeInitializeCharger);
            InitializeChargerCommand = new RelayCommand(p => InitializeCharger(p));

            SelectRFCardCommand = new RelayCommand(SelectRFCard);
            ClosePopupTagRFCardCommand = new RelayCommand(ClosePopupTagRFCard);

            SelectICCardCommand = new RelayCommand(SelectICCard);
            ClosePopupInsertICCardCommand = new RelayCommand(ClosePopupInsertICCard);

            SelectSamsungpayCommand = new RelayCommand(SelectSamsungpay);
            ClosePopupTagSamsungpayCommand = new RelayCommand(ClosePopupTagSamsungpay);

            SelectQRAuthCommand = new RelayCommand(SelectQRAuth);
            ClosePopupQrCodeCommand = new RelayCommand(ClosePopupQrCode);

            ReserveChargerCommand = new RelayCommand(ReserveCharger);
            ConfirmReservationPhoneNumberCommand = new RelayCommand(ConfirmReservationPhoneNumber);
            CancelReservationCommand = new RelayCommand(CancelReservation);
            ClosePopupReservationCommand = new RelayCommand(ClosePopupReservation);

            SelectUseReservationCommand = new RelayCommand(SelectUseReservation);
            SelectCancelReservationCommand = new RelayCommand(SelectCancelReservation);
            ShowReservationDescriptionCommand  = new RelayCommand(ShowReservationDescription);
            
            InputReservationNoCommand = new RelayCommand(InputReservationNo);
            CancelReservationNoCommand = new RelayCommand(CancelReservationNo);
            ClosePopupInputReservationNoCommand = new RelayCommand(ClosePopupInputReservationNo);
            RePopupInputReservationNumberCommand = new RelayCommand(RePopupInputReservationNumber);
            ConfirmReservationCancelCommand = new RelayCommand(ConfirmReservationCancel);

            OpenRegisterChargeEndAlarmHelpPopupCommand = new RelayCommand(OpenRegisterChargeEndAlarmHelpPopup);
            RegisterChargeEndAlarmCommand = new RelayCommand(RegisterChargeEndAlarm);
            ConfirmRegisterAlarmCommand = new RelayCommand(ConfirmRegisterAlarm);
            CancelRegisterAlarmCommand = new RelayCommand(CancelRegisterAlarm);


            ReceiptViewCommand = new RelayCommand(ReceiptView);
            BackToChargingCompleteCommand = new RelayCommand(BackToChargingComplete);

            CloseCreditCardReceiptCommand = new RelayCommand(CloseCreditCardReceipt);

            ConfirmChargeAmountCommand = new RelayCommand(ConfirmChargeAmount);
            CancelChargeAmountCommand = new RelayCommand(CancelChargeAmount);

            ContinueChargingCommand = new RelayCommand(ContinueCharging);
            ConfirmEndChargingCommand = new RelayCommand(ConfirmEndCharging);

            ConfrimCalcChargeAmountCommand = new RelayCommand(ConfirmCalcChargeAmount);

            CommonClosePopupCommand = new RelayCommand(CommonClosePopup);
            ClosePopupReportQrCodeCommand = new RelayCommand(CommonClosePopup);
            CancleWaitForConnectorPlugInCommand = new RelayCommand(CancleWaitForConnectorPlugIn);

            ContinueChargingCommand = new RelayCommand(ContinueCharging);
            ConfirmEndChargingCommand = new RelayCommand(ConfirmEndCharging);
        }

        private void HomeInitializeCharger(object param)
        {
            var now = DateTime.UtcNow;

            if (_isHomeInitializeClickLocked)
            {
                _logger.Info($"[HomeInitialize] ignored-inprogress - CH{_chargerChannel.ChannelNo}");
                return;
            }

            if ((now - _lastHomeInitializeAt) < _homeInitializeDebounce)
            {
                _logger.Info($"[HomeInitialize] ignored-debounce - CH{_chargerChannel.ChannelNo}");
                return;
            }

            _isHomeInitializeClickLocked = true;
            _lastHomeInitializeAt = now;
            IsHomeButtonEnabled = false;
            InitializeCharger(param, closePopup: true, invokedByHome: true);
        }

        private void RefreshChargerSequence()
        {
            // 무한 루프 방지
            if (_isRefreshing)
                return;
                
            _isRefreshing = true;
            try
            {
                if (CurrentChargeSequence != ChargeSequence.WaitReservation)
                {
                    DisposeReservationWaitingTimer();
                }

                DisposePaymentMethodSelectTimer();
                _paymentMethodSelectTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(AppSettingsManager.ChargerTimerSettings.PaymentMethodSelectViewTimer) };
                _paymentMethodSelectTimer.Tick += PaymentMethodSelectTimer_Tick;

                // SelectConnector 단계(초기화)로 돌아오거나 충전이 시작되면 소유권 해제
                // Charging 진입 시 해제: 이 채널은 이미 충전 시작 → 반대 채널이 독립적으로 결제 진행 가능하도록
                if (CurrentChargeSequence == ChargeSequence.SelectConnector ||
                    CurrentChargeSequence == ChargeSequence.WaitReservation ||
                    CurrentChargeSequence == ChargeSequence.Completed ||
                    CurrentChargeSequence == ChargeSequence.Charging)
                {
                    ReleaseDualChannelSelectOwnership();
                }

                // 다른 쪽이 SelectPaymentMethod나 PlugConnector 상태이고, 이쪽이 SelectConnector 상태일 때 대기 뷰 표시
                bool shouldShowWaitingOtherSide = false;
                if (_otherChargerViewModel != null && CurrentChargeSequence == ChargeSequence.SelectConnector)
                {
                    var otherSequence = _otherChargerViewModel.CurrentChargeSequence;
                    if (otherSequence == ChargeSequence.SelectPaymentMethod || otherSequence == ChargeSequence.PlugConnector)
                    {
                        shouldShowWaitingOtherSide = true;
                        _logger.Info($"[DualChannel][CH{_chargerChannel.ChannelNo}] 대기 오버레이 표시 — 상대방 채널 상태: {otherSequence}");
                    }
                }

                bool prevWaiting = _isWaitingOtherSide;
                IsWaitingOtherSide = shouldShowWaitingOtherSide && _waitingOtherSideView != null;
                if (prevWaiting != _isWaitingOtherSide)
                {
                    _logger.Info($"[DualChannel][CH{_chargerChannel.ChannelNo}] IsWaitingOtherSide 변경: {prevWaiting} → {_isWaitingOtherSide}");
                }

                switch (CurrentChargeSequence)
                {
                    case ChargeSequence.WaitReservation:
                        if(_reservationWaitView != null)
                        {
                            // 예약 대기 화면 표시 시 5분 타이머 시작
                            StartReservationWaitingTimer();
                            CurrentView = _reservationWaitView;
                        }
                        // PlugConnector 상태가 아니면 타이머 정리
                        DisposeReadyToChargingTimer();
                        break;
                    case  ChargeSequence.SelectConnector:
                        // 예약 대기 타이머 정리 (예약이 취소되거나 예약 번호 입력 시)
                        DisposeReservationWaitingTimer();
                        // PlugConnector 상태가 아니면 타이머 정리
                        DisposeReadyToChargingTimer();
                        // 이브이시스이고 싱글 채널이고 isTriple = Y이면 트리플 뷰 사용
                        // 이브이시스가 아니면 트리플이 체크되어있어도 기본 페이지 사용
                        bool isTriple = AppSettingsManager.ChargerSettings.ChargerManufacturerCode?.ToLower() == "evsis" &&
                                        AppSettingsManager.ChargerSettings.IsTriple == "Y" && 
                                        AppSettingsManager.ChargerSettings.MaxChannelCount == 1;
                        CurrentView = isTriple ? _tripleChargerSelectTypeView : _chargerSelectTypeView;
                        break;
                    case ChargeSequence.SelectPaymentMethod:
                        CurrentView = _paymentMethodSelectView;                  
                        _paymentMethodSelectTimer.Start();
                        // PlugConnector 상태가 아니면 타이머 정리
                        DisposeReadyToChargingTimer();
                        break;
                    case ChargeSequence.PlugConnector:
                        // 싱글 채널이고 채비 제조사인 경우 모델명에 따라 화면 선택
                        if (AppSettingsManager.ChargerSettings.MaxChannelCount == 1 && 
                            string.Equals(AppSettingsManager.ChargerSettings.ChargerManufacturerCode, "chaevi", StringComparison.OrdinalIgnoreCase))
                        {
                            string modelName = AppSettingsManager.ChargerSettings.ChaeviModelName ?? "";
                            string armMovableType = ChaeviModelMappingService.GetArmMovableType(modelName);
                            
                            if (armMovableType == "LR")
                            {
                                CurrentView = _chaeviReadyToChargeLRView;
                            }
                            else if (armMovableType == "UDLR")
                            {
                                CurrentView = _chaeviReadyToChargeUDLRView;
                            }
                            else
                            {
                                // 없음(NONE)이거나 기타: 일반 대기 페이지
                                CurrentView = _readyToChargingView;
                            }
                        }
                        else
                        {
                            // 듀얼 채널이거나 채비가 아닌 경우: 일반 대기 페이지
                            CurrentView = _readyToChargingView;
                        }
                        // 커넥터 연결 페이지 타이머 시작
                        StartReadyToChargingTimer();
                        break;
                    case ChargeSequence.Charging:
                        CurrentView = _progressView;
                        // 충전중 화면 진입 시 예약 개수 조회
                        LoadReservationCount();
                        // 충전 중일 때 홈 버튼 비활성화
                        IsHomeButtonEnabled = false;
                        // PlugConnector 상태가 아니면 타이머 정리
                        DisposeReadyToChargingTimer();
                        break;
                    case ChargeSequence.Completed:
                        if (_chargerChannel.CancelChargeAmount > 0)
                        {
                            CurrentView = _calChargeAmountView;
                        }
                        else
                        {
                            CurrentView = _completeView;
                           
                        }

                        _charger.InitStandby2(_chargerChannel.ChannelNo);
                        // PlugConnector 상태가 아니면 타이머 정리
                        DisposeReadyToChargingTimer();

                        break;
                }
                _otherChargerViewModel?.RefreshChargerSequence();
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void PaymentMethodSelectTimer_Tick(object sender, EventArgs e)
        {
            // 모달이 떠 있으면 타이머 실행하지 않음
            if (_parentViewModel.PopupView != null || _parentViewModel.IsDimmed)
            {
                _logger.Info("[UI] PaymentMethodSelectTimer_Tick fired but popup is open. Timer will continue.");
                return;
            }

            _logger.Info("[UI] PaymentMethodSelectTimer_Tick fired. Disposing timer and navigating to InitializeCharger (SelectConnector)");
            DisposePaymentMethodSelectTimer();
            InitializeCharger(null);
        }

        private void OpenConnectorHelp(object param)
        {
            if (param != null && int.TryParse(param.ToString(), out int connectorType))
            {
                switch (connectorType)
                {
                    case 0:
                        _parentViewModel.HelpAC3Popup(this);
                        break;
                    case 1:
                        _parentViewModel.HelpDCComboPopup(this);
                        break;
                    case 2:
                        _parentViewModel.HelpDCChademoPopup(this);
                        break;
                }
            }
            else 
            {
                switch (_chargerChannel.ChargingSelect)
                {
                    case 0:
                        _parentViewModel.HelpAC3Popup(this);
                        break;
                    case 1:
                        _parentViewModel.HelpDCComboPopup(this);
                        break;
                    case 2:
                        _parentViewModel.HelpDCChademoPopup(this);
                        break;
                }
            }
        }

        private void OpenChargingSpeedHelp(object param)
        {
            _parentViewModel.HelpChargingSpeedPopup(this);
        }

        private void SelectConnectorType(object param)
        {
            if (param != null && int.TryParse(param.ToString(), out int connectorType))
            {
                _selectedConnectorType = connectorType;
                _chargerChannel.ChargingSelect = connectorType;
                // 속성 변경 알림
                OnPropertyChanged(nameof(IsComboSelected));
                OnPropertyChanged(nameof(IsChademoSelected));
                OnPropertyChanged(nameof(IsAc3Selected));
                
                // 커넥터 타입 설정 후 결제 방식 선택으로 이동
                SelectPaymentMethod(null);
            }
        }

        private async void SelectPaymentMethod(object param)
        {
            string ts = DateTime.Now.ToString("HH:mm:ss.fff");
            _logger.Info($"[DualChannel][CH{_chargerChannel.ChannelNo}] SelectPaymentMethod 호출 — {ts} | 현재상태: {CurrentChargeSequence}");

            // ── 이중 진입 방지 (같은 채널에서 연속 탭) ──────────────────────────────
            if (_isSelectPaymentMethodInProgress)
            {
                _logger.Warn($"[DualChannel][CH{_chargerChannel.ChannelNo}] SelectPaymentMethod 중복 호출 무시 (이미 처리 중)");
                return;
            }

            // ── 듀얼 채널 동시 진입 방지 ────────────────────────────────────────────
            if (!TryAcquireDualChannelSelectOwnership())
            {
                // 상대방이 결제 단계 진행 중 → 대기 오버레이 표시
                IsWaitingOtherSide = true;
                _logger.Warn($"[DualChannel][CH{_chargerChannel.ChannelNo}] 소유권 획득 실패 — 대기 오버레이 표시");
                return;
            }

            _isSelectPaymentMethodInProgress = true;
            _logger.Info($"[DualChannel][CH{_chargerChannel.ChannelNo}] SelectPaymentMethod 처리 시작 (owner={_dualChannelSelectOwner})");

            try
            {
                // 상태를 먼저 변경해 두어 상대방 채널의 RefreshChargerSequence에서
                // IsWaitingOtherSide가 즉시 true가 되도록 보장
                CurrentChargeSequence = ChargeSequence.SelectPaymentMethod;
                _logger.Info($"[DualChannel][CH{_chargerChannel.ChannelNo}] 상태 SelectPaymentMethod 선점 완료");

                await _charger.ReadyToCharging(_chargerChannel.ChannelNo);
                _logger.Info($"[DualChannel][CH{_chargerChannel.ChannelNo}] ReadyToCharging 완료");

                _charger.SelectConnector(_chargerChannel.ChannelNo);

                _chargerChannel.BasePowerMeter = _charger.GetCurrentPowerMeter(_chargerChannel.ChannelNo);

                if (AppSettingsManager.EvCommSettings.EVSE_PayYN == "N")
                {
                    _logger.Info($"[DualChannel][CH{_chargerChannel.ChannelNo}] 무료 충전 — StartCharging 직행");
                    StartCharging(null);
                    return;
                }

                SoundService.Instance.PlaySoundAsync("select_payment_type.wav");
                _logger.Info($"[DualChannel][CH{_chargerChannel.ChannelNo}] 결제 방식 선택 화면 표시 완료");
            }
            catch (Exception ex)
            {
                _logger.Error($"[DualChannel][CH{_chargerChannel.ChannelNo}] SelectPaymentMethod 오류: {ex.Message}");
                ReleaseDualChannelSelectOwnership();
                InitializeCharger(null);
            }
            finally
            {
                _isSelectPaymentMethodInProgress = false;
            }
        }

        private async void StartCharging(object param)
        {
            if (_isStartChargingInProgress)
            {
                _logger.Info($"[UI] StartCharging ignored (already in progress). CH={_chargerChannel.ChannelNo}");
                return;
            }

            _isStartChargingInProgress = true;
            _logger.Info($"[UI] StartCharging accepted. CH={_chargerChannel.ChannelNo}, Sequence={CurrentChargeSequence}");
            try
            {
                await StartChargingAsync();
            }
            finally
            {
                _isStartChargingInProgress = false;
                _logger.Info($"[UI] StartCharging completed. CH={_chargerChannel.ChannelNo}, Sequence={CurrentChargeSequence}");
            }
        }

        private async Task StartChargingAsync()
        {
            try
            {
                SoundService.Instance.PlaySoundAsync("connect_coupler.wav");

                if (AppSettingsManager.ChargerSettings.ChargerManufacturerCode == "evsis")
                {
                    _parentViewModel.ClosePopup();

                    await RequestPlugConnector();

                    if (_chargerChannel.IsWaitForConnectorPlugInCancelled)
                    {
                        _logger.Info("IsWaitForConnectorPlugInCancelled is true. return");
                        return;
                    }
                    if (CurrentChargeSequence != ChargeSequence.PlugConnector)
                    {
                        _logger.Info("CurrentChargeSequence is not PlugConnector. return");
                        return;
                    }

                    _evsisRequestStartCharging = true;
                    SoundService.Instance.PlaySoundAsync("charge_ready.wav");

                    bool started = await WaitForChargingStartWithPopupAsync(timeoutSeconds: 100, loopDelayMs: 200, retryStartCommandInLoop: false);
                    if (!started)
                    {
                        return;
                    }

                    if (_chargerChannel.IsWaitForConnectorPlugInCancelled)
                    {
                        InitializeCharger(null);
                        return;
                    }

                    _isReservationAuth = false;
                    _chargerChannel.ChargingStartTime = DateTime.Now;
                    ChargeTime = "00:00";
                    PowerMeter = 0.0;
                    UpdateProgressInfo();
                    CurrentChargeSequence = ChargeSequence.Charging;
                    StartProgressTimer();
                    _charger.SendChargingStart(_chargerChannel.ChannelNo);

                    _evsisRequestStartCharging = true;
                    return;
                }

                _parentViewModel.ClosePopup();

                await RequestPlugConnector();

                _logger.Info("RequestPlugConnector End");
                _logger.Info("CurrentChargeSequence: " + CurrentChargeSequence);

                if (_chargerChannel.IsWaitForConnectorPlugInCancelled)
                {
                    _logger.Info("IsWaitForConnectorPlugInCancelled is true. return");
                    return;
                }
                if (CurrentChargeSequence != ChargeSequence.PlugConnector)
                {
                    _logger.Info("CurrentChargeSequence is not PlugConnector. return");
                    return;
                }

                _logger.Info("StartCharging...");
                _charger.StartCharging(_chargerChannel.ChannelNo);
                _logger.Info("parentViewModel.WaitingChargeStartPopup...");

                bool nonEvsisStarted = await WaitForChargingStartWithPopupAsync(timeoutSeconds: 100, loopDelayMs: 500, retryStartCommandInLoop: true);
                if (!nonEvsisStarted)
                {
                    return;
                }

                if (_chargerChannel.IsWaitForConnectorPlugInCancelled)
                {
                    try
                    {
                        await _charger.StopCharging(_chargerChannel.ChannelNo);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[UI] StopCharging failed after cancel (post-wait). Channel {_chargerChannel.ChannelNo}: {ex.Message}");
                    }
                    InitializeCharger(null);
                    return;
                }

                _isReservationAuth = false;
                _chargerChannel.ChargingStartTime = DateTime.Now;
                ChargeTime = "00:00";
                PowerMeter = 0.0;
                UpdateProgressInfo();
                CurrentChargeSequence = ChargeSequence.Charging;
                SoundService.Instance.PlaySoundAsync("charging.wav");
                StartProgressTimer();
                _charger.SendChargingStart(_chargerChannel.ChannelNo);
            }
            catch (Exception ex)
            {
                _logger.Error("[UI] StartChargingAsync() Error: " + ex.Message);
                DisposeWaitingChargeCountdownTimer();
                unchecked { _waitingCountdownSessionId++; }
                InitializeCharger(null);
                return;
            }
        }

        private void StartWaitingChargeCountdown(Stopwatch stopwatch, int timeoutSeconds, int sessionId)
        {
            WaitingChargeRemainSeconds = timeoutSeconds;
            WaitingChargeDisplaySeconds = timeoutSeconds;

            DisposeWaitingChargeCountdownTimer();

            _logger.Info($"[WaitCountdown] start. CH={_chargerChannel.ChannelNo}, Session={sessionId}, Timeout={timeoutSeconds}s, Count={WaitingChargeDisplaySeconds}");

            int lastLoggedRemain = -1;
            _waitingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _waitingTimerTickHandler = (s, e) =>
            {
                if (sessionId != _waitingCountdownSessionId)
                    return;

                int elapsed = (int)stopwatch.Elapsed.TotalSeconds;
                int remain = Math.Max(0, timeoutSeconds - elapsed);
                WaitingChargeRemainSeconds = remain;
                WaitingChargeDisplaySeconds = remain;

                if (remain != lastLoggedRemain)
                {
                    _logger.Info($"[WaitCountdown] tick. CH={_chargerChannel.ChannelNo}, Session={sessionId}, Count={remain}");
                    lastLoggedRemain = remain;
                }
            };
            _waitingTimer.Tick += _waitingTimerTickHandler;
            _waitingTimer.Start();
        }

        private void StopWaitingChargeCountdown(Stopwatch stopwatch, int sessionId)
        {
            stopwatch?.Stop();

            if (sessionId != _waitingCountdownSessionId)
            {
                _logger.Info($"[WaitCountdown] stop skipped (stale session). CH={_chargerChannel.ChannelNo}, Session={sessionId}, CurrentSession={_waitingCountdownSessionId}");
                return;
            }

            _logger.Info($"[WaitCountdown] stop. CH={_chargerChannel.ChannelNo}, Session={sessionId}, Remain={WaitingChargeDisplaySeconds}s");
            DisposeWaitingChargeCountdownTimer();
        }

        private async Task<bool> WaitForChargingStartWithPopupAsync(int timeoutSeconds, int loopDelayMs, bool retryStartCommandInLoop)
        {
            _parentViewModel.WaitingChargeStartPopup(this);
            DisposeReadyToChargingTimer();

            int sessionId = unchecked(++_waitingCountdownSessionId);
            _logger.Info($"[WaitCountdown] wait loop start. CH={_chargerChannel.ChannelNo}, Session={sessionId}, RetryStart={retryStartCommandInLoop}, LoopDelayMs={loopDelayMs}, Count={WaitingChargeDisplaySeconds}");

            var stopwatch = Stopwatch.StartNew();
            StartWaitingChargeCountdown(stopwatch, timeoutSeconds, sessionId);

            try
            {
                while (!_charger.CheckChargingStart(_chargerChannel.ChannelNo)
                       && stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
                {
                    if (_chargerChannel.IsWaitForConnectorPlugInCancelled)
                    {
                        _logger.Info($"[WaitCountdown] cancelled before charging-start check complete. CH={_chargerChannel.ChannelNo}, Session={sessionId}, Count={WaitingChargeDisplaySeconds}");
                        StopWaitingChargeCountdown(stopwatch, sessionId);
                        _parentViewModel.ClosePopup();
                        _parentViewModel.ConnectorErrorPopup(this);
                        InitializeCharger(null);
                        return false;
                    }

                    _logger.Info("StartCharging in while loop");
                    if (retryStartCommandInLoop)
                    {
                        _charger.StartCharging(_chargerChannel.ChannelNo);
                    }

                    await Task.Delay(loopDelayMs);

                    if (_chargerChannel.IsWaitForConnectorPlugInCancelled)
                    {
                        _logger.Info($"[WaitCountdown] cancelled after delay. CH={_chargerChannel.ChannelNo}, Session={sessionId}, Count={WaitingChargeDisplaySeconds}");
                        StopWaitingChargeCountdown(stopwatch, sessionId);
                        _parentViewModel.ClosePopup();
                        InitializeCharger(null);
                        return false;
                    }
                }

                StopWaitingChargeCountdown(stopwatch, sessionId);

                if (stopwatch.Elapsed.TotalSeconds >= timeoutSeconds)
                {
                    _logger.Warn($"[WaitCountdown] timeout. CH={_chargerChannel.ChannelNo}, Session={sessionId}, ElapsedSec={stopwatch.Elapsed.TotalSeconds:F1}, Count={WaitingChargeDisplaySeconds}");
                    InitializeCharger(null);
                    await Task.Delay(200);
                    _parentViewModel.ClosePopup();
                    _parentViewModel.ConnectorErrorPopup(this);
                    return false;
                }

                _logger.Info($"[WaitCountdown] charging-start detected. CH={_chargerChannel.ChannelNo}, Session={sessionId}, ElapsedSec={stopwatch.Elapsed.TotalSeconds:F1}, Count={WaitingChargeDisplaySeconds}");
                _parentViewModel.ClosePopup();
                return true;
            }
            finally
            {
                _logger.Info($"[WaitCountdown] wait loop end. CH={_chargerChannel.ChannelNo}, Session={sessionId}, Count={WaitingChargeDisplaySeconds}");
                StopWaitingChargeCountdown(stopwatch, sessionId);
            }
        }

        private async void CompleteCharging(object param, int chargeEndType = 0)
        {
            bool preserveEmergencyStopPopup = false;
            bool preservePopupOnChargingEnd = false;
            try
            {
                if (_isProgressChargingEnded) { return; }
                _isProgressChargingEnded = true;

                // 사용자가 직접 UI에서 버튼을 눌렀을 때만 팝업 표시
                // param이 "user"이거나 true인 경우에만 팝업 표시
                bool isUserAction = param != null && (param.ToString() == "user" || param is bool && (bool)param);
                
                // 충전 종료 확인 팝업이 떠있지 않은 경우
                // 선결제 금액이 0원 초과이고 아직 그 금액까지 충전되지 않았으면 모달 표시
                // 실제 충전 금액(_chargingCost)으로 체크
                // 사용자가 직접 버튼을 눌렀을 때만 팝업 표시
                if (isUserAction && !_isChargingEndConfirmPopupShown && _chargerChannel.UserSetChargeAmount > 0 && _chargingCost < _chargerChannel.UserSetChargeAmount)
                {
                    _isChargingEndConfirmPopupShown = true;
                    _parentViewModel.PopupChargingEndConfirm(this);
                    _isProgressChargingEnded = false;
                    return;  // 팝업 표시 후 종료
                }
                
                // 팝업을 표시하지 않는 경우 (자동 호출 또는 팝업에서 확인 버튼 클릭)
                if (isUserAction && _isChargingEndConfirmPopupShown)
                {
                    // 팝업에서 확인 버튼을 눌렀으므로 팝업 플래그 리셋
                    _isChargingEndConfirmPopupShown = false;
                }
                
                // 선결제 금액이 0원이거나 이미 충전 금액에 도달했으면 바로 충전 종료 시도
                _isChargingEndConfirmPopupShown = false;

                preserveEmergencyStopPopup = _charger.IsEmergency;
                preservePopupOnChargingEnd = preserveEmergencyStopPopup || chargeEndType == 4;

                // 충전 종료 로딩 팝업 표시
                SoundService.Instance.PlaySoundAsync("charge_end.wav");
                if (!preservePopupOnChargingEnd)
                    _parentViewModel.WaitingPopup(this);

                // 1. 충전 중지 타이머 및 UI 업데이트 중지
                DisposeProgressTimer();

                // 2. DSP에 충전 중지 요청
                // evsis와 로직 다르게 설정
                if (AppSettingsManager.ChargerSettings.ChargerManufacturerCode == "evsis")
                {
                    _evsisRequestStopCharging = true;
                    while (!_evsisRequestStopChargingFinished)
                    {
                        await Task.Delay(200);
                    }
                }
                else
                {
                    await _charger.StopCharging(_chargerChannel.ChannelNo);
                }

                // 3. 최종 충전 정보 업데이트 (동기적으로 처리하여 _chargingCost 계산 보장)
                ChargingInfo finalProgressInfo = _charger.GetChargingInfo(_chargerChannel.ChannelNo);
                double finalPowerMeter = finalProgressInfo.PowerMeter - _chargerChannel.BasePowerMeter;
                Debug.WriteLine($"[CompleteCharging] Final PowerMeter: {finalPowerMeter}");
                PowerMeter = finalPowerMeter;  // 이렇게 하면 _chargingCost가 계산됨
                _chargerChannel.FinalPowerMeter = PowerMeter * 1000.0;
                Progress = finalProgressInfo.Soc;
                Speed = finalProgressInfo.Current * finalProgressInfo.Voltage / 1000.0;

                // 4. 충전 종료 시간 기록
                _chargerChannel.ChargingEndTime = DateTime.Now;
                ChargeTime = FormatChargeTimeSeconds(_chargerChannel.ChargeTime);


                // 5. 최종 금액 계산 (과결제 시 부분 취소 로직 포함)
                CalcChargeAmount();
                
                // 6. UI 동기화 (명시적으로 다시 호출)
                OnPropertyChanged(nameof(FinalChargeAmount));
                OnPropertyChanged(nameof(CancelCost));
                OnPropertyChanged(nameof(DepositAmount));

                // 충전 종료 로딩 팝업 닫기 (충전 종료 페이지로 넘어가기 전). 비상정지 모달 유지 시에는 닫지 않음
                if (!preservePopupOnChargingEnd)
                    _parentViewModel.ClosePopup();

                // 6. UI 상태를 '충전 완료'로 변경
                CurrentChargeSequence = ChargeSequence.Completed;

                // 7. 커넥터 분리 요청
                await _charger.RequestUnplugConnector(_chargerChannel.ChannelNo);

                // 8. 서버에 충전 종료 및 알림 전송
                // 비상정지는 종료 구분을 항상 2로 우선 적용한다.
                int finalChargeEndType = chargeEndType;
                if (_charger.IsEmergency)
                {
                    finalChargeEndType = 2;
                }
                else if (finalChargeEndType == 0)
                {
                    finalChargeEndType = 0;
                }

                if (finalChargeEndType == 0)
                {
                    _charger.SendChargingEnd(_chargerChannel.ChannelNo, chargeEndType: 0);
                }
                else
                {
                    _charger.SendChargingEnd(_chargerChannel.ChannelNo, chargeEndType: finalChargeEndType);
                }
                _charger.SendChargingEndAlarm(_chargerChannel.ChannelNo);
                
                // 알림 전송 후 전화번호 초기화
                ChargeEndCallbackPhoneNumber = null;

                SoundService.Instance.PlaySoundAsync("disconnect_coupler.wav");
            }
            catch (Exception ex)
            {
                _logger.Error($"[UI] Error during CompleteCharging for channel {_chargerChannel.ChannelNo}: {ex.Message}");
                // 오류 발생 시 로딩 팝업 닫기 (비상정지 모달 유지 시 제외)
                if (!preservePopupOnChargingEnd)
                    _parentViewModel.ClosePopup();
                // 오류 발생 시에도 UI를 초기 상태로 되돌리도록 시도
                InitializeCharger(null, closePopup: !preservePopupOnChargingEnd);
            }
            finally
            {
                _isProgressChargingEnded = false;
            }
        }

        private async void InitializeCharger(object param, bool closePopup = true, bool invokedByHome = false)
        {
            int initializeVersion = Interlocked.Increment(ref _initializeVersion);
            Debug.WriteLine($"InitializeCharger v{initializeVersion}");

            try
            {
                bool shouldKeepConnectorWaitCancelled = false;

                // 충전 시작 대기 모달(WaitingChargingStart) 등이 떠 있는 상태에서 초기화만 되고 팝업이 남는 경우 제거
                DisposeWaitingChargeCountdownTimer();
                unchecked { _waitingCountdownSessionId++; }
                _isStartChargingInProgress = false;
                if (closePopup)
                    _parentViewModel.ClosePopup();

            // EVSIS 전용 로직 추가
            if (AppSettingsManager.ChargerSettings.ChargerManufacturerCode == "evsis")
            {
                _evsisRequestStartCharging = false;
            }

            // 커넥터 연결 페이지에서 홈버튼을 누른 경우 처리
            // 상태 확인을 먼저 하고 타이머는 나중에 정리
            if (CurrentChargeSequence == ChargeSequence.PlugConnector)
            {
                shouldKeepConnectorWaitCancelled = true;

                // 타이머 정리 (상태 확인 후)
                DisposeReadyToChargingTimer();

                _logger.Info($"[InitializeCharger] Channel {_chargerChannel.ChannelNo}: Home button pressed during PlugConnector state");

                // WaitForConnectorPlugIn 비동기 작업 취소
                _chargerChannel.IsWaitForConnectorPlugInCancelled = true;
                _logger.Info($"[InitializeCharger] Channel {_chargerChannel.ChannelNo}: Cancelled WaitForConnectorPlugIn");

                // 세션 삭제
                try
                {
                    ChargingSessionManager.DeleteSession(_chargerChannel.ChannelNo);
                    _logger.Info($"[InitializeCharger] Channel {_chargerChannel.ChannelNo}: Session deleted");
                }
                catch (Exception ex)
                {
                    _logger.Error($"[InitializeCharger] Channel {_chargerChannel.ChannelNo}: Error deleting session: {ex.Message}");
                }

                // Init() 호출 전에 선결제 정보 저장 (Init()에서 null로 초기화되기 때문)
                PaymentInfo savedPrePaymentInfo = _chargerChannel.PrePaymentInfo;
                int savedUserSetChargeAmount = _chargerChannel.UserSetChargeAmount;

                // 선결제가 되어있는지 확인
                if (savedPrePaymentInfo != null && savedUserSetChargeAmount > 0)
                {
                    _logger.Info($"[InitializeCharger] Channel {_chargerChannel.ChannelNo}: Canceling pre-payment and setting standby. Amount: {savedUserSetChargeAmount}");
                    
                    // 전체 금액 취소 설정
                    _chargerChannel.CancelChargeAmount = savedUserSetChargeAmount;
                    
                    // Init() 호출 전에 결제 취소 완료 (PrePaymentInfo가 null이 되기 전에)
                    try
                    {
                        await _charger.CancelPay(_chargerChannel);
                        _logger.Info($"[InitializeCharger] Channel {_chargerChannel.ChannelNo}: Pre-payment canceled successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[InitializeCharger] Channel {_chargerChannel.ChannelNo}: Error during payment cancel: {ex.Message}");
                    }
                }
            }
            else
            {
                // PlugConnector 상태가 아닐 때는 타이머만 정리
                DisposeReadyToChargingTimer();
            }

            if (_chargerChannel.CurrentSequence != ChargeSequence.SelectConnector)
            {
                _isEnterCouplerPage = true;
            }
            else
            {
                _isEnterCouplerPage = false;
            }

            if (AppSettingsManager.ChargerSettings.ChargerManufacturerCode != "evsis")
            {
                _charger.StopChargingold2(_chargerChannel.ChannelNo);
                _charger.InitCharger(_chargerChannel.ChannelNo);
                _chargerChannel.Init();
                if (shouldKeepConnectorWaitCancelled)
                {
                    _chargerChannel.IsWaitForConnectorPlugInCancelled = true;
                }
                _charger.InitStandby2(_chargerChannel.ChannelNo);
            }
            else 
            {
                _chargerChannel.Init();
                if (shouldKeepConnectorWaitCancelled)
                {
                    _chargerChannel.IsWaitForConnectorPlugInCancelled = true;
                }
            }
            // 채널 초기화 후 ViewModel 설정금액 UI도 함께 초기화 (이전 결제 금액이 남는 것 방지)
            UserSetCost = 0;
            // 커넥터 타입 속성 업데이트 알림
            OnPropertyChanged(nameof(ConnectorTypeIcon));
            OnPropertyChanged(nameof(ConnectorTypeText));

            if (initializeVersion != _initializeVersion)
            {
                _logger.Info($"[InitializeCharger] stale-result skipped before UI apply. CH={_chargerChannel.ChannelNo}, version={initializeVersion}, latest={_initializeVersion}");
                return;
            }

            string phoneNo = null;
            string reservationNo = null;
            bool hasReservation = false;

            if (!_isReservationAuth)
            {
                var reservationTask = Task.Run(() => _charger.SendGetResvStation(_chargerChannel.ChannelNo, out phoneNo, out reservationNo));
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3));
                var completedTask = await Task.WhenAny(reservationTask, timeoutTask);

                if (completedTask == reservationTask)
                {
                    hasReservation = reservationTask.Result;
                }
                else
                {
                    _logger.Warn($"[InitializeCharger] SendGetResvStation timeout (3s). CH={_chargerChannel.ChannelNo}");
                }
            }

            if (!_isReservationAuth && hasReservation)
            {
                if (initializeVersion != _initializeVersion)
                {
                    _logger.Info($"[InitializeCharger] stale-result skipped in reservation branch. CH={_chargerChannel.ChannelNo}, version={initializeVersion}, latest={_initializeVersion}");
                    return;
                }

                if (string.IsNullOrEmpty(_chargerChannel.ReservationPhoneNo)
                    || string.IsNullOrEmpty(_chargerChannel.ReservationNo)
                    || !_chargerChannel.IsReservationSmsSent)
                {
                    IsHomeButtonEnabled = false;
                    _chargerChannel.ReservationPhoneNo = phoneNo;
                    _chargerChannel.ReservationNo = reservationNo;
                    _chargerChannel.IsReservationSmsSent = _charger.SendSendSmsResvInfo(_chargerChannel.ChannelNo, phoneNo, reservationNo);
                    OnPropertyChanged(nameof(ReservationWaitingPhoneLast4));
                    OnPropertyChanged(nameof(ReservationWaitingText));
                }

                // 예약 정보가 이미 존재하더라도, 화면 표시 직전에 텍스트 갱신 알림
                OnPropertyChanged(nameof(ReservationWaitingPhoneLast4));
                OnPropertyChanged(nameof(ReservationWaitingText));

                _chargerChannel.CurrentSequence = ChargeSequence.WaitReservation;
                SoundService.Instance.StopSound();
                _logger.Info($"[DualChannel][CH{_chargerChannel.ChannelNo}] InitializeCharger → WaitReservation 전환, 소유권 해제");
                ReleaseDualChannelSelectOwnership();
                CurrentChargeSequence = ChargeSequence.WaitReservation;
                IsHomeButtonEnabled = false;
            }
            else
            {
                if (initializeVersion != _initializeVersion)
                {
                    _logger.Info($"[InitializeCharger] stale-result skipped in select-connector branch. CH={_chargerChannel.ChannelNo}, version={initializeVersion}, latest={_initializeVersion}");
                    return;
                }

                _logger.Info($"[DualChannel][CH{_chargerChannel.ChannelNo}] InitializeCharger → SelectConnector 전환, 소유권 해제");
                ReleaseDualChannelSelectOwnership();
                CurrentChargeSequence = ChargeSequence.SelectConnector;
                IsHomeButtonEnabled =  true;
                _chargerChannel.InitReservationInfo();
                OnPropertyChanged(nameof(ReservationWaitingPhoneLast4));
                OnPropertyChanged(nameof(ReservationWaitingText));
                if (_isEnterCouplerPage)
                {
                    SoundService.Instance.PlaySoundAsync("select_coupler.wav");
                }
            }
            }
            finally
            {
                if (invokedByHome)
                {
                    _isHomeInitializeClickLocked = false;
                }
            }
        }

        private async void SelectRFCard(object param)
        {
            if (!EnsurePaymentReaderReady(nameof(SelectRFCard)))
            {
                return;
            }

            _chargerChannel.PaymentMethod = PaymentMethod.RfCard;
            
            _parentViewModel.PopupTagRFCard(this);

            SoundService.Instance.PlaySoundAsync("cardreader_member_card.wav");

#if true
            await _charger.ReadRfCard(_chargerChannel);

            _parentViewModel.ClosePopup();
            
            // 회원 카드 읽기 실패 확인 (단말기 연결 안되어 있으면 빈 값 반환)
            if (string.IsNullOrEmpty(_chargerChannel.MembershipNo))
            {
                // 사용자가 취소 버튼을 눌러 중단된 경우 실패 팝업 없이 종료
                if (_isRfCardCancelledByUser)
                {
                    _isRfCardCancelledByUser = false;
                    IsPaymentCancelButtonEnabled = true;
                    return;
                }

                _logger.Warn("[SelectRFCard] Failed to read membership card. MembershipNo is empty.");
                SetDefaultPaymentFailMessage();
                _parentViewModel.PaymentFailPopup(this);
                
                return;
            }
            
            UserSetCost = 0;

            Boolean authSuccess = await _charger.RequestMemberCardAuth(_chargerChannel.ChannelNo);

            if (authSuccess)
                _parentViewModel.AuthSuccessPopup(this);
            else
                _parentViewModel.AuthFailPopup(this);


#else
            // await _charger.ReadRfCard(_chargerChannel);

            // _parentViewModel.ClosePopup();

            UserSetCost = 0;

            //Boolean authSuccess = await _charger.RequestMemberCardAuth(_chargerChannel.ChannelNo);
            Boolean authSuccess = false;
            authSuccess = true;
            if (authSuccess)
                _parentViewModel.AuthSuccessPopup(this);
            else
                _parentViewModel.AuthFailPopup(this);
#endif

        }

        private bool EnsurePaymentReaderReady(string source)
        {
            var sw = Stopwatch.StartNew();
            bool isConnected = _charger.IsPaymentServiceConnected;
            sw.Stop();
            string paymentManufacturer = AppSettingsManager.ChargerSettings.PaymentManufacturerCode ?? "unknown";

            _logger.Info($"[{source}] 단말기 상태 체크(스냅샷) 응답 경과: {sw.ElapsedMilliseconds}ms — manufacturer: {paymentManufacturer}, connected: {isConnected}");

            if (isConnected)
            {
                return true;
            }

            _logger.Warn($"[{source}] 단말기 상태 체크 실패 — 응답 경과: {sw.ElapsedMilliseconds}ms, manufacturer: {paymentManufacturer}, connected: {isConnected}");
            SetTerminalErrorPaymentFailMessage();
            _parentViewModel.PaymentFailPopup(this);
            return false;
        }

        private void SetDefaultPaymentFailMessage()
        {
            PaymentFailTitle = "결제가 실패했습니다";
            PaymentFailMessage = "결제가 실패했습니다.\n다시 시도해 주세요.";
        }

        private void SetTerminalErrorPaymentFailMessage()
        {
            PaymentFailTitle = "단말기 장애";
            PaymentFailMessage = "단말기 장애가 발생했습니다.\n다시 시도해 주세요.";
        }

        private async Task RequestPlugConnector()
        {
            // 이전 타이머 정리
            DisposeReadyToChargingTimer();

            _logger.Info("RequestPlugConnector OpenDoor...");

            await _charger.OpenDoor(_chargerChannel.ChannelNo);

            _logger.Info("RequestPlugConnector CurrentChargeSequence: " + CurrentChargeSequence);

            CurrentChargeSequence = ChargeSequence.PlugConnector;

            _charger.SetChargePrepare(_chargerChannel.ChannelNo);

            _logger.Info("RequestPlugConnector CurrentChargeSequence: " + CurrentChargeSequence);

            // 결제 완료 후 커넥터 연결 페이지로 전환 시 세션 저장
            // 결제 정보가 있으면 (PrePaymentInfo 또는 QrTid) 세션 저장
            bool isPaymentCompleted = _chargerChannel.PrePaymentInfo != null || 
                                      (_chargerChannel.PaymentMethod == PaymentMethod.QrCode && !string.IsNullOrEmpty(_chargerChannel.QrTid));
            
            if (isPaymentCompleted)
            {
                try
                {
                    // 현재 전력량 가져오기
                    double currentEnergy = _charger.GetCurrentPowerMeter(_chargerChannel.ChannelNo);
                    
                    // BasePowerMeter가 0이면 현재 전력량을 사용 (충전 시작 전이므로 StartEnergy = LastEnergy)
                    if (_chargerChannel.BasePowerMeter <= 0 && currentEnergy > 0)
                    {
                        _chargerChannel.BasePowerMeter = currentEnergy;
                    }
                    
                    // StartTime이 설정되지 않았으면 현재 시간 사용
                    if (_chargerChannel.ChargingStartTime == DateTime.MaxValue)
                    {
                        _chargerChannel.ChargingStartTime = DateTime.Now;
                    }
                    
                    // 세션 상태 저장 (PlugConnector 상태로)
                    ChargingSessionManager.SaveSession(_chargerChannel, currentEnergy, "PlugConnector");
                    _logger.Info($"[RequestPlugConnector] Channel {_chargerChannel.ChannelNo}: Session saved with payment info - StartEnergy={_chargerChannel.BasePowerMeter}, LastEnergy={currentEnergy}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"[RequestPlugConnector] Failed to save session for channel {_chargerChannel.ChannelNo}: {ex.Message}");
                }
            }

            // 취소 플래그 초기화
            _chargerChannel.IsWaitForConnectorPlugInCancelled = false;

            _logger.Info("RequestPlugConnector WaitForConnectorPlugIn...");
            
            await _charger.WaitForConnectorPlugIn(_chargerChannel.ChannelNo);

            _logger.Info("RequestPlugConnector WaitForConnectorPlugIn End");
        }
        private  void  StartProgressTimer()
        {
            DisposeProgressTimer();

            _chargeStopwatch = new Stopwatch();
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _progressTimer.Tick += async (s, e) =>
            {
                _logger.Info("StartProgressTimer CheckChargingRun");
                bool isKlinelex = string.Equals(AppSettingsManager.ChargerSettings.ChargerManufacturerCode, "klinelex", StringComparison.OrdinalIgnoreCase);
                if (isKlinelex && _charger.IsEmergency)
                {
                    // 클린일렉스는 비상정지 시 MCU가 강제 종료를 보장하지 않아 UI에서 종료 시퀀스를 직접 시작한다.
                    CompleteCharging(null, chargeEndType: 2);
                    return;
                }

                if (_charger.CheckChargingRun(_chargerChannel.ChannelNo))
                {
                    UpdateProgressInfo();

                    // 선결제 금액만큼 충전되었거나 40분 경과 시 자동 종료
                    // 실제 충전 금액(_chargingCost)으로 체크 (UI 표시용 ChargingCost가 아닌 실제 값)
                    if (_chargeStopwatch.Elapsed.Minutes >= AppSettingsManager.ChargerOperationSettings.ChargeLimitTime
                        || (_chargerChannel.UserSetChargeAmount > 0 && _chargerChannel.UserSetChargeAmount <= _chargingCost))
                    {
                        // 팝업이 표시되어 있으면 닫기
                        if (_isChargingEndConfirmPopupShown)
                        {
                            _isChargingEndConfirmPopupShown = false;
                            _parentViewModel.ClosePopup();
                        }
                        CompleteCharging(null);
                    }
                }
                else
                {
                    // DSP에서 충전이 중지된 것을 감지하면 완료 처리

                    CompleteCharging(null);
                }

            };

            _progressSendTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _progressSendTimer.Tick += async (s, e) =>
            {
                _charger.SendChargingProgress(_chargerChannel.ChannelNo);
            };

            // ★ 충전시간 UI 표시 전용 타이머 (DSP 통신과 완전 독립)
            _chargeTimeDisplayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _chargeTimeDisplayTimer.Tick += (s, e) =>
            {
                if (_chargeStopwatch != null && _chargeStopwatch.IsRunning)
                {
                    ChargeTime = _chargeStopwatch.Elapsed.ToString(@"mm\:ss");
                }
            };

            _progressTimer.Start();
            _chargeStopwatch.Start();
            _progressSendTimer.Start();
            _chargeTimeDisplayTimer.Start();  // ★ 독립 타이머 시작
        }

        private static string FormatChargeTimeSeconds(int totalSeconds)
        {
            if (totalSeconds <= 0) return "00:00";

            int totalMinutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{totalMinutes:D2}:{seconds:D2}";
        }

        private bool CalcChargeAmount()
        {
            bool result = false;

            if (_chargerChannel.PaymentMethod == PaymentMethod.IcCard ||
                _chargerChannel.PaymentMethod == PaymentMethod.SamsungPay)
            {
                // 실제 충전 금액 사용 (UI 표시용이 아닌 실제 계산값)
                result = _chargingCost < _chargerChannel.UserSetChargeAmount;

                if (result)
                {
                    _chargerChannel.CancelChargeAmount = _chargerChannel.UserSetChargeAmount - _chargingCost;
                    _chargerChannel.ChargeAmount = _chargingCost;
                    _charger.CancelPay(_chargerChannel);
                    CancelCost = _chargerChannel.CancelChargeAmount;
                }
                else
                {
                    _chargerChannel.CancelChargeAmount = 0;
                    _chargerChannel.ChargeAmount = _chargerChannel.UserSetChargeAmount;
                }
            }
            else
            {
                _chargerChannel.UserSetChargeAmount = 0;
                _chargerChannel.CancelChargeAmount = 0;
                _chargerChannel.ChargeAmount = _chargingCost;
            }

            OnPropertyChanged(nameof(DepositAmount));
            OnPropertyChanged(nameof(FinalChargeAmount));

            return result;
        }
        private void DisposeProgressTimer()
        {
            // ★ 충전시간 표시 전용 타이머 정리
            if (_chargeTimeDisplayTimer != null)
            {
                _chargeTimeDisplayTimer.Stop();
                _chargeTimeDisplayTimer = null;
            }

            if (_progressTimer != null && _progressTimer.IsEnabled)
            {
                _progressTimer.Stop();
                _progressTimer = null;
            }

            if (_chargeStopwatch != null && _chargeStopwatch.IsRunning)
            {
                _chargeStopwatch.Stop();
                _chargeStopwatch = null;
            }

            if (_progressSendTimer != null && _progressSendTimer.IsEnabled)
            {
                _progressSendTimer.Stop();
                _progressSendTimer = null;
            }
        }

        private void DisposePaymentMethodSelectTimer()
        {
            if (_paymentMethodSelectTimer != null && _paymentMethodSelectTimer.IsEnabled)
            {
                _logger.Info("[UI] Disposing PaymentMethodSelectTimer");
                _paymentMethodSelectTimer.Stop();
                _paymentMethodSelectTimer = null;
            }
        }

        private void StartReservationWaitingTimer()
        {
            DisposeReservationWaitingTimer();
            
            _reservationWaitingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(300) }; // NOTE: RESV TIMER
            _reservationWaitingTimer.Tick += ReservationWaitingTimer_Tick;
            _reservationWaitingTimer.Start();
            _logger.Info("[UI] Reservation waiting timer started (5 minutes)");
        }

        private async void ReservationWaitingTimer_Tick(object sender, EventArgs e)
        {
            if (CurrentChargeSequence != ChargeSequence.WaitReservation)
            {
                _logger.Warn($"[UI] Reservation waiting timer fired outside WaitReservation (CurrentChargeSequence={CurrentChargeSequence}). Disposing timer and ignoring.");
                DisposeReservationWaitingTimer();
                return;
            }

            _logger.Info("[UI] Reservation waiting timer expired. Auto-cancelling reservation due to no response.");
            DisposeReservationWaitingTimer();
            
            // 예약 취소 로직 실행
            if (!string.IsNullOrEmpty(_chargerChannel.ReservationPhoneNo))
            {
                bool cancelOk = _charger.SendCancelReservation(_chargerChannel.ChannelNo, _chargerChannel.ReservationPhoneNo);
                if (cancelOk)
                {
                    _charger.SendSendSmsResvCancel(_chargerChannel.ChannelNo, _chargerChannel.ReservationPhoneNo);
                }
                // 취소 요청 후 서버 처리 시간을 고려하여 약간 대기
                await Task.Delay(500);
            }
            
            // 예약 상태 다시 확인하여 취소 반영 여부 확인
            _isReservationAuth = false;
            _chargerChannel.InitReservationInfo();
            OnPropertyChanged(nameof(ReservationWaitingPhoneLast4));
            OnPropertyChanged(nameof(ReservationWaitingText));

            // 자동 취소 팝업 표시 (큰 팝업)
            _parentViewModel.PopupAutoCancelReservation(this);
        }

        private void DisposeReservationWaitingTimer()
        {
            if (_reservationWaitingTimer != null && _reservationWaitingTimer.IsEnabled)
            {
                _logger.Info("[UI] Disposing ReservationWaitingTimer");
                _reservationWaitingTimer.Stop();
                _reservationWaitingTimer = null;
            }
        }

        private void StartReadyToChargingTimer()
        {
            DisposeReadyToChargingTimer();
            
            int timerSeconds = AppSettingsManager.ChargerTimerSettings.ReadyToChargingViewTimer;
            _readyToChargingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(timerSeconds) };
            _readyToChargingTimer.Tick += ReadyToChargingTimer_Tick;
            _readyToChargingTimer.Start();
            _logger.Info($"[UI] ReadyToCharging timer started ({timerSeconds} seconds)");
        }

        private void ReadyToChargingTimer_Tick(object sender, EventArgs e)
        {
            // 모달이 떠 있으면 타이머 실행하지 않음
            if (_parentViewModel.PopupView != null || _parentViewModel.IsDimmed)
            {
                _logger.Info("[UI] ReadyToChargingTimer_Tick fired but popup is open. Timer will continue.");
                return;
            }

            _logger.Info("[UI] ReadyToChargingTimer_Tick fired. Navigating to InitializeCharger (same as home button)");
            DisposeReadyToChargingTimer();

            // 홈 버튼을 누른 것과 동일하게 동작 - InitializeCharger가 PlugConnector 상태일 때의 모든 처리를 수행
            InitializeCharger(null);
        }

        private void DisposeReadyToChargingTimer()
        {
            if (_readyToChargingTimer != null && _readyToChargingTimer.IsEnabled)
            {
                _logger.Info("[UI] Disposing ReadyToChargingTimer");
                _readyToChargingTimer.Stop();
                _readyToChargingTimer = null;
            }
        }

        private void DisposeWaitingChargeCountdownTimer()
        {
            if (_waitingTimer != null)
            {
                if (_waitingTimerTickHandler != null)
                {
                    _waitingTimer.Tick -= _waitingTimerTickHandler;
                    _waitingTimerTickHandler = null;
                }

                _waitingTimer.Stop();
                _waitingTimer = null;
                _logger.Info($"[WaitCountdown] timer disposed. CH={_chargerChannel.ChannelNo}, CurrentSession={_waitingCountdownSessionId}");
            }
        }


        private async void LoadReservationCount()
        {
            try
            {
                string stationId = AppSettingsManager.ChargerSettings.StationId;
                if (string.IsNullOrEmpty(stationId))
                {
                    _logger.Warn("[UI] StationId is empty. Cannot load reservation count.");
                    return;
                }

                // 네트워크 호출을 백그라운드 스레드에서 실행하여 UI 스레드 블로킹 방지
                JObject response = await Task.Run(() => _evCommService.SendResvCnt(stationId));
                if (response != null)
                {
                    string resvCntStr = jSonParser.GetJSonData(response, "resv_cnt");
                    if (!string.IsNullOrEmpty(resvCntStr) && int.TryParse(resvCntStr, out int count))
                    {
                        // UI 업데이트는 Dispatcher를 통해 수행
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ReservationCount = count;
                        });
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ReservationCount = 0;
                        });
                        _logger.Warn("[UI] Failed to parse reservation count from response");
                    }
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ReservationCount = 0;
                    });
                    _logger.Warn("[UI] No response from SendResvCnt");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[UI] Error loading reservation count: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ReservationCount = 0;
                });
            }
        }

        private void UpdateProgressInfo()
        {
            // DSP 제어 서비스 호출을 백그라운드 스레드에서 실행하여 UI 스레드 블로킹 방지
            Task.Run(() =>
            {
                try
                {
                    ChargingInfo progressInfo = _charger.GetChargingInfo(_chargerChannel.ChannelNo);

                    // UI 업데이트는 Dispatcher를 통해 수행
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // ========== 시간대별 요금 자동 적용 ==========
                        int currentHour = DateTime.Now.Hour;
                        float hourlyUnitCost = AppSettingsManager.ChargerOperationSettings.PriceForHour[currentHour];

                        // 현재 시간의 단가와 적용 중인 단가가 다르면 구간 변경
                        if (Math.Abs(hourlyUnitCost - _chargerChannel.CurrentUserUnitCost) > 0.01f)
                        {
                            if (_chargerChannel.BasePowerMeter <= 0 && progressInfo.PowerMeter > 0)
                            {
                                _chargerChannel.BasePowerMeter = progressInfo.PowerMeter;
                            }

                            double currentPowerMeter = progressInfo.PowerMeter - _chargerChannel.BasePowerMeter;

                            // 현재 구간의 금액 계산 (1원 자리 버림)
                            int currentSegmentCost = MoneyUtil.TruncateWonUnit(
                                (int)((currentPowerMeter - _chargerChannel.CurrentSegmentStartPowerMeter) 
                                      * _chargerChannel.CurrentUserUnitCost)
                            );

                            // 이력 저장
                            _chargerChannel.UnitCostChangeHistory.Add(new UnitCostChangeRecord
                            {
                                PowerMeter = currentPowerMeter,
                                UnitCost = _chargerChannel.CurrentUserUnitCost,
                                AccumulatedCost = _chargerChannel.AccumulatedCostBeforeCurrentSegment + currentSegmentCost
                            });

                            // 누적 금액 갱신
                            _chargerChannel.AccumulatedCostBeforeCurrentSegment += currentSegmentCost;

                            // 새 구간 시작 설정
                            _chargerChannel.CurrentSegmentStartPowerMeter = currentPowerMeter;
                            _chargerChannel.CurrentUserUnitCost = hourlyUnitCost;

                            _logger.Info($"[시간대별 요금 적용] Hour: {currentHour}, " +
                                        $"Previous UnitCost → New UnitCost: {_chargerChannel.UnitCostChangeHistory.Last().UnitCost:F2} → {hourlyUnitCost:F2}, " +
                                        $"PowerMeter: {currentPowerMeter:F2}, " +
                                        $"Accumulated: {_chargerChannel.AccumulatedCostBeforeCurrentSegment}원");
                        }
                        // ========== 시간대별 요금 자동 적용 끝 ==========

                        Progress = progressInfo.Soc;
                        // 실시간 표시용 전력량은 "충전량(delta)" 기준.
                        // BasePowerMeter(처음값)가 0이면, 최초 유효 PowerMeter(>0) 수신 시 기준값으로 확정한다.
                        if (_chargerChannel.BasePowerMeter <= 0 && progressInfo.PowerMeter > 0)
                        {
                            _chargerChannel.BasePowerMeter = progressInfo.PowerMeter;
                            _logger.Info($"[StartProgressTimer] Channel {_chargerChannel.ChannelNo}: BasePowerMeter was 0, set to progressInfo.PowerMeter={progressInfo.PowerMeter:F4}");
                        }

                        PowerMeter = Math.Max(0.0, progressInfo.PowerMeter - _chargerChannel.BasePowerMeter);
                        Speed = progressInfo.Current * progressInfo.Voltage / 1000.0;

                        OnPropertyChanged(nameof(BatteryAfterCharging));
                    });

                    _logger.Info("Voltage:" + progressInfo.Voltage +" | Current:"+progressInfo.Current+" | SOC:"+progressInfo.Soc +" | PowerMeter:"+ progressInfo.PowerMeter);

                    // 충전 중일 때 주기적으로 세션 저장 (0.5초마다 호출)
                    if (CurrentChargeSequence == ChargeSequence.Charging)
                    {
                        try
                        {
                            // 실제 전력량은 progressInfo.PowerMeter (BasePowerMeter 포함)
                            double currentEnergy = progressInfo.PowerMeter;
                            ChargingSessionManager.SaveSession(_chargerChannel, currentEnergy, "Charging");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[UpdateProgressInfo] Failed to save session for channel {_chargerChannel.ChannelNo}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"[UpdateProgressInfo] Error: {ex.Message}");
                }
            });
        }
        private async void SelectICCard(object param)
        {
            if (!EnsurePaymentReaderReady(nameof(SelectICCard)))
            {
                return;
            }

            _chargerChannel.PaymentMethod = PaymentMethod.IcCard;
            await GetNonMemberUnitCost();
            ChargeAmountPopup(param);

        }
        private async Task GetNonMemberUnitCost()
        {
            try
            {
                // 네트워크 호출을 백그라운드 스레드에서 실행하여 UI 스레드 블로킹 방지
                JObject response = await Task.Run(() => _charger.GetNonMemberUnitCost(_chargerChannel.StationId, _chargerChannel.ChargerId));

                if (response != null)
                {
                    JSonParser jsonParser = new JSonParser();
                    string responseReceive = jsonParser.GetJSonData(response, "response_receive");

                    if (responseReceive == "1")
                    {
                        // 단가 정보 파싱 및 적용
                        string unitCostStr = jsonParser.GetJSonData(response, "current_unit_cost");
                        if (!string.IsNullOrEmpty(unitCostStr) && float.TryParse(unitCostStr, out float unitCost))
                        {
                            _chargerChannel.CurrentUserUnitCost = unitCost;
                            _logger.Info($"[IC CARD] Non-member unit cost retrieved: {unitCost}");
                        }
                        else
                        {
                            // 서버에서 파싱 실패 시 현재 시간대 요금 사용
                            int currentHour = DateTime.Now.Hour;
                            _chargerChannel.CurrentUserUnitCost = AppSettingsManager.ChargerOperationSettings.PriceForHour[currentHour];
                            _logger.Warn($"[IC CARD] Failed to parse unit cost, using hourly rate for {currentHour}:00 = {_chargerChannel.CurrentUserUnitCost}");
                        }
                    }
                    else
                    {
                        // 서버 응답 실패 시 현재 시간대 요금 사용
                        int currentHour = DateTime.Now.Hour;
                        _chargerChannel.CurrentUserUnitCost = AppSettingsManager.ChargerOperationSettings.PriceForHour[currentHour];
                        _logger.Warn($"[IC CARD] Server response failed, using hourly rate for {currentHour}:00 = {_chargerChannel.CurrentUserUnitCost}");
                    }
                }
                else
                {
                    // 통신 실패 시 현재 시간대 요금 사용
                    int currentHour = DateTime.Now.Hour;
                    _chargerChannel.CurrentUserUnitCost = AppSettingsManager.ChargerOperationSettings.PriceForHour[currentHour];
                    _logger.Error($"[IC CARD] Failed to communicate with server, using hourly rate for {currentHour}:00 = {_chargerChannel.CurrentUserUnitCost}");
                }
            }
            catch (Exception ex)
            {
                // 예외 발생 시 현재 시간대 요금 사용
                int currentHour = DateTime.Now.Hour;
                _chargerChannel.CurrentUserUnitCost = AppSettingsManager.ChargerOperationSettings.PriceForHour[currentHour];
                _logger.Error($"[IC CARD] Exception during unit cost retrieval: {ex.Message}, using hourly rate for {currentHour}:00 = {_chargerChannel.CurrentUserUnitCost}");
            }
        }
        private async void SelectSamsungpay(object param)
        {
            if (!EnsurePaymentReaderReady(nameof(SelectSamsungpay)))
            {
                return;
            }

            _chargerChannel.PaymentMethod = PaymentMethod.SamsungPay;
            await GetNonMemberUnitCost();
            ChargeAmountPopup(param);
        }

        private async void SelectQRAuth(object param)
        {
            _chargerChannel.PaymentMethod = PaymentMethod.QrCode;
            _parentViewModel.PopupQrCode(this);
#if false
            ////test          
            _parentViewModel.AuthSuccessPopup(this);
            ///
#endif
        }

        private void ReserveCharger(object param)
        {
            _parentViewModel.PopupReserveCharger(this);
        }

        private void ConfirmReservationPhoneNumber(object param)
        {
            _reservationCallbackPhoneNumber = param as string;

            bool retVal = _charger.SendReservationCharger(_chargerChannel.ChannelNo, _reservationCallbackPhoneNumber);

            if (retVal)
            {
                _parentViewModel.PopupReservationComplete(this, true);
                // 예약 완료 후 예약 개수 갱신
                LoadReservationCount();
            }
            else
            {
                _parentViewModel.PopupReservationFail(this);
            }
        }

        private void CancelReservation(object param)
        {
            _reservationCallbackPhoneNumber = null;
            _parentViewModel.ClosePopup();
        }



        private bool _isReservationCancelAction;
        private bool _isReservationAuth = false;
        private void SelectUseReservation(object param)
        {
            _isReservationCancelAction = false;
            _parentViewModel.PopupInputReservationNumber(this, _isReservationCancelAction);
        }

        private void SelectCancelReservation(object param)
        {
            _isReservationCancelAction = true;
            _parentViewModel.PopupInputReservationNumber(this, _isReservationCancelAction);
        }

        private void ShowReservationDescription(object param)
        {
            _parentViewModel.PopupReservationDescription(this);
        }

        private void InputReservationNo(object param) 
        {
            // 예약 번호 입력 시 타이머 정리
            DisposeReservationWaitingTimer();
            
            string reservationNumber = param as string;
            if (reservationNumber == null || reservationNumber.Length != 4) return;

            if(_chargerChannel.ReservationNo != reservationNumber)
            {
                _parentViewModel.PopupWrongReservationNumber(this);
            }
            else
            {
                _charger.SendAuthReservation(_chargerChannel.ChannelNo, _chargerChannel.ReservationPhoneNo, _chargerChannel.ReservationNo);
                _parentViewModel.ClosePopup();
                _isReservationAuth = true;
                InitializeCharger(null);
            }
        }
        private async void CancelReservationNo(object param) 
        {
            string reservationNumber = param as string;
            if (reservationNumber == null || reservationNumber.Length != 4) return;

            if (_chargerChannel.ReservationNo != reservationNumber)
            {
                _parentViewModel.PopupWrongReservationNumber(this);
            }
            else
            {
                bool cancelOk = _charger.SendCancelReservation(_chargerChannel.ChannelNo, _chargerChannel.ReservationPhoneNo);
                if (cancelOk)
                {
                    _charger.SendSendSmsResvCancel(_chargerChannel.ChannelNo, _chargerChannel.ReservationPhoneNo);
                }
                // 취소 요청 후 서버 처리 시간을 고려하여 약간 대기
                await Task.Delay(500);
                
                // 예약 상태 다시 확인하여 취소 반영 여부 확인
                _isReservationAuth = false;
                _chargerChannel.InitReservationInfo();
                OnPropertyChanged(nameof(ReservationWaitingPhoneLast4));
                OnPropertyChanged(nameof(ReservationWaitingText));
                
                _parentViewModel.PopupCancelReservation(this);
            }
        }
        private void ClosePopupInputReservationNo(object param)
        {
            _parentViewModel.ClosePopup();
        }

        private void RePopupInputReservationNumber(object param)
        {
            _parentViewModel.PopupInputReservationNumber(this, _isReservationCancelAction);
        }

        private void ConfirmReservationCancel(object param)
        {
            // 예약 취소 팝업 확인 시 타이머 정리
            DisposeReservationWaitingTimer();
            
            _parentViewModel.ClosePopup();
            InitializeCharger(null);
        }
        private void OpenRegisterChargeEndAlarmHelpPopup(object param)
        {
            _parentViewModel.HelpChargingFinishedNotificationPopup(this);
        }
        private void RegisterChargeEndAlarm(object param) 
        {
            _parentViewModel.PopupRegisterChargeEndAlarm(this);
        }
        private void ConfirmRegisterAlarm(object param) 
        {
            ChargeEndCallbackPhoneNumber = param as string;
            _chargerChannel.ChargeEndCallbackPhoneNumber = ChargeEndCallbackPhoneNumber;
            _parentViewModel.ClosePopup();

        }
        private void CancelRegisterAlarm(object param) 
        {
            _chargeEndCallbackPhoneNumber = null;
            _chargerChannel.ChargeEndCallbackPhoneNumber = null;
            _parentViewModel.ClosePopup();
        }

        private void ChargeAmountPopup(object param)
        {
            _parentViewModel.PopupInputChargeAmount(this);
            SoundService.Instance.PlaySoundAsync("req_charge_amount.wav");
        }

        private async void ConfirmChargeAmount(object param)
        {
            // 사용자가 입력한 금액도 원단위 절삭 규칙을 동일 적용(1원 자리 버림)
            _chargerChannel.UserSetChargeAmount = MoneyUtil.TruncateWonUnit(int.Parse(param.ToString()));
            UserSetCost = _chargerChannel.UserSetChargeAmount;
            _chargerChannel.PrePaymentInfo = null;
            _isPaymentCancelledByUser = false;
            IsPaymentCancelButtonEnabled = true;

            switch (_chargerChannel.PaymentMethod)
            {
                case PaymentMethod.IcCard:
                    _parentViewModel.PopupInsertICCard(this);
                    SoundService.Instance.PlaySoundAsync("cardreader_credit_card.wav");
                    break;
                case PaymentMethod.SamsungPay:
                    _parentViewModel.PopupTagSamsungpay(this);
                    SoundService.Instance.PlaySoundAsync("cardreader_credit_card.wav");
                    break;
            }
            await _charger.PayCost(_chargerChannel);

            _parentViewModel.ClosePopup();

            if (_chargerChannel.PrePaymentInfo != null && !string.IsNullOrEmpty(_chargerChannel.PrePaymentInfo.AuthNum) && _chargerChannel.PrePaymentInfo.PgNum != "")
            {
                // 신용카드 전표 정보 업데이트 알림
                OnPropertyChanged(nameof(CardNumber));
                OnPropertyChanged(nameof(CardCompanyName));
                OnPropertyChanged(nameof(TransactionAmount));
                OnPropertyChanged(nameof(Vat));
                OnPropertyChanged(nameof(PaymentAmount));
                OnPropertyChanged(nameof(ApprovalNumber));
                OnPropertyChanged(nameof(ApprovalDateTime));
                OnPropertyChanged(nameof(TerminalNumber));
                
                _parentViewModel.PaymentSuccessPopup(this);
            }
            else
            {
                // 사용자가 결제 카드 대기 팝업에서 취소 버튼을 눌러 결제가 중단된 경우:
                // REQ_STOP 응답이 도착했으므로 이 시점에 팝업을 닫고 상태를 정리.
                // 결제 실패 팝업은 띄우지 않는다.
                if (_isPaymentCancelledByUser)
                {
                    _isPaymentCancelledByUser = false;
                    _chargerChannel.InitPaymentInfo();
                    UserSetCost = 0;
                    IsPaymentCancelButtonEnabled = true;
                    _parentViewModel.ClosePopup();
                    return;
                }

                SetDefaultPaymentFailMessage();
                _parentViewModel.PaymentFailPopup(this);
            }

                
        }
        private void CancelChargeAmount(object param)
        {
            _parentViewModel.ClosePopup();
        }

        private void ConfirmCalcChargeAmount(object param)
        {
            CurrentView = _completeView;
        }

        private void ClosePopupReservation(object param)
        {
            _parentViewModel.ClosePopup();
        }

        private async void ClosePopupInsertICCard(object param)
        {
            // 중복 클릭 방지: 이미 취소가 진행 중이면 무시
            if (!IsPaymentCancelButtonEnabled) return;

            // 취소 버튼만 비활성화한 채로 팝업은 유지하고 REQ_STOP만 송신.
            // 단말기 응답이 와서 PayCost가 종료되면 ConfirmChargeAmount의 실패 분기에서 팝업을 닫는다.
            _isPaymentCancelledByUser = true;
            IsPaymentCancelButtonEnabled = false;

            _isRfCardCancelledByUser = true;

            await _charger.CancelCardReading(_chargerChannel);
            //_chargerChannel.InitPaymentInfo();
            //UserSetCost = 0;
            //_parentViewModel.ClosePopup();
        }
        private async void ClosePopupTagRFCard(object param)
        {
            _isRfCardCancelledByUser = true;

            await _charger.CancelCardReading(_chargerChannel);
            _chargerChannel.InitPaymentInfo();
            UserSetCost = 0;
            _parentViewModel.ClosePopup();
        }
        private async void ClosePopupTagSamsungpay(object param)
        {
            if (!IsPaymentCancelButtonEnabled) return;

            _isPaymentCancelledByUser = true;
            IsPaymentCancelButtonEnabled = false;

            await _charger.CancelCardReading(_chargerChannel);
            //_chargerChannel.InitPaymentInfo();
            //UserSetCost = 0;
            //_parentViewModel.ClosePopup();
        }
        private void ClosePopupQrCode(object param)
        {
            _parentViewModel.ClosePopup();
        }

        private void CommonClosePopup(object param)
        {
            _parentViewModel.ClosePopup();
        }

        public bool IsCancelWaitForConnectorPlugInEnabled
        {
            get
            {
                var code = AppSettingsManager.ChargerSettings.ChargerManufacturerCode;
                return !string.Equals(code, "klinelex", StringComparison.OrdinalIgnoreCase);
            }
        }

        private async void CancleWaitForConnectorPlugIn(object param)
        {
            // 취소 버튼: 플래그만 올리면 DSP(StartCharging)가 이미 동작 중인 경우 실제 충전이 진행될 수 있어
            // 즉시 StartCharging 재발행 차단 + Stop/Standby/Init까지 수행한다.
            _chargerChannel.IsWaitForConnectorPlugInCancelled = true;

            // EVSIS는 200ms 타이머에서 StartCharging이 반복될 수 있으므로 즉시 차단
            _evsisRequestStartCharging = false;

            // UI 타이머/팝업 정리
            DisposeWaitingChargeCountdownTimer();
            unchecked { _waitingCountdownSessionId++; }
            _parentViewModel.ClosePopup();

            try
            {
                if (AppSettingsManager.ChargerSettings.ChargerManufacturerCode == "evsis")
                {
                    // EVSIS: 즉시 Stop + Standby로 복귀
                    _charger.StopChargingold(_chargerChannel.ChannelNo);
                    _charger.InitStandby(_chargerChannel.ChannelNo);
                }
                else
                {
                    // non-EVSIS: StopCharging 완료를 보장한 뒤 InitializeCharger로 진행 (명령 순서 꼬임 방지)
                    await _charger.StopCharging(_chargerChannel.ChannelNo);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[UI] Cancel wait-for-connector plug-in failed to stop charging. Channel {_chargerChannel.ChannelNo}: {ex.Message}");
            }
            finally
            {
                InitializeCharger(null);
            }
        }

#region CHAEVI MOTOR MOVE
        public void MoveMotorLeft()
        {
            var chaeviService = _charger.DspControlService as ChaeviDspControlService;
            if (chaeviService == null) return;
            
            chaeviService.SetMotorMoveLeft(_chargerChannel.ChannelNo);
        }

        public void MoveMotorRight()
        {
            var chaeviService = _charger.DspControlService as ChaeviDspControlService;
            if (chaeviService == null) 
            {
                return;
            }
            
            chaeviService.SetMotorMoveRight(_chargerChannel.ChannelNo);
        }

        public void MoveMotorUp()
        {
            var chaeviService = _charger.DspControlService as ChaeviDspControlService;
            if (chaeviService == null) return;
            
            chaeviService.SetMotorMoveUp(_chargerChannel.ChannelNo);
        }

        public void MoveMotorDown()
        {
            var chaeviService = _charger.DspControlService as ChaeviDspControlService;
            if (chaeviService == null) return;
            
            chaeviService.SetMotorMoveDown(_chargerChannel.ChannelNo);
        }

        public void MoveMotorEnd()
        {
            var chaeviService = _charger.DspControlService as ChaeviDspControlService;
            if (chaeviService == null) return;
            
            chaeviService.SetMotorMoveEnd(_chargerChannel.ChannelNo);
        }
#endregion


        /// <summary>
        /// 채비 이동형 모델인지 확인
        /// </summary>
        private bool IsChaeviMobileModel()
        {
            // 채비 제조사이고 팔 이동형 옵션이 체크되어 있는지 확인
            string manufacturerCode = AppSettingsManager.ChargerSettings.ChargerManufacturerCode;
            string isArmMovable = AppSettingsManager.ChargerSettings.IsArmMovable;
            
            return string.Equals(manufacturerCode, "chaevi", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(isArmMovable, "Y", StringComparison.OrdinalIgnoreCase);
        }


        private void ContinueCharging(object param)
        {
            // 계속 충전하기: 모달만 닫기
            _isChargingEndConfirmPopupShown = false;
            _parentViewModel.ClosePopup();
        }

        private void ConfirmEndCharging(object param)
        {
            // 확인: 충전 종료 메서드 호출
            _parentViewModel.ClosePopup();
            CompleteCharging(null);
        }

        public void CompleteChargingByMaintenance()
        {
            if (CurrentChargeSequence != ChargeSequence.Charging)
            {
                return;
            }

            _logger.Info($"[Maintenance] Forcing charging end due to maintenance mode. Channel {_chargerChannel.ChannelNo}");
            CompleteCharging(null, chargeEndType: 4);
        }

        public void CompleteChargingByErrorPopup()
        {
            if (CurrentChargeSequence != ChargeSequence.Charging)
            {
                return;
            }

            _logger.Info($"[ErrorPopup] Forcing charging end due to error popup. Channel {_chargerChannel.ChannelNo}");
            CompleteCharging(null, chargeEndType: 4);
        }

        private void ReceiptView(object param)
        {
            // IC카드 결제 및 삼성페이 결제일 경우 신용카드 전표 팝업 표시
            if (_chargerChannel.PaymentMethod == PaymentMethod.IcCard || _chargerChannel.PaymentMethod == PaymentMethod.SamsungPay)
            {
                _parentViewModel.PopupCreditCardReceipt(this);
            }
            else
            {
                OnPropertyChanged(nameof(DepositAmount));
                OnPropertyChanged(nameof(FinalChargeAmount));
                OnPropertyChanged(nameof(BatteryAfterCharging));
                OnPropertyChanged(nameof(ChargeTimeFormatted));
                CurrentView = _chargingReceiptView;
            }
        }

        private void BackToChargingComplete(object param)
        {
            CurrentView = _completeView;
        }

        private void CloseCreditCardReceipt(object param)
        {
            _parentViewModel.ClosePopup();
        }


        #region QRCode
        private bool _isQrChargingInProgress = false;

        private void GenerateQr(string content)
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

                QrCodeImage = image;
            }
        }

        private async void OnQrChargingStarted(object sender, Charger.QrChargingStartedEventArgs args)
        {
            _logger.Info($"[OnQrChargingStarted] stationId: {args.StationId}, chargerId: {args.ChargerId}");
            
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (_isQrChargingInProgress) return;

                _logger.Info($"[OnQrChargingStarted] Checking stationId: {_chargerChannel.StationId}, chargerId: {_chargerChannel.ChargerId}");
                _logger.Info($"[OnQrChargingStarted] Received stationId: {args.StationId}, chargerId: {args.ChargerId}");

                if (_chargerChannel.StationId == args.StationId && _chargerChannel.ChargerId == args.ChargerId)
                {
                    _isQrChargingInProgress = true;
                    _logger.Info($"[OnQrChargingStarted] stationId: {args.StationId}, chargerId: {args.ChargerId}, tid: {args.Tid}");

                    // 공지(초기) 화면이 떠있으면 먼저 닫고 진행
                    if (_parentViewModel.ShowInitView)
                    {
                        _logger.Info($"[OnQrChargingStarted] InitView is showing. Closing it before starting charge.");
                        _parentViewModel.ShowInitView = false;
                    }

                    // 팝업이 열려있으면 닫고 진행 (QR 팝업 등)
                    if (_parentViewModel.PopupView != null)
                    {
                        _logger.Info($"[OnQrChargingStarted] PopupView is open ({_parentViewModel.PopupView.GetType().Name}). Closing it before starting charge.");
                        _parentViewModel.ClosePopup();
                    }

                    // QR 결제 시 tid 저장 및 결제 방법 설정
                    if (!string.IsNullOrEmpty(args.Tid))
                    {
                        _chargerChannel.QrTid = args.Tid;
                        _chargerChannel.PaymentMethod = PaymentMethod.QrCode;
                        _chargerChannel.UserSetChargeAmount = -1;
                        _chargerChannel.PrePaymentInfo = null;
                        UserSetCost = 0;
                        _logger.Info($"[OnQrChargingStarted] QrTid set to: {args.Tid}, PaymentMethod set to QrCode");
                    }
                    try
                    {
                        await StartChargingAsync();
                    }
                    finally
                    {
                        _isQrChargingInProgress = false;
                    }
                }
                else
                {
                    _logger.Info($"[OnQrChargingStarted] stationId: {args.StationId}, chargerId: {args.ChargerId} is not matched");
                }
            });
        }

        private void OnQrChargingEnded(object sender, Charger.QrChargingEndedEventArgs args)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_chargerChannel.StationId != args.StationId || _chargerChannel.ChargerId != args.ChargerId)
                {
                    _logger.Info($"[OnQrChargingEnded] stationId: {args.StationId}, chargerId: {args.ChargerId} is not matched");
                    return;
                }
                
                try
                {
                    CompleteCharging(null, chargeEndType: 6);
                }
                catch (Exception ex)
                {
                    _logger.Error($"[OnQrChargingEnded] Error: {ex.Message}");
                }
                finally
                {
                    _isQrChargingInProgress = false;
                }
            });
        }
        #endregion

        #region Motor Move Commands (Chaevi only)
        private void MoveUp(object param)
        {
            if (IsChaeviCharger())
            {
                var chaeviService = _charger.DspControlService as ChaeviDspControlService;
                if (chaeviService != null)
                {
                    chaeviService.SetMotorMoveUp(_chargerChannel.ChannelNo);
                }
            }
        }

        private void MoveLeft(object param)
        {
            if (IsChaeviCharger())
            {
                var chaeviService = _charger.DspControlService as ChaeviDspControlService;
                if (chaeviService != null)
                {
                    chaeviService.SetMotorMoveLeft(_chargerChannel.ChannelNo);
                }
            }
        }

        private void MoveRight(object param)
        {
            if (IsChaeviCharger())
            {
                var chaeviService = _charger.DspControlService as ChaeviDspControlService;
                if (chaeviService != null)
                {
                    chaeviService.SetMotorMoveRight(_chargerChannel.ChannelNo);
                }
            }
        }

        private void MoveDown(object param)
        {
            if (IsChaeviCharger())
            {
                var chaeviService = _charger.DspControlService as ChaeviDspControlService;
                if (chaeviService != null)
                {
                    chaeviService.SetMotorMoveDown(_chargerChannel.ChannelNo);
                }
            }
        }

        private bool IsChaeviCharger()
        {
            return AppSettingsManager.ChargerSettings.ChargerManufacturerCode?.ToLower() == "chaevi";
        }
        #endregion
    }
}

