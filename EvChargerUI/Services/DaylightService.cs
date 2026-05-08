using System;

namespace EvChargerUI.Services
{
    public class DaylightService : IDaylightService
    {
        public bool IsDayTime()
        {
            DateTime now = DateTime.Now;
            return now.Hour >= 6 && now.Hour < 18;
        }

        public bool IsNightTime()
        {
            return !IsDayTime();
        }

    }
}