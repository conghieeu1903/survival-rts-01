using UnityEngine;

namespace SurvivalRts
{
    public enum PlacementRule : byte
    {
        Block = 0,
        Clear = 1
    }

    // Cấu hình quy tắc chiếm chỗ và thu dọn của vật thể tự nhiên hoặc vật cản trên bản đồ.
    [CreateAssetMenu(menuName = "SurvivalRts/Object Placement Data", fileName = "ObjectPlacementData")]
    public class ObjectPlacementData : ScriptableObject
    {
        [Header("Thông tin")]
        [Tooltip("Tên hiển thị của vật thể")]
        public string displayName;

        [Header("Quy tắc đặt công trình")]
        [Tooltip("Block: chặn đặt công trình; Clear: cho phép đặt công trình đè lên và tự thu dọn")]
        public PlacementRule placementRule = PlacementRule.Block;

        [Tooltip("Kích thước chiếm ô trên lưới (rộng x sâu)")]
        public Vector2Int footprint = Vector2Int.one;

        [Tooltip("Cho phép dân hoặc người chơi dọn bỏ vật thể này")]
        public bool clearable = true;

        [Header("Phần thưởng khi thu dọn")]
        [Tooltip("Loại tài nguyên nhận được khi dọn vật thể này")]
        public ResourceType clearRewardType = ResourceType.None;

        [Tooltip("Số lượng tài nguyên nhận được khi dọn")]
        public int clearRewardAmount;
    }
}
