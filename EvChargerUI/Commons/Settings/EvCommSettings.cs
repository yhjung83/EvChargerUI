using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvChargerUI.Commons.Settings
{
    public class EvCommSettings
    {
        public string ServerBaseUrl { get; set; } 
        public string ClientBaseUrl { get; set; } 
        public int StatusUpdateInterval { get; set; }
        public int EVSE_Status { get; set; } // 0 : 정상(운영중), 1 : 점검중,  2 : 중지 (운영중지)
        public string EVSE_PayYN { get; set; }
        public string EVSE_Test { get; set; }
        public int EVSE_EmergencyStop { get; set; } // 0: 정상, 1: 긴급중지
        public int EVSE_DSP_Status { get; set; }    // 0: 정상, 1: 오류(DSP 연결 끊김)
        public int EVSE_Network_Status { get; set; } // 0: 정상, 1: 네트워크 연결 끊김
        public string LastUiUpdateDate { get; set; } // 마지막 UI 업데이트 일시
        public int ChargerMode { get; set; } // 0: 알 수 없음, 1: 운영중, 2: 운영중지, 3: 점검중
        public bool MockMode { get; set; } // true: 로컬 테스트 모드 (네트워크 응답 검증 패스), false: 정상 모드
        public string IsDebug { get; set; } // Y: 디버그 윈도우 표시, N: 표시 안함
    }
}
