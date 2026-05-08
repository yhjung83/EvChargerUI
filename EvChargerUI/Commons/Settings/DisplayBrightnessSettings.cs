namespace EvChargerUI.Commons.Settings
{
    public class DisplayBrightnessSettings
    {
        private int _levelForDay;
        private int _levelForNight;
        public int LevelForDay
        {
            get => _levelForDay;
            set
            {
                if (value < 0) _levelForDay = 0;
                else if (value > 100) _levelForDay = 100;
                else _levelForDay = value;

            }
        }

        public int LevelForNight
        {
            get => _levelForNight;
            set
            {
                if (value < 0) _levelForNight = 0;
                else if (value > 100) _levelForNight = 100;
                else _levelForNight = value;

            }
        }
    }
}