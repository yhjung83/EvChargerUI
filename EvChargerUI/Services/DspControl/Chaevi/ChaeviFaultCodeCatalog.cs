using System;
using System.Collections.Generic;
using System.Text;

namespace EvChargerUI.Services.DspControl.Chaevi
{
    /// <summary>
    /// 환경부 UI-FW 통신 프로토콜 FaultCode(16bit): [4bit 대분류][4bit Code][8bit 세부항목]
    /// </summary>
    internal static class ChaeviFaultCodeCatalog
    {
        private static readonly Dictionary<int, string> MajorNames = new Dictionary<int, string>
        {
            { 1, "입력부" },
            { 2, "출력부" },
            { 3, "제어부" },
            { 4, "통신" },
            { 5, "인증/결제" },
            { 6, "기타" },
        };

        private static readonly Dictionary<int, string> SubNames = BuildSubNames();
        private static readonly Dictionary<long, string> DetailNames = BuildDetailNames();

        public struct ParsedFault
        {
            public ushort Raw;
            public int Major;
            public int Sub;
            public int Detail;
            public int DisplayCode;
        }

        public static ParsedFault Parse(ushort rawFaultCode)
        {
            int major = (rawFaultCode >> 12) & 0xF;
            int sub = (rawFaultCode >> 8) & 0xF;
            int detail = rawFaultCode & 0xFF;
            return new ParsedFault
            {
                Raw = rawFaultCode,
                Major = major,
                Sub = sub,
                Detail = detail,
                DisplayCode = major * 100 + sub,
            };
        }

        public static string FormatLogDescription(ushort rawFaultCode)
        {
            if (rawFaultCode == 0)
                return "";

            ParsedFault p = Parse(rawFaultCode);
            string majorName = GetMajorName(p.Major);
            string subName = GetSubName(p.Major, p.Sub);
            string detailName = GetDetailName(p.Major, p.Sub, p.Detail);

            var sb = new StringBuilder();
            sb.Append($"raw=0x{p.Raw:X4}");
            if (p.DisplayCode > 0)
                sb.Append($" code={p.DisplayCode}");
            sb.Append($" [{majorName}]");
            if (!string.IsNullOrEmpty(subName))
                sb.Append($" {subName}");
            if (!string.IsNullOrEmpty(detailName))
                sb.Append($" - {detailName}");
            else if (p.Detail != 0)
                sb.Append($" - 상세미정의(0x{p.Detail:X2})");
            return sb.ToString();
        }

        public static string GetMajorName(int major)
        {
            return MajorNames.TryGetValue(major, out string name) ? name : $"대분류{major}";
        }

        private static string GetSubName(int major, int sub)
        {
            int key = SubKey(major, sub);
            return SubNames.TryGetValue(key, out string name) ? name : (sub == 0 ? "" : $"Code{sub:D2}");
        }

        private static string GetDetailName(int major, int sub, int detail)
        {
            long key = DetailKey(major, sub, detail);
            return DetailNames.TryGetValue(key, out string name) ? name : "";
        }

        private static int SubKey(int major, int sub) => major * 16 + sub;
        private static long DetailKey(int major, int sub, int detail) => ((long)major << 16) | ((long)sub << 8) | (uint)detail;

        private static Dictionary<int, string> BuildSubNames()
        {
            var d = new Dictionary<int, string>();
            void Sub(int major, int sub, string name) => d[SubKey(major, sub)] = name;

            Sub(1, 1, "주전원 차단기");
            Sub(1, 2, "과전압");
            Sub(1, 3, "저전압");
            Sub(1, 4, "과전류");
            Sub(1, 5, "접지 Fail");

            Sub(2, 1, "MC 오류");
            Sub(2, 2, "MC 융착");
            Sub(2, 3, "과전압");
            Sub(2, 4, "과전류");
            Sub(2, 5, "선간절연이상");

            Sub(3, 1, "Pre-charging 오류");
            Sub(3, 2, "모듈 이상");
            Sub(3, 3, "온도 이상");

            Sub(4, 1, "CP Fail");
            Sub(4, 2, "CP Error");
            Sub(4, 3, "PLC 통신 Fail");
            Sub(4, 4, "PLC 통신 Error");
            Sub(4, 5, "CAN 통신 Fail");
            Sub(4, 6, "CAN 통신 Error");

            Sub(5, 1, "제어보드 Error");
            Sub(5, 2, "차량 간 협상 실패");
            Sub(5, 3, "사용자 인증 실패");

            Sub(9, 1, "비상스위치 버튼");
            Sub(9, 2, "커넥터 Lock 오류");
            Sub(9, 3, "커넥터 위치 센서 오류");
            Sub(9, 4, "전력계량기 오류");
            Sub(9, 5, "결제모듈 오류");
            Sub(9, 6, "외함 개방");
            Sub(9, 7, "차량 및 배터리");
            Sub(9, 8, "기타");
            return d;
        }

