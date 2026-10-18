using System;
using UnityEngine;

namespace SurvivalRts
{
    public enum VolumeChannel
    {
        Master,
        Music,
        Sfx,
        Ui
    }

    // Âm lượng người chơi chọn (0..1) cho từng nhóm, lưu bằng PlayerPrefs để giữ giữa các lần chơi.
    public static class AudioVolumeSettings
    {
        const float DefaultVolume = 0.8f;

        public static event Action Changed;

        public static string BusPath(VolumeChannel channel)
        {
            switch (channel)
            {
                case VolumeChannel.Music: return "bus:/Music";
                case VolumeChannel.Sfx: return "bus:/SFX";
                case VolumeChannel.Ui: return "bus:/UI";
                default: return "bus:/";
            }
        }

        public static float Get(VolumeChannel channel)
        {
            return PlayerPrefs.GetFloat(Key(channel), DefaultVolume);
        }

        public static void Set(VolumeChannel channel, float volume)
        {
            PlayerPrefs.SetFloat(Key(channel), Mathf.Clamp01(volume));
            Changed?.Invoke();
        }

        public static void ResetToDefault()
        {
            foreach (VolumeChannel channel in Enum.GetValues(typeof(VolumeChannel)))
                PlayerPrefs.SetFloat(Key(channel), DefaultVolume);
            Changed?.Invoke();
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        static string Key(VolumeChannel channel)
        {
            return "audio.volume." + channel;
        }
    }
}
