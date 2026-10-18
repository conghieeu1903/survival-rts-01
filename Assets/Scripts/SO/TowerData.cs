using UnityEngine;

namespace SurvivalRts
{
    [CreateAssetMenu(menuName = "SurvivalRts/Tower Data", fileName = "TowerData")]
    public class TowerData : ScriptableObject
    {
        [Header("Tấn công")]
        [Tooltip("Sát thương mỗi phát bắn")]
        public float damage = 14f;

        [Tooltip("Tầm bắn (m), tính từ tâm tháp tới tâm kẻ địch")]
        public float range = 12f;

        [Tooltip("Số giây giữa hai phát bắn")]
        public float fireInterval = 1.2f;

        [Header("Hiển thị")]
        [Tooltip("Độ cao (m) của nòng so với chân tháp, là điểm bắt đầu của tia đạn")]
        public float muzzleHeight = 3.5f;
    }
}
