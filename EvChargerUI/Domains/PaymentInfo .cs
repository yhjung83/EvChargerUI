namespace EvChargerUI.Domains
{
    public class PaymentInfo
    {
        public string PayCode { get; set; }
        public string AuthNum { get; set; }
        public string TotalCost { get; set; }
        public string PayDate { get; set; }
        public string PayTime { get; set; }
        public string PgNum { get; set; }
        public string MerchantName { get; set; }
        public string MerchantId { get; set; }
        public string CardIssuerName { get; set; }
        public string CardAcquirerName { get; set; }
        public string InstallmentMonths { get; set; }
        public string MaskedCardNumber { get; set; }
    }
}