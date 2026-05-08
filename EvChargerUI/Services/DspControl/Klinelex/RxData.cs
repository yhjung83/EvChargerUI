using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using EvChargerUI.Commons.Util;

namespace EvChargerUI.Services.DspControl.Klinelex
{
    public class RxData
    {
        private FileLogger _logger = ((App)Application.Current).DspLogger;
        private static int _size = 21;
        private static string ConvertHexToVersionString(ushort hex)
        {
            byte integerPart = (byte)(hex >> 8);  
            byte fractionalPart = (byte)(hex & 0xFF);

            return $"v{integerPart:X}.{fractionalPart:X}";
        }

        private static int ToInt32(ushort high, ushort low)
        {
            return (high << 16) | low;
        }

        private static bool GetBit(ushort value, int bitIndex)
        {
            return (value & (1 << bitIndex)) != 0;
        }

        // Address 400 - 충전기 상태
        private bool _isControlBoardReady;
        private bool _isChargeReady;
        private bool _isPlugConnected;
        private bool _isDoorClosed;
        private bool _isChargeRunning;
        private bool _isChargeFinished;
        private bool _isFaultOccured;
        private bool _isResetRequested;
        private bool _isConnectorLocked;
        public bool IsControllBoardReady => _isControlBoardReady;
        public bool IsChargeReady => _isChargeReady;
        public bool IsPlugConnected => _isPlugConnected;
        public bool IsDoorClosed => _isDoorClosed;
        public bool IsChargeRunning => _isChargeRunning;
        public bool IsChargeFinished => _isChargeFinished;
        public bool IsFaultOccured => _isFaultOccured;
        public bool IsResetRequested => _isResetRequested;
        public bool IsConnectorLocked => _isConnectorLocked;

        // Address 401
        private string _controlboardVersion;
        // Address 402
        private string _protocolVersion;
        // Address 403
        private int _runCount;
        // Address 404
        private int _remainedMinute;
        // Address 405
        private int _soc;
        // Address 407,408
        private double _powerInKw;
        // Address 409
        private float _voltageOut;
        // Address 410
        private float _currentOut;
        // Address 411, 412
        private bool[] _powerModuleStates;
        // Address 418
        private string _powerMeterVersion;

        public string ControlboardVersion => _controlboardVersion;
        public string ProtocolVersion => _protocolVersion;
        public int RunCount => _runCount;
        public int RemainedMinute => _remainedMinute;
        public int Soc => _soc;
        public double PowerInKw => _powerInKw;
        public float VoltageOut => _voltageOut;
        public float CurrentOut => _currentOut;
        public bool[] PowerModuleStates => _powerModuleStates;
        public string PowerMeterVersion => _powerMeterVersion;


        // Address 419
        private bool _isMainBreakerTripped;
        private bool _isInputOverVoltage;
        private bool _isInputUnderVoltage;
        private bool _isInputOverCurrent;
        private bool _isGroundFault;
        private bool _isInputMcFault;
        private bool _isOutputMcFault;
        private bool _isOutputOverVoltage;
        private bool _isOutputOverCurrent;
        private bool _isOutputLeakage;
        private bool _isPreChargingFault;
        private bool _isPowerModuleFault;
        private bool _isOverTemperature;
        // 차량연결 상태 미구현
        public bool IsMainBreakerTripped => _isMainBreakerTripped;
        public bool IsInputOverVoltage => _isInputOverVoltage;
        public bool IsInputUnderVoltage => _isInputUnderVoltage;
        public bool IsInputOverCurrent => _isInputOverCurrent;
        public bool IsGroundFault => _isGroundFault;
        public bool IsInputMcFault => _isInputMcFault;
        public bool IsOutputMcFault => _isOutputMcFault;
        public bool IsOutputOverVoltage => _isOutputOverVoltage;
        public bool IsOutputOverCurrent => _isOutputOverCurrent;
        public bool IsOutputLeakage => _isOutputLeakage;
        public bool IsPreChargingFault => _isPreChargingFault;
        public bool IsPowerModuleFault => _isPowerModuleFault;
        public bool IsOverTemperature => _isOverTemperature;

