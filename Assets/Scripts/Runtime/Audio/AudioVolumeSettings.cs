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

    // Âm lượng người chơi chọn (0..1) cho từng nhóm, lưu bằng PlayerPrefs và ánh xạ sang tham số dB của AudioMixer.
    public static class AudioVolumeSettings
    {
        const float DefaultVolume = 0.8f;

        public static event Action Changed;

        public static string MixerParameterName(VolumeChannel channel)
        {
            switch (channel)
            {
                case VolumeChannel.Music: return "MusicVolume";
                case VolumeChannel.Sfx: return "SFXVolume";
                case VolumeChannel.Ui: return "UIVolume";
                default: return "MasterVolume";
            }
        }

        public static string MixerGroupName(VolumeChannel channel)
        {
            switch (channel)
            {
                case VolumeChannel.Music: return "Music";
                case VolumeChannel.Sfx: return "SFX";
                case VolumeChannel.Ui: return "UI";
                default: return "Master";
            }
        }

        public static float ToDecibels(float linear)
        {
            return linear <= 0.0001f ? -80f : Mathf.Log10(Mathf.Clamp01(linear)) * 20f;
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
