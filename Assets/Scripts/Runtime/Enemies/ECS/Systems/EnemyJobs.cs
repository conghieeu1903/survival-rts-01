using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace SurvivalRts
{
    // Chụp vị trí và ô của mọi kẻ địch để các kẻ địch khác đọc khi né nhau.
    [BurstCompile]
    public partial struct EnemySnapshotJob : IJobEntity
    {
        public GridInfo Grid;
        [NativeDisableParallelForRestriction] public NativeArray<float3> Positions;
        [NativeDisableParallelForRestriction] public NativeArray<int> Cells;

        void Execute([EntityIndexInQuery] int index, in LocalTransform transform)
        {
            Positions[index] = transform.Position;
            Cells[index] = Grid.CellIndexClamped(transform.Position.xz);
        }
    }

    // Sắp xếp đếm theo ô: sau job này, các kẻ địch trong ô c nằm ở Sorted[CellStart[c] .. CellStart[c] + CellCount[c]).
    [BurstCompile]
    public struct EnemySortJob : IJob
    {
        public int Count;
        [ReadOnly] public NativeArray<int> Cells;
        public NativeArray<int> CellStart;
        public NativeArray<int> CellCount;
        public NativeArray<int> Sorted;

        public void Execute()
        {
            for (int i = 0; i < CellCount.Length; i++) CellCount[i] = 0;
            for (int i = 0; i < Count; i++) CellCount[Cells[i]]++;

            int running = 0;
            for (int i = 0; i < CellCount.Length; i++)
            {
                CellStart[i] = running;
                running += CellCount[i];
                CellCount[i] = 0;
            }

            for (int i = 0; i < Count; i++)
            {
                int cell = Cells[i];
                Sorted[CellStart[cell] + CellCount[cell]] = i;
                CellCount[cell]++;
            }
        }
    }

    [BurstCompile]
    public partial struct EnemySimulateJob : IJobEntity
    {
        const float RotationSharpness = 10f;

        public GridInfo Grid;
        public float DeltaTime;
        public float MaxStep;
        public int MaxNeighbors;
        public float SeparationStrength;

        [ReadOnly] public NativeArray<float2> Flow;
        [ReadOnly] public NativeArray<byte> Walk;
        [ReadOnly] public NativeArray<float> Height;
        [ReadOnly] public NativeArray<TargetInfo> Targets;
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<int> CellStart;
        [ReadOnly] public NativeArray<int> CellCount;
        [ReadOnly] public NativeArray<int> Sorted;

        public NativeQueue<DamageEvent>.ParallelWriter Damage;

        void Execute([EntityIndexInQuery] int index, ref LocalTransform transform, ref EnemyBrain brain,
            in EnemyStats stats, in EnemyHealth health)
        {
            if (health.Current <= 0f) return;

            float2 position = transform.Position.xz;
            float2 desired = float2.zero;
            float2 facing = float2.zero;
            brain.AttackTimer = math.max(0f, brain.AttackTimer - DeltaTime);
            brain.Engaged = 0;

            int target = FindTarget(position, stats.DetectRange, out float2 closest, out float distance);
            if (target >= 0)
            {
                facing = closest - position;
                if (distance <= stats.AttackRange)
                {
                    brain.Engaged = 1;
                    if (brain.AttackTimer <= 0f)
                    {
                        Damage.Enqueue(new DamageEvent { TargetId = Targets[target].Id, Amount = stats.Damage });
                        brain.AttackTimer = stats.AttackCooldown;
                    }
                }
                else
                {
                    desired = math.normalizesafe(facing) * stats.MoveSpeed;
                }
            }
            else
            {
                int cell = Grid.CellIndex(position);
                if (cell >= 0) desired = Flow[cell] * stats.MoveSpeed;
            }

            float2 push = Separation(index, position, stats.Radius * 2f);
            float2 delta = desired * DeltaTime + push * math.min(1f, SeparationStrength * DeltaTime);
            float2 next = Move(position, delta);

            int nextCell = Grid.CellIndex(next);
            float ground = nextCell >= 0 ? Height[nextCell] : transform.Position.y - stats.HeightOffset;
            transform.Position = new float3(next.x, ground + stats.HeightOffset, next.y);

            float2 look = math.lengthsq(desired) > 1e-4f ? desired : facing;
            if (math.lengthsq(look) > 1e-4f)
            {
                var rotation = quaternion.LookRotationSafe(new float3(look.x, 0f, look.y), math.up());
                transform.Rotation = math.slerp(transform.Rotation, rotation, math.saturate(RotationSharpness * DeltaTime));
            }
        }

        // Ưu tiên đơn vị, rồi công trình, rồi Nhà chính; cùng loại thì chọn gần nhất.
        int FindTarget(float2 position, float range, out float2 closest, out float distance)
        {
            int best = -1;
            float bestScore = float.MaxValue;
            closest = float2.zero;
            distance = 0f;

            for (int i = 0; i < Targets.Length; i++)
            {
                var t = Targets[i];
                float2 point = math.clamp(position, t.Center - t.HalfExtents, t.Center + t.HalfExtents);
                float d = math.distance(position, point);
                if (d > range) continue;

                float score = t.Priority * 10000f + d;
                if (score >= bestScore) continue;
                bestScore = score;
                best = i;
                closest = point;
                distance = d;
            }
            return best;
        }

        float2 Separation(int self, float2 position, float minDistance)
        {
            float2 push = float2.zero;
            int checkedCount = 0;
            float minDistanceSq = minDistance * minDistance;
            int2 center = Grid.CellCoord(position);

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var cell = center + new int2(dx, dz);
                    if (!Grid.InBounds(cell)) continue;

                    int c = Grid.Index(cell);
                    int end = CellStart[c] + CellCount[c];
                    for (int k = CellStart[c]; k < end; k++)
                    {
                        int other = Sorted[k];
                        if (other == self) continue;

                        float2 diff = position - Positions[other].xz;
                        float distSq = math.lengthsq(diff);
                        if (distSq < minDistanceSq)
                        {
                            if (distSq > 1e-6f)
                            {
                                float dist = math.sqrt(distSq);
                                push += diff / dist * (minDistance - dist);
                            }
                            else
                            {
                                // Trùng vị trí: đẩy theo hướng giả ngẫu nhiên cố định theo chỉ số.
                                push += new float2((self * 7919 % 13) - 6, (self * 104729 % 13) - 6) * 0.01f;
                            }
                        }

                        if (++checkedCount >= MaxNeighbors) return push;
                    }
                }
            }
            return push;
        }

        // Không bước vào ô không đi được; thử trượt theo từng trục để men theo vật cản.
        float2 Move(float2 from, float2 delta)
        {
            float2 to = from + delta;
            if (CanStand(from, to)) return to;

            var alongX = new float2(to.x, from.y);
            if (CanStand(from, alongX)) return alongX;

            var alongZ = new float2(from.x, to.y);
            if (CanStand(from, alongZ)) return alongZ;

            return from;
        }

        bool CanStand(float2 from, float2 to)
        {
            int a = Grid.CellIndex(from);
            int b = Grid.CellIndex(to);
            if (b < 0) return false;
            if (a == b) return true;
            if (Walk[b] == 0) return false;
            return a < 0 || math.abs(Height[b] - Height[a]) <= MaxStep;
        }
    }

    [BurstCompile]
    [WithAll(typeof(EnemyTag))]
    public partial struct EnemyDeathJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter Commands;

        void Execute(Entity entity, [ChunkIndexInQuery] int sortKey, in EnemyHealth health)
        {
            if (health.Current <= 0f) Commands.DestroyEntity(sortKey, entity);
        }
    }
}
