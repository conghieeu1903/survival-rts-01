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

        // Trả về điểm đứng làm việc quanh rìa công trình cho thợ xây theo chỉ số vị trí.
        public Vector3 GetWorkSlotPosition(int slotIndex)
        {
            var occupant = GetComponent<GridOccupant>();
            float cellSize = BuildGrid.Instance != null ? BuildGrid.Instance.CellSize : 1f;
            Vector2 half = occupant != null
                ? (Vector2)occupant.footprint * (cellSize * 0.5f)
                : new Vector2(1f, 1f);

            float angle = (slotIndex * 1.37f) % (Mathf.PI * 2f);
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var center2D = new Vector2(transform.position.x, transform.position.z);
            var edge = new Vector2(
                Mathf.Clamp(center2D.x + dir.x * (half.x + 2f), center2D.x - half.x, center2D.x + half.x),
                Mathf.Clamp(center2D.y + dir.y * (half.y + 2f), center2D.y - half.y, center2D.y + half.y));
            var outward = (edge - center2D).normalized;
            if (outward.sqrMagnitude < 0.01f) outward = Vector2.up;
            var pos2D = edge + outward * 0.45f;
            return new Vector3(pos2D.x, transform.position.y, pos2D.y);
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
                if (data.harvestResource != ResourceType.None)
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
