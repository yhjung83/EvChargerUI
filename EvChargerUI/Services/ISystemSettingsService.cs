namespace EvChargerUI.Services
{
    public interface ISystemSettingsService
    {
        bool SetSoundVolume(int dayLevel, int nightLevel);
        bool SetDisplayBrightness(int dayLevel, int nightLevel);

        /// <summary>
        /// 현재 모니터 밝기 값 가져오기 (0-100%)
        /// </summary>
        int GetCurrentBrightness();
    }
}