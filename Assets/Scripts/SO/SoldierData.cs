using UnityEngine;

namespace SurvivalRts
{
    // Chỉ số của một loại lính và cách trại lính đào tạo nó từ dân tự do.
    [CreateAssetMenu(menuName = "SurvivalRts/Soldier Data", fileName = "SoldierData")]
    public class SoldierData : ScriptableObject
    {
        [Header("Thông tin")]
        [Tooltip("Tên hiển thị trong bảng của trại lính")]
        public string displayName = "Lính kiếm";

        [Tooltip("Prefab lính (có SoldierUnit, Health, PlayerTarget, GpuMeshAnimator)")]
        public GameObject prefab;

        [Header("Đào tạo")]
        [Tooltip("Số giây để đào tạo một dân tự do thành lính")]
        public float trainTime = 6f;

        [Tooltip("Số lúa tốn cho mỗi lính đào tạo")]
        public int foodCost = 3;

        [Tooltip("Số sắt tốn cho mỗi lính đào tạo (làm kiếm). Đầu ván chưa có sắt nên mặc định 0")]
        public int ironCost;

        [Tooltip("Số vàng tốn cho mỗi lính đào tạo")]
        public int goldCost;

        [Header("Chiến đấu")]
        [Tooltip("Máu tối đa")]
        public float maxHealth = 140f;

        [Tooltip("Sát thương mỗi nhát kiếm")]
        public float damage = 22f;

        [Tooltip("Số giây giữa hai nhát kiếm (nên bằng độ dài clip Attack)")]
        public float attackInterval = 0.9f;

        [Tooltip("Thời điểm (giây) trong một nhát kiếm mà sát thương được tính, khớp lúc kiếm chém xuống trong clip")]
        public float strikeDelay = 0.45f;

        [Tooltip("Tầm chém (m), tính từ tâm lính tới tâm kẻ địch")]
        public float attackRange = 1.1f;

        [Header("Di chuyển & canh gác")]
        [Tooltip("Tốc độ chạy (m/giây)")]
        public float moveSpeed = 3.2f;

        [Tooltip("Bán kính phát hiện kẻ địch quanh vị trí canh gác (m)")]
        public float detectRange = 9f;

        [Tooltip("Đuổi quá khoảng này tính từ vị trí canh gác (m) thì bỏ mục tiêu và quay về")]
        public float leashRange = 14f;
    }
}
