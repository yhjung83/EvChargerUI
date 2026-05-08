using EvChargerUI.Models;
using EvChargerUI.Views.DualChannel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace EvChargerUI.ViewModels
{
    public class LeftChargerViewModel : ChargerViewModel
    {
        public LeftChargerViewModel(int channelNo, MainViewModel parentViewModel, Charger charger) : base(channelNo, parentViewModel, charger )
        {

        }

        protected override void InitViews()
        {
            ConnectorImage = "/Images/img_ready_connector_l.png";
            UsableButtonImage = "/Images/btn_usable_l.png";
            UsableButtonImage2 = "/Images/btn_test.png";
            UsableButtonImage3 = "/Images/btn_payYN.png";
            UsableButtonColumnIndex = "3";

            _reservationWaitView = null;
            _chargerSelectTypeView = new ChargerSelectTypeView();
            _chargerSelectTypeView.DataContext = this;
            _paymentMethodSelectView = new PaymentMethodSelectView();
            _paymentMethodSelectView.DataContext = this;
            _readyToChargingView = new ReadyToChargingView();
            _readyToChargingView.DataContext = this;
            _waitingOtherSideView = new WaitingOtherSideView();
            _waitingOtherSideView.DataContext = this;

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