        private static Dictionary<long, string> BuildDetailNames()
        {
            var d = new Dictionary<long, string>();
            void Det(int major, int sub, int detail, string name) => d[DetailKey(major, sub, detail)] = name;

            // 1 입력부
            Det(1, 1, 0, "<00> 주전원 차단기 - 주전원 차단기 Trip");
            Det(1, 2, 1, "<01> 과전압 - 검출값 >= 정격전압의 110%");
            Det(1, 2, 2, "<02> R상 이상");
            Det(1, 2, 3, "<03> S상 이상");
            Det(1, 2, 4, "<04> T상 이상");
            Det(1, 3, 1, "<01> 저전압 - 검출값 <= 정격전압의 90%");
            Det(1, 3, 2, "<02> R상 이상");
            Det(1, 3, 3, "<03> S상 이상");
            Det(1, 3, 4, "<04> T상 이상");
            Det(1, 4, 0, "<00> 과전류");
            Det(1, 5, 1, "<01> DCGF - 충전기 외함접지 상태 오류");
            Det(1, 5, 2, "<02> GMI");

            // 2 출력부
            Det(2, 1, 1, "<01> MC1 오류 - 출력측 MC 기능 이상");
            Det(2, 1, 2, "<02> MC2 오류");
            Det(2, 2, 1, "<01> MC1 융착 - 출력측 MC 장애 발생");
            Det(2, 2, 2, "<02> MC2 융착");
            Det(2, 3, 0, "<00> 과전압 - 검출값 >= 출력 요청 전압의 110%");
            Det(2, 4, 0, "<00> 과전류 - 검출값 >= 정격전류의 110%");
            Det(2, 5, 1, "<01> Cable Check Ready - 차량 연결 이후 선간절연 이상");
            Det(2, 5, 2, "<02> Cable Check Rising Error");
            Det(2, 5, 3, "<03> Cable Check Falling Error");

            // 3 제어부
            Det(3, 1, 0, "<00> Pre-charging 오류 - 충전 전 차량 간 테스트 결과 이상");
            Det(3, 2, 1, "<01> Output under Voltage");
            Det(3, 2, 2, "<02> Temperature Error");
            Det(3, 2, 3, "<03> AC Voltage Error");
            Det(3, 2, 4, "<04> AC Phase");
            Det(3, 2, 5, "<05> Unbalanced Input Voltage");
            Det(3, 2, 6, "<06> Output Over Voltage");
            Det(3, 2, 7, "<07> Interface B/D");
            Det(3, 3, 1, "<01> AC Control B/D");
            Det(3, 3, 2, "<02> DC Control B/D");
            Det(3, 3, 3, "<03> DC Connection B/D");
            Det(3, 3, 4, "<04> Wattmeter");
            Det(3, 3, 5, "<05> Power Module");
            Det(3, 3, 11, "<11> Coupler P");
            Det(3, 3, 12, "<12> Coupler N");
            Det(3, 3, 13, "<13> Cable P");
            Det(3, 3, 14, "<14> Cable N");
            Det(3, 3, 15, "<15> Cooler IN");
            Det(3, 3, 16, "<16> Cooler Out");

            // 4 통신
            Det(4, 1, 0, "<00> CP Fail");
            Det(4, 2, 1, "<01> EV-S2 OFF");
            Det(4, 2, 2, "<02> Timeout: 9V -> 6V");
            Det(4, 3, 0, "<00> Interface B/D <-> SECC 통신 오류");
            Det(4, 4, 1, "<01> FAILED");
            Det(4, 4, 2, "<02> Sequence Error");
            Det(4, 4, 3, "<03> Service ID Invalid");
            Det(4, 4, 4, "<04> Unknown Session");
            Det(4, 4, 5, "<05> Service Selection Invalid");
            Det(4, 4, 6, "<06> Payment Selection Invalid");
            Det(4, 4, 12, "<12> Contact Canceled");
            Det(4, 4, 13, "<13> Wrong Charg Parameter");
            Det(4, 4, 14, "<14> Power Delivery Not Applied");
            Det(4, 4, 19, "<19> Wrong Energy Transfer Mode");
            Det(4, 4, 30, "<30> Timeout - Communication Setup");
            Det(4, 4, 31, "<31> Timeout - Sequence");
            Det(4, 4, 32, "<32> Timeout - Notification Max Delay");
            Det(4, 4, 33, "<33> Timeout - Welding Detection");
            Det(4, 4, 40, "<40> Wrong CP Level");
            Det(4, 4, 41, "<41> Proximity Error");
            Det(4, 4, 42, "<42> HLC Error");
            Det(4, 4, 43, "<43> Heartbeat Error");
            Det(4, 4, 44, "<44> EVSE CAN Init");
            Det(4, 4, 45, "<45> HPGP Link Down");
            Det(4, 4, 46, "<46> TLS Error Alert");
            Det(4, 5, 0, "<00> CAN 통신 Fail");
            Det(4, 6, 0, "<00> CAN 통신 Error");

            // 5 인증/결제
            Det(5, 1, 0, "<00> UI 통신 불가");
            Det(5, 1, 1, "<01> Interface B/D 통신");
            Det(5, 1, 2, "<02> AC Control B/D 통신");
            Det(5, 1, 3, "<03> DC Control B/D 통신");
            Det(5, 1, 4, "<04> DC Connection B/D 통신");
            Det(5, 2, 7, "<07> Certificate Expired");
            Det(5, 2, 8, "<08> Signature Error");
            Det(5, 2, 9, "<09> No Certificate Available");
            Det(5, 2, 10, "<10> Cert Chain Error");
            Det(5, 2, 11, "<11> Challenge Invalid");
            Det(5, 2, 15, "<15> Tariff Selection Invalid");
            Det(5, 2, 16, "<16> Charging Profile Invalid");
            Det(5, 2, 17, "<17> Metering Signature Not Valid");
            Det(5, 2, 18, "<18> No Charge Service Selected");
            Det(5, 2, 22, "<22> Certificate Revoked");
            Det(5, 2, 23, "<23> No Negotiation");
            Det(5, 3, 0, "<00> 사용자 인증 실패");

            // 9 기타
            Det(9, 1, 0, "<00> 비상스위치 동작");
            Det(9, 2, 1, "<01> COMBO");
            Det(9, 2, 2, "<02> CHAdeMO");
            Det(9, 2, 3, "<03> AC3");
            Det(9, 3, 1, "<01> COMBO");
            Det(9, 3, 2, "<02> CHAdeMO");
            Det(9, 3, 3, "<03> AC3");
            Det(9, 4, 1, "<01> DC 전력량계 오류");
            Det(9, 4, 2, "<02> AC 전력량계 오류");
            Det(9, 5, 0, "<00> 결제모듈 오류");
            Det(9, 6, 1, "<01> 전면 도어");
            Det(9, 6, 2, "<02> 후면 도어");
            Det(9, 6, 3, "<03> 우측 도어");
            Det(9, 6, 4, "<04> 좌측 도어");
            Det(9, 6, 5, "<05> PB 전면 도어");
            Det(9, 6, 6, "<06> PB 후면 도어");
            Det(9, 9, 1, "<01> RESS Temperature inhibit");
            Det(9, 9, 2, "<02> EV Shift Position");
            Det(9, 9, 3, "<03> Charger Connector Lock Fault");
            Det(9, 9, 4, "<04> EV RESS Malfunction");
            Det(9, 9, 5, "<05> Charging Current Differential");
            Det(9, 9, 6, "<06> ChargingVoltage Out of Range");
            Det(9, 9, 10, "<10> Charging System Incompatibility");
            Det(9, 9, 11, "<11> No Data");
            Det(9, 10, 0, "<00> 기타");
            Det(9, 10, 1, "<01> FUSE 1 오류");
            Det(9, 10, 2, "<02> FUSE 2 오류");
            Det(9, 10, 3, "<03> FUSE 3 오류");
            Det(9, 10, 4, "<04> FUSE 4 오류");
            Det(9, 10, 11, "<11> RELAY1 오류");
            Det(9, 10, 12, "<12> RELAY2 오류");
            Det(9, 10, 13, "<13> RELAY3 오류");
            Det(9, 10, 14, "<14> RELAY4 오류");
            Det(9, 10, 15, "<15> RELAY5 오류");
            Det(9, 10, 16, "<16> RELAY6 오류");
            Det(9, 10, 21, "<21> RELAY1 융착");
            Det(9, 10, 22, "<22> RELAY2 융착");
            Det(9, 10, 23, "<23> RELAY3 융착");
            Det(9, 10, 24, "<24> RELAY4 융착");
            Det(9, 10, 25, "<25> RELAY5 융착");
            Det(9, 10, 26, "<26> RELAY6 융착");
            Det(9, 10, 31, "<31> SEMI -> FULL");
            Det(9, 10, 32, "<32> FULL -> SEMI");
            Det(9, 10, 41, "<41> COOLER 상태 오류");
            Det(9, 10, 42, "<42> COOLER 오일 부족");
            Det(9, 10, 46, "<46> EOCR 오류");
            Det(9, 10, 47, "<47> CHARGING TYPE ERROR");

            return d;
        }
    }
}
