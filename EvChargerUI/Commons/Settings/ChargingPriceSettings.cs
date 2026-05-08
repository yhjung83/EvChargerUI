using System.Windows.Forms;

namespace EvChargerUI.Commons.Settings
{
    public class ChargingPriceSettings
    {
        public float[] PriceForHour { get; private set; }

        public ChargingPriceSettings()
        {
            PriceForHour = new float[24];
        }

    }
}