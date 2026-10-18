using System;

namespace SurvivalRts
{
    // Giao diện chung cho các công trình cho phép người chơi cộng/trừ công nhân từ dân tự do.
    public interface IWorkerBuilding
    {
        string WorkerRoleName { get; }
        int WorkerCount { get; }
        int MaxWorkers { get; }
        bool CanAddWorker { get; }
        bool CanRemoveWorker { get; }
        bool TryAddWorker();
        bool TryRemoveWorker();
        event Action WorkersChanged;
    }
}
