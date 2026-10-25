using UnityEngine;

namespace SurvivalRts
{
    // Thứ tự ở đây là thứ tự tab trong menu xây dựng.
    public enum BuildingCategory
    {
        Economy,
        Military,
        Defense,
        Test
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

        [Header("Quân sự")]
        [Tooltip("Gán nếu công trình là trại lính: đào tạo dân tự do thành loại lính này. Số lính tối đa lấy từ maxWorkers")]
        public SoldierData soldier;

        [Header("Chi phí xây dựng")]
        [Tooltip("Số gỗ cần để xây công trình này")]
        public int woodCost;

        [Tooltip("Số đá cần để xây công trình này")]
        public int stoneCost;

        [Tooltip("Số sắt cần để xây công trình này")]
        public int ironCost;

        [Tooltip("Số lúa cần để xây công trình này")]
        public int foodCost;

        [Tooltip("Số tiền vàng cần để xây công trình này")]
        public int goldCost;

        [Header("Xây dựng & Dân cư")]
        [Tooltip("Thời gian (giây) để 1 thợ xây hoàn thành công trình này")]
        public float buildTime = 6f;

        [Tooltip("Thời gian (giây) để tháo dỡ hoàn toàn công trình này")]
        public float demolishTime = 2.5f;

        [Tooltip("Số dân cộng thêm khi công trình xây xong (dùng cho Nhà dân)")]
        public int populationProvided;

        [Tooltip("Số thợ tối đa có thể gán vào công trình này (dùng cho Nhà Builder và Nhà khai thác)")]
        public int maxWorkers;

        [Header("Khai thác tài nguyên")]
        [Tooltip("Loại tài nguyên công trình này khai thác (None nếu không phải nhà khai thác)")]
        public ResourceType harvestResource;

        [Tooltip("Bán kính khai thác (m) tính từ tâm công trình (dùng cho Trại gỗ)")]
        public float harvestRadius = 8f;

        [Tooltip("Bắt buộc đặt sát cạnh mỏ tài nguyên và tự xoay mặt trước hướng vào mỏ (dùng cho Xưởng đá)")]
        public bool requireAdjacentResource;

        [Tooltip("Thời gian (giây) công nhân chặt/đập tại điểm tài nguyên mỗi chuyến")]
        public float harvestInterval = 2.5f;

        [Tooltip("Lượng tài nguyên thu về mỗi chuyến")]
        public int harvestAmount = 5;

        public bool HasAnyCost =>
            woodCost > 0 || stoneCost > 0 || ironCost > 0 || foodCost > 0 || goldCost > 0;

        // Trả về chuỗi tóm tắt chi phí để hiển thị trên thẻ xây dựng.
        public string FormatCost()
        {
            if (!HasAnyCost) return "Miễn phí";

            var parts = new System.Collections.Generic.List<string>(3);
            if (woodCost > 0) parts.Add($"{woodCost} Gỗ");
            if (stoneCost > 0) parts.Add($"{stoneCost} Đá");
            if (ironCost > 0) parts.Add($"{ironCost} Sắt");
            if (foodCost > 0) parts.Add($"{foodCost} Lúa");
            if (goldCost > 0) parts.Add($"{goldCost} Vàng");
            return string.Join(" · ", parts);
        }
    }
}
