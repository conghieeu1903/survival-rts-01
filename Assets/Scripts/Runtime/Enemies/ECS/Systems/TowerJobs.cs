using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace SurvivalRts
{
    // Mỗi tháp hết hồi chiêu thì tìm kẻ địch gần nhất trong tầm qua spatial hash và xếp một phát bắn vào hàng đợi.
    [BurstCompile]
    public partial struct TowerFireJob : IJobEntity
    {
        public GridInfo Grid;
        public float DeltaTime;

        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<byte> Alive;
        [ReadOnly] public NativeArray<int> CellStart;
        [ReadOnly] public NativeArray<int> CellCount;
        [ReadOnly] public NativeArray<int> Sorted;

        public NativeQueue<TowerShot>.ParallelWriter Shots;

        void Execute(ref TowerState state, in TowerStats stats)
        {
            state.Cooldown = math.max(0f, state.Cooldown - DeltaTime);
            if (state.Cooldown > 0f) return;

            int target = FindNearest(stats.Muzzle.xz, stats.Range);
            if (target < 0) return;

            Shots.Enqueue(new TowerShot
            {
                EnemyIndex = target,
                Damage = stats.Damage,
                From = stats.Muzzle,
                To = Positions[target]
            });
            state.Cooldown = stats.FireInterval;
        }

        int FindNearest(float2 position, float range)
        {
            int2 min = math.max(Grid.CellCoord(position - range), int2.zero);
            int2 max = math.min(Grid.CellCoord(position + range), Grid.Size - 1);
            int best = -1;
            float bestSq = range * range;

            for (int y = min.y; y <= max.y; y++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    int c = Grid.Index(new int2(x, y));
                    int end = CellStart[c] + CellCount[c];
                    for (int k = CellStart[c]; k < end; k++)
                    {
                        int enemy = Sorted[k];
                        if (Alive[enemy] == 0) continue;
                        float distSq = math.distancesq(position, Positions[enemy].xz);
                        if (distSq > bestSq) continue;
                        bestSq = distSq;
                        best = enemy;
                    }
                }
            }
            return best;
        }
    }

    // Trừ máu kẻ địch theo các phát bắn trong frame. Chạy một luồng nên nhiều tháp bắn cùng một con không bị tranh ghi.
    [BurstCompile]
    public struct TowerApplyShotsJob : IJob
    {
        public NativeQueue<TowerShot> Shots;
        [ReadOnly] public NativeArray<Entity> Entities;
        public ComponentLookup<EnemyHealth> Health;
        public NativeList<TowerShot> Fired;

        public void Execute()
        {
            while (Shots.TryDequeue(out var shot))
            {
                var entity = Entities[shot.EnemyIndex];
                var health = Health[entity];
                if (health.Current <= 0f) continue;
                health.Current = math.max(0f, health.Current - shot.Damage);
                Health[entity] = health;
                Fired.Add(shot);
            }
        }
    }
}
