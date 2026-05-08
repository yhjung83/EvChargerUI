using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using EvChargerUI.Commons.Util;

namespace EvChargerUI.Services.DspControl.Chaevi
{
    public class TxData
    {
        private static int _size = 9;
        public int ChannelNo { get; set; }
        // Address 200
        private bool _standBy;      // 0 bit
        private bool _ready;        // 1 bit
        private bool _startPnc;     // 2 bit
        private bool _startEim;     // 3 bit
        private bool _stop;         // 4 bit
        private bool _run;          // 5 bit
        private bool _finish;       // 6 bit
        private bool _cylinderFrontForStart; // 7 bit
        private bool _motorMoveLeft; // 8 bit
        private bool _motorMoveRight; // 9 bit
        private bool _motorMoveDown; // 10 bit
        private bool _motorMoveUp; // 11 bit
        private bool _cylinderFrontForStop; // 12 bit
        private bool _reset;        // 13 bit
        private bool _update;       // 15 bit

        private ushort _type;                 // Address 201
        private ushort _remainConnectTime;    // Address 202
        private ushort _faultCode;            // Address 203

        // Address 204
        private bool _canopy;       // 0 bit
        private bool _outSp;        // 1 bit
        private bool _mc1;          // 2 bit
        private bool _mc2;          // 3 bit
        private bool _mc3;          // 4 bit
        private bool _relayLeftP;   // 5 bit
        private bool _relayLeftN;   // 6 bit
        private bool _relayRightP;  // 7 bit
        private bool _relayRightN;  // 8 bit
        private bool _relayMergeP;  // 9 bit
        private bool _relayMergeN;  // 10 bit
        private bool _relayDischargeLeft;   // 11 bit
        private bool _relayDischargeRight;  // 12 bit
        private bool _fan1;                 // 13 bit
        private bool _fan2;                 // 14 bit
        private bool _coolerOperation;      // 15 bit

        // Address 205
        private bool _chademoD1;    // 0 bit
        private bool _chademoD2;    // 1 bit
        private bool _chademoSol;   // 2 bit
        private bool _cpRelay1;     // 3 bit
        private bool _cpRelay2;     // 4 bit
        private bool _door1;        // 5 bit
        private bool _door2;        // 6 bit
        private bool _door3;        // 7 bit
        private bool _plcReset1;    // 8 bit
        private bool _plcReset2;    // 9 bit
        private bool _motorMove1;   // 10 bit
        private bool _motorMove2;   // 11 bit
        private bool _motorMove3;   // 12 bit
        private bool _motorMove4;   // 13 bit
        private bool _sylinderForward;  // 14 bit
        private bool _sylinderReverse;  // 15 bit

        private ushort _testLoadVoltage;    // Address 206
        private ushort _testLoadCurrent;    // Address 207

        // Address 208
        private byte _chargerType;  // 0-3 bit
        private byte _chargerWatt;  // 4-5 bit
        private bool _avd;          // 6 bit
        private bool _eut;          // 7 bit
        private bool _semi;         // 8 bit
        private bool _doorFault;    // 9 bit
        private byte _mcCount;      // 10-11 bit
        private bool _acd;          // 12 bit
        private byte _signageStatus;    // 13-14bit


        // Address 200
        public bool StandBy { set => _standBy = value; }          // 0 bit
        public bool Ready { set => _ready = value; }            // 1 bit
        public bool StartPnc { set => _startPnc = value; }         // 2 bit
        public bool StartEim { set => _startEim = value; }         // 3 bit
        public bool Stop { set => _stop = value;
            get => _stop;
        }             // 4 bit
        public bool Run { set => _run = value; }              // 5 bit
        public bool Finish { set => _finish = value; }           // 6 bit
        public bool CylinderFrontForStart { set => _cylinderFrontForStart = value; } // 7 bit
        public bool MotorMoveLeft { set => _motorMoveLeft = value; } // 8 bit
        public bool MotorMoveRight { set => _motorMoveRight = value; } // 9 bit
        public bool MotorMoveDown { set => _motorMoveDown = value; } // 10 bit
        public bool MotorMoveUp { set => _motorMoveUp = value; } // 11 bit
        public bool CylinderFrontForStop { set => _cylinderFrontForStop = value; } // 12 bit
        public bool Reset { set => _reset = value; }            // 13 bit
        public bool Update { set => _update = value; }           // 15 bit

