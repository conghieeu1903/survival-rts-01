using UnityEngine;

namespace SurvivalRts
{
    public class GridOccupant : MonoBehaviour
    {
        [Header("Lưới")]
        [Tooltip("Số ô (rộng x sâu) mà vật thể này chiếm trên lưới xây dựng, đã tính theo hướng xoay")]
        public Vector2Int footprint = Vector2Int.one;

        [Tooltip("Bật cho công trình phe mình: kẻ địch có thể phá để đi qua. Tắt cho vật cản tĩnh (cây, đá, tường): kẻ địch luôn đi vòng")]
        public bool isBuilding;

        bool registered;

        public Vector2Int Min { get; private set; }

        void Start()
        {
            Register();
        }

        public void Register()
        {
            var grid = BuildGrid.Instance;
            if (registered || grid == null) return;

            Min = grid.MinCell(transform.position, footprint);
            grid.SetOccupancy(Min, footprint, isBuilding ? CellOccupancy.Building : CellOccupancy.Static);
            registered = true;
        }

        void OnDestroy()
        {
            if (registered && BuildGrid.Instance != null)
                BuildGrid.Instance.SetOccupancy(Min, footprint, CellOccupancy.None);
        }
    }
}
