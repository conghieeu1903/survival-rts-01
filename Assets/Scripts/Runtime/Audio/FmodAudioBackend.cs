using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace SurvivalRts
{
    public class FmodAudioBackend : IAudioBackend
    {
        readonly Dictionary<SoundId, EventDescription> descriptions = new Dictionary<SoundId, EventDescription>();
        readonly Dictionary<SoundId, EventInstance> loops = new Dictionary<SoundId, EventInstance>();
        readonly Dictionary<string, Bus> buses = new Dictionary<string, Bus>();
        readonly Dictionary<string, float> globalParams = new Dictionary<string, float>();

        public bool IsReady { get; private set; }

        public void Initialize(SoundLibrary library)
        {
            if (!IsConfigured())
            {
                Debug.LogWarning("Âm thanh: FMOD chưa được nối với project FMOD Studio (Settings trống), tạm không phát tiếng.");
                return;
            }

            if (!RuntimeManager.HaveMasterBanksLoaded)
            {
                Debug.LogWarning("Âm thanh: FMOD chưa nạp được Master bank, tạm không phát tiếng.");
                return;
            }

            foreach (var rule in library.rules)
            {
                if (string.IsNullOrEmpty(rule.eventPath)) continue;
                var result = RuntimeManager.StudioSystem.getEvent(rule.eventPath, out var description);
                if (result != FMOD.RESULT.OK)
                {
                    Debug.LogWarning($"Âm thanh: không tìm thấy event {rule.eventPath} ({result}).");
                    continue;
                }
                // Nạp sẵn dữ liệu âm thanh để lần phát đầu không bị khựng.
                if (rule.kind != SoundKind.Loop) description.loadSampleData();
                descriptions[rule.id] = description;
            }
            IsReady = descriptions.Count > 0;
        }

        static bool IsConfigured()
        {
#if UNITY_EDITOR
            var settings = Settings.Instance;
            return settings != null && (!settings.HasSourceProject || !string.IsNullOrEmpty(settings.SourceProjectPath));
#else
            return true;
#endif
        }

        public void UpdateListener(Transform listener)
        {
            if (IsReady) RuntimeManager.SetListenerLocation(listener.gameObject);
        }

        public void PlayOneShot(SoundLibrary.Rule rule, Vector3 position, float volume, float parameter = 0f)
        {
            if (!IsReady || !descriptions.TryGetValue(rule.id, out var description)) return;
            if (description.createInstance(out var instance) != FMOD.RESULT.OK) return;

            if (rule.kind == SoundKind.OneShot3D) instance.set3DAttributes(position.To3DAttributes());
            if (rule.id == SoundId.BuilderHammer)
            {
                instance.setParameterByName("BuildProgress", Mathf.Clamp01(parameter));
            }
            instance.setVolume(volume);
            instance.start();
            instance.release();
        }

        public void SetLoopVolume(SoundLibrary.Rule rule, float volume)
        {
            if (!IsReady || !descriptions.TryGetValue(rule.id, out var description)) return;

            if (!loops.TryGetValue(rule.id, out var instance))
            {
                // Chưa cần tiếng thì chưa tạo; chỉ khởi tạo khi âm lượng lớn hơn 0.
                if (volume <= 0.001f) return;
                if (description.createInstance(out instance) != FMOD.RESULT.OK) return;
                instance.setVolume(volume);
                instance.start();
                loops[rule.id] = instance;
                return;
            }

            if (volume <= 0.001f)
            {
                instance.setVolume(0f);
                if (instance.getPlaybackState(out var stopState) == FMOD.RESULT.OK && stopState != PLAYBACK_STATE.STOPPED)
                {
                    instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                }
                return;
            }

            if (instance.getPlaybackState(out var state) == FMOD.RESULT.OK && state == PLAYBACK_STATE.STOPPED)
            {
                // Phát lại từ đầu khi bắt đầu bật lại loop.
                instance.start();
            }
            instance.setVolume(volume);
        }

        public void SetGlobalParameter(string parameterName, float value)
        {
            if (!IsReady) return;
            if (globalParams.TryGetValue(parameterName, out float prev) && Mathf.Abs(prev - value) < 0.002f) return;
            globalParams[parameterName] = value;
            RuntimeManager.StudioSystem.setParameterByName(parameterName, value);
        }

        public void SetBusVolume(string busPath, float volume)
        {
            if (IsReady && TryGetBus(busPath, out var bus)) bus.setVolume(volume);
        }

        public void SetBusPaused(string busPath, bool paused)
        {
            if (IsReady && TryGetBus(busPath, out var bus)) bus.setPaused(paused);
        }

        bool TryGetBus(string busPath, out Bus bus)
        {
            if (buses.TryGetValue(busPath, out bus)) return true;
            if (RuntimeManager.StudioSystem.getBus(busPath, out bus) != FMOD.RESULT.OK)
            {
                Debug.LogWarning($"Âm thanh: không tìm thấy bus {busPath}.");
                return false;
            }
            buses[busPath] = bus;
            return true;
        }

        public void Shutdown()
        {
            foreach (var instance in loops.Values)
            {
                instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                instance.release();
            }
            loops.Clear();
            descriptions.Clear();
            buses.Clear();
            globalParams.Clear();
            IsReady = false;
        }
    }
}