        // Address 201 ~ 203
        public ushort Type { set => _type = value; }                 // Address 201
        public ushort RemainConnectTime { set => _remainConnectTime = value; }    // Address 202
        public ushort FaultCodeFlag { set => _faultCode = value; }            // Address 203

        // Address 204
        public bool Canopy { set => _canopy = value; }               // 0 bit
        public bool OutSp { set => _outSp = value; }                // 1 bit
        public bool Mc1 { set => _mc1 = value; }                  // 2 bit
        public bool Mc2 { set => _mc2 = value; }                  // 3 bit
        public bool Mc3 { set => _mc3 = value; }                  // 4 bit
        public bool RelayLeftP { set => _relayLeftP = value; }           // 5 bit
        public bool RelayLeftN { set => _relayLeftN = value; }           // 6 bit
        public bool RelayRightP { set => _relayRightP = value; }          // 7 bit
        public bool RelayRightN { set => _relayRightN = value; }          // 8 bit
        public bool RelayMergeP { set => _relayMergeP = value; }          // 9 bit
        public bool RelayMergeN { set => _relayMergeN = value; }          // 10 bit
        public bool RelayDischargeLeft { set => _relayDischargeLeft = value; }   // 11 bit
        public bool RelayDischargeRight { set => _relayDischargeRight = value; }  // 12 bit
        public bool Fan1 { set => _fan1 = value; }                 // 13 bit
        public bool Fan2 { set => _fan2 = value; }                 // 14 bit
        public bool CoolerOperation { set => _coolerOperation = value; }      // 15 bit

        // Address 205
        public bool ChademoD1 { set => _chademoD1 = value; }        // 0 bit
        public bool ChademoD2 { set => _chademoD2 = value; }        // 1 bit
        public bool ChademoSol { set => _chademoSol = value; }       // 2 bit
        public bool CpRelay1 { set => _cpRelay1 = value; }         // 3 bit
        public bool CpRelay2 { set => _cpRelay2 = value; }         // 4 bit
        public bool Door1 { set => _door1 = value; }            // 5 bit
        public bool Door2 { set => _door2 = value; }            // 6 bit
        public bool Door3 { set => _door3 = value; }            // 7 bit
        public bool PlcReset1 { set => _plcReset1 = value; }        // 8 bit
        public bool PlcReset2 { set => _plcReset2 = value; }        // 9 bit
        public bool MotorMove1 { set => _motorMove1 = value; }       // 10 bit
        public bool MotorMove2 { set => _motorMove2 = value; }       // 11 bit
        public bool MotorMove3 { set => _motorMove3 = value; }       // 12 bit
        public bool MotorMove4 { set => _motorMove4 = value; }       // 13 bit
        public bool SylinderForward { set => _sylinderForward = value; }  // 14 bit
        public bool SylinderReverse { set => _sylinderReverse = value; }  // 15 bit

        // Address 206 ~ 207
        public ushort TestLoadVoltage { set => _testLoadVoltage = value; } // Address 206
        public ushort TestLoadCurrent { set => _testLoadCurrent = value; } // Address 207

        // Address 208
        public byte ChargerType { set => _chargerType = value; }       // 0-3 bit
        public byte ChargerWatt { set => _chargerWatt = value; }       // 4-5 bit
        public bool Avd { set => _avd = value; }               // 6 bit
        public bool Eut { set => _eut = value; }               // 7 bit
        public bool Semi { set => _semi = value; }              // 8 bit
        public bool DoorFault { set => _doorFault = value; }         // 9 bit
        public byte McCount { set => _mcCount = value; }           // 10-11 bit
        public bool Acd { set => _acd = value; }               // 12 bit
        public byte SignageStatus { set => _signageStatus = value; }     // 13-14 bit

