using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication.ExtendedProtection;
using System.Text;
using System.Threading.Tasks;

namespace EvChargerUI.Services.DspControl.Klinelex
{
    public class TxData
    {
        private static int _size = 10;

        // Address 200
        private bool _standBy;
        private bool _ready;
        private bool _run;
        private bool _start;
        private bool _stop;
        private bool _doorOpen;
        private bool _fault;
        private bool _testMode;
        private bool _resetRequest;
        private bool _channel;
        private bool _dualChargeReady;

        // Address 201
        private ushort _cableType;
        // Address 202
        private ushort _powerSetpoint;
        // Address 203
        private ushort _runCount;
        // Address 204
        private bool _noLoadTest;
        private bool _resistiveLoadTest;
        // Address 205-206
        private uint _testModeVoltage;
        // Address 207-208
        private uint _testModeCurrent;
        // Address 209
        private string _protocolVersion;

        // Address 200
        public bool StandBy
        {
            set => _standBy = value;
        }

        public bool Ready
        {
            set => _ready = value;
        }

        public bool Run
        {
            set => _run = value;
        }

        public bool Start
        {
            set => _start = value;
        }

        public bool Stop
        {
            set => _stop = value;
        }
        public bool DoorOpen
        {
            set => _doorOpen = value;
        }
        public bool Fault
        {
            set => _fault = value;
        }
        public bool TestMode
        {
            set => _testMode = value;
        }
        public bool ResetRequest
        {
            set => _resetRequest = value;
        }
        public bool Channel
        {
            set => _channel = value;
        }
        public bool DualChargeReady
        {
            set => _dualChargeReady = value;
        }

        // Address 201
        public ushort CableType
        {
            set => _cableType = value;
        }

        // Address 202
        public ushort PowerSetpoint
        {
            set => _powerSetpoint = value;
        }

        // Address 203
        public ushort RunCount
        {
            set => _runCount = value;
        }

        public int ChannelNo { get; set; }

        public void Initialize()
        {
            // Address 200
            _standBy = false;
            _ready = false;
            _run = false;
            _start = false;
            _stop = false;
            _doorOpen = false;
            _fault = false;
            _testMode = false;
            _resetRequest = false;
            _channel = false;
            _dualChargeReady = false;

            // Address 201~203
            _cableType = 0;
            _powerSetpoint = 0;
            _runCount = 0;

            // Address 204
            _noLoadTest = false;
            _resistiveLoadTest = false;

            // Address 205~208
            _testModeVoltage = 0;
            _testModeCurrent = 0;

            // Address 209
            _protocolVersion = string.Empty;
        }

        public ushort[] ToRawData()
        {
            // Address 200: 상태 비트 묶음
            ushort[] rawData = new ushort[_size];
            rawData[0] = 0;
            if (_standBy) rawData[0] |= 1 << 0;
            if (_ready) rawData[0] |= 1 << 1;
            if (_run) rawData[0] |= 1 << 2;
            if (_start) rawData[0] |= 1 << 3;
            if (_stop) rawData[0] |= 1 << 4;
            if (_doorOpen) rawData[0] |= 1 << 6;
            if (_fault) rawData[0] |= 1 << 7;
            if (_testMode) rawData[0] |= 1 << 9;
            if (_resetRequest) rawData[0] |= 1 << 11;
            if (_channel) rawData[0] |= 1 << 14;
            if (_dualChargeReady) rawData[0] |= 1 << 15;

            // Address 201~203
            rawData[1] = _cableType;
            rawData[2] = _powerSetpoint;
            rawData[3] = _runCount;

            // Address 204: 테스트 상태 비트
            rawData[4] = 0;
            if (_noLoadTest) rawData[4] |= 1 << 14;
            if (_resistiveLoadTest) rawData[4] |= 1 << 15;

            // Address 205~206: TestModeVoltage (uint → 2 ushort)
            rawData[5] = (ushort)(_testModeVoltage >> 16);      // 상위
            rawData[6] = (ushort)(_testModeVoltage & 0xFFFF);   // 하위

            // Address 207~208: TestModeCurrent (uint → 2 ushort)
            rawData[7] = (ushort)(_testModeCurrent >> 16);
            rawData[8] = (ushort)(_testModeCurrent & 0xFFFF);

            // Address 209: ProtocolVersion 
            rawData[9] = 0;

            return rawData;
        }
        public TxData Clone()
        {
            return (TxData)this.MemberwiseClone();
        }
    }
}
