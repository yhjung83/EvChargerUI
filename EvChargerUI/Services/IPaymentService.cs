using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EvChargerUI.Domains;

namespace EvChargerUI.Services
{
    public interface IPaymentService
    {
        bool Open();
        void Close();
        bool IsConnected { get; }
        bool IsAvailable { get; }

        Task<PaymentInfo> PayCost(int cost, string csName);
        Task<string> ReadRfCard();
        Task<bool> CancelPay(PaymentInfo paymentInfo, int cancelCost, string csName);
        Task<bool> CancelCardReading();
    }
}
