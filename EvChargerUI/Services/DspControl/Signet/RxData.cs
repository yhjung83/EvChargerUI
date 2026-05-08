using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvChargerUI.Services.DspControl.Signet
{
    public class RxData
    {
        private static int _size = 41;

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


        private ushort _chargerStatus; // Address 400

        // Addreess 401
        private bool _isPlugIn; // 0bit
        private bool _isButton1On; // 1bit
        private bool _isButton2On; // 2bit
        private bool _isButton3On; // 3bit
        private bool _isButton4On; // 4bit
        private bool _isEmergencyButtonOn; // 5bit
        private bool _isLimitSwitch1On; // 6bit
        private bool _isLimitSwitch2On; // 7bit
        private bool _isLimitSwitch3On; // 8bit
        private bool _isLimitSwitch4On; // 9bit
        private bool _isEvseCurrentLimitAchieved; // 10bit
        private bool _isEvseVoltageLimitAchieved; // 11bit
        private bool _isEvsePowerLimitAchieved; // 12bit
        private bool _isPlugErrorBeforePayment; // 14bit
        private bool _isDebugRequested; // 15bit

        private ushort _errorCode; // Address 402
        private ushort _plcModemErrorCode; // Address 403

        private double _powerMeter; // Address 404 - 407
        private ushort _finishCode; // Address 408
        private ushort _runCount; // Address 409

        // Address 410
        private bool _generalFlag; // 1bit
        private bool _operationResponse; // 2bit
        private bool _reportInitFlag; // 3bit
        private bool _startGqFlag; // 4bit
        private bool _reportSlacFlag; // 5bit
        private bool _tlsControl; // 6bit
        private bool _reportSdpFlag; // 7bit
        private bool _reportProtocolRequest; // 8bit
        private bool _reportProtocolResponse; // 9bit
        private bool _reportV2gFlag; // 10bit
        private bool _sessionSetupFlag; // 11bit
        private bool _serviceDiscoveryFlag; // 12bit
        private bool _servicePaymentSelectionFlag; // 13bit
        private bool _paymentDetails; // 14bit
        private bool _serviceDetails; // 15bit

        // Address 411
        private bool _contractAuthenticationFlag; // 0bit
        private bool _authorizationRequest; // 1bit
        private bool _chargeParameterFlag; // 2bit
        private bool _cableCheckFlag; // 3bit
        private bool _prechargeFlag; // 4bit
        private bool _powerDeliveryOnFlag; // 5bit
        private bool _currentDemandFlag; // 6bit
        private bool _powerDeliveryOffFlag; // 7bit
        private bool _weldingDetectionFlag; // 8bit
        private bool _sessionStopFlag; // 9bit
        private bool _allStopFlag; // 10bit
        private bool _certificateInstallation; // 11bit
        private bool _certificateUpdate; // 12bit

        private ushort _plugNumber; // Address 412
        private ushort _powerModule1State; // Address 413
        private ushort _powerModule2State; // Address 414
        private ushort _powerModule3State; // Address 415
        private short _couplerxPTH; // Address 416
        private short _couplerxNTH; // Address 417
        private short _couplerxOutletPTH; // Address 418
        private short _couplerxOutletNTH; // Address 419
        private short _chillerInTH; // Address 420
        private short _chillerOutTH; // Address 421
        private short _tankTH; // Address 422
        private ushort _levelSensorAlarm; // Address 423
        private ushort _levelSensorWarning; // Address 424
        private ushort _chillerAlarm; // Address 425
        private ushort _chillerFault; // Address 426
        private ushort _deratingLevel; // Address 427
        private ushort _deratingReason; // Address 428
        private ushort _remainingTimeBulk; // Address 429
        private ushort _remainingTimeFull; // Address 430
        private ushort _evTargerVoltage; // Address 431
        private ushort _evTargerCurrent; // Address 432
        private ushort _evMaxVoltageLimit; // Address 433
        private ushort _evMaxCurrentLimit; // Address 434
        private ushort _evMaxPowerLimit; // Address 435
        private ushort _soc; // Address 436
        private ushort _bulkSoc; // Address 437
        private ushort _fullSoc; // Address 438
        private ushort _energyCapacity; // Address 439
        private ushort _energyRequest; // Address 440


        public ushort ChargerStatus => _chargerStatus;

        // Address 401
        public bool IsPlugIn => _isPlugIn;
        public bool IsButton1On => _isButton1On;
        public bool IsButton2On => _isButton2On;
        public bool IsButton3On => _isButton3On;
        public bool IsButton4On => _isButton4On;
        public bool IsEmergencyButtonOn => _isEmergencyButtonOn;
        public bool IsLimitSwitch1On => _isLimitSwitch1On;
        public bool IsLimitSwitch2On => _isLimitSwitch2On;
        public bool IsLimitSwitch3On => _isLimitSwitch3On;
        public bool IsLimitSwitch4On => _isLimitSwitch4On;
        public bool IsEvseCurrentLimitAchieved => _isEvseCurrentLimitAchieved;
        public bool IsEvseVoltageLimitAchieved => _isEvseVoltageLimitAchieved;
        public bool IsEvsePowerLimitAchieved => _isEvsePowerLimitAchieved;
        public bool IsPlugErrorBeforePayment => _isPlugErrorBeforePayment;
        public bool IsDebugRequested => _isDebugRequested;

        // Address 402~403
        public ushort ErrorCode => _errorCode;
        public ushort PlcModemErrorCode => _plcModemErrorCode;

        // Address 404~407
        public double PowerMeter => _powerMeter;

        // Address 408~409
        public ushort FinishCode => _finishCode;
        public ushort RunCount => _runCount;

        // Address 410
        public bool GeneralFlag => _generalFlag;
        public bool OperationResponse => _operationResponse;
        public bool ReportInitFlag => _reportInitFlag;
        public bool StartGqFlag => _startGqFlag;
        public bool ReportSlacFlag => _reportSlacFlag;
        public bool TlsControl => _tlsControl;
        public bool ReportSdpFlag => _reportSdpFlag;
        public bool ReportProtocolRequest => _reportProtocolRequest;
        public bool ReportProtocolResponse => _reportProtocolResponse;
        public bool ReportV2gFlag => _reportV2gFlag;
        public bool SessionSetupFlag => _sessionSetupFlag;
        public bool ServiceDiscoveryFlag => _serviceDiscoveryFlag;
        public bool ServicePaymentSelectionFlag => _servicePaymentSelectionFlag;
        public bool PaymentDetails => _paymentDetails;
        public bool ServiceDetails => _serviceDetails;

        // Address 411
        public bool ContractAuthenticationFlag => _contractAuthenticationFlag;
        public bool AuthorizationRequest => _authorizationRequest;
        public bool ChargeParameterFlag => _chargeParameterFlag;
        public bool CableCheckFlag => _cableCheckFlag;
        public bool PrechargeFlag => _prechargeFlag;
        public bool PowerDeliveryOnFlag => _powerDeliveryOnFlag;
        public bool CurrentDemandFlag => _currentDemandFlag;
        public bool PowerDeliveryOffFlag => _powerDeliveryOffFlag;
        public bool WeldingDetectionFlag => _weldingDetectionFlag;
        public bool SessionStopFlag => _sessionStopFlag;
        public bool AllStopFlag => _allStopFlag;
        public bool CertificateInstallation => _certificateInstallation;
        public bool CertificateUpdate => _certificateUpdate;

        // Address 412~415
        public ushort PlugNumber => _plugNumber;
        public ushort PowerModule1State => _powerModule1State;
        public ushort PowerModule2State => _powerModule2State;
        public ushort PowerModule3State => _powerModule3State;

        // Address 416~422 (온도, short)
        public short CouplerxPTH => _couplerxPTH;
        public short CouplerxNTH => _couplerxNTH;
        public short CouplerxOutletPTH => _couplerxOutletPTH;
        public short CouplerxOutletNTH => _couplerxOutletNTH;
        public short ChillerInTH => _chillerInTH;
        public short ChillerOutTH => _chillerOutTH;
        public short TankTH => _tankTH;

        // Address 423~428
        public ushort LevelSensorAlarm => _levelSensorAlarm;
        public ushort LevelSensorWarning => _levelSensorWarning;
        public ushort ChillerAlarm => _chillerAlarm;
        public ushort ChillerFault => _chillerFault;
        public ushort DeratingLevel => _deratingLevel;
        public ushort DeratingReason => _deratingReason;

        // Address 429~430
        public ushort RemainingTimeBulk => _remainingTimeBulk;
        public ushort RemainingTimeFull => _remainingTimeFull;

        // Address 431~435
        public ushort EvTargerVoltage => _evTargerVoltage;
        public ushort EvTargerCurrent => _evTargerCurrent;
        public ushort EvMaxVoltageLimit => _evMaxVoltageLimit;
        public ushort EvMaxCurrentLimit => _evMaxCurrentLimit;
        public ushort EvMaxPowerLimit => _evMaxPowerLimit;

        // Address 436~440
        public ushort Soc => _soc;
        public ushort BulkSoc => _bulkSoc;
        public ushort FullSoc => _fullSoc;
        public ushort EnergyCapacity => _energyCapacity;
        public ushort EnergyRequest => _energyRequest;

        public void LoadFromRawData(ushort[] raw)
        {
            if (raw == null || raw.Length != _size)
                throw new ArgumentException("Invalid raw data length.");


            // Address 400
            _chargerStatus = (ushort)(raw[0] - 48);

            // Address 401
            _isPlugIn = GetBit(raw[1], 0);
            _isButton1On = GetBit(raw[1], 1);
            _isButton2On = GetBit(raw[1], 2);
            _isButton3On = GetBit(raw[1], 3);
            _isButton4On = GetBit(raw[1], 4);
            _isEmergencyButtonOn = GetBit(raw[1], 5);
            _isLimitSwitch1On = GetBit(raw[1], 6);
            _isLimitSwitch2On = GetBit(raw[1], 7);
            _isLimitSwitch3On = GetBit(raw[1], 8);
            _isLimitSwitch4On = GetBit(raw[1], 9);
            _isEvseCurrentLimitAchieved = GetBit(raw[1], 10);
            _isEvseVoltageLimitAchieved = GetBit(raw[1], 11);
            _isEvsePowerLimitAchieved = GetBit(raw[1], 12);
            _isPlugErrorBeforePayment = GetBit(raw[1], 14);
            _isDebugRequested = GetBit(raw[1], 15);

            // Address 402~403
            _errorCode = raw[2];
            _plcModemErrorCode = raw[3];

            // Address 404~407 (uint 32bit)
            _powerMeter = (float) ((uint)ToInt32(raw[5], raw[4])) / 10000.0;

            // Address 408~409
            _finishCode = raw[8];
            _runCount = raw[9];

            // Address 410
            _generalFlag = GetBit(raw[10], 1);
            _operationResponse = GetBit(raw[10], 2);
            _reportInitFlag = GetBit(raw[10], 3);
            _startGqFlag = GetBit(raw[10], 4);
            _reportSlacFlag = GetBit(raw[10], 5);
            _tlsControl = GetBit(raw[10], 6);
            _reportSdpFlag = GetBit(raw[10], 7);
            _reportProtocolRequest = GetBit(raw[10], 8);
            _reportProtocolResponse = GetBit(raw[10], 9);
            _reportV2gFlag = GetBit(raw[10], 10);
            _sessionSetupFlag = GetBit(raw[10], 11);
            _serviceDiscoveryFlag = GetBit(raw[10], 12);
            _servicePaymentSelectionFlag = GetBit(raw[10], 13);
            _paymentDetails = GetBit(raw[10], 14);
            _serviceDetails = GetBit(raw[10], 15);

            // Address 411
            _contractAuthenticationFlag = GetBit(raw[11], 0);
            _authorizationRequest = GetBit(raw[11], 1);
            _chargeParameterFlag = GetBit(raw[11], 2);
            _cableCheckFlag = GetBit(raw[11], 3);
            _prechargeFlag = GetBit(raw[11], 4);
            _powerDeliveryOnFlag = GetBit(raw[11], 5);
            _currentDemandFlag = GetBit(raw[11], 6);
            _powerDeliveryOffFlag = GetBit(raw[11], 7);
            _weldingDetectionFlag = GetBit(raw[11], 8);
            _sessionStopFlag = GetBit(raw[11], 9);
            _allStopFlag = GetBit(raw[11], 10);
            _certificateInstallation = GetBit(raw[11], 11);
            _certificateUpdate = GetBit(raw[11], 12);

            // Address 412 ~ 440
            _plugNumber = raw[12];
            _powerModule1State = raw[13];
            _powerModule2State = raw[14];
            _powerModule3State = raw[15];
            _couplerxPTH = (short)(raw[16] - 40);
            _couplerxNTH = (short)(raw[17] - 40);
            _couplerxOutletPTH = (short)(raw[18] - 40);
            _couplerxOutletNTH = (short)(raw[19] - 40);
            _chillerInTH = (short)(raw[20] - 40);
            _chillerOutTH = (short)(raw[21] - 40);
            _tankTH = (short)(raw[22] - 40);
            _levelSensorAlarm = raw[23];
            _levelSensorWarning = raw[24];
            _chillerAlarm = raw[25];
            _chillerFault = raw[26];
            _deratingLevel = raw[27];
            _deratingReason = raw[28];
            _remainingTimeBulk = raw[29];
            _remainingTimeFull = raw[30];
            _evTargerVoltage = raw[31];
            _evTargerCurrent = raw[32];
            _evMaxVoltageLimit = raw[33];
            _evMaxCurrentLimit = raw[34];
            _evMaxPowerLimit = raw[35];
            _soc = (ushort)(raw[36] / 2);
            _bulkSoc = (ushort)(raw[37] / 2);
            _fullSoc = (ushort)(raw[38] / 2);
            _energyCapacity = raw[39];
            _energyRequest = raw[40];
        }


        public RxData Clone()
        {
            return (RxData)this.MemberwiseClone();
        }
    }

}
