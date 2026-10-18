using UnityEngine;

namespace SurvivalRts
{
    [CreateAssetMenu(menuName = "SurvivalRts/Enemy Data", fileName = "EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Header("Hiển thị")]
        [Tooltip("Mesh vẽ kẻ địch (ECS, Entities Graphics)")]
        public Mesh mesh;

        [Tooltip("Material của kẻ địch, phải hỗ trợ DOTS instancing (URP Lit dùng được)")]
        public Material material;

        [Tooltip("Tỉ lệ đồng đều của mesh")]
        public float scale = 0.85f;

        [Tooltip("Đổ bóng. Tắt để giảm chi phí vẽ khi có rất nhiều kẻ địch")]
        public bool castShadows = true;

        [Header("Chỉ số")]
        [Tooltip("Máu tối đa")]
        public float maxHealth = 80f;

        [Tooltip("Tốc độ di chuyển (m/giây)")]
        public float moveSpeed = 3f;

        [Tooltip("Bán kính thân (m), dùng để các kẻ địch không chồng lên nhau")]
        public float radius = 0.4f;

        [Header("Tấn công")]
        [Tooltip("Sát thương mỗi đòn")]
        public float damage = 10f;

        [Tooltip("Tầm đánh (m), tính từ tâm kẻ địch tới mép mục tiêu")]
        public float attackRange = 1f;

        [Tooltip("Số giây giữa hai đòn")]
        public float attackCooldown = 1f;

        [Tooltip("Tầm phát hiện mục tiêu (m). Ngoài tầm này kẻ địch đi theo flow field về Nhà chính")]
        public float detectRange = 6f;
    }
}
