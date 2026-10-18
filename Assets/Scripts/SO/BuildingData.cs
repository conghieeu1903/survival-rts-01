using UnityEngine;

namespace SurvivalRts
{
    // Thứ tự ở đây là thứ tự tab trong menu xây dựng.
    public enum BuildingCategory
    {
        Economy,
        Military,
        Defense
    }

    [CreateAssetMenu(menuName = "SurvivalRts/Building Data", fileName = "BuildingData")]
    public class BuildingData : ScriptableObject
    {
        [Header("Thông tin")]
        [Tooltip("Tên hiển thị trên menu xây dựng")]
        public string displayName;

        [Tooltip("Nhóm của công trình, quyết định nó nằm ở tab nào trong menu xây dựng")]
        public BuildingCategory category;

        [Tooltip("Prefab của công trình")]
        public GameObject prefab;

        [Header("Kích thước")]
        [Tooltip("Số ô chiếm trên lưới (rộng x sâu), mỗi ô 1m")]
        public Vector2Int footprint = new Vector2Int(2, 2);

        [Tooltip("Chiều cao (m), dùng cho vật cản NavMesh")]
        public float height = 2f;

        [Header("Chiến đấu")]
        [Tooltip("Máu tối đa khi công trình được đặt xuống")]
        public float maxHealth = 300f;

        [Tooltip("Gán nếu công trình là tháp tự bắn kẻ địch, để trống với công trình thường")]
        public TowerData tower;
    }
}
