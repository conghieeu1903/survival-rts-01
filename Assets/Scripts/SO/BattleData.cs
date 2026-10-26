using System;
using UnityEngine;

namespace SurvivalRts
{
    // Một loại lính trong trận: chỉ số chiến đấu, số tiểu đội mỗi đội và hiển thị.
    [Serializable]
    public class BattleUnitType
    {
        [Header("Thông tin")]
        [Tooltip("Tên loại lính, chữ cái đầu hiện trên nhãn tiểu đội")]
        public string displayName = "Lính kiếm";

        [Tooltip("Số tiểu đội loại này trong mỗi đội. Loại ở trên xếp hàng đầu, gần tâm bản đồ nhất")]
        public int squadCount = 28;

        [Tooltip("Bắn từ xa: mỗi phát bắn vẽ một tia mũi tên từ lính tới mục tiêu")]
        public bool ranged;

        [Header("Hiển thị")]
        [Tooltip("Mesh vẽ lính (Entities Graphics)")]
        public Mesh mesh;

        [Tooltip("Material GpuSkinnedLit của lính")]
        public Material material;

        [Tooltip("Tỉ lệ đồng đều của mesh")]
        public float scale = 0.85f;

        [Tooltip("Đổ bóng. Tắt để giảm chi phí vẽ khi có hàng chục nghìn lính")]
        public bool castShadows;

        [Tooltip("Animation đã nướng (cần các clip Idle, Walk, Attack, Death)")]
        public GpuAnimationData animation;

        [Tooltip("Màu nhân thêm vào màu đội để phân biệt loại lính")]
        public Color tint = Color.white;

        [Header("Huấn luyện")]
        [Tooltip("Vàng trừ cho mỗi tiểu đội huấn luyện")]
        public int cost = 200;

        [Tooltip("Số giây huấn luyện một tiểu đội")]
        public float trainSeconds = 15f;

        [Tooltip("Số lính của tiểu đội huấn luyện; 0 hoặc lớn hơn squadSize thì lấy squadSize")]
        public int trainSquadSize = 0;

        [Header("Chỉ số")]
        [Tooltip("Máu tối đa")]
        public int maxHealth = 140;

        [Tooltip("Sát thương mỗi đòn")]
        public int damage = 22;

        [Tooltip("Số giây giữa hai đòn")]
        public float attackInterval = 0.9f;

        [Tooltip("Thời điểm (giây) trong một đòn mà sát thương được tính")]
        public float strikeDelay = 0.45f;

        [Tooltip("Tầm đánh (m), tính từ tâm lính tới tâm địch")]
        public float attackRange = 1.1f;

        [Tooltip("Tầm phát hiện địch (m), nên lớn hơn tầm đánh")]
        public float detectRange = 8f;

        [Tooltip("Tốc độ chạy (m/giây)")]
        public float moveSpeed = 3.2f;

        [Tooltip("Bán kính thân (m), lính đẩy nhau để không chồng lên")]
        public float radius = 0.4f;
    }

    // Cấu hình trận đại chiến nhiều đội: các loại lính, bản đồ và màu đội.
    [CreateAssetMenu(menuName = "SurvivalRts/Battle Data", fileName = "BattleData")]
    public class BattleData : ScriptableObject
    {
        [Header("Trận")]
        [Tooltip("Số lính mỗi tiểu đội, đơn vị nhận lệnh di chuyển")]
        public int squadSize = 100;

        [Tooltip("Khoảng cách giữa hai lính trong đội hình (m)")]
        public float formationSpacing = 1.2f;

        [Tooltip("Nửa cạnh bản đồ vuông, tính từ gốc tọa độ (m)")]
        public float mapHalfSize = 200f;

        [Tooltip("Màu từng đội, nhân vào màu gốc của vật liệu")]
        public Color[] teamColors =
        {
            new Color(0.35f, 0.55f, 1f), new Color(1f, 0.4f, 0.35f),
            new Color(0.4f, 0.9f, 0.45f), new Color(1f, 0.85f, 0.3f)
        };

        [Tooltip("Số giây xác nằm lại sau khi chết xong clip Death")]
        public float corpseLinger = 3f;

        [Header("Kinh tế")]
        [Tooltip("Vàng mỗi đội có khi bắt đầu trận")]
        public int startGold = 400;

        [Tooltip("Vàng mỗi đội nhận được mỗi giây (số nguyên)")]
        public int goldPerSecond = 10;

        [Tooltip("Số tiểu đội dự trữ cho mỗi loại lính trong mỗi đội, chỗ để huấn luyện thêm")]
        public int reserveSquadsPerTeam = 10;

        [Header("Loại lính")]
        [Tooltip("Các loại lính của mỗi đội theo thứ tự xếp hàng từ đầu tới cuối")]
        public BattleUnitType[] unitTypes = { new BattleUnitType() };

        public int SquadsPerTeam
        {
            get
            {
                int total = 0;
                foreach (var type in unitTypes) total += type.squadCount;
                return total;
            }
        }

        public int UnitsPerTeam => SquadsPerTeam * squadSize;

        // Tổng số chỗ tiểu đội mỗi đội gồm cả phần dự trữ chưa xuất hiện.
        public int SquadCapacity => SquadsPerTeam + Mathf.Max(0, reserveSquadsPerTeam) * unitTypes.Length;
    }
}
