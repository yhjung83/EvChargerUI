using EvChargerUI.Commons.Settings;
using EvChargerUI.Commons.Util;
using EvChargerUI.Domains;
using EvChargerUI.Models;
using EvChargerUI.Services;
using EvChargerUI.ViewModels.Commons;
using EvChargerUI.Views;
using EvChargerUI.Views.LogView;
using EvChargerUI.Views.PriceScheduleView;
using EvChargerUI.Views.PriceChangeLogView;
using EvChargerUI.Services.Database;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EvChargerUI.ViewModels
{
    public class AdminViewModel : BaseViewModel
    {
        private AdminMainView _adminMainView;
        private AdminSettingView _adminSettingView;
        private FileLogger _logger = ((App)Application.Current).AppLogger;

        public ObservableCollection<ManufacturerCode> ManufacturerList { get; }


        private string _selectedManufacturerId;
        public string SelectedManufacturerId
        {
            get => _selectedManufacturerId;
            set
            {
                _selectedManufacturerId = value;
                OnPropertyChanged(nameof(SelectedManufacturerId));
                OnPropertyChanged(nameof(IsEvsisManufacturer));
                OnPropertyChanged(nameof(IsChaeviManufacturer));
            }
        }

        public ObservableCollection<ManufacturerCode> PaymentManufacturerList { get; }

        private string _selectedPaymentManufacturerId;
        public string SelectedPaymentManufacturerId
        {
            get => _selectedPaymentManufacturerId;
            set
            {
                _selectedPaymentManufacturerId = value;
                OnPropertyChanged(nameof(SelectedPaymentManufacturerId));
            }
        }


        private UserControl _currentView;
        public UserControl CurrentView
        {
            get => _currentView;
            set
            {
                _currentView = value;
                OnPropertyChanged(nameof(CurrentView));
            }
        }

        private string _stationId;
        public string StationId
        {
            get => _stationId;
            set
            {
                _stationId = value;
                OnPropertyChanged(nameof(StationId));
            }
        }

        private string _stationName;
        public string StationName
        {
            get => _stationName;
            set
            {
                _stationName = value;
                OnPropertyChanged(nameof(StationName));
            }
        }

        private string _leftChargerId;
        public string LeftChargerId
        {
            get => _leftChargerId;
            set
            {
                _leftChargerId = value;
                OnPropertyChanged(nameof(LeftChargerId));
            }
        }

        private string _rightChargerId;
        public string RightChargerId
        {
            get => _rightChargerId;
            set
            {
                _rightChargerId = value;
                OnPropertyChanged(nameof(RightChargerId));
            }
        }

        private string _serverUrl;
        public string ServerUrl
        {
            get => _serverUrl;
            set
            {
                _serverUrl = value;
                OnPropertyChanged(nameof(ServerUrl));
            }
        }

        private string _leftQrCode;
        public string LeftQrCode
        {
            get => _leftQrCode;
            set
            {
                _leftQrCode = value;
                OnPropertyChanged(nameof(LeftQrCode));
            }
        }

        private string _rightQrCode;
        public string RightQrCode
        {
            get => _rightQrCode;
            set
            {
                _rightQrCode = value;
                OnPropertyChanged(nameof(RightQrCode));
            }
        }

        private string _dspComPort;
        public string DspComPort
        {
            get => _dspComPort;
            set
            {
                _dspComPort = value;
                OnPropertyChanged(nameof(DspComPort));
            }
        }

        private string _dspBaudRate;
        public string DspBaudRate
        {
            get => _dspBaudRate;
            set
            {
                _dspBaudRate = value;
                OnPropertyChanged(nameof(DspBaudRate));
            }
        }


        private string _paymentDeviceComPort;
        public string PaymentDeviceComPort
        {
            get => _paymentDeviceComPort;
            set
            {
                _paymentDeviceComPort = value;
                OnPropertyChanged(nameof(PaymentDeviceComPort));
            }
        }

        private string _paymentDeviceBaudRate;
        public string PaymentDeviceBaudRate
        {
            get => _paymentDeviceBaudRate;
            set
            {
                _paymentDeviceBaudRate = value;
                OnPropertyChanged(nameof(PaymentDeviceBaudRate));
            }
        }

        private string _chargingSpeed;
        public string ChargingSpeed
        {
            get => _chargingSpeed;
            set
            {
                _chargingSpeed = value;
                OnPropertyChanged(nameof(ChargingSpeed));
            }
        }

        private int _leftConnectorType;
        public int LeftConnectorType
        {
            get => _leftConnectorType;
            set
            {
                _leftConnectorType = value;
                OnPropertyChanged(nameof(LeftConnectorType));
            }
        }

        private int _rightConnectorType;
        public int RightConnectorType
        {
            get => _rightConnectorType;
            set
            {
                _rightConnectorType = value;
                OnPropertyChanged(nameof(RightConnectorType));
            }
        }

        private bool _isTriple;
        public bool IsTriple
        {
            get => _isTriple;
            set
            {
                _isTriple = value;
                OnPropertyChanged(nameof(IsTriple));
            }
        }

        private ObservableCollection<ChaeviModelItem> _chaeviModelList;
        public ObservableCollection<ChaeviModelItem> ChaeviModelList
        {
            get => _chaeviModelList;
            set
            {
                _chaeviModelList = value;
                OnPropertyChanged(nameof(ChaeviModelList));
            }
        }

        private string _selectedChaeviModelName;
        public string SelectedChaeviModelName
        {
            get => _selectedChaeviModelName;
            set
            {
                _selectedChaeviModelName = value;
                OnPropertyChanged(nameof(SelectedChaeviModelName));
            }
        }

        public bool IsEvsisManufacturer => SelectedManufacturerId?.ToLowerInvariant() == "evsis";
        public bool IsChaeviManufacturer => SelectedManufacturerId?.ToLowerInvariant() == "chaevi";

        private string _clientUrl;
        public string ClientUrl
        {
            get => _clientUrl;
            set
            {
                _clientUrl = value;
                OnPropertyChanged(nameof(ClientUrl));
            }
        }

        private ObservableCollection<ConnectorTypeItem> _connectorTypeList;
        public ObservableCollection<ConnectorTypeItem> ConnectorTypeList
        {
            get => _connectorTypeList;
            set
            {
                _connectorTypeList = value;
                OnPropertyChanged(nameof(ConnectorTypeList));
            }
        }

        private string _showKeyboardCaption;
        public string ShowKeyboardCaption
        {
            get => _showKeyboardCaption;
            set
            {
                _showKeyboardCaption = value;
                OnPropertyChanged(nameof(ShowKeyboardCaption));
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
        public ICommand ShowKeyboardCommand { get; }
        public ICommand ExitCommand { get;  }
        public ICommand RunMainUiCommand { get; }
        public ICommand RestartChargerCommand { get; }
        public ICommand RebootOsCommand { get; }
        public ICommand ChangeToSettingViewCommand { get; }
        public ICommand ChangeToMainViewCommand { get; }
        public ICommand SaveSettingCommand { get; }
        public ICommand OpenLogViewCommand { get; }
        public ICommand CheckPriceScheduleCommand { get; }

        private Process _oskProcess;

        private DispatcherTimer _statusTimer;
        private DispatcherTimer _AdminMainViewTimer;
        private Brush _serverStatusColor;

        public Brush ServerStatusColor
        {
            get => _serverStatusColor;
            set
            {
                _serverStatusColor = value;
                OnPropertyChanged(nameof(ServerStatusColor));
            }
        }

        private Brush _paymentDeviceStatusColor;
        public Brush PaymentDeviceStatusColor
        {
            get => _paymentDeviceStatusColor;
            set
            {
                _paymentDeviceStatusColor = value;
                OnPropertyChanged(nameof(PaymentDeviceStatusColor));
            }
        }

        private bool _isDiagnosing;
        public bool IsDiagnosing
        {
            get => _isDiagnosing;
            set
            {
                _isDiagnosing = value;
                OnPropertyChanged(nameof(IsDiagnosing));
            }
        }

        private string _diagnosingMessage;
        public string DiagnosingMessage
        {
            get => _diagnosingMessage;
            set
            {
                _diagnosingMessage = value;
                OnPropertyChanged(nameof(DiagnosingMessage));
            }
        }

        private int _loadingProgress;
        public int LoadingProgress
        {
            get => _loadingProgress;
            set
            {
                _loadingProgress = value;
                OnPropertyChanged(nameof(LoadingProgress));
            }
        }

        private string _currentTime;
        public string CurrentTime
        {
            get => _currentTime;
            set
            {
                _currentTime = value;
                OnPropertyChanged(nameof(CurrentTime));
            }
        }

        private string _uiUpdateDate;
        public string UiUpdateDate
        {
            get => _uiUpdateDate;
            set
            {
                _uiUpdateDate = value;
                OnPropertyChanged(nameof(UiUpdateDate));
            }
        }

        public void UpdateUiUpdateDate()
        {
            string updateDate = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
            UiUpdateDate = updateDate;
            
            // config 파일에 저장
            AppSettingsManager.EvCommSettings.LastUiUpdateDate = updateDate;
            AppSettingsManager.Save();
        }

        public AdminViewModel()
        {
            _adminMainView = new AdminMainView();
            _adminMainView.DataContext = this;

            _adminSettingView = new AdminSettingView();
            _adminSettingView.DataContext = this;

            ServerStatusColor = Brushes.Gray;
            PaymentDeviceStatusColor = Brushes.Gray;

            ManufacturerList = new ObservableCollection<ManufacturerCode>()
            {
                new ManufacturerCode { Id = "signet", Name = "SK시그넷" },
                new ManufacturerCode { Id = "evsis", Name = "이브이시스" },
                new ManufacturerCode { Id = "chaevi", Name = "채비" },
                new ManufacturerCode { Id = "klinelex", Name = "클린일렉스" }

            };

            SelectedManufacturerId = null;

            PaymentManufacturerList = new ObservableCollection<ManufacturerCode>()
            {
                new ManufacturerCode { Id = "nice", Name = "나이스" },
                new ManufacturerCode { Id = "techleader", Name = "테크리더" }

            };

            SelectedPaymentManufacturerId = null;

            ConnectorTypeList = new ObservableCollection<ConnectorTypeItem>()
            {
                new ConnectorTypeItem { Id = 0, Name = "AC3" },
                new ConnectorTypeItem { Id = 1, Name = "DC콤보" },
                new ConnectorTypeItem { Id = 2, Name = "차데모" }
            };

            // 채비 모델명 목록 초기화
            var modelNames = ChaeviModelMappingService.GetAllModelNames();
            ChaeviModelList = new ObservableCollection<ChaeviModelItem>();
            foreach (var modelName in modelNames)
            {
                string armMovableType = ChaeviModelMappingService.GetArmMovableType(modelName);
                string displayName = $"{modelName} ({armMovableType})";
                ChaeviModelList.Add(new ChaeviModelItem 
                { 
                    ModelName = modelName, 
                    DisplayName = displayName 
                });
            }

            ShowKeyboardCommand = new RelayCommand(ShowKeyboard);
            ExitCommand = new RelayCommand(ExitAdmin);
            RunMainUiCommand = new RelayCommand(RunMainUi);
            RestartChargerCommand = new RelayCommand(RestartCharger);
            RebootOsCommand = new RelayCommand(RebootOs);
            ChangeToSettingViewCommand = new RelayCommand(ChangeToSettingView);
            ChangeToMainViewCommand = new RelayCommand(ChangeToMainView);
            SaveSettingCommand = new RelayCommand(SaveSetting);
            OpenLogViewCommand = new RelayCommand(OpenLogView);
            CheckPriceScheduleCommand = new RelayCommand(CheckPriceSchedule);


            ShowKeyboardCaption = "화상키보드 보이기";
            _oskProcess = null;

            // 마지막 UI 업데이트 시간을 config 파일에서 읽어옴 (없으면 현재 시각으로 초기화)
            string lastUpdateDate = AppSettingsManager.EvCommSettings.LastUiUpdateDate;
            if (string.IsNullOrEmpty(lastUpdateDate))
            {
                lastUpdateDate = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
                AppSettingsManager.EvCommSettings.LastUiUpdateDate = lastUpdateDate;
                AppSettingsManager.Save();
            }
            UiUpdateDate = lastUpdateDate;
            CurrentTime = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(1);
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();

            _AdminMainViewTimer = new DispatcherTimer();
            _AdminMainViewTimer.Interval = TimeSpan.FromSeconds(AppSettingsManager.ChargerTimerSettings.AdminMainViewTimer);
            _AdminMainViewTimer.Tick += AdminMainView_Close;
            _AdminMainViewTimer.Start();

            // 어셈블리 버전 초기화
            AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            LoadSettings();
            CurrentView = _adminMainView;
        }

        private void AdminMainView_Close(object sender, EventArgs e)
        {
            _AdminMainViewTimer.Stop();
            //((App)Application.Current).ShowMainWindow();
            RunMainUiCommand.Execute(null);
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            // 현재 시간 업데이트
            CurrentTime = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

            // EVSE_Network_Status에 따라 서버 상태 색상 설정 (0: 초록색, 1: 빨간색)
            int networkStatus = AppSettingsManager.EvCommSettings.EVSE_Network_Status;
            ServerStatusColor = networkStatus == 0 ? Brushes.Green : Brushes.Red;

            // 어드민 윈도우가 열려있을 때만 결제 단말기 헬스체크 수행
            var app = (App)Application.Current;
            if (app?.AdminWindow != null && app.AdminWindow.IsVisible)
            {
                var charger = app.Charger;
                if (charger != null)
                {
                    // 결제 단말기 헬스체크 수행
                    charger.CheckPaymentDeviceHealth();
                    
                    PaymentDeviceStatusColor = charger.IsPaymentServiceConnected ? Brushes.Green : Brushes.Red;
                }
                else
                {
                    PaymentDeviceStatusColor = Brushes.Gray;
                }
            }
        }

        private void LoadSettings()
        {
            ChargerSettings settings = AppSettingsManager.ChargerSettings;
            StationId = settings.StationId;
            StationName = settings.StationName;
            LeftChargerId = settings.LeftChannelChargerId;
            RightChargerId = settings.RightChannelChargerId;
            LeftQrCode = settings.LeftQrCode;
            RightQrCode = settings.RightQrCode;
            SelectedManufacturerId = settings.ChargerManufacturerCode;
            SelectedPaymentManufacturerId = settings.PaymentManufacturerCode;
            DspComPort = settings.DspComPortNo;
            DspBaudRate = settings.DspBaudRate.ToString();
            PaymentDeviceComPort = settings.PaymentDeviceComPortNo;
            PaymentDeviceBaudRate = settings.PaymentDeviceBaudRate.ToString();
            ChargingSpeed = settings.ChargingSpeed.ToString();
            ServerUrl = AppSettingsManager.EvCommSettings.ServerBaseUrl;
            LeftConnectorType = settings.LeftConnectorType;
            RightConnectorType = settings.RightConnectorType;
            IsTriple = settings.IsTriple == "Y";
            SelectedChaeviModelName = settings.ChaeviModelName;
            ClientUrl = AppSettingsManager.EvCommSettings.ClientBaseUrl;
        }

        private void saveSettings()
        {
            ChargerSettings settings = AppSettingsManager.ChargerSettings;

            settings.StationId = StationId;
            settings.StationName = StationName;
            settings.LeftChannelChargerId = LeftChargerId;
            settings.RightChannelChargerId = RightChargerId;
            settings.LeftQrCode = LeftQrCode;
            settings.RightQrCode = RightQrCode;
            settings.ChargerManufacturerCode = SelectedManufacturerId;
            settings. PaymentManufacturerCode = SelectedPaymentManufacturerId;
            settings.DspComPortNo = DspComPort;
            
            if (!string.IsNullOrEmpty(DspBaudRate) && int.TryParse(DspBaudRate, out int dspBaudRate))
                settings.DspBaudRate = dspBaudRate;
            
            settings.PaymentDeviceComPortNo = PaymentDeviceComPort;
            
            if (!string.IsNullOrEmpty(PaymentDeviceBaudRate) && int.TryParse(PaymentDeviceBaudRate, out int paymentBaudRate))
                settings.PaymentDeviceBaudRate = paymentBaudRate;
            
            if (!string.IsNullOrEmpty(ChargingSpeed) && int.TryParse(ChargingSpeed, out int chargingSpeed))
                settings.ChargingSpeed = chargingSpeed;
            
            settings.LeftConnectorType = LeftConnectorType;
            settings.RightConnectorType = RightConnectorType;
            settings.IsTriple = IsTriple ? "Y" : "N";
            settings.ChaeviModelName = SelectedChaeviModelName ?? "";
            AppSettingsManager.EvCommSettings.ServerBaseUrl = ServerUrl;
            AppSettingsManager.EvCommSettings.ClientBaseUrl = ClientUrl;

            AppSettingsManager.Save();
        }

        private void ShowKeyboard(object param)
        {
            if(_oskProcess == null) OpenKeyboard();
            else CloseKeyboard();
        }

        private void OpenKeyboard()
        {
            string sysnativePath = Path.Combine(
                Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.System)).FullName,
                "Sysnative",
                "cmd.exe"
            );

            var psi = new ProcessStartInfo
            {
                FileName = sysnativePath,
                Arguments = "/c osk.exe",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            _oskProcess = Process.Start(psi);
            ShowKeyboardCaption = "화상키보드 숨기기";
        }

        private void CloseKeyboard()
        {
            if (!_oskProcess.HasExited)
            {
                Process[] processesByName = Process.GetProcessesByName("osk");
                if (processesByName.GetLength(0) > 0)
                    processesByName[0].Kill();
            }

            _oskProcess = null;
            ShowKeyboardCaption = "화상키보드 보이기";
        }

        private void ExitAdmin(object param)
        {
            ((App)Application.Current).ShutdownWithReason("Admin: ExitAdmin 버튼 클릭");
        }

        private void RunMainUi(object param)
        {
            ((App)Application.Current).ShowMainWindow();
        }

        private void RestartCharger(object param)
        {
            try
            {
                if (MessageBox.Show(
                        "UI 프로그램을 재시작하시겠습니까?\n[예]를 누르면, UI가 종료 후 다시 실행됩니다.",
                        "UI 재시작",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                string exePath = Process.GetCurrentProcess()?.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    _logger?.Warn("[Admin] UI restart failed. exePath is empty.");
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                });
                _logger?.Info($"[Admin] UI restart requested. exePath={exePath}");

                ((App)Application.Current).ShutdownWithReason("Admin: RestartCharger 버튼 클릭");
            }
            catch (Exception ex)
            {
                _logger.Error($"[Admin] RestartCharger exception: {ex.Message}");
            }
        }
        private void RebootOs(object param)
        {
            if (MessageBox.Show("Windows를 재시작하시겠습니까?\n[예]를 누르면, 바로 재시작됩니다.", "Windows 재시작", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            Process.Start("shutdown.exe", "-r -f -t 00");
        }
        private void ChangeToSettingView(object param)
        {
            LoadSettings();
            CurrentView = _adminSettingView;
        }
        private void ChangeToMainView(object param)
        {
            LoadSettings();
            CurrentView = _adminMainView;
        }
        private void SaveSetting(object param)
        {
            try
            {
                saveSettings();
                LoadSettings();
                CurrentView = _adminMainView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 저장 중 오류가 발생했습니다.\n\n{ex.Message}", "저장 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowAdminMainView()
        {
            CurrentView = _adminMainView;
        }

        private async void OpenLogView(object param)
        {
            IsDiagnosing = true;
            DiagnosingMessage = "진단 중입니다.";
            LoadingProgress = 0;

            // Simulate 30-second diagnosis
            for (int i = 0; i <= 30; i++)
            {
                LoadingProgress = (int)((double)i / 30 * 100); // Update progress from 0 to 100
                await Task.Delay(1000); // Wait for 1 second
            }

            IsDiagnosing = false;
            CurrentView = new Views.LogView.LogView
            {
                DataContext = new EvChargerUI.ViewModels.LogViewModel(ShowAdminMainView)
            };
        }

        private void CheckPriceSchedule(object param)
        {
            CurrentView = new PriceScheduleView
            {
                DataContext = new PriceScheduleViewModel(ShowAdminMainView, ShowPriceChangeLogView)
            };
        }

        private void ShowPriceChangeLogView(object param)
        {
            CurrentView = new PriceChangeLogView
            {
                DataContext = new PriceChangeLogViewModel(ShowAdminMainView, _ => ShowPriceScheduleView())
            };
        }

        private void ShowPriceScheduleView()
        {
            CurrentView = new PriceScheduleView
            {
                DataContext = new PriceScheduleViewModel(ShowAdminMainView, ShowPriceChangeLogView)
            };
        }

    }
}
