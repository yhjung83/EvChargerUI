using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EvChargerUI.Models;
using EvChargerUI.Views.SingleChannel;


namespace EvChargerUI.ViewModels
{
    public class SingleChargerViewModel : ChargerViewModel
    {
        public SingleChargerViewModel(int channelNo, MainViewModel parentViewModel, Charger charger) : base(channelNo, parentViewModel, charger)
        {

        }

        protected override void InitViews()
        {
            ConnectorImage = null;
            UsableButtonImage = null;
            UsableButtonColumnIndex = null;

            _reservationWaitView = new ReservationWaitingView();
            _reservationWaitView.DataContext = this;
            _chargerSelectTypeView = new ChargerSelectTypeView();
            _chargerSelectTypeView.DataContext = this;
            _tripleChargerSelectTypeView = new TripleChargerSelectTypeView();
            _tripleChargerSelectTypeView.DataContext = this;
            _paymentMethodSelectView = new PaymentMethodSelectView();
            _paymentMethodSelectView.DataContext = this;
            _readyToChargingView = new ReadyToChargingView();
            _readyToChargingView.DataContext = this;
            _chaeviReadyToChargeLRView = new ChaeviReadyToChargeLRView();
            _chaeviReadyToChargeLRView.DataContext = this;
            _chaeviReadyToChargeUDLRView = new ChaeviReadyToChargeUDLRView();
            _chaeviReadyToChargeUDLRView.DataContext = this;

            _progressView = new ChargingProgressView();
            _progressView.DataContext = this;
            _calChargeAmountView = new CalcChargeAmountView();
            _calChargeAmountView.DataContext = this;
            _completeView = new ChargingCompleteView();
            _completeView.DataContext = this;

            _chargingReceiptView = new ChargingReceiptView();
            _chargingReceiptView.DataContext = this;

        }
    }
}