        // Address 420
        private bool _isControlPilotFault;
        private bool _isEvFault;
        private bool _isPlcCommFault;
        private bool _isCanCommFault;
        private bool _isMcuFault;
        private bool _isEvNegotiationFailed;
        private bool _isEmergencyStopActivated;
        private bool _isConnectorLockFault;
        private bool _isConnectorPositionSensorFault;
        private bool _isPowerMeterFault;
        private bool _isCabinetDoorOpened;
        private bool _isWaterIngressFault;
        private bool _isTiltFault;
        private bool _isGunOverTemperature;
        private bool _isDcCurrentSensorFault;
        private bool _isGeneralFault;
        public bool IsControlPilotFault => _isControlPilotFault;
        public bool IsEvFault => _isEvFault;
        public bool IsPlcCommFault => _isPlcCommFault;
        public bool IsCanCommFault => _isCanCommFault;
        public bool IsMcuFault => _isMcuFault;
        public bool IsEvNegotiationFailed => _isEvNegotiationFailed;
        public bool IsEmergencyStopActivated => _isEmergencyStopActivated;
        public bool IsConnectorLockFault => _isConnectorLockFault;
        public bool IsConnectorPositionSensorFault => _isConnectorPositionSensorFault;
        public bool IsPowerMeterFault => _isPowerMeterFault;
        public bool IsCabinetDoorOpened => _isCabinetDoorOpened;
        public bool IsWaterIngressFault => _isWaterIngressFault;
        public bool IsTiltFault => _isTiltFault;
        public bool IsGunOverTemperature => _isGunOverTemperature;
        public bool IsDcCurrentSensorFault => _isDcCurrentSensorFault;
        public bool IsGeneralFault => _isGeneralFault;

        // Address 419-420: FaultCode (32비트: 0~31비트, 0=복구, 1=오류발생)
        // 419번지가 하위 16비트(0~15), 420번지가 상위 16비트(16~31)
        private uint _faultCode;
        public uint FaultCode => _faultCode;

        /// <summary>
        /// FaultCode를 분석하여 오류 설명 목록을 반환합니다.
        /// </summary>
        public List<string> GetFaultDescriptions()
        {
            List<string> descriptions = new List<string>();
            
            if (_faultCode == 0)
            {
                descriptions.Add("정상 (오류 없음)");
                return descriptions;
            }

            // Address 419 (비트 0~15): 전원 관련 이상 상태
            if ((_faultCode & (1U << 0)) != 0) descriptions.Add("메인 브레이커 트립");
            if ((_faultCode & (1U << 1)) != 0) descriptions.Add("입력 과전압");
            if ((_faultCode & (1U << 2)) != 0) descriptions.Add("입력 저전압");
            if ((_faultCode & (1U << 3)) != 0) descriptions.Add("입력 과전류");
            if ((_faultCode & (1U << 4)) != 0) descriptions.Add("접지 오류");
            if ((_faultCode & (1U << 5)) != 0) descriptions.Add("입력 MC 오류");
            if ((_faultCode & (1U << 6)) != 0) descriptions.Add("출력 MC 오류");
            if ((_faultCode & (1U << 7)) != 0) descriptions.Add("출력 과전압");
            if ((_faultCode & (1U << 8)) != 0) descriptions.Add("출력 과전류");
            if ((_faultCode & (1U << 9)) != 0) descriptions.Add("출력 누전");
            if ((_faultCode & (1U << 10)) != 0) descriptions.Add("프리차징 오류");
            if ((_faultCode & (1U << 11)) != 0) descriptions.Add("파워모듈 오류");
            if ((_faultCode & (1U << 12)) != 0) descriptions.Add("과온도");
            // 비트 13~15는 Address 419에서 미사용 또는 예약

            // Address 420 (비트 16~31): 기타 상태
            if ((_faultCode & (1U << 16)) != 0) descriptions.Add("컨트롤 파일럿 오류");
            if ((_faultCode & (1U << 17)) != 0) descriptions.Add("EV 오류");
            if ((_faultCode & (1U << 18)) != 0) descriptions.Add("PLC 통신 오류");
            if ((_faultCode & (1U << 19)) != 0) descriptions.Add("CAN 통신 오류");
            if ((_faultCode & (1U << 20)) != 0) descriptions.Add("MCU 오류");
            if ((_faultCode & (1U << 21)) != 0) descriptions.Add("EV 협상 실패");
            if ((_faultCode & (1U << 22)) != 0) descriptions.Add("비상정지 활성화");
            if ((_faultCode & (1U << 23)) != 0) descriptions.Add("커넥터 잠금 오류");
            if ((_faultCode & (1U << 24)) != 0) descriptions.Add("커넥터 위치 센서 오류");
            if ((_faultCode & (1U << 25)) != 0) descriptions.Add("전력량계 오류");
            if ((_faultCode & (1U << 26)) != 0) descriptions.Add("캐비닛 도어 열림");
            if ((_faultCode & (1U << 27)) != 0) descriptions.Add("침수 오류");
            if ((_faultCode & (1U << 28)) != 0) descriptions.Add("경사 오류");
            if ((_faultCode & (1U << 29)) != 0) descriptions.Add("건 과온도");
            if ((_faultCode & (1U << 30)) != 0) descriptions.Add("DC 전류 센서 오류");
            if ((_faultCode & (1U << 31)) != 0) descriptions.Add("일반 오류");

            return descriptions;
        }

