using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Security.Authentication.ExtendedProtection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace EvChargerUI.Services.DspControl.Signet
{
    public class TxData
    {
        private static int _size = 22;

        private ushort _hmiCommand; // Address 200
        private ushort _cpoCode;    // Address 201
        private ushort _marketCode; // Address 202
        private ushort _chargeMethod;   // Address 203
        private ushort _chillerCode;    // Address 204
        private ushort _imdCode;    // Address 205
        private ushort _powerMeterCode; // Address 206
        private ushort _cableType;  // Address 207
        private ushort _chargePowerCode;    // Address 208
        private ushort _couplerPowerLimit;  // Address 209
        private ushort _couplerCurrentLimit;    // Address 210
        private ushort _evseMaxCurrent;     // Address 211
        private ushort _evseMaxVoltage;     // Address 212
        private ushort _evseMaxPower;       // Address 213
        private ushort _modeCode;       // Address 214
        private ushort _settingVoltage; // Address 215
        private ushort _settingCurrent; // Address 216
        private ushort _settingModuleCount; // Address 217
        private ushort _settingVoltageLevel;    // Address 218
        private ushort _protocolVersion;    // Address 219
        private ushort _runCount;       // Address 220
        // Address 221
        private bool _resetHmiPower;    // 0bit
        private bool _resetPlc;         // 1bit
        private bool _resetPayment;     // 2bit
        private bool _resetModem;       // 3bit
        private bool _resetMainboard;   // 4bit
        private bool _resetPower;       // 5bit
        private bool _resetPowerCabinet;    // 6bit


        public ushort HmiCommand { set => _hmiCommand = value; }
        public ushort CpoCode { set => _cpoCode = value; }
        public ushort MarketCode { set => _marketCode = value; }
        public ushort ChargeMethod { set => _chargeMethod = value; }
        public ushort ChillerCode { set => _chillerCode = value; }
        public ushort ImdCode { set => _imdCode = value; }
        public ushort PowerMeterCode { set => _powerMeterCode = value; }
        public ushort CableType { set => _cableType = value; }
        public ushort ChargePowerCode { set => _chargePowerCode = value; }
        public ushort CouplerPowerLimit { set => _couplerPowerLimit = value; }
        public ushort CouplerCurrentLimit { set => _couplerCurrentLimit = value; }
        public ushort EvseMaxCurrent { set => _evseMaxCurrent = value; }
        public ushort EvseMaxVoltage { set => _evseMaxVoltage = value; }
        public ushort EvseMaxPower { set => _evseMaxPower = value; }
        public ushort ModeCode { set => _modeCode = value; }
        public ushort SettingVoltage { set => _settingVoltage = value; }
        public ushort SettingCurrent { set => _settingCurrent = value; }
        public ushort SettingModuleCount { set => _settingModuleCount = value; }
        public ushort SettingVoltageLevel { set => _settingVoltageLevel = value; }
        public ushort ProtocolVersion { set => _protocolVersion = value; }
        public ushort RunCount { set => _runCount = value; }

        // Bit-wise flags from Address 221
        public bool ResetHmiPower { set => _resetHmiPower = value; }
        public bool ResetPlc { set => _resetPlc = value; }
        public bool ResetPayment { set => _resetPayment = value; }
        public bool ResetModem { set => _resetModem = value; }
        public bool ResetMainboard { set => _resetMainboard = value; }
        public bool ResetPower { set => _resetPower = value; }
        public bool ResetPowerCabinet { set => _resetPowerCabinet = value; }

        public int ChannelNo { get; set; }

        public void Initialize()
        {
            _hmiCommand = 3;
            _cpoCode = 0;
            //_cpoCode = 27;
            _marketCode = 3;
            _chargeMethod = 1;
            _chillerCode = 0;
            //_imdCode = 2;
            _imdCode = 1;

            _powerMeterCode = 2;
            _cableType = 3;
            _chargePowerCode = 4;
            _couplerPowerLimit = 200;
            _couplerCurrentLimit = 200;
            _evseMaxCurrent = 0;
            _evseMaxVoltage = 0;
            _evseMaxPower = 0;
            _modeCode = 0;
            _settingVoltage = 0;
            _settingCurrent = 0;
            _settingModuleCount = 0;
            _settingVoltageLevel = 0;
            _protocolVersion = 0;
            _runCount = 0;

            _resetHmiPower = false;
            _resetPlc = false;
            _resetPayment = false;
            _resetModem = false;
            _resetMainboard = false;
            _resetPower = false;
            _resetPowerCabinet = false;
        }

        public ushort[] ToRawData()
        {
            ushort[] rawData = new ushort[_size];

            rawData[0] = _hmiCommand;
            rawData[1] = _cpoCode;
            rawData[2] = _marketCode;
            rawData[3] = _chargeMethod;
            rawData[4] = _chillerCode;
            rawData[5] = _imdCode;
            rawData[6] = _powerMeterCode;
            rawData[7] = _cableType;                                                        
            rawData[8] = _chargePowerCode;
            rawData[9] = _couplerPowerLimit;
            rawData[10] = _couplerCurrentLimit;
            rawData[11] = _evseMaxCurrent;
            rawData[12] = _evseMaxVoltage;
            rawData[13] = _evseMaxPower;
            rawData[14] = _modeCode;
            rawData[15] = _settingVoltage;
            rawData[16] = _settingCurrent;
            rawData[17] = _settingModuleCount;
            rawData[18] = _settingVoltageLevel;
            rawData[19] = _protocolVersion;
            rawData[20] = _runCount;

            rawData[21] = 0;
            if (_resetHmiPower) rawData[0] |= 1 << 0;
            if (_resetPlc) rawData[0] |= 1 << 1;
            if (_resetPayment) rawData[0] |= 1 << 2;
            if (_resetModem) rawData[0] |= 1 << 3;
            if (_resetMainboard) rawData[0] |= 1 << 4;
            if (_resetPower) rawData[0] |= 1 << 5;
            if (_resetPowerCabinet) rawData[0] |= 1 << 6;

            return rawData;
        }
        public TxData Clone()
        {
            return (TxData)this.MemberwiseClone();
        }
    }
}
