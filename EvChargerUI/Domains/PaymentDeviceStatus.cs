using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvChargerUI.Domains
{
    public enum PaymentDeviceStatus
    {
        Unknown = 0,
        Success = 1,
        Fail = 2,
        SecurityBreachDetected = 3
    }

    public static class PaymentDeviceStatusMessageMananger
    {
        private static readonly Dictionary<PaymentDeviceStatus, string> _messages = new Dictionary<PaymentDeviceStatus, string>()
        {
            { PaymentDeviceStatus.Success, "결제단말기 정상" },
            { PaymentDeviceStatus.Fail, "결제단말기 연결 및 상태 이상" },
            { PaymentDeviceStatus.SecurityBreachDetected, "결제단말기 보안 침해" }
        };

        public static string GetMessage(PaymentDeviceStatus status)
        {

            if (_messages.TryGetValue(status, out var message))
            {
                return message;
            }
            else
            {
                return "알 수 없는 상태";
            }
        }
    }
}
