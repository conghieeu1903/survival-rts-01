using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace SurvivalRts
{
    // Backend phát âm thanh bằng Unity AudioSource + AudioMixer:
    // định tuyến từng tiếng vào đúng AudioMixerGroup (Music, SFX, UI), quản lý pool giọng phát,
    // lọc tần số theo độ cao camera, ưu tiên giữ tiếng to hơn, và tự hạ nhẹ nhạc nền khi có tiếng nổ/cảnh báo.
    public class UnityAudioBackend : IAudioBackend
    {
        const int PoolSize = 28;
        const float MinSpatialDistance = 3f;
        const float MaxSpatialDistance = 42f;

        // Nếu AudioMixer có tham số này (Lowpass trên nhóm SFX, tham số Cutoff freq được expose) thì chỉ dùng một bộ lọc chung;
        // nếu không thì mới dùng bộ lọc trên từng giọng, và chỉ bật khi tần số cắt thấp hơn FilterOffAbove (đang zoom xa).
        const string MixerLowpassParam = "SFXLowpassCutoff";
        const float FilterOffAbove = 20000f;
        const float MinCutoff = 1600f;
        const float MaxCutoff = 22000f;
        const float CutoffSmoothing = 6f;

        // Độ ưu tiên của AudioSource (0 = quan trọng nhất). Khi vượt số giọng thật trong Project Settings, Unity tắt giọng ưu tiên thấp trước.
        const int PriorityMusic = 0;
        const int PriorityAlert = 16;
        const int PriorityLoop = 32;
        const int PriorityUi = 64;
        const int PrioritySfx = 128;
        const int PriorityExpendable = 200;

        class Voice
        {
            public GameObject Go;
            public Transform Tr;
            public AudioSource Source;
            public AudioLowPassFilter LowPass;
            public SoundId Id;
            public VolumeChannel Channel;
            public float BaseVolume;
            public float StartTime;
            public bool Is3D;
        }

        class LoopTrack
        {
            public SoundLibrary.Rule Rule;
            public AudioSource Source;
            public AudioLowPassFilter LowPass;
            public float BaseVolume;
        }

        readonly List<Voice> pool = new List<Voice>(PoolSize);
        readonly Dictionary<SoundId, LoopTrack> loops = new Dictionary<SoundId, LoopTrack>();
        readonly Dictionary<SoundId, int> lastClipIndex = new Dictionary<SoundId, int>();
        readonly AudioMixerGroup[] mixerGroups = new AudioMixerGroup[4];
        readonly float[] channelVolumes = { 0.8f, 0.8f, 0.8f, 0.8f };
        readonly bool[] channelPaused = new bool[4];

        AudioMixer mixer;
        bool mixerLowpass;
        float smoothedCutoff = MaxCutoff;
        GameObject rootGo;
        AudioListener activeListener;
        float cameraZoom = 1f;
        float combatIntensity;
        float duckEnvelope;

        public bool IsReady { get; private set; }

        public void Initialize(SoundLibrary library)
        {
            if (library == null || library.rules == null) return;

            mixer = library.mixer;
            mixerLowpass = mixer != null && mixer.GetFloat(MixerLowpassParam, out _);
            foreach (VolumeChannel ch in System.Enum.GetValues(typeof(VolumeChannel)))
            {
                var directGroup = library.GetMixerGroup(ch);
                if (directGroup != null)
                {
                    mixerGroups[(int)ch] = directGroup;
                }
                else if (mixer != null)
                {
                    var found = mixer.FindMatchingGroups(AudioVolumeSettings.MixerGroupName(ch));
                    if (found != null && found.Length > 0) mixerGroups[(int)ch] = found[0];
                }
            }

            rootGo = new GameObject("UnityAudioPool");
            Object.DontDestroyOnLoad(rootGo);

            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Voice_" + i);
                go.transform.SetParent(rootGo.transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = MinSpatialDistance;
                src.maxDistance = MaxSpatialDistance;
                src.dopplerLevel = 0f;
                var lp = go.AddComponent<AudioLowPassFilter>();
                lp.cutoffFrequency = 22000f;
                lp.enabled = false;

                pool.Add(new Voice
                {
                    Go = go,
                    Tr = go.transform,
                    Source = src,
                    LowPass = lp
                });
            }

            foreach (var rule in library.rules)
            {
                if (rule == null || rule.kind != SoundKind.Loop) continue;
                if (rule.clips == null || rule.clips.Length == 0 || rule.clips[0] == null) continue;

                var go = new GameObject("Loop_" + rule.id);
                go.transform.SetParent(rootGo.transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.spatialBlend = 0f;
                src.clip = rule.clips[0];
                src.outputAudioMixerGroup = GetMixerGroup(rule.channel);
                src.volume = 0f;
                src.priority = PriorityFor(rule);

                AudioLowPassFilter lp = null;
                if (rule.channel == VolumeChannel.Sfx && !mixerLowpass)
                {
                    lp = go.AddComponent<AudioLowPassFilter>();
                    lp.cutoffFrequency = MaxCutoff;
                    lp.enabled = false;
                }

                loops[rule.id] = new LoopTrack
                {
                    Rule = rule,
                    Source = src,
                    LowPass = lp,
                    BaseVolume = 0f
                };
            }

            IsReady = true;
        }

        public void UpdateListener(Transform listener)
        {
            if (!IsReady || listener == null) return;

            if (activeListener == null)
            {
                // Tắt các AudioListener dư thừa trên Camera để tránh cảnh báo 2 AudioListener trong scene.
                var existing = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
                foreach (var l in existing)
                {
                    if (l.transform != listener) l.enabled = false;
                }

                activeListener = listener.GetComponent<AudioListener>();
                if (activeListener == null) activeListener = listener.gameObject.AddComponent<AudioListener>();
                activeListener.enabled = true;
            }

            // Giảm dần mức nén (ducking) theo thời gian thực và cập nhật âm lượng + bộ lọc tần số.
            duckEnvelope = Mathf.MoveTowards(duckEnvelope, 0f, Time.unscaledDeltaTime * 2.2f);
            float targetCutoff = Mathf.Lerp(MinCutoff, MaxCutoff, cameraZoom * cameraZoom);
            smoothedCutoff = Mathf.Lerp(smoothedCutoff, targetCutoff, 1f - Mathf.Exp(-CutoffSmoothing * Time.unscaledDeltaTime));
            float sfxCutoff = smoothedCutoff;
            bool voiceFilters = !mixerLowpass && sfxCutoff < FilterOffAbove;
            if (mixerLowpass) mixer.SetFloat(MixerLowpassParam, sfxCutoff);

            for (int i = 0; i < pool.Count; i++)
            {
                var v = pool[i];
                if (!v.Source.isPlaying) continue;
                v.Source.volume = ComputeSourceVolume(v.Channel, v.BaseVolume, false);
                if (v.LowPass != null)
                {
                    bool on = voiceFilters && v.Channel == VolumeChannel.Sfx;
                    if (v.LowPass.enabled != on) v.LowPass.enabled = on;
                    if (on) v.LowPass.cutoffFrequency = sfxCutoff;
                }
            }

            foreach (var kv in loops)
            {
                var track = kv.Value;
                if (track.BaseVolume <= 0.001f) continue;

                bool isDucked = track.Rule.channel == VolumeChannel.Music || track.Rule.id == SoundId.CrowdMarch || track.Rule.id == SoundId.Battle;
                track.Source.volume = ComputeSourceVolume(track.Rule.channel, track.BaseVolume, isDucked);

                if (track.LowPass != null)
                {
                    if (track.LowPass.enabled != voiceFilters) track.LowPass.enabled = voiceFilters;
                    if (voiceFilters) track.LowPass.cutoffFrequency = sfxCutoff;
                }

                if (track.Rule.id == SoundId.Battle)
                {
                    track.Source.pitch = Mathf.Lerp(0.94f, 1.08f, combatIntensity);
                }
            }
        }

        public void PlayOneShot(SoundLibrary.Rule rule, Vector3 position, float volume, float parameter = 0f)
        {
            if (!IsReady || rule == null || rule.clips == null || rule.clips.Length == 0) return;
            if (channelPaused[(int)rule.channel]) return;

            var clip = PickClip(rule);
            if (clip == null) return;

            var voice = AcquireVoice(rule, volume);
            if (voice == null) return;

            voice.Id = rule.id;
            voice.Channel = rule.channel;
            voice.BaseVolume = volume;
            voice.StartTime = Time.unscaledTime;
            voice.Is3D = rule.kind == SoundKind.OneShot3D;

            voice.Tr.position = position;
            voice.Source.clip = clip;
            voice.Source.outputAudioMixerGroup = GetMixerGroup(rule.channel);
            voice.Source.spatialBlend = voice.Is3D ? 0.8f : 0f;

            float pitch = 1f + (rule.pitchRandom > 0f ? Random.Range(-rule.pitchRandom, rule.pitchRandom) : 0f);
            if (rule.id == SoundId.BuilderHammer)
            {
                // Lúc mới đặt móng tiếng búa trầm, càng gần hoàn thành tiếng gõ càng đanh và cao hơn.
                pitch += Mathf.Lerp(-0.12f, 0.22f, Mathf.Clamp01(parameter));
            }
            else if (rule.id == SoundId.UiClick && parameter > 0f)
            {
                // Tiếng rê chuột vào nút nhẹ và cao hơn tiếng nhấn.
                pitch += 0.28f;
                volume *= 0.45f;
                voice.BaseVolume = volume;
            }
            voice.Source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            voice.Source.volume = ComputeSourceVolume(rule.channel, volume, false);
            voice.Source.priority = PriorityFor(rule);

            if (voice.LowPass != null)
            {
                bool useFilter = rule.channel == VolumeChannel.Sfx && !mixerLowpass && smoothedCutoff < FilterOffAbove;
                voice.LowPass.enabled = useFilter;
                if (useFilter) voice.LowPass.cutoffFrequency = smoothedCutoff;
            }

            // Kích hoạt sidechain ducking nhẹ khi có tiếng sập nhà, cảnh báo hoặc phát bắn lớn.
            if (rule.id == SoundId.BuildingCollapse || rule.id == SoundId.AlertHq)
            {
                duckEnvelope = Mathf.Max(duckEnvelope, 0.45f);
            }
            else if (rule.id == SoundId.TowerShot || rule.id == SoundId.EnemyDeath)
            {
                duckEnvelope = Mathf.Max(duckEnvelope, Mathf.Clamp01(volume * 0.22f));
            }

            voice.Source.Play();
        }

        public void SetLoopVolume(SoundLibrary.Rule rule, float volume)
        {
            if (!IsReady || rule == null || !loops.TryGetValue(rule.id, out var track)) return;

            track.BaseVolume = Mathf.Max(0f, volume);
            if (track.BaseVolume <= 0.001f)
            {
                track.Source.volume = 0f;
                if (track.Source.isPlaying) track.Source.Stop();
                return;
            }

            bool isDucked = rule.channel == VolumeChannel.Music || rule.id == SoundId.CrowdMarch || rule.id == SoundId.Battle;
            track.Source.volume = ComputeSourceVolume(rule.channel, track.BaseVolume, isDucked);

            if (!track.Source.isPlaying && !channelPaused[(int)rule.channel])
            {
                track.Source.Play();
            }
        }

        public void SetGlobalParameter(string parameterName, float value)
        {
            if (parameterName == "CameraZoom")
            {
                cameraZoom = Mathf.Clamp01(value);
            }
            else if (parameterName == "CombatIntensity")
            {
                combatIntensity = Mathf.Clamp01(value);
            }
        }

        public void SetChannelVolume(VolumeChannel channel, float volume)
        {
            float clamped = Mathf.Clamp01(volume);
            channelVolumes[(int)channel] = clamped;
            if (mixer != null)
            {
                mixer.SetFloat(AudioVolumeSettings.MixerParameterName(channel), AudioVolumeSettings.ToDecibels(clamped));
            }
            RefreshAllVolumes();
        }

        public void SetChannelPaused(VolumeChannel channel, bool paused)
        {
            channelPaused[(int)channel] = paused;
            for (int i = 0; i < pool.Count; i++)
            {
                var v = pool[i];
                if (v.Channel != channel) continue;
                if (paused && v.Source.isPlaying) v.Source.Pause();
                else if (!paused) v.Source.UnPause();
            }

            foreach (var kv in loops)
            {
                var track = kv.Value;
                if (track.Rule.channel != channel) continue;
                if (paused && track.Source.isPlaying) track.Source.Pause();
                else if (!paused && track.BaseVolume > 0.001f) track.Source.UnPause();
            }
        }

        public void Shutdown()
        {
            if (rootGo != null) Object.Destroy(rootGo);
            pool.Clear();
            loops.Clear();
            lastClipIndex.Clear();
            IsReady = false;
        }

        static int PriorityFor(SoundLibrary.Rule rule)
        {
            if (rule.channel == VolumeChannel.Music) return PriorityMusic;
            if (rule.id == SoundId.AlertHq) return PriorityAlert;
            if (rule.kind == SoundKind.Loop) return PriorityLoop;
            if (rule.kind == SoundKind.OneShot2D) return PriorityUi;
            // Tiếng đánh trúng phát nhiều nhất và ít quan trọng nhất; cho nó bị cắt trước.
            if (rule.id == SoundId.EnemyHit) return PriorityExpendable;
            return PrioritySfx;
        }

        AudioMixerGroup GetMixerGroup(VolumeChannel channel)
        {
            return mixerGroups[(int)channel] ?? mixerGroups[(int)VolumeChannel.Master];
        }

        AudioClip PickClip(SoundLibrary.Rule rule)
        {
            int count = rule.clips.Length;
            if (count == 1) return rule.clips[0];

            int idx = Random.Range(0, count);
            if (lastClipIndex.TryGetValue(rule.id, out int prev) && idx == prev)
            {
                idx = (idx + 1) % count;
            }
            lastClipIndex[rule.id] = idx;
            return rule.clips[idx];
        }

        Voice AcquireVoice(SoundLibrary.Rule rule, float requestedVolume)
        {
            int maxSim = Mathf.Max(1, rule.maxSimultaneous);
            int activeSameId = 0;
            Voice quietestSameId = null;
            Voice freeVoice = null;
            Voice quietestAny = null;

            for (int i = 0; i < pool.Count; i++)
            {
                var v = pool[i];
                if (!v.Source.isPlaying && !channelPaused[(int)v.Channel])
                {
                    if (freeVoice == null) freeVoice = v;
                    continue;
                }

                if (quietestAny == null || v.BaseVolume < quietestAny.BaseVolume)
                {
                    quietestAny = v;
                }

                if (v.Id == rule.id)
                {
                    activeSameId++;
                    if (quietestSameId == null || v.BaseVolume < quietestSameId.BaseVolume)
                    {
                        quietestSameId = v;
                    }
                }
            }

            // Đạt trần số tiếng cùng loại: chỉ chiếm lại giọng nhỏ nhất nếu tiếng mới lớn hơn hoặc bằng.
            if (activeSameId >= maxSim)
            {
                if (quietestSameId != null && requestedVolume >= quietestSameId.BaseVolume * 0.9f)
                {
                    return quietestSameId;
                }
                return null;
            }

            return freeVoice ?? quietestAny;
        }

        float ComputeSourceVolume(VolumeChannel channel, float baseVolume, bool applyDucking)
        {
            float duck = applyDucking ? Mathf.Clamp01(1f - duckEnvelope) : 1f;
            // Khi đã gắn AudioMixerGroup, AudioMixer tự suy giảm dB theo thanh trượt kênh; chỉ nhân thủ công nếu chưa có Mixer.
            if (mixer != null && GetMixerGroup(channel) != null)
            {
                return Mathf.Clamp01(baseVolume * duck);
            }
            float master = channelVolumes[(int)VolumeChannel.Master];
            float group = channelVolumes[(int)channel];
            return Mathf.Clamp01(baseVolume * master * group * duck);
        }

        void RefreshAllVolumes()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                var v = pool[i];
                if (v.Source != null && v.Source.isPlaying)
                {
                    v.Source.volume = ComputeSourceVolume(v.Channel, v.BaseVolume, false);
                }
            }

            foreach (var kv in loops)
            {
                var track = kv.Value;
                if (track.Source != null && track.BaseVolume > 0.001f)
                {
                    bool isDucked = track.Rule.channel == VolumeChannel.Music || track.Rule.id == SoundId.CrowdMarch || track.Rule.id == SoundId.Battle;
                    track.Source.volume = ComputeSourceVolume(track.Rule.channel, track.BaseVolume, isDucked);
                }
            }
        }
    }
}
