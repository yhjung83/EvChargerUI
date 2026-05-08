using Newtonsoft.Json;
using System;

namespace EvChargerUI.Commons.Settings
{
    public class ChargerOperationSettings
    {
        public bool IsChargeTimeLimited { get; set; }
        public int ChargeLimitTime { get; set; }
        public bool IsTestOperation { get; set; }
        public bool IsPaymentApplied { get; set; }
        public int LogRetentionDays { get; set; } = 30;
        public bool DspLogSaveYn { get; set; }

        //xxx 251218 추가
        [JsonProperty] 
        public float[] PriceForHour { get; private set; }

        public ChargerOperationSettings()
        {
            PriceForHour = new float[24];
            for (int i = 0; i < 24; i++)
                PriceForHour[i] = 347.2f;
        }
    }
}