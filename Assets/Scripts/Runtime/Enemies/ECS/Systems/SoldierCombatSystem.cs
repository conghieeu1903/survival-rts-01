using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SurvivalRts
{
    // Cầu nối giữa lính (GameObject) và kẻ địch (ECS):
    // mỗi frame tìm kẻ địch còn sống gần nhất quanh vị trí canh gác của từng lính (job Burst),
    // và trừ máu kẻ địch theo các nhát chém lính gửi lên.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemySimulationSystem))]
    public partial class SoldierCombatSystem : SystemBase
    {
        struct PendingDamage
        {
            public Entity Target;
            public float Amount;
        }

        static readonly List<SoldierUnit> registered = new List<SoldierUnit>();
        static readonly List<PendingDamage> pendingDamage = new List<PendingDamage>();

        readonly List<SoldierUnit> frameSoldiers = new List<SoldierUnit>();
        EntityQuery enemyQuery;
        NativeArray<float3> enemyPositions;
        NativeArray<Entity> enemyEntities;
        NativeArray<byte> enemyAlive;
        NativeArray<float4> soldierInputs;
        NativeArray<int> soldierResults;
        JobHandle handle;
        int resultCount;

        public static void Register(SoldierUnit soldier)
        {
            if (!registered.Contains(soldier)) registered.Add(soldier);
        }

        public static void Unregister(SoldierUnit soldier)
        {
            registered.Remove(soldier);
        }

        public static void QueueDamage(Entity target, float amount)
        {
            if (target == Entity.Null || amount <= 0f) return;
            pendingDamage.Add(new PendingDamage { Target = target, Amount = amount });
        }

        protected override void OnCreate()
        {
            enemyQuery = GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<EnemyHealth>(),
                ComponentType.ReadOnly<EnemyTag>());
        }

        protected override void OnDestroy()
        {
            handle.Complete();
            Dispose(ref enemyPositions);
            Dispose(ref enemyEntities);
            Dispose(ref enemyAlive);
            Dispose(ref soldierInputs);
            Dispose(ref soldierResults);
        }

        protected override void OnUpdate()
        {
            handle.Complete();
            DeliverTargets();
            ApplyDamage();

            frameSoldiers.Clear();
            foreach (var soldier in registered)
                if (soldier != null && soldier.isActiveAndEnabled) frameSoldiers.Add(soldier);

            int soldierCount = frameSoldiers.Count;
            int enemyCount = soldierCount > 0 ? enemyQuery.CalculateEntityCount() : 0;
            resultCount = 0;
            if (soldierCount == 0) return;
            if (enemyCount == 0)
            {
                foreach (var soldier in frameSoldiers) soldier.SetTarget(Entity.Null, Vector3.zero);
                return;
            }

            EnsureCapacity(ref enemyPositions, enemyCount);
            EnsureCapacity(ref enemyEntities, enemyCount);
            EnsureCapacity(ref enemyAlive, enemyCount);
            EnsureCapacity(ref soldierInputs, soldierCount);
            EnsureCapacity(ref soldierResults, soldierCount);

            for (int i = 0; i < soldierCount; i++)
            {
                var guard = frameSoldiers[i].GuardPosition;
                soldierInputs[i] = new float4(guard.x, guard.z, frameSoldiers[i].DetectRange, 0f);
            }

            var snapshot = new SnapshotJob
            {
                Positions = enemyPositions,
                Entities = enemyEntities,
                Alive = enemyAlive
            }.ScheduleParallel(enemyQuery, Dependency);

            handle = new FindNearestJob
            {
                EnemyCount = enemyCount,
                Positions = enemyPositions,
                Alive = enemyAlive,
                Soldiers = soldierInputs,
                Results = soldierResults
            }.Schedule(soldierCount, 4, snapshot);

            Dependency = handle;
            resultCount = soldierCount;
        }

        // Kết quả của frame trước; lệch một frame không đáng kể với tốc độ kẻ địch.
        void DeliverTargets()
        {
            for (int i = 0; i < resultCount && i < frameSoldiers.Count; i++)
            {
                var soldier = frameSoldiers[i];
                if (soldier == null) continue;
                int index = soldierResults[i];
                if (index < 0) soldier.SetTarget(Entity.Null, Vector3.zero);
                else soldier.SetTarget(enemyEntities[index], enemyPositions[index]);
            }
        }

        void ApplyDamage()
        {
            if (pendingDamage.Count == 0) return;
            var manager = EntityManager;
            foreach (var hit in pendingDamage)
            {
                if (!manager.Exists(hit.Target) || !manager.HasComponent<EnemyHealth>(hit.Target)) continue;
                var health = manager.GetComponentData<EnemyHealth>(hit.Target);
                if (health.Current <= 0f) continue;
                health.Current = math.max(0f, health.Current - hit.Amount);
                manager.SetComponentData(hit.Target, health);
            }
            pendingDamage.Clear();
        }

        static void EnsureCapacity<T>(ref NativeArray<T> array, int count) where T : unmanaged
        {
            if (array.IsCreated && array.Length >= count) return;
            int capacity = math.max(count, array.IsCreated ? array.Length * 2 : 64);
            Dispose(ref array);
            array = new NativeArray<T>(capacity, Allocator.Persistent);
        }

        static void Dispose<T>(ref NativeArray<T> array) where T : unmanaged
        {
            if (array.IsCreated) array.Dispose();
        }

        [BurstCompile]
        partial struct SnapshotJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public NativeArray<float3> Positions;
            [NativeDisableParallelForRestriction] public NativeArray<Entity> Entities;
            [NativeDisableParallelForRestriction] public NativeArray<byte> Alive;

            void Execute(Entity entity, [EntityIndexInQuery] int index, in LocalTransform transform, in EnemyHealth health)
            {
                Positions[index] = transform.Position;
                Entities[index] = entity;
                Alive[index] = (byte)(health.Current > 0f ? 1 : 0);
            }
        }

        // Mỗi lính quét mọi kẻ địch: số lính ít nên rẻ (vài chục lính × 10.000 kẻ địch vẫn dưới 1 ms với Burst).
        [BurstCompile]
        struct FindNearestJob : IJobParallelFor
        {
            public int EnemyCount;
            [ReadOnly] public NativeArray<float3> Positions;
            [ReadOnly] public NativeArray<byte> Alive;
            [ReadOnly] public NativeArray<float4> Soldiers;
            [WriteOnly] public NativeArray<int> Results;

            public void Execute(int i)
            {
                var soldier = Soldiers[i];
                float bestSq = soldier.z * soldier.z;
                int best = -1;
                for (int e = 0; e < EnemyCount; e++)
                {
                    if (Alive[e] == 0) continue;
                    float dx = Positions[e].x - soldier.x;
                    float dz = Positions[e].z - soldier.y;
                    float dSq = dx * dx + dz * dz;
                    if (dSq >= bestSq) continue;
                    bestSq = dSq;
                    best = e;
                }
                Results[i] = best;
            }
        }
    }
}
