using System;
using UnityEngine;

namespace SurvivalRts
{
    public enum SoundKind
    {
        OneShot3D,
        OneShot2D,
        Loop
    }

    // Quy tắc phát cho từng loại âm thanh: event nào, có lọc theo camera không, gộp và giới hạn ra sao.
    [CreateAssetMenu(menuName = "SurvivalRts/Sound Library", fileName = "SoundLibrary")]
    public class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public class Rule
        {
            [Tooltip("Loại âm thanh mà gameplay phát ra")]
            public SoundId id;

            [Tooltip("Đường dẫn event trong FMOD Studio, ví dụ event:/Enemy/Hit")]
            public string eventPath;

            [Tooltip("OneShot3D: phát một lần tại vị trí. OneShot2D: phát một lần không theo vị trí (UI, cảnh báo). Loop: phát lặp, chỉ đổi âm lượng")]
            public SoundKind kind = SoundKind.OneShot3D;

            [Tooltip("Bỏ sự kiện nằm ngoài khung nhìn camera (cộng lề). Tắt cho UI và cảnh báo")]
            public bool cullOutsideView = true;

            [Tooltip("Cạnh ô gộp (m): các sự kiện cùng loại trong một ô trong cùng frame gộp thành một tiếng. 0 = không gộp")]
            public float mergeCellSize = 6f;

            [Tooltip("Số tiếng mới tối đa mỗi giây của loại này (sau khi gộp)")]
            public int maxStartsPerSecond = 20;

            [Tooltip("Thời gian chờ tối thiểu (giây) giữa hai lần phát của loại này, ví dụ cho cảnh báo. 0 = không chờ")]
            public float cooldown;

            [Tooltip("Âm lượng gốc (1 = bằng âm lượng trong FMOD)")]
            public float volume = 1f;

            [Tooltip("Âm lượng cộng thêm theo log2(số sự kiện đã gộp), để chỗ đánh nhau đông nghe to hơn")]
            public float mergeVolumeBoost = 0.25f;

            [Tooltip("Âm lượng tối đa sau khi cộng thêm")]
            public float maxVolume = 1.6f;
        }

        [Header("Quy tắc")]
        [Tooltip("Mỗi SoundId một quy tắc")]
        public Rule[] rules;

        public Rule Get(SoundId id)
        {
            if (rules == null) return null;
            foreach (var rule in rules)
                if (rule.id == id) return rule;
            return null;
        }
    }
}
