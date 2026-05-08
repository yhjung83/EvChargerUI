
using EvChargerUI.Commons.Util;
using EvChargerUI.Services.Database;
using IniParser;
using IniParser.Model;
using Newtonsoft.Json;
using System;
using System.Data.SQLite;
using System.IO;

namespace EvChargerUI.Commons.Settings
{
    public static class AppSettingsManager
    {
        private static readonly string _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
        private static FileIniDataParser _parser = new FileIniDataParser();
        private static IniData _data;
        private static FileLogger _logger;

        public static ChargerSettings ChargerSettings { get; private set; }
        public static EvCommSettings EvCommSettings { get; private set; }

        public static DisplayBrightnessSettings DisplayBrightnessSettings { get; private set; }
        public static SoundVolumeSettings SoundVolumeSettings { get; private set; }
        public static ChargerOperationSettings ChargerOperationSettings { get; private set; }

        public static ChargerTimerSettings ChargerTimerSettings { get; private set; }

        /// <summary>
        /// 설정이 변경되었을 때 발생하는 이벤트
        /// </summary>
        public static event EventHandler SettingsChanged;

        // Helper methods for safe reading
        private static string GetValue(string section, string key, string defaultValue)
        {
            if (!_data.Sections.ContainsSection(section) || !_data[section].ContainsKey(key))
            {
                EnsureSectionAndKey(section, key, defaultValue);
                return defaultValue;
            }
            return _data[section][key] ?? defaultValue;
        }

        private static int GetIntValue(string section, string key, int defaultValue)
        {
            string value = GetValue(section, key, defaultValue.ToString());
            if (int.TryParse(value, out int result))
            {
                return result;
            }
            return defaultValue;
        }

        private static float GetFloatValue(string section, string key, float defaultValue)
        {
            string value = GetValue(section, key, defaultValue.ToString());
            if (float.TryParse(value, out float result))
            {
                return result;
            }
            return defaultValue;
        }

        private static bool GetBoolValue(string section, string key, bool defaultValue)
        {
            string value = GetValue(section, key, defaultValue.ToString());
            if (bool.TryParse(value, out bool result))
            {
                return result;
            }
            return defaultValue;
        }

        private static void EnsureSectionAndKey(string section, string key, string defaultValue)
        {
            if (!_data.Sections.ContainsSection(section))
            {
                _data.Sections.AddSection(section);
            }
            if (!_data[section].ContainsKey(key))
            {
                _data[section].AddKey(key, defaultValue);
            }
        }
        private static void SetLogger()
        {
            if (_logger == null)
            {
                _logger = ((App)System.Windows.Application.Current).AppLogger;
            }
        }
        public static bool Load()
        {
            SetLogger();
            bool isFileReadSuccess = false;

            if (File.Exists(_filePath))
            {
                try
                {
                    _data = _parser.ReadFile(_filePath);

                    if (_data.Sections.Count == 0)
                    {
                        throw new InvalidDataException("Settings file is empty or corrupted.");
                    }
                    isFileReadSuccess = true;
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"[AppSettingsManager] Failed to read settings.ini: {ex.Message}.");
                    isFileReadSuccess = false;
                }
            }

            if (isFileReadSuccess)
            {
                _logger?.Info("[AppSettingsManager] Successfully loaded from settings.ini.");
                LoadAllSettingsFromIniData();
            }
            else
            {
                _logger?.Warn("[AppSettingsManager] settings.ini not found or corrupted. Attempting to restore from DB.");
                if (RestoreSettingsFromDb())
                {
                    _logger?.Info("[AppSettingsManager] Successfully restored settings from DB. Re-creating settings.ini.");
                    Save(); 
                }
                else
                {
                    _logger?.Error("[AppSettingsManager] Failed to restore from DB. Creating and saving default settings.");
                    _data = new IniData();
                    CreateAllDefaultSettings(); 
                    Save(); 
                }
            }

            return isFileReadSuccess;
        }


