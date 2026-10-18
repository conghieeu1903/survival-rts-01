using UnityEngine;

namespace SurvivalRts
{
    [CreateAssetMenu(menuName = "SurvivalRts/Building Data", fileName = "BuildingData")]
    public class BuildingData : ScriptableObject
    {
        [Header("Thông tin")]
        [Tooltip("Tên hiển thị trên menu xây dựng")]
        public string displayName;

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
    }
}
