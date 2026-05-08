using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvChargerUI.Services.DspControl.Chaevi
{
    public class RxData
    {
        private static int _size = 20;

        // Address 400
        private bool _isReady;              // 0bit
        private bool _isConnectorPlugged;   // 1bit
        private bool _isChargeRunning;      // 2bit
        private bool _isChargeFinished;     // 3bit
        private bool _isFaultOccured;       // 4bit
        private bool _flagSaveLog;          // 5bit
        private bool _connectorStatus;      // 8bit
        private bool _coonectorLocked;      // 9bit
        private bool _isEmergencyStopActivated; // 10bit
        private bool _ioTest;                   // 14bit
        private bool _loadTest;                 // 15bit

        private float _voltage;             // Address 401
        private float _current;             // Address 402
        private float _remainSeconds;       // Address 403
        private int _soc;                   // Address 404
        private float _targerVoltage;       // Address 405
        private float _targerCurrent;       // Address 406
        private double _powerMeterInKw;     // Address 407-408

        private byte _comboSequence;        // Address 409 : 0-3 bit
        private byte _stopCode;             // Address 409 : 4-8 bit
        private byte _pmtSequence;          // Address 409 : 9-15 bit

        private ushort _faultCode;          // Address 410
        private string _faVersion;          // Address 411

        private byte _cpMinorVersion;       // Address 412 : 0-7bit
        private byte _cpMajorVersion;       // Address 412 : 8-15bit

        private ushort _pmSequence;         // Address 413 : 0-7bit
        private ushort _pmAliveCount;       // Address 413 : 8-15bit

        private ushort _pmAliveFlagMsb;     // Address 414
        private ushort _pmAliveFlagLsb;     // Address 415

        private byte _chargerType;          // Address 416 : 0-2bit
        private byte _motorType;            // Address 416 : 3-5bit
        private byte _capacity;             // Address 416 : 6-8bit
        private bool _cpCount;              // Address 416 : 9 bit
        private bool _powerLimitSet;        // Address 416 : 10bit

        private sbyte _couplerTempM;        // Address 417 : 0-7bit
        private sbyte _couplerTempP;        // Address 417 : 8-15bit

        private sbyte _cableTempM;          // Address 418 : 0-7bit
        private sbyte _cableTempP;          // Address 418 : 8-15bit

        private sbyte _coolerTempM;        // Address 419 : 0-7bit
        private sbyte _coolerTempP;        // Address 419 : 8-15bit


        // Address 400
        public bool IsReady => _isReady;                        // 0bit
        public bool IsConnectorPlugged => _isConnectorPlugged;  // 1bit
        public bool IsChargeRunning => _isChargeRunning;        // 2bit
        public bool IsChargeFinished => _isChargeFinished;      // 3bit
        public bool IsFaultOccured => _isFaultOccured;          // 4bit
        public bool FlagSaveLog => _flagSaveLog;                // 5bit
        public bool ConnectorStatus => _connectorStatus;        // 8bit
        public bool CoonectorLocked => _coonectorLocked;        // 9bit
        public bool IsEmergencyStopActivated => _isEmergencyStopActivated; // 10bit
        public bool IoTest => _ioTest;                          // 14bit
        public bool LoadTest => _loadTest;                      // 15bit

        public float Voltage => _voltage;                       // Address 401
        public float Current => _current;                       // Address 402
        public float RemainSeconds => _remainSeconds;           // Address 403
        public int Soc => _soc;                                 // Address 404
        public float TargerVoltage => _targerVoltage;           // Address 405
        public float TargerCurrent => _targerCurrent;           // Address 406
        public double PowerMeterInKw => _powerMeterInKw;        // Address 407-408

        public byte ComboSequence => _comboSequence;            // Address 409 : 0-3 bit
        public byte StopCode => _stopCode;                      // Address 409 : 4-8 bit
        public byte PmtSequence => _pmtSequence;                // Address 409 : 9-15 bit

        public ushort FaultCode => _faultCode;                  // Address 410
        public string FaVersion => _faVersion;                  // Address 411

        public byte CpMinorVersion => _cpMinorVersion;          // Address 412 : 0-7bit
        public byte CpMajorVersion => _cpMajorVersion;          // Address 412 : 8-15bit

        public ushort PmSequence => _pmSequence;                // Address 413 : 0-7bit
        public ushort PmAliveCount => _pmAliveCount;            // Address 413 : 8-15bit

        public ushort PmAliveFlagMsb => _pmAliveFlagMsb;        // Address 414
        public ushort PmAliveFlagLsb => _pmAliveFlagLsb;        // Address 415

        public byte ChargerType => _chargerType;                // Address 416 : 0-2bit
        public byte MotorType => _motorType;                    // Address 416 : 3-5bit
        public byte Capacity => _capacity;                      // Address 416 : 6-8bit
        public bool CpCount => _cpCount;                        // Address 416 : 9 bit
        public bool PowerLimitSet => _powerLimitSet;            // Address 416 : 10bit

        public sbyte CouplerTempM => _couplerTempM;             // Address 417 : 0-7bit
        public sbyte CouplerTempP => _couplerTempP;             // Address 417 : 8-15bit

        public sbyte CableTempM => _cableTempM;                 // Address 418 : 0-7bit
        public sbyte CableTempP => _cableTempP;                 // Address 418 : 8-15bit

        public sbyte CoolerTempM => _coolerTempM;               // Address 419 : 0-7bit
        public sbyte CoolerTempP => _coolerTempP;               // Address 419 : 8-15bit


        private bool GetBit(ushort value, int bit) => ((value >> bit) & 1) == 1;
        private byte GetBits(ushort value, int offset, int length)
        {
            int mask = (1 << length) - 1;
            return (byte)((value >> offset) & mask);
        }
        private sbyte ToSByte(byte b) => unchecked((sbyte)b);

        private static string ConvertHexToVersionString(ushort reg)
        {
            int n3 = (reg >> 12) & 0xF;
            int n2 = (reg >> 8) & 0xF;
            int n1 = (reg >> 4) & 0xF;
            int n0 = reg & 0xF;

            return $"{n3:X}.{n2:X}.{n1:X}.{n0:X}";
        }

        public void LoadFromRawData(ushort[] raw)
        {
            if (raw == null || raw.Length != _size)
                throw new ArgumentException("Invalid raw data length.");

            ushort r400 = raw[0];
            _isReady = GetBit(r400, 0);
            _isConnectorPlugged = GetBit(r400, 1);
            _isChargeRunning = GetBit(r400, 2);
            _isChargeFinished = GetBit(r400, 3);
            _isFaultOccured = GetBit(r400, 4);
            _flagSaveLog = GetBit(r400, 5);
            _connectorStatus = GetBit(r400, 8);
            _coonectorLocked = GetBit(r400, 9);
            _isEmergencyStopActivated = GetBit(r400, 10);
            _ioTest = GetBit(r400, 14);
            _loadTest = GetBit(r400, 15);

            // Address 401 ~ 406 
            _voltage = (float)((float) raw[1] / 10.0);
            _current = (float)((float)raw[2] / 10.0);
            _remainSeconds = raw[3];
            _soc = raw[4];
            _targerVoltage = (float)((float)raw[5] / 10.0);
            _targerCurrent = (float)((float)raw[6] / 10.0);

            // Address 407-408 
            uint pmRaw = (uint)(raw[7] << 16 | raw[8]);
            _powerMeterInKw = pmRaw / 1000.0f;

            // Address 409
            ushort r409 = raw[9];
            _comboSequence = GetBits(r409, 0, 4);
            _stopCode = GetBits(r409, 4, 5);
            _pmtSequence = GetBits(r409, 9, 7);

            // Address 410
            _faultCode = raw[10];

            // Address 411 
            _faVersion = ConvertHexToVersionString(raw[11]);

            // Address 412
            ushort r412 = raw[12];
            _cpMinorVersion = (byte)(r412 & 0xFF);
            _cpMajorVersion = (byte)(r412 >> 8);

            // Address 413
            ushort r413 = raw[13];
            _pmSequence = (byte)(r413 & 0xFF);
            _pmAliveCount = (byte)(r413 >> 8);

            // Address 414-415
            _pmAliveFlagMsb = raw[14];
            _pmAliveFlagLsb = raw[15];

            // Address 416
            ushort r416 = raw[16];
            _chargerType = GetBits(r416, 0, 3);
            _motorType = GetBits(r416, 3, 3);
            _capacity = GetBits(r416, 6, 3);
            _cpCount = GetBit(r416, 9);
            _powerLimitSet = GetBit(r416, 10);

            // Address 417
            ushort r417 = raw[17];
            _couplerTempM = ToSByte((byte)(r417 & 0xFF));
            _couplerTempP = ToSByte((byte)(r417 >> 8));

            // Address 418
            ushort r418 = raw[18];
            _cableTempM = ToSByte((byte)(r418 & 0xFF));
            _cableTempP = ToSByte((byte)(r418 >> 8));

            // Address 419
            ushort r419 = raw[19];
            _coolerTempM = ToSByte((byte)(r419 & 0xFF));
            _coolerTempP = ToSByte((byte)(r419 >> 8));
        }

        public RxData Clone()
        {
            return (RxData)this.MemberwiseClone();
        }
    }
}
