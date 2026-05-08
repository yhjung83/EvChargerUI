using System;

namespace EvChargerUI.Commons.Settings
{
    public class ChargerTimerSettings
    {
        public int AutoReturnToInitViewTimer { get; set; }
        public int ChargerSelectTypeViewTimer { get; set; }
        public int ChargingCompleteViewTimer { get; set; }
        public int ChargingReceiptViewTimer { get; set; }
        public int PaymentMethodSelectViewTimer { get; set; }
        public int ReadyToChargingViewTimer { get; set; }
        public int ReservationWaitingViewTimer { get; set; }
        public int AdminMainViewTimer { get; set; }
        public int AdminSettingViewTimer { get; set; }

        /// <summary>
        /// popup
        /// </summary>
        public int AuthFailPopupViewTimer { get; set; }
        public int AuthSuccessPopupViewTimer { get; set; }
        public int ChargeInputPopupViewTimer { get; set; }
        public int InputPasswordPopupViewTimer { get; set; }
        public int InputPhoneNumberPopupViewTimer { get; set; }
        public int InputReservationNumberPopupViewTimer { get; set; }
        public int InsertICCardPopupViewTimer { get; set; }
        public int PaymentFailPopupViewTimer { get; set; }
        public int PaymentSuccessPopupViewTimer { get; set; }
        public int QrCodePopupViewTimer { get; set; }

        public int ReportQrCodePopupViewTimer { get; set; }
        public int ReservationCancelPopupViewTimer { get; set; }
        public int ReservationDescriptionPopupViewTimer { get; set; }     
        public int ReservationSuccessPopupViewTimer { get; set; }
        public int SearchStationQrCodePopupViewTimer { get; set; }
        public int TagRFCardPopupViewTimer { get; set; }
        public int TagSamsungpayPopupViewTimer { get; set; }
        public int WaitingChargingStartPopupViewTimer { get; set; }
        public int WrongReservationNoPopupViewTimer { get; set; }
        public int ConnectorErrorPopupViewTimer { get; set; }
        public int HelpPopupViewTimer { get; set; }
        public int CreditCardReceiptPopupViewTimer { get; set; }
    }
      
        
}