        private FileLogger _logger = ((App)Application.Current).DspLogger;

        private void SetBit(ref ushort v, int bit, bool on)
        {
            if (on) v = (ushort)(v | (1 << bit));
        }

        private void SetBits(ref ushort v, int offset, int count, int value)
        {
            int mask = ((1 << count) - 1) << offset;
            v = (ushort)((v & ~mask) | ((value << offset) & mask));
        }

        public void Initialize()
        {
            // Address 200
            _standBy = false;     // 0 bit
            _ready = false;       // 1 bit
            _startPnc = false;    // 2 bit
            _startEim = false;    // 3 bit
            _stop = false;        // 4 bit
            _run = false;         // 5 bit
            _finish = false;      // 6 bit
            _cylinderFrontForStart = false; // 7 bit
            _motorMoveLeft = false; // 8 bit
            _motorMoveRight = false; // 9 bit
            _motorMoveDown = false; // 10 bit
            _motorMoveUp = false; // 11 bit
            _cylinderFrontForStop = false; // 12 bit
            _reset = false;       // 13 bit
            _update = false;      // 15 bit

            // Address 201~203
            _type = 0;
            _remainConnectTime = 0;
            _faultCode = 0;

            // Address 204
            _canopy = false;
            _outSp = false;
            _mc1 = false;
            _mc2 = false;
            _mc3 = false;
            _relayLeftP = false;
            _relayLeftN = false;
            _relayRightP = false;
            _relayRightN = false;
            _relayMergeP = false;
            _relayMergeN = false;
            _relayDischargeLeft = false;
            _relayDischargeRight = false;
            _fan1 = false;
            _fan2 = false;
            _coolerOperation = false;

            // Address 205
            _chademoD1 = false;
            _chademoD2 = false;
            _chademoSol = false;
            _cpRelay1 = false;
            _cpRelay2 = false;
            _door1 = false;
            _door2 = false;
            _door3 = false;
            _plcReset1 = false;
            _plcReset2 = false;
            _motorMove1 = false;
            _motorMove2 = false;
            _motorMove3 = false;
            _motorMove4 = false;
            _sylinderForward = false;
            _sylinderReverse = false;

            // Address 206~207
            _testLoadVoltage = 0;
            _testLoadCurrent = 0;

            // Address 208
            _chargerType = 0;      // 0-3 bit
            _chargerWatt = 0;      // 4-5 bit
            _avd = false;          // 6 bit
            _eut = false;          // 7 bit
            _semi = false;         // 8 bit
            _doorFault = false;    // 9 bit
            _mcCount = 0;          // 10-11 bit
            _acd = false;          // 12 bit
            _signageStatus = 0;    // 13-14 bit
        }

