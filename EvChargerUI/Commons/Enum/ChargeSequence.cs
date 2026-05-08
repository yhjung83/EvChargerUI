using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvChargerUI.Commons.Enum
{
    public enum ChargeSequence    
    {
        WaitReservation = 0,
        SelectConnector = 1,
        SelectPaymentMethod = 2,
        PlugConnector = 3,
        Charging = 4,
        Completed = 5,
    }
}
