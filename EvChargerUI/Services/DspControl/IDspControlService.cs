using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EvChargerUI.Services.DspControl
{
    public interface IDspControlService
    {
        bool Open();
        void Close();
        bool IsOpen();

        bool IsEnableStartChargingBeforePlugCheck();
        bool GetStandByStatus(int channel);
        bool GetChargerReadyStatus(int channel);
        bool GetChargerDoorStatus(int channel);
        bool GetPlugCheckStatus(int channel);
        bool GetChargingRunStatus(int channel);
        bool GetChargingFinishStatus(int channel);
        bool GetFaultStatus(int channel);
        int GetRunCount(int channel);
        int GetSoc(int channel);
        double GetPowerMeter(int channel);
        double GetCurrent(int channel);
        double GetVoltage(int channel);
        int GetRemainedMinute(int channel);
        string GetChargerFirmwareVersion();
        bool GetEmergencyStatus();
        string GetFaultCode(int channel);
        // TODO: 전력량계(PMS) 통신 연결 여부. Evsis만 사용, 그 외는 true 반환. 추후 확인 필요.
        bool IsPmsConnected();

        /// <summary>실시간 상태 전송용 파워모듈 비트 문자열(제조사별). 이브이시스는 Modbus 434번(raw) 16비트.</summary>
        string GetPowerModuleStatusBits(int channel);

        bool GetCharginPrepareCheck(int channel);

        void SetChargerInit(int channel);
        void SetChargeStandBy(int channel);
        void SetChargeReady(int channel, int connectorType);
        void SetCableType(int channel, int connectorType);
        void SetChargeStart(int channel, int connectorType);
        void SetChargePrepare(int channel, int connectorType);
        void SetChargeRun(int channel, int connectorType);
        void SetChargeStop(int channel, int connectorType);
        void SetDoorStatus(int channel, bool doorOpen);
        void SetUiFault(int channel);
        void SetChargeComplete(int channel);

        void ResetCharger();
    }
}
