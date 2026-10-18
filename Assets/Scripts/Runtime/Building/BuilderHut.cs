using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalRts
{
    // Gắn lên Nhà của Builder sau khi xây xong: cho phép người chơi cộng/trừ worker (Builder) cho nhà này.
    public class BuilderHut : MonoBehaviour, IWorkerBuilding
    {
        readonly List<BuilderUnit> builders = new List<BuilderUnit>();
        int maxWorkers = 3;

        public string WorkerRoleName => "Thợ xây";
        public int WorkerCount => builders.Count;
        public int MaxWorkers => maxWorkers;

        public event Action WorkersChanged;

        public void Setup(int maxCount)
        {
            maxWorkers = Mathf.Max(1, maxCount);
            // Tự động gán 1 thợ ngay khi xây xong nếu đang còn dân rỗi.
            TryAddWorker();
        }

        public bool CanAddWorker =>
            builders.Count < maxWorkers
            && PopulationManager.Instance != null
            && PopulationManager.Instance.FreePopulation > 0;

        public bool CanRemoveWorker => builders.Count > 0;

        public bool TryAddWorker()
        {
            if (!CanAddWorker) return false;
            if (!PopulationManager.Instance.TryAssignBuilder(transform, out var builder)) return false;

            builders.Add(builder);
            WorkersChanged?.Invoke();
            return true;
        }

        public bool TryRemoveWorker()
        {
            if (builders.Count == 0) return false;
            int last = builders.Count - 1;
            var builder = builders[last];
            builders.RemoveAt(last);

            if (PopulationManager.Instance != null) PopulationManager.Instance.UnassignBuilder(builder);
            WorkersChanged?.Invoke();
            return true;
        }

        void OnDestroy()
        {
            if (PopulationManager.Instance != null)
            {
                for (int i = builders.Count - 1; i >= 0; i--)
                {
                    PopulationManager.Instance.UnassignBuilder(builders[i]);
                }
            }
            builders.Clear();
        }
    }
}
