using UnityEngine;

namespace SurvivalRts
{
    // Lớp phát tiếng cuối cùng dùng Unity Audio thuần.
    public interface IAudioBackend
    {
        bool IsReady { get; }

        void Initialize(SoundLibrary library);

        void UpdateListener(Transform listener);

        void PlayOneShot(SoundLibrary.Rule rule, Vector3 position, float volume, float parameter = 0f);

        void SetLoopVolume(SoundLibrary.Rule rule, float volume);

        // Điều khiển thông số toàn cục (ví dụ CameraZoom, CombatIntensity).
        void SetGlobalParameter(string parameterName, float value);

        // Âm lượng (0..1) và tạm dừng theo từng nhóm kênh (Master, Music, Sfx, Ui).
        void SetChannelVolume(VolumeChannel channel, float volume);

        void SetChannelPaused(VolumeChannel channel, bool paused);

        void Shutdown();
    }
}
