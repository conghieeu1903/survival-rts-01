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

        [Tooltip("Animation đã nướng thành bone texture (cần các clip Idle, Walk, Attack, Death). Để trống nếu mesh không có animation")]
        public GpuAnimationData animation;

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

        [Tooltip("Tầm với (m), tính từ mép thân kẻ địch (tâm + bán kính) tới mép mục tiêu. Nhỏ hơn đường kính thân để con đứng sau không đánh xuyên qua con đứng trước")]
        public float attackRange = 0.3f;

        [Tooltip("Số giây giữa hai đòn")]
        public float attackCooldown = 1f;

        [Tooltip("Tầm phát hiện mục tiêu (m). Ngoài tầm này kẻ địch đi theo flow field về Nhà chính")]
        public float detectRange = 6f;
    }
}
