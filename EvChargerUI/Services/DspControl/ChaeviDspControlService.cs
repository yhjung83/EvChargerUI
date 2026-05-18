using System.Diagnostics;
using System.Management.Instrumentation;
using EvChargerUI.Commons.Settings;
using EvChargerUI.Commons.Util;
using EvChargerUI.Services.DspControl.Chaevi;

namespace EvChargerUI.Services.DspControl
{
    public class ChaeviDspControlService : IDspControlService
    {
        private Chaevi.DspControl _dspControl;

        public ChaeviDspControlService()
        {
            _dspControl = new Chaevi.DspControl(AppSettingsManager.ChargerSettings.MaxChannelCount);
        }
        public bool Open()
        {
            _dspControl.Open(AppSettingsManager.ChargerSettings.DspComPortNo, AppSettingsManager.ChargerSettings.DspBaudRate);

            return _dspControl.IsOpen;
        }

        public void Close()
        {
            if (_dspControl != null && _dspControl.IsOpen)
            {
                _dspControl.Close();
            }
        }

        public bool IsOpen()
        {
            if (_dspControl == null || !_dspControl.IsOpen) // Checks if the serial port is open
                return false;

            return _dspControl.IsConnected; // Checks our custom connection status
        }

        public bool IsEnableStartChargingBeforePlugCheck()
        {
            return false;
        }

        public bool GetStandByStatus(int channel)
        {
            return true;
        }

        public bool GetChargerReadyStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.IsReady;
        }

        public bool GetChargerDoorStatus(int channel)
        {
            return false;
        }

