using EvChargerUI.Commons.Settings;
using EvChargerUI.Services.DspControl.Klinelex;

namespace EvChargerUI.Services.DspControl
{
    public class KlinelexDspControlService : IDspControlService
    {
        private Klinelex.DspControl _dspControl;
        public KlinelexDspControlService()
        {
            _dspControl = new Klinelex.DspControl(AppSettingsManager.ChargerSettings.MaxChannelCount);
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
            RxData data = _dspControl.GetRxData(channel);

            return data.IsControllBoardReady;
        }

        public bool GetChargerReadyStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.IsChargeReady;
        }

        public bool GetChargerDoorStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.IsDoorClosed;
        }

        public bool GetPlugCheckStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.IsPlugConnected;
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
            return data.PowerInKw;
        }

        public double GetCurrent(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.CurrentOut * 100;
        }

        public double GetVoltage(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.VoltageOut;
        }

        public int GetRemainedMinute(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.RemainedMinute;
        }


        public string GetChargerFirmwareVersion()
        {
            RxData data = _dspControl.GetRxData(0);
            return data.ControlboardVersion; 
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
            data.StandBy = false;
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
        /// 표준 커넥터 타입을 Klinelex 제조사별 타입으로 매핑
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
            data.Start = true;
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargePrepare(int channel, int connectorType)
        {
        }
        public void SetChargeRun(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.Run = true;
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargeStop(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();
            data.Stop = true;
            _dspControl.ClearWriteBuffer();
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetDoorStatus(int channel, bool doorOpen)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();

            data.DoorOpen = doorOpen;

            _dspControl.RequestWriteRegister(channel);
        }

        public void SetUiFault(int channel)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();

            data.Fault = true;
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargeComplete(int channel)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Initialize();

            _dspControl.RequestWriteRegister(channel);
        }

        public void ResetCharger()
        {
            TxData data = _dspControl.GetTxData(0);
            data.Initialize();
            data.ResetRequest = true;
            _dspControl.RequestWriteRegister(0);
        }

        public bool GetEmergencyStatus()
        {
            RxData data = _dspControl.GetRxData(0);

            return data.IsEmergencyStopActivated;
        }

        private int GetFaultCodeFromBits(RxData d)
        {
            if (d == null) return 0;

            // EVSIS Fault code 정의에 최대한 맞춰 매핑 (단일 코드, 우선순위 방식)
            if (d.IsEmergencyStopActivated) return 901; // 비상정지 버튼 동작
            if (d.IsCabinetDoorOpened) return 906;      // CP Door1 Open

            if (d.IsPowerMeterFault) return 904;        // 전력계 프로그램 이상

            if (d.IsInputOverVoltage) return 102;       // 입력 과전압
            if (d.IsInputUnderVoltage) return 103;      // 입력 저전압

            // 출력 과전압/과전류는 EVSIS의 (콤보)에러_과전압/과전류 코드로 근접 매핑
            if (d.IsOutputOverVoltage) return 203;
            if (d.IsOutputOverCurrent) return 204;

            if (d.IsPreChargingFault) return 301;       // 프리차지 용량불충 에러
            if (d.IsPowerModuleFault) return 302;       // 모듈#1 오류(Warning) (클린일렉스는 모듈 Fault로 제공)

            if (d.IsOverTemperature || d.IsGunOverTemperature) return 303; // 내부 과온도(근접)

            // PLC 계열: 클린일렉스는 타임아웃/파라미터 오류를 분리하지 않고 PLC 통신 오류만 제공
            if (d.IsPlcCommFault) return 404;           // (콤보)에러_컨트롤보드 통신 오류(근접)

            // 기타 통신/제어 계열은 컨트롤보드 통신 오류로 근접 매핑
            if (d.IsCanCommFault) return 404;
            if (d.IsControlPilotFault) return 404;

            // EV 협상 실패/EV 오류 등은 EVSIS 단일 코드 테이블에 직접 대응이 없어 기타로 처리
            if (d.IsEvFault || d.IsEvNegotiationFailed || d.IsMcuFault) return 910;

            // 누전/접지/차단기/MC류도 EVSIS 테이블에 직접 항목이 없어 기타로 처리
            if (d.IsGroundFault || d.IsMainBreakerTripped || d.IsInputMcFault || d.IsOutputMcFault || d.IsOutputLeakage) return 910;

            if (d.IsGeneralFault || d.IsFaultOccured) return 910;

            return 0;
        }

        public string GetFaultCode(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            int code = GetFaultCodeFromBits(data);
            return code.ToString();
        }

        public string GetPowerModuleStatusBits(int channel) => "0000000000000000";

        public bool IsPmsConnected() => true;
    }
}