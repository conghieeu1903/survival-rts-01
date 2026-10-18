using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalRts
{
    // Điều khiển quá trình tháo dỡ công trình: hoàn 100% tài nguyên, chuyển ngược khung giàn giáo (3 -> 2 -> 1),
    // rung lắc + hạ dần chiều cao công trình, phát âm thanh gõ tháo dỡ và kết thúc bằng tiếng sập.
    public class DemolishSite : MonoBehaviour
    {
        const float DefaultDemolishTime = 2.5f;
        const float HammerInterval = 0.5f;

        readonly List<Transform> partTransforms = new List<Transform>();
        readonly List<Vector3> partBasePos = new List<Vector3>();
        readonly List<Vector3> partBaseScale = new List<Vector3>();
        readonly GameObject[] stageInstances = new GameObject[3];

        BuildingData data;
        GameObject stageRoot;
        float duration = DefaultDemolishTime;
        float elapsed;
        float buildingHeight = 2f;
        float hammerTimer;
        int currentStage = -1;
        bool initialized;
        bool completed;

        public BuildingData Data => data;
        public float Duration => duration;
        public float Elapsed => elapsed;
        public float Remaining => Mathf.Max(0f, duration - elapsed);
        public float Progress => duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
        public int StageNumber => currentStage >= 0 ? currentStage + 1 : 3;

        public event Action<float, float, int> ProgressChanged;
        public event Action<DemolishSite> Completed;

        // Kiểm tra một đối tượng có phải là công trình phe mình được phép tháo dỡ hay không (không cho phá Nhà chính).
        public static bool CanDemolish(Health target)
        {
            if (target == null || target.IsDead) return false;
            if (target.GetComponent<Headquarters>() != null) return false;
            if (target.GetComponent<DemolishSite>() != null) return false;
            var occ = target.GetComponent<GridOccupant>();
            return occ != null && occ.isBuilding;
        }

        public void Setup(BuildingData buildingData, GameObject[] stagePrefabs)
        {
            if (initialized) return;
            initialized = true;

            var occ = GetComponent<GridOccupant>();
            data = buildingData != null ? buildingData : (occ != null ? occ.buildingData : null);
            duration = data != null && data.demolishTime > 0f ? data.demolishTime : DefaultDemolishTime;
            buildingHeight = data != null && data.height > 0f ? data.height : 2f;

            // Hoàn lại 100% chi phí xây dựng ngay khi xác nhận tháo dỡ.
            if (PopulationManager.Instance != null && data != null)
            {
                PopulationManager.Instance.RefundResources(data);
            }

            // Dừng các thành phần hoạt động của công trình trước khi bắt đầu diễn hoạt tháo dỡ.
            var construction = GetComponent<ConstructionSite>();
            if (construction != null)
            {
                construction.CancelForDemolish();
                Destroy(construction);
            }

            var tower = GetComponent<Tower>();
            if (tower != null) Destroy(tower);

            var camp = GetComponent<ResourceCamp>();
            if (camp != null) Destroy(camp);

            var hut = GetComponent<BuilderHut>();
            if (hut != null) Destroy(hut);

            var house = GetComponent<HouseBuilding>();
            if (house != null) Destroy(house);

            // Lưu vị trí và tỉ lệ gốc của các phần thân công trình để diễn hoạt rung và lún dần xuống đất.
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null || child.name == "ConstructionStages" || child.name == "DemolishStages") continue;
                partTransforms.Add(child);
                partBasePos.Add(child.localPosition);
                partBaseScale.Add(child.localScale);
            }

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r != null) r.enabled = true;
            }

            // Dựng khung giàn giáo 3 giai đoạn để chuyển ngược từ Stage 3 -> Stage 2 -> Stage 1.
            stageRoot = new GameObject("DemolishStages");
            stageRoot.transform.SetParent(transform, false);
            stageRoot.transform.localPosition = Vector3.zero;
            stageRoot.transform.localRotation = Quaternion.identity;

            float cellSize = BuildGrid.Instance != null ? BuildGrid.Instance.CellSize : 1f;
            Vector2Int fp = data != null ? data.footprint : (occ != null ? occ.footprint : new Vector2Int(2, 2));
            stageRoot.transform.localScale = new Vector3(
                fp.x * cellSize,
                Mathf.Max(1.5f, buildingHeight),
                fp.y * cellSize);

            if (stagePrefabs != null)
            {
                for (int i = 0; i < 3 && i < stagePrefabs.Length; i++)
                {
                    if (stagePrefabs[i] == null) continue;
                    var inst = Instantiate(stagePrefabs[i], stageRoot.transform);
                    inst.name = "DemolishStage_" + (i + 1);
                    inst.transform.localPosition = Vector3.zero;
                    inst.transform.localRotation = Quaternion.identity;
                    inst.transform.localScale = Vector3.one;
                    inst.SetActive(false);
                    stageInstances[i] = inst;
                }
            }

            // Phát âm thanh bắt đầu tháo dỡ.
            AudioDirector.Post(SoundId.UiPlace, transform.position);
            AudioDirector.Post(SoundId.BuilderHammer, transform.position);
            hammerTimer = HammerInterval;

            UpdateVisuals();
            ProgressChanged?.Invoke(Progress, Remaining, StageNumber);
        }

        void Update()
        {
            if (!initialized || completed) return;

            elapsed += Time.deltaTime;
            UpdateVisuals();

            hammerTimer -= Time.deltaTime;
            if (hammerTimer <= 0f && elapsed < duration - 0.15f)
            {
                hammerTimer = HammerInterval;
                AudioDirector.Post(SoundId.BuilderHammer, transform.position);
            }

            if (elapsed >= duration)
            {
                FinishDemolish();
            }
            else
            {
                ProgressChanged?.Invoke(Progress, Remaining, StageNumber);
            }
        }

        void UpdateVisuals()
        {
            float p = Progress;

            // Chuyển ngược khung giàn giáo: 0..33% hiện Khung 3, 33..67% hiện Khung 2, 67..100% hiện Khung 1.
            int nextStage = p < 0.34f ? 2 : p < 0.67f ? 1 : 0;
            if (nextStage != currentStage)
            {
                currentStage = nextStage;
                for (int i = 0; i < stageInstances.Length; i++)
                {
                    if (stageInstances[i] != null) stageInstances[i].SetActive(i == currentStage);
                }
            }

            // Rung nhẹ theo phương ngang và hạ dần chiều cao công trình xuống mặt đất.
            float shakeAmp = 0.055f * (0.4f + 0.6f * p);
            float shakeX = Mathf.Sin(elapsed * 46f) * shakeAmp;
            float shakeZ = Mathf.Cos(elapsed * 38f) * shakeAmp;
            float scaleYFactor = Mathf.Lerp(1f, 0.18f, p);
            float sinkY = p * buildingHeight * 0.55f;

            for (int i = 0; i < partTransforms.Count; i++)
            {
                var part = partTransforms[i];
                if (part == null) continue;
                var basePos = partBasePos[i];
                var baseScale = partBaseScale[i];

                part.localPosition = new Vector3(
                    basePos.x + shakeX,
                    basePos.y * scaleYFactor - sinkY * 0.35f,
                    basePos.z + shakeZ);
                part.localScale = new Vector3(
                    baseScale.x * Mathf.Lerp(1f, 0.92f, p),
                    baseScale.y * scaleYFactor,
                    baseScale.z * Mathf.Lerp(1f, 0.92f, p));
            }

            if (stageRoot != null)
            {
                stageRoot.transform.localPosition = new Vector3(shakeX * 0.5f, -p * 0.2f, shakeZ * 0.5f);
            }
        }

        void FinishDemolish()
        {
            if (completed) return;
            completed = true;

            AudioDirector.Post(SoundId.BuildingCollapse, transform.position);
            ProgressChanged?.Invoke(1f, 0f, 1);
            Completed?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
