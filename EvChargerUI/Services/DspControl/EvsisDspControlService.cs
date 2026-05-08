using EvChargerUI.Commons.Settings;
using EvChargerUI.Services.DspControl.Evsis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using EvChargerUI.Commons.Util;

namespace EvChargerUI.Services.DspControl
{
    public class EvsisDspControlService : IDspControlService
    {
        private Evsis.DspControl _dspControl;
        private FileLogger _logger = ((App)Application.Current).AppLogger;

        public EvsisDspControlService()
        {
            _dspControl = new Evsis.DspControl(AppSettingsManager.ChargerSettings.MaxChannelCount);
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

            return data.dspReady;
        }

        public bool GetChargerReadyStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.dspReady;
        }

        public bool GetChargerDoorStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.dspDoorState;
        }

        public bool GetPlugCheckStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.dspPlugState;
        }

        public bool GetChargingRunStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.dspChargingState;
        }

        public bool GetChargingFinishStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.dspChargingFinish;
        }

        public bool GetFaultStatus(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.dspFaultState;
        }

        public int GetRunCount(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.runCnt;
        }

        public int GetSoc(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.soc;
        }

        public double GetPowerMeter(int channel)
        {
            // AC3/콤보 전력량 분리(가정): PMS 패킷의 5/7/8은 AC3
            TxData tx = _dspControl.GetTxData(channel);
            int evsisConnector = tx?.chargingSelect ?? 0; // 1=AC3, 2=차데모, 3=DC콤보
            
            _logger.Info($"[GetPowerMeter] Channel {channel}: evsisConnector={evsisConnector}");

            if (evsisConnector == 1) // AC3
                return _dspControl.PowerMeterInKwAc3;

            return _dspControl.PowerMeterInKw;

        }

        public double GetCurrent(int channel)
        {
            TxData tx = _dspControl.GetTxData(channel);
            int evsisConnector = tx?.chargingSelect ?? 0; // 1=AC3, 2=차데모, 3=DC콤보

            if (evsisConnector == 1) // AC3
                return _dspControl.CurrentAc3;

            return _dspControl.Current;
        }

        public double GetVoltage(int channel)
        {
            TxData tx = _dspControl.GetTxData(channel);
            int evsisConnector = tx?.chargingSelect ?? 0; // 1=AC3, 2=차데모, 3=DC콤보

            if (evsisConnector == 1) // AC3
                return _dspControl.VoltageAc3;

            return _dspControl.Voltage;
        }

        public int GetRemainedMinute(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return data.remainTime / 60;
        }

        public string GetChargerFirmwareVersion()
        {
            RxData data = _dspControl.GetRxData(0);
            return data.fwVersion.ToString();

        }

        public bool GetCharginPrepareCheck(int channel)
        {
            return true;
        }
        public void SetChargerInit(int channel)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Init();
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargeStandBy(int channel)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Init();
            data.uiReady = true;
            _dspControl.RequestWriteRegister(channel);
        }

        /// <summary>
        /// 표준 커넥터 타입을 이브이시스 제조사별 타입으로 매핑
        /// 표준: 0=AC3, 1=DC콤보, 2=차데모
        /// 이브이시스: 1=AC3, 2=차데모, 3=DC콤보
        /// </summary>
        private int MapConnectorType(int standardType)
        {
            switch (standardType)
            {
                case 0: // AC3
                    return 1;
                case 1: // DC콤보 (Combo)
                    return 3;
                case 2: // 차데모 (Chademo)
                    return 2;
                default:
                    return standardType;
            }
        }

        public void SetChargeReady(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Init();
            data.uiReady = true;
            data.isReady = true;
            data.chargingSelect = MapConnectorType(connectorType);
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetCableType(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.chargingSelect = MapConnectorType(connectorType);
            // _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargeStart(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Init();
            data.uiReady = true;
            data.isReady = true;
            data.btnStart = true;
            data.iscarpaymentAuth = true;
            data.chargingSelect = MapConnectorType(connectorType);
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargePrepare(int channel, int connectorType)
        {
            // no-op: evsis 제조사는 charge prepare 단계가 필요 없음
        }

        public void SetChargeRun(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Init();
            data.uiReady = true;
            data.iscarpaymentAuth = true;
            data.chargingSelect = MapConnectorType(connectorType);
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargeStop(int channel, int connectorType)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Init();
            data.chargingSelect = MapConnectorType(connectorType);
            data.uiReady = true;
            data.btnEnd = true;
            data.isFinish = true;
            data.iscarpaymentAuth = true;
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetDoorStatus(int channel, bool doorOpen)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Init();
            data.doorOpenFlag = doorOpen;
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetUiFault(int channel)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Init();
            data.uiFault = true;
            _dspControl.RequestWriteRegister(channel);
        }

        public void SetChargeComplete(int channel)
        {
            TxData data = _dspControl.GetTxData(channel);
            data.Init();
            data.uiReady = true;
            data.btnEnd = true;
            data.isFinish = true;
            _dspControl.RequestWriteRegister(channel);
        }

        public void ResetCharger()
        {
            TxData data = _dspControl.GetTxData(0);
            data.Init();
            data.boardReset = true;
            _dspControl.RequestWriteRegister(0);

        }

        public string GetPowerModuleStatusBits(int channel)
        {
            RxData data = _dspControl.GetRxData(channel);
            return Convert.ToString(data.rawData[34], 2).PadLeft(16, '0');
        }

        public bool GetEmergencyStatus()
        {
            RxData data = _dspControl.GetRxData(0);
            return data.emergencyState;
        }

        private int GetFaultCodeFromBits(RxData d)
        {
            if (d == null) return 0;

            // 우선순위: 안전/현장 조치가 필요한 항목 → 통신/절차 → 기타
            if (d.emergencyState) return 901;              // 비상정지 버튼 동작
            if (d.dspDoorState) return 906;                // CP Door1 Open

            if (d.inputOvervol) return 102;                // 입력 과전압
            if (d.inputLowvol) return 103;                 // 입력 저전압

            if (d.relay1Error) return 201;                 // Relay 1 접점에러/융착
            if (d.relay2Error) return 202;                 // Relay 2 접점에러/융착

            // (콤보) 과전압/과전류로 정의된 비트가 별도 제공됨 (문서 기준)
            if (d.comboError1) return 203;                 // (콤보)에러_과전압
            if (d.comboError2) return 204;                 // (콤보)에러_과전류

            if (d.cablechkError2 || d.cablechkcpError) return 205; // 케이블체크2 에러

            if (d.prechargeError) return 301;              // 프리차지 용량불충 에러

            // 모듈 Warning은 다수 존재하나, 현재 Fault code 정의는 "모듈#1 오류(Warning)"만 있음
            if (d.modulewarning33) return 302;

            if (d.tempError || d.coolerOverTemp || d.cableOverTemp) return 303; // 내부 과온도

            // 디스커버리 단계 응답/정보 오류
            if (d.servicediscoverycommError || d.discoveryInfoError || d.discoverycommError) return 401;

            if (d.plcworngpara) return 402;                // PLC WrongChargeParameter

            // PLC/모뎀/보드 통신 계열
            if (d.plctimeOut1 || d.plctimeOut2 || d.plctimeOut3) return 502; // PLC TIMEOUT NotificationMaxDelay
            if (d.protocolcommError || d.communicateError1 || d.communicateError2 || d.communicateError3 || d.communicateError4) return 404; // 컨트롤보드 통신 오류
            if (d.plcinitError || d.slacommError) return 403; // PLC 모뎀 상태 에러(재부팅/교체)

            if (d.carBatteryError) return 902;             // 차량 배터리 온도 문제 발생(문서상 콤보)

            if (d.spdError) return 905;                    // 단말기 이상

            // Fault 상태인데 위 매핑에 걸리지 않으면 "기타"
            if (d.dspFaultState) return 910;               // 기타(알수없음)

            return 0;
        }

        public string GetFaultCode(int channel)
        {
            try
            {
                RxData data = _dspControl.GetRxData(channel);
                int code = GetFaultCodeFromBits(data);
                return code.ToString();
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// EVSIS 전용: faultCode가 910(기타)로 떨어질 때, 켜진 비트 중 "대표 1개"의 세부 사유를 한글로 반환.
        /// A/S 로그에서 "기타"를 구체화하기 위한 용도. (여러 개 나열하지 않음)
        /// </summary>
        public string GetFaultDetails(int channel)
        {
            try
            {
                RxData d = _dspControl.GetRxData(channel);
                if (d == null) return "";
                if (!d.dspFaultState) return "";

                // 우선순위 1개만 선택 (A/S 관점에서 가장 직접적인 원인부터)
                if (d.fusePOpenState) return "퓨즈(P) 오픈";
                if (d.rcdOffState) return "누전차단기(RCD) 오프";
                if (d.acLeakState) return "AC 누설(Leak) 감지";
                if (d.acRcmError) return "AC RCM 오류";

                if (d.mc1Error || d.mc2Error || d.mc3Error) return "MC 오류";
                if (d.relay3Error || d.relay4Error) return "릴레이 오류";
                if (d.acRelayError) return "AC 릴레이 오류";

                if (d.coolerLevelError) return "냉각수(레벨) 오류";
                if (d.coolerTempError) return "냉각 온도 오류";
                if (d.cableTempError || d.cableOverTemp) return "케이블 과열/온도 오류";

                if (d.couplerLockError) return "커플러 잠금 오류";
                if (d.chargingDeviationError1 || d.chargingDeviationError2) return "충전 편차 오류";
                if (d.compatibilityError) return "차량 호환성 오류";

                // PLC/프로토콜/통신 계열
                if (d.plcsequenceError) return "PLC 시퀀스 오류";
                if (d.protocolcommError) return "프로토콜 통신 오류";
                if (d.communicateError1 || d.communicateError2 || d.communicateError3 || d.communicateError4) return "통신 오류";
                if (d.slacommError) return "SLA 통신 오류";
                if (d.plcinitError) return "PLC 초기화 오류";
                if (d.prechargecommError) return "프리차지 통신 오류";
                if (d.paymentcommError) return "결제 통신 오류";
                if (d.authcommError) return "인증 통신 오류";
                if (d.cablecommError) return "케이블 통신 오류";

                // 차량/협상
                if (d.failed_noneGotiation) return "차량 협상 실패";
                if (d.carGearError) return "차량 기어 상태 오류";
                if (d.carBmsError) return "차량 BMS 오류";
                if (d.carpaymentauthError) return "차량 결제/인증 오류";

                // 기타 링크/타임아웃/모듈
                if (d.faultHPGLinkDown) return "HPG 링크 다운";
                if (d.timeout_weldingDetection) return "용착 검사 타임아웃";
                if (d.modulewarning34 || d.modulewarning35 || d.modulewarning36 || d.modulewarning37 || d.modulewarning38 || d.modulewarning39 || d.modulewarning40) return "모듈 경고";
                if (d.powerunitspdError) return "파워유닛(SPD) 오류";
                if (d.powerunitinputLowvol) return "파워유닛 입력 저전압";
                if (d.powerunitpm1Error || d.powerunitpm2Error) return "파워유닛 모듈 오류";

                return "기타(상세 비트 미확인)";
            }
            catch
            {
                return "";
            }
        }

        public bool IsPmsConnected()
        {
            return _dspControl != null && _dspControl.IsPmsConnected;
        }
    }
}