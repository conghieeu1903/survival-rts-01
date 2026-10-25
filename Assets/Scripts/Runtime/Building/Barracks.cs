using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalRts
{
    // Gắn lên trại lính sau khi xây xong: nút + đào tạo một dân tự do thành lính (mất thời gian và tài nguyên),
    // nút − huỷ lượt đào tạo cuối (hoàn lại) hoặc cho một lính giải ngũ về làm dân.
    public class Barracks : MonoBehaviour, IWorkerBuilding
    {
        const float UiRefreshInterval = 0.25f;
        const float DoorOffset = 0.8f;
        const float RallyDistance = 2.4f;
        const float SlotSpacing = 1.1f;
        const int SlotColumns = 3;

        readonly List<SoldierUnit> soldiers = new List<SoldierUnit>();
        BuildingData data;
        SoldierData soldier;
        int maxSoldiers = 1;
        int queued;
        float trainTimer;
        float uiTimer;

        public SoldierData Soldier => soldier;
        public int QueuedCount => queued;
        public float TrainingProgress => queued > 0 && soldier.trainTime > 0f ? 1f - trainTimer / soldier.trainTime : 0f;

        public string WorkerRoleName
        {
            get
            {
                if (soldier == null) return "Lính";
                if (queued > 0) return $"{soldier.displayName} (đang luyện {queued}: {Mathf.FloorToInt(TrainingProgress * 100f)}%)";
                string missing = MissingCost();
                return missing.Length > 0 ? $"{soldier.displayName} (thiếu {missing})" : soldier.displayName;
            }
        }

        // Chuỗi tài nguyên còn thiếu để đào tạo thêm một lính, rỗng nếu đủ.
        string MissingCost()
        {
            var population = PopulationManager.Instance;
            if (population == null) return string.Empty;
            var parts = new List<string>(3);
            if (population.Food < soldier.foodCost) parts.Add($"{soldier.foodCost - population.Food} lúa");
            if (population.Iron < soldier.ironCost) parts.Add($"{soldier.ironCost - population.Iron} sắt");
            if (population.Gold < soldier.goldCost) parts.Add($"{soldier.goldCost - population.Gold} vàng");
            return string.Join(", ", parts);
        }

        public int WorkerCount => soldiers.Count + queued;
        public int MaxWorkers => maxSoldiers;

        public bool CanAddWorker =>
            soldier != null
            && WorkerCount < maxSoldiers
            && PopulationManager.Instance != null
            && PopulationManager.Instance.FreePopulation > 0
            && PopulationManager.Instance.CanAfford(0, 0, soldier.ironCost, soldier.foodCost, soldier.goldCost);

        public bool CanRemoveWorker => WorkerCount > 0;

        public event Action WorkersChanged;

        public void Setup(BuildingData buildingData)
        {
            data = buildingData;
            soldier = buildingData.soldier;
            maxSoldiers = Mathf.Max(1, buildingData.maxWorkers);
        }

        public bool TryAddWorker()
        {
            if (!CanAddWorker) return false;
            var population = PopulationManager.Instance;
            if (!population.TrySpend(0, 0, soldier.ironCost, soldier.foodCost, soldier.goldCost)) return false;
            if (!population.TryReserveWorker())
            {
                population.Refund(0, 0, soldier.ironCost, soldier.foodCost, soldier.goldCost);
                return false;
            }

            if (queued == 0) trainTimer = soldier.trainTime;
            queued++;
            WorkersChanged?.Invoke();
            return true;
        }

        public bool TryRemoveWorker()
        {
            var population = PopulationManager.Instance;
            if (queued > 0)
            {
                // Huỷ lượt đang chờ cuối hàng: trả dân và hoàn tài nguyên.
                queued--;
                if (queued == 0) trainTimer = 0f;
                if (population != null)
                {
                    population.Refund(0, 0, soldier.ironCost, soldier.foodCost, soldier.goldCost);
                    population.ReleaseWorker(false);
                }
                WorkersChanged?.Invoke();
                return true;
            }

            if (soldiers.Count == 0) return false;
            int last = soldiers.Count - 1;
            var unit = soldiers[last];
            soldiers.RemoveAt(last);
            if (unit != null) unit.Dismiss();
            if (population != null) population.ReleaseWorker(false);
            UpdateGuardSlots();
            WorkersChanged?.Invoke();
            return true;
        }

        void Update()
        {
            if (queued == 0 || soldier == null) return;

            trainTimer -= Time.deltaTime;
            if (trainTimer <= 0f)
            {
                SpawnSoldier();
                queued--;
                trainTimer = queued > 0 ? soldier.trainTime : 0f;
                WorkersChanged?.Invoke();
                return;
            }

            // Bảng thông tin chỉ vẽ lại khi có sự kiện, nên phát định kỳ để thanh tiến độ đào tạo chạy.
            uiTimer -= Time.unscaledDeltaTime;
            if (uiTimer <= 0f)
            {
                uiTimer = UiRefreshInterval;
                WorkersChanged?.Invoke();
            }
        }

        void SpawnSoldier()
        {
            if (soldier.prefab == null)
            {
                Debug.LogWarning($"Trại lính: {soldier.name} chưa có prefab lính.");
                PopulationManager.Instance?.ReleaseWorker(false);
                return;
            }

            var parent = PopulationManager.Instance != null ? PopulationManager.Instance.UnitRoot : null;
            var go = Instantiate(soldier.prefab, DoorPosition(), transform.rotation, parent);
            go.name = soldier.displayName;
            var unit = go.GetComponent<SoldierUnit>();
            if (unit == null) unit = go.AddComponent<SoldierUnit>();
            unit.Setup(this, soldier, GuardSlot(soldiers.Count));
            soldiers.Add(unit);
            AudioDirector.Post(SoundId.UiPlace, transform.position);
        }

        // Lính chết (bị kẻ địch đánh): mất một dân.
        public void OnSoldierDied(SoldierUnit unit)
        {
            if (!soldiers.Remove(unit)) return;
            PopulationManager.Instance?.ReleaseWorker(true);
            UpdateGuardSlots();
            WorkersChanged?.Invoke();
        }

        void UpdateGuardSlots()
        {
            for (int i = 0; i < soldiers.Count; i++)
                if (soldiers[i] != null) soldiers[i].SetBarracksSlot(GuardSlot(i));
        }

        float HalfDepth => data != null ? data.footprint.y * 0.5f : 1.5f;

        Vector3 DoorPosition()
        {
            return transform.position + transform.forward * (HalfDepth + DoorOffset);
        }

        // Vị trí canh gác xếp thành hàng trước cửa trại, mỗi hàng SlotColumns lính.
        Vector3 GuardSlot(int index)
        {
            int row = index / SlotColumns;
            int column = index % SlotColumns;
            float x = (column - (SlotColumns - 1) * 0.5f) * SlotSpacing;
            float z = HalfDepth + RallyDistance + row * SlotSpacing;
            return transform.position + transform.right * x + transform.forward * z;
        }

        void OnDestroy()
        {
            // Trại bị phá hoặc tháo dỡ: huỷ đào tạo (hoàn lại) và cho toàn bộ lính về làm dân.
            var population = PopulationManager.Instance;
            if (population != null && soldier != null)
            {
                for (int i = 0; i < queued; i++)
                {
                    population.Refund(0, 0, soldier.ironCost, soldier.foodCost, soldier.goldCost);
                    population.ReleaseWorker(false);
                }
            }
            queued = 0;

            for (int i = soldiers.Count - 1; i >= 0; i--)
            {
                if (soldiers[i] != null) soldiers[i].Dismiss();
                if (population != null) population.ReleaseWorker(false);
            }
            soldiers.Clear();
        }
    }
}
