using EvChargerUI.Commons.Settings;
using EvChargerUI.Services.DspControl.Signet;

namespace EvChargerUI.Services.DspControl
{
    public class SignetDspControlService : IDspControlService
    {
        private Signet.DspControl _dspControl;

        public SignetDspControlService()
        {
            _dspControl = new Signet.DspControl(AppSettingsManager.ChargerSettings.MaxChannelCount);
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
            return true;
        }


        public bool GetStandByStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);

            return data.ChargerStatus == 0;
        }

        public bool GetChargerReadyStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);

            return data.ChargerStatus == 1;
        }

        public bool GetChargerDoorStatus(int channel)
        {
            return false;
        }

        public bool GetPlugCheckStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.ChargerStatus == 5;
        }

        public bool GetChargingRunStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.ChargerStatus == 2;
        }

        public bool GetChargingFinishStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data .ChargerStatus == 3;
        }

        public bool GetFaultStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.ChargerStatus == 4;
        }

        public int GetRunCount(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.RunCount;
        }

        public int GetSoc(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.Soc;
        }

        public double GetPowerMeter(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.PowerMeter;
        }

        public double GetCurrent(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.EvTargerCurrent;
        }

        public double GetVoltage(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.EvTargerVoltage;
        }

        public int GetRemainedMinute(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.RemainingTimeFull;
        }

        public string GetChargerFirmwareVersion()
        {
            //TODO: 
            return "";
        }

        public bool GetCharginPrepareCheck(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.ChargerStatus == 8;
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
            data.HmiCommand = 3;
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargeReady(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();

            _dspControl.RequestWriteRegister(channel);
        }

        /// <summary>
        /// 표준 커넥터 타입을 Signet 제조사별 타입으로 매핑
        /// </summary>
        private ushort MapConnectorType(int standardType)
        {
            switch (standardType)
            {
                case 0:
                    return 1;
                case 1:
                    return 3;
                case 2:
                    return 2;
                default:
                    return (ushort)standardType;
            }
        }

        public void SetCableType(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.CableType = MapConnectorType(connectorType);
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargeStart(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.CableType = MapConnectorType(connectorType);
            data.HmiCommand = 2;
            for(int i = 0; i < 5; i++)
                _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargePrepare(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();

            data.HmiCommand = 4;
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargeRun(int channel, int connectorType)
        {
        }

        public void SetChargeStop(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.HmiCommand = 1;
            _dspControl.ClearWriteBuffer();
            for (int i = 0; i < 10; i++)
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
            data.HmiCommand = 3;
            _dspControl.RequestWriteRegister(channel);
        }

        public void ResetCharger()
        {
            TxData data = _dspControl.GetTxData(0);
            data.Initialize();
            data.ResetMainboard = true;
            _dspControl.RequestWriteRegister(0);
        }

        public bool GetEmergencyStatus()
        {
            RxData data = _dspControl.GetRxData(0);
            return data.IsEmergencyButtonOn;
        }

        public string GetFaultCode(int channel)
        {
            try
            {
                RxData data = _dspControl.GetRxData(channel);
                return data.ErrorCode.ToString();
            }
            catch
            {
                return "";
            }
        }

        public string GetPowerModuleStatusBits(int channel) => "0000000000000000";

        public bool IsPmsConnected() => true;
    }
}