using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using EvChargerUI.Commons.Util;

namespace EvChargerUI.Services.DspControl.Evsis
{
    public class TxData
    {
        public const int DSP_TX_START_ADDR = 200;
        public const int DSP_TX_DATA_CNT = 31;
        public ushort[] rawData = new ushort[31];
        public bool uiReady = true;
        public bool isReady = false;
        public bool isCharging = false;
        public bool btnStart = false;
        public bool btnEnd = false;
        public bool testFlag = false;
        public bool doorOpenFlag = false;
        public bool uiFault = false;
        public bool isFinish = false;
        public bool isbtnPush = false;
        public bool istestMode = false;
        public bool boardReset = false;
        public bool canopyLight = false;
        public bool modemcommStatus = false;
        public bool fanControl = false;
        public bool fwUpdate = false;
        public int chargingSelect = 0;
        public int testmodecapacitySelect = 0;
        public int runCnt = 0;
        public bool iscarpaymentAuth = false;
        public bool dspTestOn = false;
        public float inputVol = 0.0f;
        public float inputCur = 0.0f;
        public int setPower = 0;
        public int setVol = 0;
        public int uiVersion = 0;
        public int cost = 0;
        public int authCard = 0;
        public int chargingTime = 0;
        public int memberAuthcnt = 0;
        public bool serverCommError = false;
        public bool suspension = false;
        public bool rfCardCommError = false;
        public bool fwdownloadingStatus = false;
        public bool resetflagStatus = false;
        public bool freeMode = false;
        public bool rfCardTagingevt = false;
        public bool timeOut = false;
        public bool commError = false;
        public bool uvLedControl = false;
        public bool acwinchDown = false;
        public bool acwinchUp = false;
        public bool chademowinchDown = false;
        public bool chademowinchUp = false;
        public bool combowinchDown = false;
        public bool combowinchUp = false;
        public float outputAcVol = 0.0f;
        public float outputAcCur = 0.0f;
        public float acpowerMeter = 0.0f;
        public float outputDcVol = 0.0f;
        public float outputDcCur = 0.0f;
        public float dcpowerMeter = 0.0f;
        public int carpaymentId1 = 0;
        public int carpaymentId2 = 0;

        public int ChannelNo { get; set; }

        private FileLogger _logger = ((App)Application.Current).DspLogger;

        public static ushort SetBit(ushort src, int bitNum, bool tf)
        {
            return tf ? (ushort)((uint)src | (uint)(1 << bitNum)) : (ushort)((uint)src & (uint)~(1 << bitNum));
        }
        public void Init()
        {
            this.uiReady = true;
            this.isReady = false;
            this.isCharging = false;
            this.btnStart = false;
            this.btnEnd = false;
            this.testFlag = false;
            this.doorOpenFlag = false;
            this.uiFault = false;
            this.isFinish = false;
            this.isbtnPush = false;
            this.istestMode = false;
            this.boardReset = false;
            this.canopyLight = false;
            this.modemcommStatus = false;
            this.fanControl = false;
            this.fwUpdate = false;
            //this.chargingSelect = 0;
            this.testmodecapacitySelect = 0;
            //this.runCnt = 0;
            this.iscarpaymentAuth = false;
            this.inputVol = 0.0f;
            this.inputCur = 0.0f;
            this.setPower = 0;
            this.setVol = 0;
            this.uiVersion = 0;
            this.cost = 0;
            this.authCard = 0;
            this.chargingTime = 0;
            this.memberAuthcnt = 0;
            this.serverCommError = false;
            this.suspension = false;
            this.rfCardCommError = false;
            this.fwdownloadingStatus = false;
            this.resetflagStatus = false;
            this.freeMode = false;
            this.rfCardTagingevt = false;
            this.timeOut = false;
            this.commError = false;
            this.uvLedControl = false;
            this.acwinchDown = false;
            this.acwinchUp = false;
            this.chademowinchDown = false;
            this.chademowinchUp = false;
            this.combowinchDown = false;
            this.combowinchUp = false;
            this.outputAcVol = 0.0f;
            this.outputAcCur = 0.0f;
            this.acpowerMeter = 0.0f;
            this.outputDcVol = 0.0f;
            this.outputDcCur = 0.0f;
            this.dcpowerMeter = 0.0f;
            this.carpaymentId1 = 0;
            this.carpaymentId2 = 0;
        }

