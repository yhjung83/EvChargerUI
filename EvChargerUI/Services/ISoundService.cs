namespace EvChargerUI.Services
{
    public interface ISoundService
    {
        void PlaySoundAsync(string fileName);

        void StopSound();
    }
}

