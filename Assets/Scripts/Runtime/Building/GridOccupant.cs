using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalRts
{
    // Đăng ký phạm vi chiếm ô của công trình hoặc vật thể tự nhiên trên BuildGrid.
    public class GridOccupant : MonoBehaviour
    {
        static readonly List<GridOccupant> all = new List<GridOccupant>();

        [Header("Lưới")]
        [Tooltip("Số ô (rộng x sâu) mà vật thể này chiếm trên lưới xây dựng, đã tính theo hướng xoay")]
        public Vector2Int footprint = Vector2Int.one;

        [Tooltip("Số ô (rộng x sâu) của hình chữ nhật chưa xoay; để (0,0) thì lấy theo footprint")]
        public Vector2Int baseFootprint;

        [Tooltip("Góc xoay quanh trục Y (độ nguyên), 0 là hướng gốc của prefab")]
        public int angle;

        [Tooltip("Bật cho công trình phe mình: kẻ địch có thể phá để đi qua. Tắt cho vật cản tĩnh (cây, đá, tường): kẻ địch luôn đi vòng")]
        public bool isBuilding;

        [Tooltip("Dữ liệu cấu hình của công trình đặt từ menu, dùng để tra tên, chi phí hoàn lại và thời gian tháo dỡ")]
        public BuildingData buildingData;

        [Tooltip("Quy tắc đặt đè (Block hoặc Clear) của vật thể tự nhiên trên bản đồ")]
        public ObjectPlacementData placementData;

        bool registered;
        readonly List<int> cells = new List<int>();

        public static IReadOnlyList<GridOccupant> All => all;

        public Vector2Int Min { get; private set; }

        // Chỉ số các ô trong lưới mà vật thể chiếm (đã bỏ ô ngoài lưới).
        public IReadOnlyList<int> Cells => cells;

        public bool IsClearRule => !isBuilding && placementData != null && placementData.placementRule == PlacementRule.Clear;

        public CellOccupancy DesiredOccupancy =>
            isBuilding ? CellOccupancy.Building
            : IsClearRule ? CellOccupancy.Clearable
            : CellOccupancy.Static;

        public Vector2Int BaseFootprint =>
            baseFootprint.x > 0 && baseFootprint.y > 0 ? baseFootprint
            : placementData != null && placementData.footprint.x > 0 && placementData.footprint.y > 0 ? placementData.footprint
            : footprint;

        public event Action ClearedByBuilding;

        // Nửa kích thước hộp bao song song trục của hình chữ nhật đã xoay (m).
        public Vector2 WorldHalfExtents(float cellSize)
        {
            var half = (Vector2)BaseFootprint * (cellSize * 0.5f);
            float rad = angle * Mathf.Deg2Rad;
            float c = Mathf.Abs(Mathf.Cos(rad));
            float s = Mathf.Abs(Mathf.Sin(rad));
            return new Vector2(c * half.x + s * half.y, s * half.x + c * half.y);
        }

        public string DisplayName =>
            buildingData != null && !string.IsNullOrEmpty(buildingData.displayName)
                ? buildingData.displayName
                : placementData != null && !string.IsNullOrEmpty(placementData.displayName)
                    ? placementData.displayName
                    : gameObject.name;

        public float DemolishTime =>
            buildingData != null && buildingData.demolishTime > 0f
                ? buildingData.demolishTime
                : 2.5f;

        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        void Start()
        {
            Register();
        }

        void OnDisable()
        {
            all.Remove(this);
        }

        public void Register()
        {
            var grid = BuildGrid.Instance;
            if (registered || grid == null) return;

            cells.Clear();
            Min = grid.MinCell(transform.position, footprint);
            var buffer = new List<int>();
            grid.CollectCells(transform.position, BaseFootprint, angle, buffer);
            foreach (int index in buffer)
                if (index >= 0) cells.Add(index);
            grid.SetOccupancy(cells, DesiredOccupancy);
            registered = true;
        }

        public void Unregister()
        {
            if (!registered) return;
            if (BuildGrid.Instance != null)
            {
                var grid = BuildGrid.Instance;
                var toClear = new List<int>(cells.Count);
                for (int i = 0; i < cells.Count; i++)
                {
                    int idx = cells[i];
                    if (idx >= 0 && grid.Occupancy.IsCreated && grid.Occupancy[idx] == DesiredOccupancy)
                        toClear.Add(idx);
                }
                if (toClear.Count > 0)
                    grid.SetOccupancy(toClear, CellOccupancy.None);
            }
            cells.Clear();
            registered = false;
        }

        // Tự động thu dọn các vật thể có placementRule = Clear nằm trùng với tập ô công trình sắp đặt.
        public static int ClearOverlappingClearables(IReadOnlyList<int> targetCells)
        {
            if (targetCells == null || targetCells.Count == 0) return 0;

            var cellSet = new HashSet<int>();
            for (int i = 0; i < targetCells.Count; i++)
            {
                if (targetCells[i] >= 0) cellSet.Add(targetCells[i]);
            }
            if (cellSet.Count == 0) return 0;

            var toClear = new List<GridOccupant>();
            for (int i = 0; i < all.Count; i++)
            {
                var occ = all[i];
                if (occ == null || !occ.isActiveAndEnabled || !occ.IsClearRule) continue;
                var occCells = occ.Cells;
                for (int c = 0; c < occCells.Count; c++)
                {
                    if (cellSet.Contains(occCells[c]))
                    {
                        toClear.Add(occ);
                        break;
                    }
                }
            }

            for (int i = 0; i < toClear.Count; i++)
            {
                var occ = toClear[i];
                if (occ.placementData != null && occ.placementData.clearRewardAmount > 0 && PopulationManager.Instance != null)
                {
                    PopulationManager.Instance.AddResource(occ.placementData.clearRewardType, occ.placementData.clearRewardAmount);
                }
                occ.ClearedByBuilding?.Invoke();
                occ.Unregister();
                occ.gameObject.SetActive(false);
            }

            return toClear.Count;
        }

        void OnDestroy()
        {
            all.Remove(this);
            Unregister();
        }
    }
}