        public bool GetPlugCheckStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);

            return data.ConnectorStatus;
        }

        public bool GetChargingRunStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);

            return data.IsChargeRunning;
        }

        public bool GetChargingFinishStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.IsChargeFinished;
        }

        public bool GetFaultStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.IsFaultOccured;
        }

        public int GetRunCount(int channel)
        {
            return 0;
        }

        public int GetSoc(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.Soc;
        }

        public double GetPowerMeter(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.PowerMeterInKw;
        }

        public double GetCurrent(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.Current;
        }

        public double GetVoltage(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.Voltage;
        }

        public int GetRemainedMinute(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return (int) data.RemainSeconds / 60;
        }

        public string GetChargerFirmwareVersion()
        {
            RxData data = _dspControl.GetRxData(0);
            return data.FaVersion;
        }

        public bool GetCharginPrepareCheck(int channel)
        {
            return true;
        }
        public void SetChargerInit(int channel)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargeStandBy(int channel)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.StandBy = true;
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargeReady(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.Ready = true;
            _dspControl.RequestWriteRegister(channel);
        }

        /// <summary>
        /// 표준 커넥터 타입을 Chaevi 제조사별 타입으로 매핑
        /// </summary>
        private ushort MapConnectorType(int standardType)
        {
            switch (standardType)
            {
                case 0:
                    return 1;
                case 1:
                    return 2;
                case 2:
                    return 3;
                default:
                    return (ushort)standardType;
            }
        }

        public void SetCableType(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            // if (AppSettingsManager.ChargerSettings.ChaeviModelName == "DVC-3FS100N-U" || AppSettingsManager.ChargerSettings.ChaeviModelName == "DVC-3FS100W-U")
            //     data.StartPnc = true;
            data.Type = MapConnectorType(connectorType);
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargeStart(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.StartPnc = true;
            data.Type = MapConnectorType(connectorType); 
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargePrepare(int channel, int connectorType)
        {
            Debug.WriteLine("SetChargePrepare");
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            if (AppSettingsManager.ChargerSettings.ChaeviModelName == "DVC-3FS100N-U")
                data.CylinderFrontForStop = true;
            if (AppSettingsManager.ChargerSettings.ChaeviModelName == "DVC-3FNHOC-U")
                data.CylinderFrontForStart = true;
            if (AppSettingsManager.ChargerSettings.ChaeviModelName == "DVC-3FS100W-U")
                data.CylinderFrontForStop = true;
                // data.CylinderFrontForStor = false;
            data.Type = MapConnectorType(connectorType);
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetMotorMoveLeft(int channel)
        {
            Debug.WriteLine("SetMotorMoveLeft");
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.MotorMoveLeft = true;
            data.CylinderFrontForStop = false;
            if (AppSettingsManager.ChargerSettings.ChaeviModelName == "DVC-3FS100W-U")
            {
                data.StartPnc = true;
                data.Canopy200 = true;
                data.Type = 2;
            }
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetMotorMoveRight(int channel)
        {
            Debug.WriteLine("SetMotorMoveRight");
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.MotorMoveRight = true;
            data.CylinderFrontForStop = false;
            if (AppSettingsManager.ChargerSettings.ChaeviModelName == "DVC-3FS100W-U")
            {
                data.StartPnc = true;
                data.Canopy200 = true;
                data.Type = 2;
            }
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetMotorMoveUp(int channel)
        {
            Debug.WriteLine("SetMotorMoveUp");
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.MotorMoveUp = true;
            data.CylinderFrontForStop = false;
            if (AppSettingsManager.ChargerSettings.ChaeviModelName == "DVC-3FS100W-U")
            {
                data.StartPnc = true;
                data.Canopy200 = true;
                data.Type = 2;
            }
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetMotorMoveDown(int channel)
        {
            Debug.WriteLine("SetMotorMoveDown");
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.MotorMoveDown = true;
            data.CylinderFrontForStop = false;
            if (AppSettingsManager.ChargerSettings.ChaeviModelName == "DVC-3FS100W-U")
            {
                data.StartPnc = true;
                data.Canopy200 = true;
                data.Type = 2;
            }
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetMotorMoveEnd(int channel)
        {
            Debug.WriteLine("SetMotorMoveEnd");
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.MotorMoveLeft = false;
            data.MotorMoveRight = false;
            data.MotorMoveUp = false;
            data.MotorMoveDown = false;
            if (AppSettingsManager.ChargerSettings.ChaeviModelName == "DVC-3FS100W-U")
            {
                data.StartPnc = true;
                data.Canopy200 = true;
                data.Type = 2;
            }
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargeRun(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.Type = MapConnectorType(connectorType);
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargeStop(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.Type = MapConnectorType(connectorType);
            if (AppSettingsManager.ChargerSettings.ChaeviModelName == "DVC-3FS100N-U" || AppSettingsManager.ChargerSettings.ChaeviModelName == "DVC-3FS100W-U")
                data.StartEim = true;
            else if (AppSettingsManager.ChargerSettings.ChaeviModelName == "DVC-3FNHOC-U")
            {
                data.StartEim = true;
                data.Finish = true;
            }
            else
                data.Stop = true;
            _dspControl.ClearWriteBuffer();
            for (int i = 0; i < 5; i++)
                _dspControl.RequestWriteRegister(channel);
        }

        public void SetDoorStatus(int channel, bool doorOpen)
        {
        }

        public void SetUiFault(int channel)
        {
        }

        public void SetChargeComplete(int channel)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.Finish = true;
            _dspControl.RequestWriteRegister(channel);
        }

        public void ResetCharger()
        {
            TxData data = _dspControl.GetTxData(0);
            data.Initialize();
            data.Reset = true;
            _dspControl.RequestWriteRegister(0);
        }

        public bool GetEmergencyStatus()
        {
            RxData data = _dspControl.GetRxData(0);
            return data.IsEmergencyStopActivated;
        }

        public string GetFaultCode(int channel)
        {
            try
            {
                RxData data = _dspControl.GetRxData(channel);
                return ParseChaeviFaultCode(data.FaultCode);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Chaevi FaultCode(16bit): [4bit 백의자리][4bit 두자리][8bit] — 표시·전송에는 앞 8bit만 사용.
        /// 예) 0x1203 → 백의자리 1, 두자리 02 → "102"
        /// </summary>
        internal static string ParseChaeviFaultCode(ushort rawFaultCode)
        {
            if (rawFaultCode == 0)
                return "";

            ChaeviFaultCodeCatalog.ParsedFault parsed = ChaeviFaultCodeCatalog.Parse(rawFaultCode);
            return parsed.DisplayCode == 0 ? "" : parsed.DisplayCode.ToString();
        }

        /// <summary>
        /// 프로토콜 문서 기준 대분류/Code/세부항목(8bit) 설명. A/S 로그용.
        /// </summary>
        public string GetFaultDetails(int channel)
        {
            try
            {
                RxData data = _dspControl.GetRxData(channel);
                if (!data.IsFaultOccured && data.FaultCode == 0)
                    return "";
                return ChaeviFaultCodeCatalog.FormatLogDescription(data.FaultCode);
            }
            catch
            {
                return "";
            }
        }

        public ushort GetRawFaultCode(int channel)
        {
            try
            {
                return _dspControl.GetRxData(channel).FaultCode;
            }
            catch
            {
                return 0;
            }
        }

        public string GetPowerModuleStatusBits(int channel) => "0000000000000000";

        public bool IsPmsConnected() => true;
    }
}