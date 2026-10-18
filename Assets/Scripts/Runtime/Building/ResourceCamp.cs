using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalRts
{
    // Gắn lên Trại gỗ (Lumber Camp) hoặc Xưởng đá (Stone Quarry) sau khi xây xong:
    // quản lý công nhân khai thác và tìm cây trong bán kính hoặc vách đá sát cạnh.
    public class ResourceCamp : MonoBehaviour, IWorkerBuilding
    {
        readonly List<HarvesterUnit> workers = new List<HarvesterUnit>();
        readonly List<ResourceNode> nodesInRange = new List<ResourceNode>();

        ResourceType resourceType;
        float harvestRadius = 8f;
        bool adjacentMode;
        float harvestInterval = 2.5f;
        int harvestAmount = 5;
        int maxWorkers = 3;

        public ResourceType ResourceType => resourceType;
        public float HarvestRadius => harvestRadius;
        public float HarvestInterval => harvestInterval;
        public int HarvestAmount => harvestAmount;

        public string WorkerRoleName
        {
            get
            {
                if (resourceType == ResourceType.Stone) return "Thợ mỏ đá";
                if (resourceType == ResourceType.Iron) return "Thợ mỏ sắt";
                if (resourceType == ResourceType.Food) return "Nông dân";
                return "Tiều phu";
            }
        }
        public int WorkerCount => workers.Count;
        public int MaxWorkers => maxWorkers;

        public event Action WorkersChanged;

        public void Setup(BuildingData data)
        {
            resourceType = data.harvestResource;
            harvestRadius = data.harvestRadius;
            adjacentMode = data.requireAdjacentResource;
            harvestInterval = Mathf.Max(0.5f, data.harvestInterval);
            harvestAmount = Mathf.Max(1, data.harvestAmount);
            maxWorkers = Mathf.Max(1, data.maxWorkers);

            ResourceNode.EnsureSceneNodesRegistered();
            RefreshNodes();
            // Tự động gán 1 công nhân ngay khi xây xong nếu còn dân rỗi.
            TryAddWorker();
        }

        public bool CanAddWorker =>
            workers.Count < maxWorkers
            && PopulationManager.Instance != null
            && PopulationManager.Instance.FreePopulation > 0;

        public bool CanRemoveWorker => workers.Count > 0;

        public bool TryAddWorker()
        {
            if (!CanAddWorker) return false;
            if (!PopulationManager.Instance.TryAssignHarvester(this, workers.Count, out var unit)) return false;

            workers.Add(unit);
            WorkersChanged?.Invoke();
            return true;
        }

        public bool TryRemoveWorker()
        {
            if (workers.Count == 0) return false;
            int last = workers.Count - 1;
            var unit = workers[last];
            workers.RemoveAt(last);

            if (PopulationManager.Instance != null) PopulationManager.Instance.UnassignHarvester(unit);
            WorkersChanged?.Invoke();
            return true;
        }

        public Vector3 GetDropOffPosition(int slotIndex)
        {
            float lateral = (slotIndex - 1) * 0.55f;
            if (resourceType == ResourceType.Food)
            {
                return transform.position + transform.rotation * new Vector3(lateral * 0.6f, 0f, 0.92f);
            }
            float forwardOffset = adjacentMode ? -0.9f : 1.15f;
            return transform.position + transform.rotation * new Vector3(lateral, 0f, forwardOffset);
        }

        public bool TryGetHarvestPoint(int slotIndex, out Vector3 workPos, out Vector3 lookTarget)
        {
            RefreshNodes();

            // Trang trại (Food): nông dân đi qua cổng vào giữa các luống rau và lúa bên trong ô ruộng 2x2 để canh tác.
            if (resourceType == ResourceType.Food && nodesInRange.Count == 0)
            {
                float lateral = ((slotIndex % 3) - 1) * 0.48f;
                float depth = (slotIndex % 2 == 0) ? -0.32f : 0.18f;
                workPos = transform.position + transform.rotation * new Vector3(lateral, 0f, depth);
                lookTarget = workPos - transform.forward;
                return true;
            }

            if (nodesInRange.Count == 0)
            {
                workPos = transform.position;
                lookTarget = transform.position + transform.forward;
                return false;
            }

            if (adjacentMode)
            {
                // Xưởng đá: công nhân đứng ngay bề mặt tiếp giáp giữa mặt trước xưởng (+Z) và mỏ đá.
                var rock = nodesInRange[0];
                float lateral = (slotIndex - 1) * 0.55f;
                workPos = transform.position + transform.rotation * new Vector3(lateral, 0f, 1.05f);
                lookTarget = rock.transform.position;
                return true;
            }

            // Trại gỗ hoặc bụi quả: chia công nhân tới các điểm tài nguyên trong bán kính khai thác.
            var node = nodesInRange[Mathf.Abs(slotIndex) % nodesInRange.Count];
            var toCamp = transform.position - node.transform.position;
            toCamp.y = 0f;
            if (toCamp.sqrMagnitude < 0.01f) toCamp = Vector3.back;
            float angleOffset = (slotIndex % 3 - 1) * 28f;
            var standDir = Quaternion.Euler(0f, angleOffset, 0f) * toCamp.normalized;
            workPos = node.transform.position + standDir * 0.75f;
            lookTarget = node.transform.position;
            return true;
        }

        void RefreshNodes()
        {
            nodesInRange.Clear();
            var all = ResourceNode.All;
            float rSq = harvestRadius * harvestRadius;
            for (int i = 0; i < all.Count; i++)
            {
                var node = all[i];
                if (node == null || node.resourceType != resourceType) continue;

                var diff = node.transform.position - transform.position;
                diff.y = 0f;
                if (adjacentMode)
                {
                    if (diff.sqrMagnitude <= 16f) nodesInRange.Add(node);
                }
                else
                {
                    if (diff.sqrMagnitude <= rSq) nodesInRange.Add(node);
                }
            }
        }

        void OnDestroy()
        {
            if (PopulationManager.Instance != null)
            {
                for (int i = workers.Count - 1; i >= 0; i--)
                {
                    PopulationManager.Instance.UnassignHarvester(workers[i]);
                }
            }
            workers.Clear();
        }
    }
}
