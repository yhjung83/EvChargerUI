using System;

namespace EvChargerUI.Commons.Util
{
    public static class ChargingAmountUtil
    {
        public static uint RoundChargeWToNearest10(uint chargeW)
        {
            ulong v = chargeW;
            return (uint)(((v + 5UL) / 10UL) * 10UL);
        }

        public static uint ToRoundedChargeW(double deltaKwh)
        {
            if (double.IsNaN(deltaKwh) || double.IsInfinity(deltaKwh)) return 0;
            double safe = Math.Max(0.0, deltaKwh);
            uint raw = (uint)(safe * 1000.0);
            return RoundChargeWToNearest10(raw);
        }

        public static double ToRoundedDisplayKwh(double deltaKwh)
        {
            return ToRoundedChargeW(deltaKwh) / 1000.0;
        }
    }
}

