using System;

namespace EvChargerUI.Domains
{
    public class ChaeviModelItem
    {
        public string ModelName { get; set; }
        public string DisplayName { get; set; }

        public override string ToString() => DisplayName;
    }
}