        /// <summary>
        /// FaultCode를 분석하여 오류 설명 문자열을 반환합니다.
        /// </summary>
        public string GetFaultDescriptionString()
        {
            var descriptions = GetFaultDescriptions();
            return descriptions.Count > 0 ? string.Join(", ", descriptions) : "정상";
        }

        public void LoadFromRawData(ushort[] raw)
        {
            if (raw == null || raw.Length != _size)
                throw new ArgumentException("Invalid raw data length.");


            // Address 400: 상태 비트
            _isControlBoardReady = GetBit(raw[0], 0);
            _isChargeReady = GetBit(raw[0], 1);
            _isPlugConnected = GetBit(raw[0], 2);
            _isDoorClosed = GetBit(raw[0], 3);
            _isChargeRunning = GetBit(raw[0], 4);
            _isChargeFinished = GetBit(raw[0], 5);
            _isFaultOccured = GetBit(raw[0], 6);
            _isResetRequested = GetBit(raw[0], 7);
            _isConnectorLocked = GetBit(raw[0], 9);

            // Address 401
            _controlboardVersion = ConvertHexToVersionString(raw[1]);

            // Address 402
            _protocolVersion = ConvertHexToVersionString(raw[2]);

            // Address 403
            _runCount = raw[3];

            // Address 404
            _remainedMinute = raw[4];

            // Address 405
            _soc = raw[5];

            // Address 407-408
            byte[] low = BitConverter.GetBytes(raw[7]);
            byte[] high = BitConverter.GetBytes(raw[8]);
            byte[] value = new byte[4];
            value[0] = high[0];
            value[1] = high[1];
            value[2] = low[0];
            value[3] = low[1];
            _powerInKw = BitConverter.ToUInt32(value, 0) / 100.0;

            // Address 409
            _voltageOut = (float) (raw[9] / 10.0);

            // Address 410
            _currentOut = (float) (raw[10] / 10.0);

            // Address 411-412: 파워모듈 상태 (비트플래그)
            _powerModuleStates = new bool[32];
            for (int i = 0; i < 16; i++)
                _powerModuleStates[i] = GetBit(raw[11], i);
            for (int i = 0; i < 16; i++)
                _powerModuleStates[i + 16] = GetBit(raw[12], i);

            // Address 418
            _powerMeterVersion = ConvertHexToVersionString(raw[18]);

            // Address 419: 전원 관련 이상 상태
            _isMainBreakerTripped = GetBit(raw[19], 0);
            _isInputOverVoltage = GetBit(raw[19], 1);
            _isInputUnderVoltage = GetBit(raw[19], 2);
            _isInputOverCurrent = GetBit(raw[19], 3);
            _isGroundFault = GetBit(raw[19], 4);
            _isInputMcFault = GetBit(raw[19], 5);
            _isOutputMcFault = GetBit(raw[19], 6);
            _isOutputOverVoltage = GetBit(raw[19], 7);
            _isOutputOverCurrent = GetBit(raw[19], 8);
            _isOutputLeakage = GetBit(raw[19], 9);
            _isPreChargingFault = GetBit(raw[19], 10);
            _isPowerModuleFault = GetBit(raw[19], 11);
            _isOverTemperature = GetBit(raw[19], 12);

            // Address 420: 기타 상태
            _isControlPilotFault = GetBit(raw[20], 0);
            _isEvFault = GetBit(raw[20], 1);
            _isPlcCommFault = GetBit(raw[20], 2);
            _isCanCommFault = GetBit(raw[20], 3);
            _isMcuFault = GetBit(raw[20], 4);
            _isEvNegotiationFailed = GetBit(raw[20], 5);
            _isEmergencyStopActivated = GetBit(raw[20], 6);
            _isConnectorLockFault = GetBit(raw[20], 7);
            _isConnectorPositionSensorFault = GetBit(raw[20], 8);
            _isPowerMeterFault = GetBit(raw[20], 9);
            _isCabinetDoorOpened = GetBit(raw[20], 10);
            _isWaterIngressFault = GetBit(raw[20], 11);
            _isTiltFault = GetBit(raw[20], 12);
            _isGunOverTemperature = GetBit(raw[20], 13);
            _isDcCurrentSensorFault = GetBit(raw[20], 14);
            _isGeneralFault = GetBit(raw[20], 15);

            // Address 419-420: FaultCode 계산
            // 419번지(하위 16비트, 0~15)와 420번지(상위 16비트, 16~31)를 합쳐서 32비트 값으로 생성
            // 각 비트: 0=복구, 1=오류발생
            _faultCode = (uint)((raw[20] << 16) | raw[19]);
        }

        public RxData Clone()
        {
           return (RxData)this.MemberwiseClone();
        }
    }
}