        public static void Save()
        {
            SetLogger();
            if (_data == null) _data = new IniData();
            
            SaveChargerSettings();
            SaveEvCommSettings();
            SaveDisplayBrightnessSettings();
            SaveSoundVolumeSettings();
            SaveChargerOperationSettings();
            SaveChargerTimerSettings();

            try
            {
                _parser.WriteFile(_filePath, _data);
                _logger?.Info("[AppSettingsManager] Settings saved to settings.ini. Backing up to DB.");
                BackupSettingsToDb();
            }
            catch (Exception ex)
            {
                _logger?.Error($"[AppSettingsManager] Failed to write to settings.ini: {ex.Message}");
            }
            
            SettingsChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// DB의 PriceSchedules에서 현재 시각에 활성인 스케줄을 읽어
        /// ChargerOperationSettings.PriceForHour에 반영하고 INI/DB를 저장합니다.
        /// Charger.cs의 1분 타이머에서 주기적으로 호출합니다.
        /// station_id 기준으로 처리합니다.
        /// </summary>
        public static bool ApplyActivePriceSchedule(string stationId)
        {
            SetLogger();
            try
            {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "evcharger.db");
                if (!File.Exists(dbPath))
                    return false;

                var sqlite = new SqliteService(dbPath);
                sqlite.Initialize();
                var repo   = new PriceScheduleRepository(sqlite);
                string nowStr = DateTime.Now.ToString("yyyyMMddHHmmss");

                // PurgeExpired 전에 레코드 존재 여부 확인
                bool hasAnySchedule = repo.HasAny(stationId);

                // 만료된 레코드 정리
                repo.PurgeExpired(stationId, nowStr);

                var activeSchedule = repo.GetActiveSchedule(stationId, nowStr);
                if (activeSchedule == null || activeSchedule.Prices == null || activeSchedule.Prices.Length < 24)
                {
                    if (!hasAnySchedule)
                    {
                        // DB에 이 채널의 레코드 자체가 없음 → 다른 채널이 적용한 공유 배열을 건드리지 않음
                        _logger?.Info($"[AppSettingsManager] No price schedule records for stationId={stationId}. Skipping.");
                        return false;
                    }

                    _logger?.Info($"[AppSettingsManager] No active price schedule for stationId={stationId} at {nowStr}. Restoring default 347.2f.");
                    // 이 채널 관련 스케줄이 있었으나 만료 → 기본값 347.2f 복원
                    // 현재 시간대만 확인
                    int restoreHour = DateTime.Now.Hour;
                    float restoreOldPrice = ChargerOperationSettings.PriceForHour[restoreHour];

                    if (Math.Abs(restoreOldPrice - 347.2f) > 0.001f)
                    {
                        string restoreTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        // 현재 시간대 기본값 복원 이력 기록
                        try
                        {
                            sqlite.ExecuteNonQuery(
                                @"INSERT INTO PriceChangeLog (station_id, hour_index, old_price, new_price, change_source, changed_at)
                                  VALUES (@sid, @hour, @old, @new, @src, @at)",
                                new SQLiteParameter("@sid", stationId),
                                new SQLiteParameter("@hour", restoreHour),
                                new SQLiteParameter("@old", Math.Round((double)restoreOldPrice, 1)),
                                new SQLiteParameter("@new", 347.2),
                                new SQLiteParameter("@src", "INI"),
                                new SQLiteParameter("@at", restoreTime));
                        }
                        catch (Exception logEx)
                        {
                            _logger?.Error($"[AppSettingsManager] PriceChangeLog 기록 실패 H{restoreHour:D2}: {logEx.Message}");
                        }

                        // 전체 시간대 복원
                        for (int i = 0; i < 24; i++)
                        {
                            ChargerOperationSettings.PriceForHour[i] = 347.2f;
                        }

                        _logger?.Info($"[AppSettingsManager] Default price 347.2f restored for stationId={stationId}, H{restoreHour:D2}: {restoreOldPrice} → 347.2");
                        Save();
                    }
                    return false;
                }

                // 이미 적용된 스케줄이라도 현재 INI 값과 다르면 다시 적용해야 함
                // (다른 스케줄 종료 후 이전 스케줄로 복귀하는 경우)
                bool alreadyApplied = !string.IsNullOrEmpty(activeSchedule.AppliedAt);

                // 현재 시간대만 확인
                int currentHour = DateTime.Now.Hour;
                float currentPrice = ChargerOperationSettings.PriceForHour[currentHour];
                float scheduledPrice = (float)activeSchedule.Prices[currentHour];
                bool changed = false;

                if (Math.Abs(currentPrice - scheduledPrice) > 0.001f)
                {
                    string changeTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    // 현재 시간대 단가 변경 이력 기록
                    try
                    {
                        sqlite.ExecuteNonQuery(
                            @"INSERT INTO PriceChangeLog (station_id, hour_index, old_price, new_price, change_source, changed_at)
                              VALUES (@sid, @hour, @old, @new, @src, @at)",
                            new SQLiteParameter("@sid", stationId),
                            new SQLiteParameter("@hour", currentHour),
                            new SQLiteParameter("@old", Math.Round((double)currentPrice, 1)),
                            new SQLiteParameter("@new", Math.Round((double)scheduledPrice, 1)),
                            new SQLiteParameter("@src", "DB"),
                            new SQLiteParameter("@at", changeTime));
                    }
                    catch (Exception logEx)
                    {
                        _logger?.Error($"[AppSettingsManager] PriceChangeLog 기록 실패 H{currentHour:D2}: {logEx.Message}");
                    }

                    // 전체 시간대 스케줄 적용
                    for (int i = 0; i < 24; i++)
                    {
                        ChargerOperationSettings.PriceForHour[i] = (float)activeSchedule.Prices[i];
                    }
                    changed = true;
                }

                if (changed)
                {
                    _logger?.Info($"[AppSettingsManager] 단가 스케줄 자동 전환 stationId={stationId} at {nowStr}, H{currentHour:D2}: {currentPrice} → {scheduledPrice}");
                    Save();

                    // DB에 적용 시점 기록 (이미 적용된 적 있어도 최신 시점으로 갱신)
                    try
                    {
                        repo.MarkAsApplied(stationId, activeSchedule.CreateDate);
                        if (alreadyApplied)
                        {
                            _logger?.Info($"[AppSettingsManager] Updated applied_at timestamp. createDate={activeSchedule.CreateDate}");
                        }
                        else
                        {
                            _logger?.Info($"[AppSettingsManager] Marked schedule as applied. createDate={activeSchedule.CreateDate}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"[AppSettingsManager] Failed to mark schedule as applied: {ex.Message}");
                    }

                    _logger?.Info($"[AppSettingsManager] Applied active price schedule for stationId={stationId}");
                }
                else if (alreadyApplied)
                {
                    // 값 변경 없고 이미 적용된 스케줄이면 조용히 스킵
                    _logger?.Debug($"[AppSettingsManager] Schedule already applied and no change needed. createDate={activeSchedule.CreateDate}");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"[AppSettingsManager] ApplyActivePriceSchedule failed: {ex.Message}");
                return false;
            }
        }
        private static void CreateAllDefaultSettings()
        {
            CreateDefaultChargerSettings();
            CreateDefaultEvCommSettings();
            CreateDefaultDisplayBrightnessSettings();
            CreateDefaultSoundVolumeSettings();
            CreateDefaultChargerOperationSettings();
            CreateDefaultChargerTimerSettings();
        }
        private static void LoadAllSettingsFromIniData()
        {
            LoadChargerSettings();
            LoadEvCommSettings();
            LoadDisplayBrightnessSettings();
            LoadSoundVolumeSettings();
            LoadChargerOperationSettings();
            LoadChargerTimerSettings();
        }

        private static void BackupSettingsToDb()
        {
            try
            {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "evcharger.db");
                var sqlite = new SqliteService(dbPath);
                sqlite.Initialize();

                // 각 설정 객체를 JSON으로 직렬화하여 저장
                SaveSettingToDb(sqlite, "Settings.ChargerSettings", ChargerSettings);
                SaveSettingToDb(sqlite, "Settings.EvCommSettings", EvCommSettings);
                SaveSettingToDb(sqlite, "Settings.DisplayBrightnessSettings", DisplayBrightnessSettings);
                SaveSettingToDb(sqlite, "Settings.SoundVolumeSettings", SoundVolumeSettings);
                SaveSettingToDb(sqlite, "Settings.ChargerOperationSettings", ChargerOperationSettings);
                SaveSettingToDb(sqlite, "Settings.ChargerTimerSettings", ChargerTimerSettings);
                _logger?.Info("[AppSettingsManager] Successfully backed up settings to database.");
            }
            catch (Exception ex)
            {
                _logger?.Error($"[AppSettingsManager] Failed to backup settings to DB: {ex.Message}");
            }
        }

        private static void SaveSettingToDb<T>(SqliteService sqlite, string key, T value)
        {
            string jsonValue = JsonConvert.SerializeObject(value, Formatting.Indented);
            string sql = "INSERT OR REPLACE INTO KeyValues (Key, Value) VALUES (@Key, @Value)";
            sqlite.ExecuteNonQuery(sql, new SQLiteParameter("@Key", key), new SQLiteParameter("@Value", jsonValue));
        }

        private static bool RestoreSettingsFromDb()
        {
            try
            {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "evcharger.db");
                if (!File.Exists(dbPath))
                {
                    _logger?.Warn("[AppSettingsManager] Database file not found. Cannot restore settings.");
                    return false;
                }
                var sqlite = new SqliteService(dbPath);

                // 각 설정을 DB에서 읽어와 역직렬화
                var chargerSettings = LoadSettingFromDb<ChargerSettings>(sqlite, "Settings.ChargerSettings");
                var evCommSettings = LoadSettingFromDb<EvCommSettings>(sqlite, "Settings.EvCommSettings");
                var displayBrightnessSettings = LoadSettingFromDb<DisplayBrightnessSettings>(sqlite, "Settings.DisplayBrightnessSettings");
                var soundVolumeSettings = LoadSettingFromDb<SoundVolumeSettings>(sqlite, "Settings.SoundVolumeSettings");
                var chargerOperationSettings = LoadSettingFromDb<ChargerOperationSettings>(sqlite, "Settings.ChargerOperationSettings");
                var chargerTimerSettings = LoadSettingFromDb<ChargerTimerSettings>(sqlite, "Settings.ChargerTimerSettings");

                // 하나라도 null이면 복원 실패로 간주 (온전한 세트가 아니면 의미 없음)
                if (chargerSettings == null || evCommSettings == null || displayBrightnessSettings == null ||
                    soundVolumeSettings == null || chargerOperationSettings == null || chargerTimerSettings == null)
                {
                    _logger?.Warn("[AppSettingsManager] Could not find a complete set of settings in the database.");
                    return false;
                }

                // 복원된 설정 할당
                ChargerSettings = chargerSettings;
                EvCommSettings = evCommSettings;
                DisplayBrightnessSettings = displayBrightnessSettings;
                SoundVolumeSettings = soundVolumeSettings;
                ChargerOperationSettings = chargerOperationSettings;
                ChargerTimerSettings = chargerTimerSettings;
                
                 _data = new IniData();
                _logger?.Info("[AppSettingsManager] Successfully restored settings from database.");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"[AppSettingsManager] Failed to restore settings from DB: {ex.Message}");
                return false;
            }
        }
        private static T LoadSettingFromDb<T>(SqliteService sqlite, string key) where T : class
        {
            string sql = "SELECT Value FROM KeyValues WHERE Key = @Key";
            object result = sqlite.ExecuteScalar(sql, new SQLiteParameter("@Key", key));

            if (result != null && result != DBNull.Value)
            {
                string jsonValue = (string)result;
                return JsonConvert.DeserializeObject<T>(jsonValue);
            }
            return null;
        }

        #region ChargerSettings
        private static void CreateDefaultChargerSettings()
        {
            ChargerSettings = new ChargerSettings();
        }
        private static void LoadChargerSettings()
        {
            ChargerSettings = new ChargerSettings
            {
                StationId = GetValue("ChargerSettings", "StationId", ""),
                StationName = GetValue("ChargerSettings", "StationName", ""),
                LeftChannelChargerId = GetValue("ChargerSettings", "LeftChannelChargerId", ""),
                RightChannelChargerId = GetValue("ChargerSettings", "RightChannelChargerId", ""),
                LeftQrCode = GetValue("ChargerSettings", "LeftQrCode", ""),
                RightQrCode = GetValue("ChargerSettings", "RightQrCode", ""),
                LeftConnectorType = GetIntValue("ChargerSettings", "LeftConnectorType", 0),
                RightConnectorType = GetIntValue("ChargerSettings", "RightConnectorType", 0),
                IsTriple = GetValue("ChargerSettings", "IsTriple", "N"),
                IsArmMovable = GetValue("ChargerSettings", "IsArmMovable", "N"),
                ChargerManufacturerCode = GetValue("ChargerSettings", "ChargerManufacturerCode", ""),
                PaymentManufacturerCode = GetValue("ChargerSettings", "PaymentManufacturerCode", ""),
                DspComPortNo = GetValue("ChargerSettings", "DspComPortNo", ""),
                DspBaudRate = GetIntValue("ChargerSettings", "DspComBaudRate", 9600),
                PaymentDeviceComPortNo = GetValue("ChargerSettings", "PaymentDeviceComPortNo", ""),
                PaymentDeviceBaudRate = GetIntValue("ChargerSettings", "PaymentDeviceBaudRate", 9600),
                ChargingSpeed = GetIntValue("ChargerSettings", "ChargingSpeed", 200),
                PaymentDeviceHealthCheckInterval = GetIntValue("ChargerSettings", "PaymentDeviceHealthCheckInterval", 5000),
                ChaeviModelName = GetValue("ChargerSettings", "ChaeviModelName", "")
            };

        }
        private static void SaveChargerSettings()
        {
            EnsureSectionAndKey("ChargerSettings", "StationId", ChargerSettings.StationId);
            _data["ChargerSettings"]["StationId"] = ChargerSettings.StationId;
            _data["ChargerSettings"]["StationName"] = ChargerSettings.StationName;
            _data["ChargerSettings"]["LeftChannelChargerId"] = ChargerSettings.LeftChannelChargerId;
            _data["ChargerSettings"]["RightChannelChargerId"] = ChargerSettings.RightChannelChargerId;
            _data["ChargerSettings"]["ChargerManufacturerCode"] = ChargerSettings.ChargerManufacturerCode;
            _data["ChargerSettings"]["PaymentManufacturerCode"] = ChargerSettings.PaymentManufacturerCode;
            _data["ChargerSettings"]["LeftQrCode"] = ChargerSettings.LeftQrCode;
            _data["ChargerSettings"]["RightQrCode"] = ChargerSettings.RightQrCode;
            _data["ChargerSettings"]["LeftConnectorType"] = ChargerSettings.LeftConnectorType.ToString();
            _data["ChargerSettings"]["RightConnectorType"] = ChargerSettings.RightConnectorType.ToString();
            _data["ChargerSettings"]["IsTriple"] = ChargerSettings.IsTriple;
            _data["ChargerSettings"]["IsArmMovable"] = ChargerSettings.IsArmMovable;
            _data["ChargerSettings"]["DspComPortNo"] = ChargerSettings.DspComPortNo;
            _data["ChargerSettings"]["DspComBaudRate"] = ChargerSettings.DspBaudRate.ToString();
            _data["ChargerSettings"]["PaymentDeviceComPortNo"] = ChargerSettings.PaymentDeviceComPortNo;
            _data["ChargerSettings"]["PaymentDeviceBaudRate"] = ChargerSettings.PaymentDeviceBaudRate.ToString();
            _data["ChargerSettings"]["ChargingSpeed"] = ChargerSettings.ChargingSpeed.ToString();
            _data["ChargerSettings"]["PaymentDeviceHealthCheckInterval"] = ChargerSettings.PaymentDeviceHealthCheckInterval.ToString();
            _data["ChargerSettings"]["ChaeviModelName"] = ChargerSettings.ChaeviModelName ?? "";

        }
        #endregion

        #region EVCommSettings
        private static void CreateDefaultEvCommSettings()
        {
            string currentTime = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
            EvCommSettings = new EvCommSettings
            {
                ServerBaseUrl = "http://10.101.160.34:8080/",
                ClientBaseUrl = "http://192.168.10.2:5050/",
                StatusUpdateInterval = 1,
                EVSE_Status = 0,
                EVSE_PayYN = "Y",
                EVSE_Test = "N",
                ChargerMode = 1,
                EVSE_DSP_Status = 0,
                EVSE_EmergencyStop = 0,
                EVSE_Network_Status = 0,
                MockMode = false,
                IsDebug = "N",
                LastUiUpdateDate = currentTime
            };
        }
        private static void LoadEvCommSettings()
        {
            // LastUiUpdateDate가 없으면 현재 시각으로 초기화
            string lastUiUpdateDate = GetValue("EvCommSettings", "LastUiUpdateDate", "");
            if (string.IsNullOrEmpty(lastUiUpdateDate))
            {
                lastUiUpdateDate = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
            }

            EvCommSettings = new EvCommSettings
            {
                ServerBaseUrl = GetValue("EvCommSettings", "ServerBaseUrl", "http://10.101.160.34:8080/"),
                ClientBaseUrl = GetValue("EvCommSettings", "ClientBaseUrl", "http://192.168.10.2:5050/"),
                StatusUpdateInterval = GetIntValue("EvCommSettings", "StatusUpdateInterval", 1),
                EVSE_Status = GetIntValue("EvCommSettings", "EVSE_Status", 0),
                EVSE_PayYN = GetValue("EvCommSettings", "EVSE_PayYN", "Y"),
                EVSE_Test = GetValue("EvCommSettings", "EVSE_Test", "N"),
                ChargerMode = GetIntValue("EvCommSettings", "ChargerMode", 1),
                EVSE_DSP_Status = GetIntValue("EvCommSettings", "EVSE_DSP_Status", 0),
                EVSE_EmergencyStop = GetIntValue("EvCommSettings", "EVSE_EmergencyStop", 0),
                EVSE_Network_Status = GetIntValue("EvCommSettings", "EVSE_Network_Status", 0),
                MockMode = GetBoolValue("EvCommSettings", "MockMode", false),
                IsDebug = GetValue("EvCommSettings", "IsDebug", "N"),
                LastUiUpdateDate = lastUiUpdateDate
            };
        }
        private static void SaveEvCommSettings()
        {
            EnsureSectionAndKey("EvCommSettings", "ServerBaseUrl", EvCommSettings.ServerBaseUrl);
            _data["EvCommSettings"]["ServerBaseUrl"] = EvCommSettings.ServerBaseUrl;
            _data["EvCommSettings"]["ClientBaseUrl"] = EvCommSettings.ClientBaseUrl;
            _data["EvCommSettings"]["StatusUpdateInterval"] = EvCommSettings.StatusUpdateInterval.ToString();
            _data["EvCommSettings"]["EVSE_Status"] = EvCommSettings.EVSE_Status.ToString();
            _data["EvCommSettings"]["EVSE_PayYN"] = EvCommSettings.EVSE_PayYN.ToString();
            _data["EvCommSettings"]["EVSE_Test"] = EvCommSettings.EVSE_Test.ToString();
            _data["EvCommSettings"]["ChargerMode"] = EvCommSettings.ChargerMode.ToString();
            _data["EvCommSettings"]["EVSE_DSP_Status"] = EvCommSettings.EVSE_DSP_Status.ToString();
            _data["EvCommSettings"]["EVSE_EmergencyStop"] = EvCommSettings.EVSE_EmergencyStop.ToString();
            _data["EvCommSettings"]["EVSE_Network_Status"] = EvCommSettings.EVSE_Network_Status.ToString();
            _data["EvCommSettings"]["MockMode"] = EvCommSettings.MockMode.ToString();
            _data["EvCommSettings"]["IsDebug"] = EvCommSettings.IsDebug ?? "N";
            _data["EvCommSettings"]["LastUiUpdateDate"] = EvCommSettings.LastUiUpdateDate ?? DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
        }
        private static void SaveEvCommSettings_EVSE_Status()
        {
            _data["EvCommSettings"]["EVSE_Status"] = EvCommSettings.EVSE_Status.ToString();
        }
        #endregion

        #region DisplayBrightnessSettings
        private static void CreateDefaultDisplayBrightnessSettings()
        {
            DisplayBrightnessSettings = new DisplayBrightnessSettings { LevelForDay = 70, LevelForNight = 30 };
        }
        private static void LoadDisplayBrightnessSettings()
        {
            DisplayBrightnessSettings = new DisplayBrightnessSettings
            {
                LevelForDay = GetIntValue("DisplayBrightnessSettings", "Day", 70),
                LevelForNight = GetIntValue("DisplayBrightnessSettings", "Night", 30)
            };
        }

        private static void SaveDisplayBrightnessSettings()
        {
            EnsureSectionAndKey("DisplayBrightnessSettings", "Day", DisplayBrightnessSettings.LevelForDay.ToString());
            EnsureSectionAndKey("DisplayBrightnessSettings", "Night", DisplayBrightnessSettings.LevelForNight.ToString());
            _data["DisplayBrightnessSettings"]["Day"] = DisplayBrightnessSettings.LevelForDay.ToString();
            _data["DisplayBrightnessSettings"]["Night"] = DisplayBrightnessSettings.LevelForNight.ToString();
        }

        #endregion

        #region SoundVolumeSettings
        private static void CreateDefaultSoundVolumeSettings()
        {
            SoundVolumeSettings = new SoundVolumeSettings { LevelForDay = 30, LevelForNight = 0 };
        }
        private static void LoadSoundVolumeSettings()
        {
            SoundVolumeSettings = new SoundVolumeSettings
            {
                LevelForDay = GetIntValue("SoundVolumeSettings", "Day", 30),
                LevelForNight = GetIntValue("SoundVolumeSettings", "Night", 0)
            };
        }

        private static void SaveSoundVolumeSettings()
        {
            EnsureSectionAndKey("SoundVolumeSettings", "Day", SoundVolumeSettings.LevelForDay.ToString());
            EnsureSectionAndKey("SoundVolumeSettings", "Night", SoundVolumeSettings.LevelForNight.ToString());
            _data["SoundVolumeSettings"]["Day"] = SoundVolumeSettings.LevelForDay.ToString();
            _data["SoundVolumeSettings"]["Night"] = SoundVolumeSettings.LevelForNight.ToString();
        }

        #endregion

        #region ChargerOperationSettings
        private static void CreateDefaultChargerOperationSettings()
        {
            ChargerOperationSettings = new ChargerOperationSettings
            {
                IsChargeTimeLimited = true,
                ChargeLimitTime = 40,
                IsTestOperation = false,
                IsPaymentApplied = true,
                LogRetentionDays = 30,
                DspLogSaveYn = false
            };
            // PriceForHour: 생성자에서 347.2f로 초기화됨
            for (int i = 0; i < 24; i++)
                ChargerOperationSettings.PriceForHour[i] = 347.2f;
        }
        private static void LoadChargerOperationSettings()
        {
            ChargerOperationSettings = new ChargerOperationSettings
            {
                IsChargeTimeLimited = GetValue("ChargerOperationSettings", "TimeLimitYn", "Y").Equals("Y"),
                ChargeLimitTime = GetIntValue("ChargerOperationSettings", "TimeLimitValue", 40),
                IsTestOperation = GetValue("ChargerOperationSettings", "TestYn", "N").Equals("Y"),
                IsPaymentApplied = GetValue("ChargerOperationSettings", "PayYn", "Y").Equals("Y"),
                LogRetentionDays = GetIntValue("ChargerOperationSettings", "LogRetentionDays", 30),
                DspLogSaveYn = GetValue("ChargerOperationSettings", "DspLogSaveYn", "N").Equals("Y")
            };
            for (int i = 0; i < 24; i++)
            {
                ChargerOperationSettings.PriceForHour[i] = GetFloatValue("ChargerOperationSettings", $"H{i:D2}", 347.2f);
            }
        }

        private static void SaveChargerOperationSettings()
        {
            EnsureSectionAndKey("ChargerOperationSettings", "TimeLimitYn", ChargerOperationSettings.IsChargeTimeLimited ? "Y" : "N");
            _data["ChargerOperationSettings"]["TimeLimitYn"] = ChargerOperationSettings.IsChargeTimeLimited ? "Y" : "N";
            _data["ChargerOperationSettings"]["TimeLimitValue"] = ChargerOperationSettings.ChargeLimitTime.ToString();
            _data["ChargerOperationSettings"]["TestYn"] = ChargerOperationSettings.IsTestOperation ? "Y" : "N";
            _data["ChargerOperationSettings"]["PayYn"] = ChargerOperationSettings.IsPaymentApplied ? "Y" : "N";
            _data["ChargerOperationSettings"]["LogRetentionDays"] = ChargerOperationSettings.LogRetentionDays.ToString();
            _data["ChargerOperationSettings"]["DspLogSaveYn"] = ChargerOperationSettings.DspLogSaveYn ? "Y" : "N";

            for (int i = 0; i < 24; i++)
            {
                EnsureSectionAndKey("ChargerOperationSettings", $"H{i:D2}", ChargerOperationSettings.PriceForHour[i].ToString());
                _data["ChargerOperationSettings"][$"H{i:D2}"] = ChargerOperationSettings.PriceForHour[i].ToString();
            }
        }

        #endregion

        #region ChargerTimerSettings
        private static void CreateDefaultChargerTimerSettings()
        {
            ChargerTimerSettings = new ChargerTimerSettings();
        }
        private static void LoadChargerTimerSettings()
        {
            ChargerTimerSettings = new ChargerTimerSettings
            {
                AutoReturnToInitViewTimer = GetIntValue("ChargerTimerSettings", "AutoReturnToInitViewTimer", 60),
                ChargerSelectTypeViewTimer = GetIntValue("ChargerTimerSettings", "ChargerSelectTypeViewTimer", 30),
                ChargingCompleteViewTimer = GetIntValue("ChargerTimerSettings", "ChargingCompleteViewTimer", 30),
                ChargingReceiptViewTimer = GetIntValue("ChargerTimerSettings", "ChargingReceiptViewTimer", 30),
                PaymentMethodSelectViewTimer = GetIntValue("ChargerTimerSettings", "PaymentMethodSelectViewTimer", 30),
                ReadyToChargingViewTimer = GetIntValue("ChargerTimerSettings", "ReadyToChargingViewTimer", 30),
                ReservationWaitingViewTimer = GetIntValue("ChargerTimerSettings", "ReservationWaitingViewTimer", 30),
                AdminMainViewTimer = GetIntValue("ChargerTimerSettings", "AdminMainViewTimer", 300),
                AdminSettingViewTimer = GetIntValue("ChargerTimerSettings", "AdminSettingViewTimer", 300),
                AuthFailPopupViewTimer = GetIntValue("ChargerTimerSettings", "AuthFailPopupViewTimer", 30),
                AuthSuccessPopupViewTimer = GetIntValue("ChargerTimerSettings", "AuthSuccessPopupViewTimer", 30),
                ChargeInputPopupViewTimer = GetIntValue("ChargerTimerSettings", "ChargeInputPopupViewTimer", 30),
                InputPasswordPopupViewTimer = GetIntValue("ChargerTimerSettings", "InputPasswordPopupViewTimer", 30),
                InputPhoneNumberPopupViewTimer = GetIntValue("ChargerTimerSettings", "InputPhoneNumberPopupViewTimer", 30),
                InputReservationNumberPopupViewTimer = GetIntValue("ChargerTimerSettings", "InputReservationNumberPopupViewTimer", 30),
                InsertICCardPopupViewTimer = GetIntValue("ChargerTimerSettings", "InsertICCardPopupViewTimer", 30),
                PaymentFailPopupViewTimer = GetIntValue("ChargerTimerSettings", "PaymentFailPopupViewTimer", 30),
                PaymentSuccessPopupViewTimer = GetIntValue("ChargerTimerSettings", "PaymentSuccessPopupViewTimer", 30),
                QrCodePopupViewTimer = GetIntValue("ChargerTimerSettings", "QrCodePopupViewTimer", 180),
                ReportQrCodePopupViewTimer = GetIntValue("ChargerTimerSettings", "ReportQrCodePopupViewTimer", 30),
                ReservationCancelPopupViewTimer = GetIntValue("ChargerTimerSettings", "ReservationCancelPopupViewTimer", 30),
                ReservationDescriptionPopupViewTimer = GetIntValue("ChargerTimerSettings", "ReservationDescriptionPopupViewTimer", 30),
                ReservationSuccessPopupViewTimer = GetIntValue("ChargerTimerSettings", "ReservationSuccessPopupViewTimer", 30),
                SearchStationQrCodePopupViewTimer = GetIntValue("ChargerTimerSettings", "SearchStationQrCodePopupViewTimer", 30),
                TagRFCardPopupViewTimer = GetIntValue("ChargerTimerSettings", "TagRFCardPopupViewTimer", 30),
                TagSamsungpayPopupViewTimer = GetIntValue("ChargerTimerSettings", "TagSamsungpayPopupViewTimer", 30),
                WaitingChargingStartPopupViewTimer = GetIntValue("ChargerTimerSettings", "WaitingChargingStartPopupViewTimer", 30),
                WrongReservationNoPopupViewTimer = GetIntValue("ChargerTimerSettings", "WrongReservationNoPopupViewTimer", 30),
                ConnectorErrorPopupViewTimer = GetIntValue("ChargerTimerSettings", "ConnectorErrorPopupViewTimer", 30),
                HelpPopupViewTimer = GetIntValue("ChargerTimerSettings", "HelpPopupViewTimer", 30),
                CreditCardReceiptPopupViewTimer = GetIntValue("ChargerTimerSettings", "CreditCardReceiptPopupViewTimer", 30)
            };
        }
        
        private static void SaveChargerTimerSettings()
        {
            EnsureSectionAndKey("ChargerTimerSettings", "AutoReturnToInitViewTimer", ChargerTimerSettings.AutoReturnToInitViewTimer.ToString());
            _data["ChargerTimerSettings"]["AutoReturnToInitViewTimer"] = ChargerTimerSettings.AutoReturnToInitViewTimer.ToString();
            _data["ChargerTimerSettings"]["ChargerSelectTypeViewTimer"] = ChargerTimerSettings.ChargerSelectTypeViewTimer.ToString();
            _data["ChargerTimerSettings"]["ChargingCompleteViewTimer"] = ChargerTimerSettings.ChargingCompleteViewTimer.ToString();
            _data["ChargerTimerSettings"]["ChargingReceiptViewTimer"] = ChargerTimerSettings.ChargingReceiptViewTimer.ToString();
            _data["ChargerTimerSettings"]["PaymentMethodSelectViewTimer"] = ChargerTimerSettings.PaymentMethodSelectViewTimer.ToString();
            _data["ChargerTimerSettings"]["ReadyToChargingViewTimer"] = ChargerTimerSettings.ReadyToChargingViewTimer.ToString();
            _data["ChargerTimerSettings"]["ReservationWaitingViewTimer"] = ChargerTimerSettings.ReservationWaitingViewTimer.ToString();
            _data["ChargerTimerSettings"]["AdminMainViewTimer"] = ChargerTimerSettings.AdminMainViewTimer.ToString();
            _data["ChargerTimerSettings"]["AdminSettingViewTimer"] = ChargerTimerSettings.AdminSettingViewTimer.ToString();

            _data["ChargerTimerSettings"]["AuthFailPopupViewTimer"] = ChargerTimerSettings.AuthFailPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["AuthSuccessPopupViewTimer"] = ChargerTimerSettings.AuthSuccessPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["ChargeInputPopupViewTimer"] = ChargerTimerSettings.ChargeInputPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["InputPasswordPopupViewTimer"] = ChargerTimerSettings.InputPasswordPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["InputPhoneNumberPopupViewTimer"] = ChargerTimerSettings.InputPhoneNumberPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["InputReservationNumberPopupViewTimer"] = ChargerTimerSettings.InputReservationNumberPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["InsertICCardPopupViewTimer"] = ChargerTimerSettings.InsertICCardPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["PaymentFailPopupViewTimer"] = ChargerTimerSettings.PaymentFailPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["PaymentSuccessPopupViewTimer"] = ChargerTimerSettings.PaymentSuccessPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["QrCodePopupViewTimer"] = ChargerTimerSettings.QrCodePopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["ReportQrCodePopupViewTimer"] = ChargerTimerSettings.ReportQrCodePopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["ReservationCancelPopupViewTimer"] = ChargerTimerSettings.ReservationCancelPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["ReservationDescriptionPopupViewTimer"] = ChargerTimerSettings.ReservationDescriptionPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["ReservationSuccessPopupViewTimer"] = ChargerTimerSettings.ReservationSuccessPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["SearchStationQrCodePopupViewTimer"] = ChargerTimerSettings.SearchStationQrCodePopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["TagRFCardPopupViewTimer"] = ChargerTimerSettings.TagRFCardPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["TagSamsungpayPopupViewTimer"] = ChargerTimerSettings.TagSamsungpayPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["WaitingChargingStartPopupViewTimer"] = ChargerTimerSettings.WaitingChargingStartPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["WrongReservationNoPopupViewTimer"] = ChargerTimerSettings.WrongReservationNoPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["ConnectorErrorPopupViewTimer"] = ChargerTimerSettings.ConnectorErrorPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["HelpPopupViewTimer"] = ChargerTimerSettings.HelpPopupViewTimer.ToString();
            _data["ChargerTimerSettings"]["CreditCardReceiptPopupViewTimer"] = ChargerTimerSettings.CreditCardReceiptPopupViewTimer.ToString();
        }

        #endregion
    }
}