        public ushort[] Encode()
        {
            this.rawData[0] = (ushort)0;
            this.rawData[0] = SetBit(this.rawData[0], 0, this.uiReady);
            this.rawData[0] = SetBit(this.rawData[0], 1, this.isReady);
            this.rawData[0] = SetBit(this.rawData[0], 2, this.isCharging);
            this.rawData[0] = SetBit(this.rawData[0], 3, this.btnStart);
            this.rawData[0] = SetBit(this.rawData[0], 4, this.btnEnd);
            this.rawData[0] = SetBit(this.rawData[0], 5, this.testFlag);
            this.rawData[0] = SetBit(this.rawData[0], 6, this.doorOpenFlag);
            this.rawData[0] = SetBit(this.rawData[0], 7, this.uiFault);
            this.rawData[0] = SetBit(this.rawData[0], 8, this.isFinish);
            this.rawData[0] = SetBit(this.rawData[0], 9, this.isbtnPush);
            this.rawData[0] = SetBit(this.rawData[0], 10, this.istestMode);
            this.rawData[0] = SetBit(this.rawData[0], 11, this.boardReset);
            this.rawData[0] = SetBit(this.rawData[0], 12, this.canopyLight);
            this.rawData[0] = SetBit(this.rawData[0], 13, this.modemcommStatus);
            this.rawData[0] = SetBit(this.rawData[0], 14, this.fanControl);
            this.rawData[0] = SetBit(this.rawData[0], 15, this.fwUpdate);
            this.rawData[1] = (ushort)this.chargingSelect;
            this.rawData[2] = (ushort)this.testmodecapacitySelect;
            this.rawData[3] = (ushort)this.runCnt;
            this.rawData[4] = (ushort)0;  // 초기화 후 설정
            this.rawData[4] = SetBit(this.rawData[4], 12, this.iscarpaymentAuth);
            this.rawData[4] = SetBit(this.rawData[4], 6, this.dspTestOn);
            this.rawData[5] = this.FloattouShort(this.inputVol)[0];
            this.rawData[6] = this.FloattouShort(this.inputVol)[1];
            this.rawData[7] = this.FloattouShort(this.inputCur)[0];
            this.rawData[8] = this.FloattouShort(this.inputCur)[1];
            this.rawData[9] = (ushort)this.setPower;
            this.rawData[10] = (ushort)this.setVol;
            this.rawData[11] = (ushort)this.uiVersion;
            this.rawData[12] = (ushort)this.cost;
            this.rawData[13] = (ushort)this.authCard;
            this.rawData[14] = (ushort)this.chargingTime;
            this.rawData[15] = (ushort)this.memberAuthcnt;
            this.rawData[16] = (ushort)0;  // 초기화 후 설정
            this.rawData[16] = SetBit(this.rawData[16], 0, this.serverCommError);
            this.rawData[16] = SetBit(this.rawData[16], 1, this.suspension);
            this.rawData[16] = SetBit(this.rawData[16], 2, this.rfCardCommError);
            this.rawData[16] = SetBit(this.rawData[16], 3, this.fwdownloadingStatus);
            this.rawData[16] = SetBit(this.rawData[16], 4, this.resetflagStatus);
            this.rawData[16] = SetBit(this.rawData[16], 5, this.freeMode);
            this.rawData[16] = SetBit(this.rawData[16], 6, this.rfCardTagingevt);
            this.rawData[16] = SetBit(this.rawData[16], 7, this.timeOut);
            this.rawData[16] = SetBit(this.rawData[16], 8, this.commError);
            this.rawData[16] = SetBit(this.rawData[16], 9, this.uvLedControl);
            this.rawData[16] = SetBit(this.rawData[16], 10, this.acwinchDown);
            this.rawData[16] = SetBit(this.rawData[16], 11, this.acwinchUp);
            this.rawData[16] = SetBit(this.rawData[16], 12, this.chademowinchDown);
            this.rawData[16] = SetBit(this.rawData[16], 13, this.chademowinchUp);
            this.rawData[16] = SetBit(this.rawData[16], 14, this.combowinchDown);
            this.rawData[16] = SetBit(this.rawData[16], 15, this.combowinchUp);
            this.rawData[17] = this.FloattouShort(this.outputAcVol)[0];
            this.rawData[18] = this.FloattouShort(this.outputAcVol)[1];
            this.rawData[19] = this.FloattouShort(this.outputAcCur)[0];
            this.rawData[20] = this.FloattouShort(this.outputAcCur)[1];
            this.rawData[21] = this.FloattouShort(this.acpowerMeter)[0];
            this.rawData[22] = this.FloattouShort(this.acpowerMeter)[1];
            this.rawData[23] = this.FloattouShort(this.outputDcVol)[0];
            this.rawData[24] = this.FloattouShort(this.outputDcVol)[1];
            this.rawData[25] = this.FloattouShort(this.outputDcCur)[0];
            this.rawData[26] = this.FloattouShort(this.outputDcCur)[1];
            this.rawData[27] = this.FloattouShort(this.dcpowerMeter)[0];
            this.rawData[28] = this.FloattouShort(this.dcpowerMeter)[1];
            this.rawData[29] = (ushort)this.carpaymentId1;
            this.rawData[30] = (ushort)this.carpaymentId2;

            var hexBuilder = new StringBuilder(DSP_TX_DATA_CNT * 12);
            for (int i = 0; i < DSP_TX_DATA_CNT; i++)
            {
                if (hexBuilder.Length > 0)
                {
                    hexBuilder.Append(' ');
                }

                int registerAddr = DSP_TX_START_ADDR + i;
                hexBuilder.Append('[')
                          .Append(registerAddr)
                          .Append("]=0x")
                          .Append(this.rawData[i].ToString("X4"));
            }

            _logger.Debug($"RunCount: {this.runCnt}, TxData: {hexBuilder}");
            Debug.WriteLine($"RunCount: {this.runCnt}, TxData: {hexBuilder}");
            return this.rawData;
        }

        public ushort[] FloattouShort(float a)
        {
            byte[] bytes = BitConverter.GetBytes(a);
            return new ushort[2]
            {
                BitConverter.ToUInt16(bytes, 0),
                 BitConverter.ToUInt16(bytes, 2)
            };
        }

        public TxData Clone()
        {
            TxData dspTxData = (TxData)this.MemberwiseClone();
            if (this.rawData != null)
                dspTxData.rawData = (ushort[])this.rawData.Clone();
            return dspTxData;
        }
    }
}