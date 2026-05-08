using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvChargerUI.Domains
{
    public class ChargingInfo
    {
        public int Soc { get; set; }
        public double PowerMeter { get; set; }
        public double Current { get; set; }
        public double Voltage { get; set; }
    }
}
