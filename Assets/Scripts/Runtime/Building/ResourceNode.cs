using System.Collections.Generic;
using UnityEngine;

namespace SurvivalRts
{
    public enum ResourceType
    {
        None = 0,
        Wood = 1,
        Stone = 2,
        Food = 3,
        Iron = 4
    }

    // Đánh dấu vật thể tài nguyên trên bản đồ (Cây gỗ, Mỏ đá, Bụi quả, Mỏ sắt) để nhà khai thác kiểm tra vị trí và công nhân tới thu hoạch.
    [RequireComponent(typeof(GridOccupant))]
    public class ResourceNode : MonoBehaviour
    {
        static readonly List<ResourceNode> all = new List<ResourceNode>();

        [Header("Tài nguyên")]
        [Tooltip("Loại tài nguyên của điểm này (Wood = Cây gỗ, Stone = Mỏ đá, Food = Nông sản, Iron = Mỏ sắt)")]
        public ResourceType resourceType = ResourceType.Wood;

        GridOccupant occupant;

        public static IReadOnlyList<ResourceNode> All => all;

        public GridOccupant Occupant
        {
            get
            {
                if (occupant == null) occupant = GetComponent<GridOccupant>();
                return occupant;
            }
        }

        void Awake()
        {
            occupant = GetComponent<GridOccupant>();
        }

        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        void OnDisable()
        {
            all.Remove(this);
        }

        // Tự động gắn ResourceNode cho các Tree, StoneMine, BerryBush và IronDeposit có sẵn trong scene nếu chưa được gắn.
        public static void EnsureSceneNodesRegistered()
        {
            var occupants = FindObjectsByType<GridOccupant>(FindObjectsSortMode.None);
            foreach (var occ in occupants)
            {
                if (occ == null || occ.isBuilding) continue;
                if (occ.GetComponent<ResourceNode>() != null) continue;

                string n = occ.gameObject.name;
                if (n.StartsWith("Tree"))
                {
                    occ.gameObject.AddComponent<ResourceNode>().resourceType = ResourceType.Wood;
                }
                else if (n.StartsWith("StoneMine"))
                {
                    occ.gameObject.AddComponent<ResourceNode>().resourceType = ResourceType.Stone;
                }
                else if (n.StartsWith("BerryBush"))
                {
                    occ.gameObject.AddComponent<ResourceNode>().resourceType = ResourceType.Food;
                }
                else if (n.StartsWith("IronDeposit") || n.StartsWith("IronOre"))
                {
                    occ.gameObject.AddComponent<ResourceNode>().resourceType = ResourceType.Iron;
                }
            }
        }
    }
}
