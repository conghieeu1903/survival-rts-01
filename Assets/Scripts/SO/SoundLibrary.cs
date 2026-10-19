using System;
using UnityEngine;
using UnityEngine.Audio;

namespace SurvivalRts
{
    public enum SoundKind
    {
        OneShot3D,
        OneShot2D,
        Loop
    }

    // Quy tắc phát cho từng loại âm thanh: danh sách AudioClip, nhóm AudioMixer, có lọc theo camera không, gộp và giới hạn ra sao.
    [CreateAssetMenu(menuName = "SurvivalRts/Sound Library", fileName = "SoundLibrary")]
    public class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public class Rule
        {
            [Tooltip("Loại âm thanh mà gameplay phát ra")]
            public SoundId id;

            [Tooltip("Danh sách clip phát ngẫu nhiên cho loại âm thanh này")]
            public AudioClip[] clips;

            [Tooltip("Nhóm âm lượng điều khiển trong menu cài đặt và AudioMixer (Music, Sfx, Ui)")]
            public VolumeChannel channel = VolumeChannel.Sfx;

            [Tooltip("OneShot3D: phát một lần tại vị trí. OneShot2D: phát một lần không theo vị trí (UI, cảnh báo). Loop: phát lặp, chỉ đổi âm lượng")]
            public SoundKind kind = SoundKind.OneShot3D;

            [Tooltip("Bỏ sự kiện nằm ngoài khung nhìn camera (cộng lề). Tắt cho UI và cảnh báo")]
            public bool cullOutsideView = true;

            [Tooltip("Cạnh ô gộp (m): các sự kiện cùng loại trong một ô trong cùng frame gộp thành một tiếng. 0 = không gộp")]
            public float mergeCellSize = 6f;

            [Tooltip("Số tiếng mới tối đa mỗi giây của loại này (sau khi gộp)")]
            public int maxStartsPerSecond = 20;

            [Tooltip("Số tiếng cùng loại phát đồng thời tối đa; vượt mức này sẽ ưu tiên giữ tiếng to/gần hơn")]
            public int maxSimultaneous = 8;

            [Tooltip("Biên độ lệch cao độ ngẫu nhiên (ví dụ 0.08 = +-8%)")]
            public float pitchRandom = 0.08f;

            [Tooltip("Thời gian chờ tối thiểu (giây) giữa hai lần phát của loại này, ví dụ cho cảnh báo. 0 = không chờ")]
            public float cooldown;

            [Tooltip("Âm lượng gốc")]
            public float volume = 1f;

            [Tooltip("Âm lượng cộng thêm theo log2(số sự kiện đã gộp), để chỗ đánh nhau đông nghe to hơn")]
            public float mergeVolumeBoost = 0.25f;

            [Tooltip("Âm lượng tối đa sau khi cộng thêm")]
            public float maxVolume = 1.6f;
        }

        [Header("Bộ trộn âm thanh (AudioMixer)")]
        [Tooltip("AudioMixer chính của game gồm các nhóm Master, Music, SFX, UI")]
        public AudioMixer mixer;
        public AudioMixerGroup masterGroup;
        public AudioMixerGroup musicGroup;
        public AudioMixerGroup sfxGroup;
        public AudioMixerGroup uiGroup;

        [Header("Quy tắc")]
        [Tooltip("Mỗi SoundId một quy tắc")]
        public Rule[] rules;

        public AudioMixerGroup GetMixerGroup(VolumeChannel channel)
        {
            switch (channel)
            {
                case VolumeChannel.Music: return musicGroup != null ? musicGroup : masterGroup;
                case VolumeChannel.Sfx: return sfxGroup != null ? sfxGroup : masterGroup;
                case VolumeChannel.Ui: return uiGroup != null ? uiGroup : masterGroup;
                default: return masterGroup;
            }
        }

        public Rule Get(SoundId id)
        {
            if (rules == null) return null;
            foreach (var rule in rules)
                if (rule.id == id) return rule;
            return null;
        }
    }
}