        public ushort[] ToRawData()
        {
            // 200 ~ 208 포함 
            var raw = new ushort[_size];


            // Address 200
            ushort r200 = 0;
            SetBit(ref r200, 0, _standBy);                  // 0 bit
            SetBit(ref r200, 1, _ready);                    // 1 bit
            SetBit(ref r200, 2, _startPnc);                 // 2 bit
            SetBit(ref r200, 3, _startEim);                 // 3 bit
            SetBit(ref r200, 4, _stop);                     // 4 bit
            SetBit(ref r200, 5, _run);                      // 5 bit
            SetBit(ref r200, 6, _finish);                   // 6 bit
            SetBit(ref r200, 7, _cylinderFrontForStart);    // 7 bit
            SetBit(ref r200, 8, _motorMoveLeft);            // 8 bit
            SetBit(ref r200, 9, _motorMoveRight);           // 9 bit
            SetBit(ref r200, 10, _motorMoveDown);           // 10 bit
            SetBit(ref r200, 11, _motorMoveUp);             // 11 bit
            SetBit(ref r200, 12, _cylinderFrontForStop);    // 12 bit
            SetBit(ref r200, 13, _reset);                   // 13 bit
            SetBit(ref r200, 15, _update);                  // 15 bit
            raw[0] = r200;

            // Address 201 ~ 203
            raw[1] = _type;                  // Address 201
            raw[2] = _remainConnectTime;    // Address 202
            raw[3] = _faultCode;            // Address 203

            // Address 204
            ushort r204 = 0;
            SetBit(ref r204, 0, _canopy);
            SetBit(ref r204, 1, _outSp);
            SetBit(ref r204, 2, _mc1);
            SetBit(ref r204, 3, _mc2);
            SetBit(ref r204, 4, _mc3);
            SetBit(ref r204, 5, _relayLeftP);
            SetBit(ref r204, 6, _relayLeftN);
            SetBit(ref r204, 7, _relayRightP);
            SetBit(ref r204, 8, _relayRightN);
            SetBit(ref r204, 9, _relayMergeP);
            SetBit(ref r204, 10, _relayMergeN);
            SetBit(ref r204, 11, _relayDischargeLeft);
            SetBit(ref r204, 12, _relayDischargeRight);
            SetBit(ref r204, 13, _fan1);
            SetBit(ref r204, 14, _fan2);
            SetBit(ref r204, 15, _coolerOperation);
            raw[4] = r204;

            // Address 205
            ushort r205 = 0;
            SetBit(ref r205, 0, _chademoD1);
            SetBit(ref r205, 1, _chademoD2);
            SetBit(ref r205, 2, _chademoSol);
            SetBit(ref r205, 3, _cpRelay1);
            SetBit(ref r205, 4, _cpRelay2);
            SetBit(ref r205, 5, _door1);
            SetBit(ref r205, 6, _door2);
            SetBit(ref r205, 7, _door3);
            SetBit(ref r205, 8, _plcReset1);
            SetBit(ref r205, 9, _plcReset2);
            SetBit(ref r205, 10, _motorMove1);
            SetBit(ref r205, 11, _motorMove2);
            SetBit(ref r205, 12, _motorMove3);
            SetBit(ref r205, 13, _motorMove4);
            SetBit(ref r205, 14, _sylinderForward);
            SetBit(ref r205, 15, _sylinderReverse);
            raw[5] = r205;

            // Address 206 ~ 207
            raw[6] = _testLoadVoltage;   // 206
            raw[7] = _testLoadCurrent;   // 207

            // Address 208 (비트필드)
            ushort r208 = 0;
            SetBits(ref r208, 0, 4, _chargerType & 0xF); // 0-3 bit (4bits)
            SetBits(ref r208, 4, 2, _chargerWatt & 0x3); // 4-5 bit (2bits)
            SetBit(ref r208, 6, _avd);               // 6 bit
            SetBit(ref r208, 7, _eut);               // 7 bit
            SetBit(ref r208, 8, _semi);              // 8 bit
            SetBit(ref r208, 9, _doorFault);         // 9 bit
            SetBits(ref r208, 10, 2, _mcCount & 0x3); // 10-11 bit (2bits)
            SetBit(ref r208, 12, _acd);               // 12 bit
            SetBits(ref r208, 13, 2, _signageStatus & 0x3); // 13-14 bit (2bits)
                                                            // bit15 미사용
            raw[8] = r208;

            var hexBuilder = new StringBuilder(_size * 12);
            for (int i = 0; i < _size; i++)
            {
                if (hexBuilder.Length > 0)
                {
                    hexBuilder.Append(' ');
                }

                int registerAddr = 200 + i;
                hexBuilder.Append('[')
                          .Append(registerAddr)
                          .Append("]=0x")
                          .Append(raw[i].ToString("X4"));
            }

            _logger.Debug($"TxData: {hexBuilder}");
            Debug.WriteLine($"TxData: {hexBuilder}");

            return raw;
        }


        public TxData Clone()
        {
            return (TxData)this.MemberwiseClone();
        }
    }
}