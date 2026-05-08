using EvChargerUI.Commons.Settings;
using EvChargerUI.Services.Database;
using EvChargerUI.ViewModels.Commons;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace EvChargerUI.ViewModels
{
    public class PriceChangeLogViewModel : BaseViewModel
    {
        private readonly Action _goBackAction;
        private readonly Action<object> _showPriceScheduleAction;

        private string _stationInfoText;
        public string StationInfoText
        {
            get => _stationInfoText;
            set
            {
                _stationInfoText = value;
                OnPropertyChanged(nameof(StationInfoText));
            }
        }

        private string _dbPathText;
        public string DbPathText
        {
            get => _dbPathText;
            set
            {
                _dbPathText = value;
                OnPropertyChanged(nameof(DbPathText));
            }
        }

        public ObservableCollection<PriceChangeLogDisplayItem> ChangeLogs { get; }

        public ICommand ReloadCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand ShowPriceScheduleCommand { get; }

        public PriceChangeLogViewModel(Action goBackAction, Action<object> showPriceScheduleAction = null)
        {
            _goBackAction = goBackAction;
            _showPriceScheduleAction = showPriceScheduleAction;
            ChangeLogs = new ObservableCollection<PriceChangeLogDisplayItem>();

            ReloadCommand = new RelayCommand(Reload);
            BackCommand = new RelayCommand(GoBack);
            ShowPriceScheduleCommand = new RelayCommand(ShowPriceSchedule);

            LoadLogs();
        }

        private void Reload(object param)
        {
            LoadLogs();
        }

        private void GoBack(object param)
        {
            _goBackAction?.Invoke();
        }

        private void ShowPriceSchedule(object param)
        {
            _showPriceScheduleAction?.Invoke(param);
        }

        private void LoadLogs()
        {
            string stationId = AppSettingsManager.ChargerSettings.StationId;
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "evcharger.db");

            DbPathText = $"DB: {dbPath}";

            if (string.IsNullOrWhiteSpace(stationId))
            {
                StationInfoText = "충전소 ID가 설정되어 있지 않습니다.";
                ChangeLogs.Clear();
                return;
            }

            try
            {
                var sqlite = new SqliteService(dbPath);
                sqlite.Initialize();

                var repository = new PriceChangeLogRepository(sqlite);
                var rows = repository.GetAll(stationId);

                ChangeLogs.Clear();

                foreach (var row in rows)
                {
                    ChangeLogs.Add(new PriceChangeLogDisplayItem
                    {
                        Id = row.Id,
                        StationId = row.StationId,
                        HourIndex = row.HourIndex,
                        OldPrice = row.OldPrice,
                        NewPrice = row.NewPrice,
                        ChangeSource = row.ChangeSource,
                        ChangedAt = row.ChangedAt
                    });
                }

                StationInfoText = $"충전소 ID: {stationId} / 변경 기록: {ChangeLogs.Count}건";
            }
            catch (Exception ex)
            {
                StationInfoText = $"조회 오류: {ex.Message}";
                ChangeLogs.Clear();
            }
        }
    }

    public class PriceChangeLogDisplayItem
    {
        public long Id { get; set; }
        public string StationId { get; set; }
        public int HourIndex { get; set; }
        public double OldPrice { get; set; }
        public double NewPrice { get; set; }
        public string ChangeSource { get; set; }
        public string ChangedAt { get; set; }

        public string HourText => $"H{HourIndex:D2}";
        public string OldPriceText => $"{OldPrice:F1}";
        public string NewPriceText => $"{NewPrice:F1}";
        public string PriceChangeText
        {
            get
            {
                double diff = NewPrice - OldPrice;
                string sign = diff >= 0 ? "+" : "";
                return $"{sign}{diff:F1}";
            }
        }
        public string ChangeSourceText => ChangeSource == "INI" ? "서버" : ChangeSource == "DB" ? "스케줄" : ChangeSource;
    }
}
