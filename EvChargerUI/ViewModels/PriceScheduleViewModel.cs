using EvChargerUI.Commons.Settings;
using EvChargerUI.Services.Database;
using EvChargerUI.ViewModels.Commons;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace EvChargerUI.ViewModels
{
    public class PriceScheduleViewModel : BaseViewModel
    {
        private readonly Action _goBackAction;
        private readonly Action<object> _showChangeLogAction;

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

        public ObservableCollection<PriceScheduleDisplayItem> PriceSchedules { get; }

        public ICommand ReloadCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand ShowChangeLogCommand { get; }

        public PriceScheduleViewModel(Action goBackAction, Action<object> showChangeLogAction = null)
        {
            _goBackAction = goBackAction;
            _showChangeLogAction = showChangeLogAction;
            PriceSchedules = new ObservableCollection<PriceScheduleDisplayItem>();

            ReloadCommand = new RelayCommand(Reload);
            BackCommand = new RelayCommand(GoBack);
            ShowChangeLogCommand = new RelayCommand(ShowChangeLog);

            LoadSchedules();
        }

        private void Reload(object param)
        {
            LoadSchedules();
        }

        private void GoBack(object param)
        {
            _goBackAction?.Invoke();
        }

        private void ShowChangeLog(object param)
        {
            _showChangeLogAction?.Invoke(param);
        }

        private void LoadSchedules()
        {
            string stationId = AppSettingsManager.ChargerSettings.StationId;
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "evcharger.db");

            DbPathText = $"DB: {dbPath}";

            if (string.IsNullOrWhiteSpace(stationId))
            {
                StationInfoText = "충전소 ID가 설정되어 있지 않습니다.";
                PriceSchedules.Clear();
                return;
            }

            try
            {
                var sqlite = new SqliteService(dbPath);
                sqlite.Initialize();

                var repository = new PriceScheduleRepository(sqlite);
                List<PriceScheduleRow> rows = repository.GetAll(stationId)
                    .OrderByDescending(x => x.ApplyDate)
                    .ThenByDescending(x => x.CreateDate)
                    .ToList();

                PriceSchedules.Clear();
                int currentHour = DateTime.Now.Hour;

                foreach (var row in rows)
                {
                    bool hasCurrentHourPrice = row.Prices != null && row.Prices.Length > currentHour;
                    double currentHourPriceValue = hasCurrentHourPrice ? row.Prices[currentHour] : 0d;
                    string currentHourPrice = hasCurrentHourPrice ? $"{currentHourPriceValue:F1}원" : "-";

                    var hourlyPrices = new double[24];
                    if (row.Prices != null)
                    {
                        int copyLength = Math.Min(24, row.Prices.Length);
                        Array.Copy(row.Prices, hourlyPrices, copyLength);
                    }

                    var hourlyMismatchFlags = new bool[24];
                    if (row.Prices != null && row.Prices.Length > 0)
                    {
                        double maxPrice = hourlyPrices.Max();
                        for (int i = 0; i < 24; i++)
                        {
                            hourlyMismatchFlags[i] = Math.Abs(hourlyPrices[i] - maxPrice) > 0.01;
                        }
                    }

                    string pricesText = string.Empty;
                    if (row.Prices != null && row.Prices.Length > 0)
                    {
                        pricesText = string.Join(", ", row.Prices.Select((price, hour) => $"{hour:00}:{price:F1}"));
                    }

                    PriceSchedules.Add(new PriceScheduleDisplayItem
                    {
                        Id = row.Id,
                        CreateDate = row.CreateDate,
                        ApplyDate = row.ApplyDate,
                        EndDate = row.EndDate,
                        CurrentHourPriceText = currentHourPrice,
                        HourlyPrices = hourlyPrices,
                        HourlyMismatchFlags = hourlyMismatchFlags,
                        PricesText = pricesText,
                        AppliedAt = row.AppliedAt
                    });
                }

                StationInfoText = $"충전소 ID: {stationId} / 조회 건수: {PriceSchedules.Count}";
            }
            catch (Exception ex)
            {
                StationInfoText = $"조회 오류: {ex.Message}";
                PriceSchedules.Clear();
            }
        }
    }

    public class PriceScheduleDisplayItem
    {
        public long Id { get; set; }
        public string CreateDate { get; set; }
        public string ApplyDate { get; set; }
        public string EndDate { get; set; }
        public string CurrentHourPriceText { get; set; }
        public double[] HourlyPrices { get; set; }
        public bool[] HourlyMismatchFlags { get; set; }
        public string PricesText { get; set; }
        public string AppliedAt { get; set; }
        public string AppliedStatus
        {
            get
            {
                if (string.IsNullOrEmpty(AppliedAt))
                    return "미적용";

                // "2026-04-16 06:43:45" 형식을 파싱하여 날짜와 시간 표시
                if (DateTime.TryParse(AppliedAt, out DateTime appliedTime))
                {
                    return $"적용됨({appliedTime:yyyy-MM-dd HH:mm:ss})";
                }

                return "적용됨";
            }
        }
    }
}
