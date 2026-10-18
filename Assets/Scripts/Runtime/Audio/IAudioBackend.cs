using UnityEngine;

namespace SurvivalRts
{
    // Lớp phát tiếng cuối cùng. AudioDirector chỉ nói chuyện qua interface này, nên đổi FMOD sang engine khác chỉ cần viết backend mới.
    public interface IAudioBackend
    {
        bool IsReady { get; }

        void Initialize(SoundLibrary library);

        void UpdateListener(Transform listener);

        void PlayOneShot(SoundLibrary.Rule rule, Vector3 position, float volume, float parameter = 0f);

        void SetLoopVolume(SoundLibrary.Rule rule, float volume);

        // Điều khiển parameter toàn cục trong FMOD (ví dụ CameraZoom, CombatIntensity).
        void SetGlobalParameter(string parameterName, float value);

        // Âm lượng (0..1) và tạm dừng của một nhóm tiếng, ví dụ "bus:/Music". "bus:/" là tổng.
        void SetBusVolume(string busPath, float volume);

        void SetBusPaused(string busPath, bool paused);

        void Shutdown();
    }
}
