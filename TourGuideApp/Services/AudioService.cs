using Plugin.Maui.Audio;

namespace TourGuideApp.Services;
public class AudioService
{
    readonly IAudioManager audioManager;

    public AudioService()
    {
        audioManager = AudioManager.Current;
    }

    public async Task PlayAudio(string file)
    {
        var player = audioManager.CreatePlayer(await FileSystem.OpenAppPackageFileAsync(file));
        player.Play();
    }
}