using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalRts
{
    // Quản lý tiến trình dựng khung 3 giai đoạn của công trình cho tới khi hoàn thành 100%.
    public class ConstructionSite : MonoBehaviour
    {
        static readonly List<ConstructionSite> activeSites = new List<ConstructionSite>();

        BuildingData data;
        Renderer[] buildingRenderers;
        GameObject stageRoot;
        readonly GameObject[] stageInstances = new GameObject[3];
        int currentStage = -1;
        float elapsed;
        float buildTime = 6f;

        public static IReadOnlyList<ConstructionSite> ActiveSites => activeSites;

        public BuildingData Data => data;
        public bool IsCompleted { get; private set; }
        public float Progress => buildTime > 0f ? Mathf.Clamp01(elapsed / buildTime) : 1f;
        public int StageIndex => currentStage + 1;

        public event Action<float, int> ProgressChanged;
        public event Action<ConstructionSite> Completed;

        public void Setup(BuildingData buildingData, GameObject[] stagePrefabs)
        {
            data = buildingData;
            buildTime = Mathf.Max(0.5f, buildingData.buildTime);

            // Ẩn model công trình hoàn chỉnh trong lúc đang xây.
            buildingRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in buildingRenderers) r.enabled = false;

            // Tạo khung giàn giáo 3 giai đoạn co giãn theo kích thước thực của công trình.
            // Khung là con của công trình đã xoay, nên dùng footprint gốc (chưa xoay) theo trục local;
            // dùng footprint đã xoay sẽ bị xoay thêm lần nữa và khung trở lại hướng cũ.
            stageRoot = new GameObject("ConstructionStages");
            stageRoot.transform.SetParent(transform, false);
            stageRoot.transform.localPosition = Vector3.zero;
            stageRoot.transform.localRotation = Quaternion.identity;

            float cellSize = BuildGrid.Instance != null ? BuildGrid.Instance.CellSize : 1f;
            stageRoot.transform.localScale = new Vector3(
                buildingData.footprint.x * cellSize,
                Mathf.Max(1.5f, buildingData.height),
                buildingData.footprint.y * cellSize);

            if (stagePrefabs != null)
            {
                for (int i = 0; i < 3 && i < stagePrefabs.Length; i++)
                {
                    if (stagePrefabs[i] == null) continue;
                    var inst = Instantiate(stagePrefabs[i], stageRoot.transform);
                    inst.name = "Stage_" + (i + 1);
                    inst.transform.localPosition = Vector3.zero;
                    inst.transform.localRotation = Quaternion.identity;
                    inst.transform.localScale = Vector3.one;
                    inst.SetActive(false);
                    stageInstances[i] = inst;
                }
            }

            UpdateStageVisual();
            if (!activeSites.Contains(this)) activeSites.Add(this);
        }

        // Thợ xây gọi mỗi frame khi đứng gõ búa tại công trường.
        public void AddWork(float workSeconds)
        {
            if (IsCompleted) return;
            elapsed += workSeconds;
            UpdateStageVisual();

            if (elapsed >= buildTime) Complete();
            else ProgressChanged?.Invoke(Progress, StageIndex);
        }

        // Trả về điểm đứng làm việc trên mặt đất phẳng quanh rìa công trình cho thợ xây theo chỉ số vị trí.
        public Vector3 GetWorkSlotPosition(int slotIndex)
        {
            var occupant = GetComponent<GridOccupant>();
            var grid = BuildGrid.Instance;
            float cellSize = grid != null ? grid.CellSize : 1f;
            Vector2 half = occupant != null
                ? (Vector2)occupant.BaseFootprint * (cellSize * 0.5f)
                : new Vector2(1f, 1f);

            float baseAngle = (slotIndex * 1.37f) % (Mathf.PI * 2f);
            Vector3 bestWorld = transform.position;
            float bestDiff = float.MaxValue;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = baseAngle + attempt * (Mathf.PI * 0.25f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var edge = new Vector2(
                    Mathf.Clamp(dir.x * (half.x + 2f), -half.x, half.x),
                    Mathf.Clamp(dir.y * (half.y + 2f), -half.y, half.y));
                var outward = edge.normalized;
                if (outward.sqrMagnitude < 0.01f) outward = Vector2.up;
                var local = edge + outward * 0.55f;
                var candidate = transform.TransformPoint(new Vector3(local.x, 0f, local.y));

                if (grid == null)
                {
                    return new Vector3(candidate.x, transform.position.y, candidate.z);
                }

                float groundY = grid.SampleWorldHeight(candidate);
                candidate.y = groundY;
                var cell = grid.MinCell(candidate, Vector2Int.one);
                bool walkable = grid.InBounds(cell)
                    && grid.Terrain[grid.Index(cell)] == CellState.Free
                    && grid.GetOccupancy(cell) == CellOccupancy.None;
                float diff = Mathf.Abs(groundY - transform.position.y) + (walkable ? 0f : 10f);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestWorld = candidate;
                    if (walkable && diff <= 0.35f) break;
                }
            }

            return bestWorld;
        }

        void UpdateStageVisual()
        {
            float p = Progress;
            int nextStage = p < 0.34f ? 0 : p < 0.67f ? 1 : 2;
            if (nextStage == currentStage) return;

            currentStage = nextStage;
            for (int i = 0; i < stageInstances.Length; i++)
            {
                if (stageInstances[i] != null) stageInstances[i].SetActive(i == currentStage);
            }
        }

        void Complete()
        {
            if (IsCompleted) return;
            IsCompleted = true;
            activeSites.Remove(this);

            if (stageRoot != null) Destroy(stageRoot);
            if (buildingRenderers != null)
            {
                foreach (var r in buildingRenderers)
                {
                    if (r != null) r.enabled = true;
                }
            }

            // Kích hoạt chức năng của công trình sau khi xây xong 100%.
            if (data != null)
            {
                if (data.tower != null) gameObject.AddComponent<Tower>().Setup(data.tower);
                if (data.populationProvided > 0) gameObject.AddComponent<HouseBuilding>().Setup(data.populationProvided);
                if (data.soldier != null)
                {
                    gameObject.AddComponent<Barracks>().Setup(data);
                }
                else if (data.harvestResource != ResourceType.None)
                {
                    gameObject.AddComponent<ResourceCamp>().Setup(data);
                }
                else if (data.maxWorkers > 0)
                {
                    gameObject.AddComponent<BuilderHut>().Setup(data.maxWorkers);
                }
            }

            AudioDirector.Post(SoundId.UiPlace, transform.position);
            ProgressChanged?.Invoke(1f, 3);
            Completed?.Invoke(this);
        }

        // Dừng thi công để chuyển sang quá trình tháo dỡ công trình.
        public void CancelForDemolish()
        {
            if (IsCompleted) return;
            IsCompleted = true;
            activeSites.Remove(this);

            if (stageRoot != null) Destroy(stageRoot);
            if (buildingRenderers != null)
            {
                foreach (var r in buildingRenderers)
                {
                    if (r != null) r.enabled = true;
                }
            }
        }

        void OnDestroy()
        {
            activeSites.Remove(this);
        }
    }
}
