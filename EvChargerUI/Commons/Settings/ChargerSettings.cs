using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvChargerUI.Commons.Settings
{
    public class ChargerSettings
    {
        public string   StationId { get; set; }
        public string   StationName { get; set; }
        public string   LeftChannelChargerId    { get; set; }
        public string   RightChannelChargerId   { get; set; }
        public string   ChargerManufacturerCode { get; set; }
        public string   PaymentManufacturerCode { get; set; }

        public string   LeftQrCode { get; set; }
        public string   RightQrCode { get; set; }
        public int      LeftConnectorType { get; set; } = 1; // 기본값: DC콤보 (표준: 0=AC3, 1=DC콤보, 2=차데모)
        public int      RightConnectorType { get; set; } = 1;
        public string   IsTriple { get; set; } = "N"; // 트리플 커넥터 선택 화면 사용 여부 (Y/N, 기본값: N)
        public string   IsArmMovable { get; set; } = "N"; // 팔 이동형 여부 (Y/N, 기본값: N) - 채비 전용
        public string   DspComPortNo            { get; set; }
        public int   DspBaudRate { get; set; }

        public string   PaymentDeviceComPortNo  { get; set; }
        public int PaymentDeviceBaudRate { get; set; }
        public int ChargingSpeed { get; set; } = 200; // 기본값: 200kW
        /// <summary>
        /// 결제 단말기 헬스체크 주기(초). INI에는 60처럼 초 단위로 기입합니다. (기존 60000ms 설정은 로드 시 60초로 변환)
        /// </summary>
        public int PaymentDeviceHealthCheckInterval { get; set; } = 60;
        public string   ChaeviModelName { get; set; } = ""; // 채비 모델명 (DSP 통신 방식 선택용) 

        [JsonIgnore]
        public int MaxChannelCount
        {
            get
            {
                int result = 0;
                if (!string.IsNullOrEmpty(LeftChannelChargerId)) result++;
                if (!string.IsNullOrEmpty(RightChannelChargerId)) result++;
                return result;
            }
        }
    }
}
