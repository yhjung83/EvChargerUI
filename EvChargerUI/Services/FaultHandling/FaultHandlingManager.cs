using System;
using System.Collections.Generic;
using System.Linq;
using EvChargerUI.Models;
using EvChargerUI.Commons.Settings;
using EvChargerUI.Commons.Util;
using System.Windows;
using EvChargerUI.Services.DspControl;

namespace EvChargerUI.Services.FaultHandling
{
    public class FaultHandlingManager
    {
        private readonly Charger _charger;
        private readonly FileLogger _logger;

        // App 로그/팝업 메시지 매핑을 위한 코드-문구 테이블.
        private static readonly Dictionary<string, string> FaultCodeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "101", "주전원 차단기" },
            { "102", "입력부 과전압" },
            { "103", "입력부 저전압" },
            { "104", "입력부 과전류" },
            { "105", "접지 Fail" },
            { "201", "출력부 MC 오류" },
            { "202", "출력부 MC 융착" },
            { "203", "출력부 과전압" },
            { "204", "출력부 과전류" },
            { "205", "선간절연이상" },
            { "301", "Pre-charging 오류" },
            { "302", "모듈 이상" },
            { "303", "온도 이상" },
            { "401", "Control Pilot 기능 오류" },
            { "402", "Control Pilot 정보 오류" },
            { "403", "PLC 통신 오류" },
            { "404", "PLC 통신 정보 오류" },
            { "405", "CAN 통신 오류" },
            { "406", "CAN 통신 정보 오류" },
            { "501", "MCU 제어보드 통신 오류" },
            { "502", "차량 간 협상 실패" },
            { "503", "사용자 인증 실패" },
            { "901", "비상스위치 동작" },
            { "902", "커넥터 Lock 오류" },
            { "903", "커넥터 위치센서 오류" },
            { "904", "전력량계 오류" },
            { "905", "결제모듈 오류" },
            { "906", "외함 개방" },
            { "909", "차량 및 배터리 이상" },
            { "910", "기타 오류" },
            { "913", "침수 오류" },
            { "914", "기울임 오류" },
            { "915", "충전건 온도 오류" },
            { "916", " DC 전류센서 오류" },
            { "917", "입력 MC 오류" },
            { "918", "제어보드 내부 오류" },
        };

        private bool _alarmRaised = false;
        private string _lastAlarmCode = "0000";
        private string _lastLoggedAlarmCode = null;

        private bool _paymentDeviceAlarmRaised = false;
        private const string PaymentDeviceAlarmCode = "0905";

        public FaultHandlingManager(Charger charger)
        {
            _charger = charger ?? throw new ArgumentNullException(nameof(charger));
            _logger = ((App)Application.Current)?.AppLogger;
        }

        public bool HandleFaultState(
            bool dspStatus,
            bool networkStatus,
            bool pmsStatus,
            bool shouldShowError,
            string errorCode,
            bool isAnyCharging,
            bool isMaintenancePopupByAdmin)
        {
            bool shouldShowErrorPopup = shouldShowError && !isAnyCharging && !isMaintenancePopupByAdmin;

            if (shouldShowError && !isMaintenancePopupByAdmin)
            {
                string currentAlarmCode = NormalizeAlarmCode(errorCode);
                if (_alarmRaised && _lastLoggedAlarmCode != null && !string.Equals(_lastLoggedAlarmCode, currentAlarmCode, StringComparison.Ordinal))
                {
                    string message = ResolveFaultMessage(dspStatus, networkStatus, pmsStatus, errorCode);
                    _logger?.Warn($"[FAULT] UPDATED alarmCode={currentAlarmCode} message={message}");
                    _lastLoggedAlarmCode = currentAlarmCode;
                }
                RaiseAlarmHistory(errorCode);
            }
            else if (!shouldShowError && !isMaintenancePopupByAdmin)
            {
                ClearAlarmHistory();
            }

            return shouldShowErrorPopup;
        }

        public void RaisePaymentDeviceCommAlarm()
        {
            if (_paymentDeviceAlarmRaised)
                return;

            _logger?.Warn($"[FAULT] PaymentDevice comm alarm RAISING (alarmCode={PaymentDeviceAlarmCode})");
            try
            {
                foreach (var channel in _charger.Channels)
                {
                    if (channel == null) continue;
                    _charger.EvCommService.SendAlarmHistory(
                        channel.StationId,
                        channel.ChargerId,
                        "0",
                        DateTime.Now.ToString("yyyyMMddHHmmss"),
                        PaymentDeviceAlarmCode);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"[FAULT] PaymentDevice comm alarm RAISE failed: {ex.Message}");
            }

            _paymentDeviceAlarmRaised = true;
            _logger?.Warn($"[FAULT] PaymentDevice comm alarm RAISED (alarmCode={PaymentDeviceAlarmCode})");
        }

        public void ClearPaymentDeviceCommAlarm()
        {
            _logger?.Info($"[FAULT] PaymentDevice comm alarm CLEARING (alarmCode={PaymentDeviceAlarmCode})");
            try
            {
                foreach (var channel in _charger.Channels)
                {
                    if (channel == null) continue;
                    _charger.EvCommService.SendAlarmHistory(
                        channel.StationId,
                        channel.ChargerId,
                        "1",
                        DateTime.Now.ToString("yyyyMMddHHmmss"),
                        PaymentDeviceAlarmCode);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"[FAULT] PaymentDevice comm alarm CLEAR failed: {ex.Message}");
            }

            _paymentDeviceAlarmRaised = false;
            _logger?.Info($"[FAULT] PaymentDevice comm alarm CLEARED (alarmCode={PaymentDeviceAlarmCode})");
        }

        private void RaiseAlarmHistory(string errorCode)
        {
            if (_alarmRaised)
                return;

            string alarmCode = NormalizeAlarmCode(errorCode);
            string message = ResolveFaultMessage(true, true, false, errorCode);

            _logger?.Warn($"[FAULT] RAISING alarmCode={alarmCode} message={message}");
            try
            {
                foreach (var channel in _charger.Channels)
                {
                    if (channel == null) continue;
                    _charger.EvCommService.SendAlarmHistory(
                        channel.StationId,
                        channel.ChargerId,
                        "0",
                        DateTime.Now.ToString("yyyyMMddHHmmss"),
                        alarmCode);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"[FAULT] RAISE SendAlarmHistory failed: {ex.Message}");
            }

            _lastAlarmCode = alarmCode;
            _alarmRaised = true;
            _lastLoggedAlarmCode = alarmCode;

            _logger?.Warn($"[FAULT] RAISED alarmCode={alarmCode} message={message}");
        }

        private void ClearAlarmHistory()
        {
            if (!_alarmRaised)
                return;

            string clearedAlarmCode = _lastAlarmCode;
            _logger?.Info($"[FAULT] CLEARING alarmCode={clearedAlarmCode}");
            try
            {
                foreach (var channel in _charger.Channels)
                {
                    if (channel == null) continue;
                    _charger.EvCommService.SendAlarmHistory(
                        channel.StationId,
                        channel.ChargerId,
                        "1",
                        DateTime.Now.ToString("yyyyMMddHHmmss"),
                        _lastAlarmCode);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"[FAULT] CLEAR SendAlarmHistory failed: {ex.Message}");
            }

            _alarmRaised = false;
            _lastAlarmCode = "0000";
            _lastLoggedAlarmCode = null;

            _logger?.Info($"[FAULT] CLEARED alarmCode={clearedAlarmCode}");
        }

        private string NormalizeAlarmCode(string rawFaultCode)
        {
            if (IsEmergencyActive())
            {
                return "0901";
            }
            if (string.IsNullOrWhiteSpace(rawFaultCode))
            {
                return "0000";
            }

            if (int.TryParse(rawFaultCode, out int code))
            {
                int normalized = Math.Abs(code) % 10000;
                string normalizedStr = normalized.ToString("D4");
                if (normalizedStr == "0000" && IsEmergencyActive())
                    return "0901";
                return normalizedStr;
            }

            string digitsOnly = new string(rawFaultCode.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length == 0)
            {
                return IsEmergencyActive() ? "0901" : "0000";
            }

            if (digitsOnly.Length > 4)
            {
                digitsOnly = digitsOnly.Substring(digitsOnly.Length - 4);
            }

            string padded = digitsOnly.PadLeft(4, '0');
            if (padded == "0000" && IsEmergencyActive())
                return "0901";
            return padded;
        }

        private bool IsEmergencyActive()
        {
            try
            {
                // Charger 내부 플래그와 설정 플래그 둘 다 반영
                return (_charger?.IsEmergency ?? false)
                       || (AppSettingsManager.EvCommSettings.EVSE_EmergencyStop == 1);
            }
            catch
            {
                return false;
            }
        }

        private string ResolveFaultMessage(bool dspStatus, bool networkStatus, bool pmsStatus, string errorCode)
        {
            if (!dspStatus)
                return "MCU 제어보드 통신 오류";

            if (!networkStatus)
            return "서버와 연결이 되지 않습니다.";

            if (pmsStatus)
                return "전력량계 통신 오류";


            if (!string.IsNullOrWhiteSpace(errorCode)
                && FaultCodeMessageMap.TryGetValue(errorCode, out string mapped))
            {
                if (string.Equals(errorCode, "910", StringComparison.Ordinal)
                    && _charger.DspControlService is EvsisDspControlService evsis)
                {
                    string detail = TryGetEvsisFaultDetail(evsis);
                    if (!string.IsNullOrWhiteSpace(detail))
                        return detail;
                }
                return mapped;
            }

            return string.IsNullOrWhiteSpace(errorCode) ? "알 수 없는 오류" : $"Error code {errorCode}";
        }

        /// <summary>
        /// EVSIS: fault가 걸린 채널에서 GetFaultDetails로 세부 사유를 가져온다. (첫 번째 비어 있지 않은 값)
        /// </summary>
        private string TryGetEvsisFaultDetail(EvsisDspControlService evsis)
        {
            if (evsis == null)
                return null;

            ChargerChannel[] channels = _charger.Channels;
            if (channels == null)
                return null;

            foreach (ChargerChannel ch in channels)
            {
                if (ch == null)
                    continue;

                try
                {
                    if (!evsis.GetFaultStatus(ch.ChannelNo))
                        continue;

                    string detail = evsis.GetFaultDetails(ch.ChannelNo);
                    if (!string.IsNullOrWhiteSpace(detail))
                        return detail;
                }
                catch
                {
                    // 채널별 읽기 실패는 건너뜀
                }
            }

            return null;
        }

        public static string ResolveFaultMessageFromCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return "알 수 없는 오류";

            if (FaultCodeMessageMap.TryGetValue(code, out string mapped))
                return mapped;

            return "알 수 없는 오류";
        }
    }
}

