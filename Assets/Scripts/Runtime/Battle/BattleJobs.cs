using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace SurvivalRts
{
    public static class BattleGrid
    {
        public static int Cell(in BattleStats s, int x, int z)
        {
            int cx = math.clamp((x + s.MapHalf) / s.CellSize, 0, s.GridWidth - 1);
            int cz = math.clamp((z + s.MapHalf) / s.CellSize, 0, s.GridWidth - 1);
            return cx + cz * s.GridWidth;
        }
    }

    // Xếp lính còn sống vào lưới ô theo thứ tự chỉ số, nên kết quả giống hệt nhau trên mọi máy.
    [BurstCompile(FloatMode = FloatMode.Deterministic)]
    public struct BattleGridJob : IJob
    {
        public BattleStats Stats;
        public int UnitCount;
        [ReadOnly] public NativeArray<int> PosX;
        [ReadOnly] public NativeArray<int> PosZ;
        [ReadOnly] public NativeArray<int> Health;
        public NativeArray<int> CellStart; // GridWidth^2 + 1 phần tử
        public NativeArray<int> Sorted;
        public NativeArray<int> Fill;

        public void Execute()
        {
            int cells = Stats.GridWidth * Stats.GridWidth;
            for (int c = 0; c <= cells; c++) CellStart[c] = 0;

            for (int i = 0; i < UnitCount; i++)
            {
                if (Health[i] <= 0) continue;
                CellStart[BattleGrid.Cell(Stats, PosX[i], PosZ[i]) + 1]++;
            }
            for (int c = 0; c < cells; c++)
            {
                CellStart[c + 1] += CellStart[c];
                Fill[c] = CellStart[c];
            }
            for (int i = 0; i < UnitCount; i++)
            {
                if (Health[i] <= 0) continue;
                int c = BattleGrid.Cell(Stats, PosX[i], PosZ[i]);
                Sorted[Fill[c]++] = i;
            }
        }
    }

    // Một bước mô phỏng cho từng lính: chọn mục tiêu, đánh hoặc đi, và đẩy nhau. Chỉ ghi vào ô của chính nó.
    [BurstCompile(FloatMode = FloatMode.Deterministic)]
    public struct BattleStepJob : IJobParallelFor
    {
        const int MaxSeparationNeighbors = 12;

        public BattleStats Stats;
        public int StepIndex;
        public int UnitsPerTeam;
        public int SquadSize;
        public int SquadsPerTeam;

        [ReadOnly] public NativeArray<BattleUnitStats> Types;
        [ReadOnly] public NativeArray<byte> UnitType;
        [ReadOnly] public NativeArray<int> PosX;
        [ReadOnly] public NativeArray<int> PosZ;
        [ReadOnly] public NativeArray<int> Health;
        [ReadOnly] public NativeArray<int> SlotX;
        [ReadOnly] public NativeArray<int> SlotZ;
        [ReadOnly] public NativeArray<int> SquadDestX;
        [ReadOnly] public NativeArray<int> SquadDestZ;
        [ReadOnly] public NativeArray<int> CellStart;
        [ReadOnly] public NativeArray<int> Sorted;

        public NativeArray<int> NextX;
        public NativeArray<int> NextZ;
        public NativeArray<int> Target;
        public NativeArray<int> Timer;
        public NativeArray<int> DamageTarget;
        public NativeArray<byte> State;

        public void Execute(int i)
        {
            if (Health[i] <= 0)
            {
                NextX[i] = PosX[i];
                NextZ[i] = PosZ[i];
                DamageTarget[i] = -1;
                if (State[i] != BattleState.Inactive) State[i] = BattleState.Dead;
                return;
            }

            int x = PosX[i];
            int z = PosZ[i];
            int team = i / UnitsPerTeam;
            var type = Types[UnitType[i]];
            long detectSq = (long)type.DetectRange * type.DetectRange;
            long leashSq = detectSq * 4;
            long rangeSq = (long)type.AttackRange * type.AttackRange;

            int target = Target[i];
            if (target >= 0)
            {
                if (Health[target] <= 0 || BattleFixed.DistSq(x, z, PosX[target], PosZ[target]) > leashSq) target = -1;
            }
            if (target < 0 || (StepIndex + i) % Stats.RetargetInterval == 0)
            {
                int found = FindTarget(team, x, z, type.DetectRange, detectSq);
                if (found >= 0 || target < 0) target = found;
            }
            Target[i] = target;

            int timer = Timer[i];
            int damageTarget = -1;
            bool inRange = target >= 0 && BattleFixed.DistSq(x, z, PosX[target], PosZ[target]) <= rangeSq;
            if (timer > 0)
            {
                timer--;
                if (timer == type.HitMark && inRange) damageTarget = target;
            }
            if (inRange && timer == 0) timer = type.CooldownSteps;
            Timer[i] = timer;
            DamageTarget[i] = damageTarget;

            int goalX = 0;
            int goalZ = 0;
            bool moving = false;
            if (target >= 0 && !inRange)
            {
                goalX = PosX[target];
                goalZ = PosZ[target];
                moving = true;
            }
            else if (target < 0)
            {
                int squad = team * SquadsPerTeam + (i % UnitsPerTeam) / SquadSize;
                goalX = SquadDestX[squad] + SlotX[i];
                goalZ = SquadDestZ[squad] + SlotZ[i];
                long arrive = (long)Stats.ArriveRadius * Stats.ArriveRadius;
                moving = BattleFixed.DistSq(x, z, goalX, goalZ) > arrive;
            }

            int moveX = 0;
            int moveZ = 0;
            if (moving)
            {
                long dx = goalX - x;
                long dz = goalZ - z;
                long dist = BattleFixed.Isqrt(dx * dx + dz * dz);
                if (dist > 0)
                {
                    long step = math.min((long)type.Speed, dist);
                    moveX = (int)(dx * step / dist);
                    moveZ = (int)(dz * step / dist);
                }
            }

            Separate(i, x, z, type, out int pushX, out int pushZ);

            NextX[i] = math.clamp(x + moveX + pushX, -Stats.MapHalf, Stats.MapHalf);
            NextZ[i] = math.clamp(z + moveZ + pushZ, -Stats.MapHalf, Stats.MapHalf);
            State[i] = inRange && timer > 0 ? BattleState.Attack : moving ? BattleState.Walk : BattleState.Idle;
        }

        int FindTarget(int team, int x, int z, int detectRange, long detectSq)
        {
            int cx = math.clamp((x + Stats.MapHalf) / Stats.CellSize, 0, Stats.GridWidth - 1);
            int cz = math.clamp((z + Stats.MapHalf) / Stats.CellSize, 0, Stats.GridWidth - 1);
            int reach = (detectRange + Stats.CellSize - 1) / Stats.CellSize;
            long bestSq = detectSq + 1;
            int best = -1;

            for (int gz = math.max(cz - reach, 0); gz <= math.min(cz + reach, Stats.GridWidth - 1); gz++)
            {
                for (int gx = math.max(cx - reach, 0); gx <= math.min(cx + reach, Stats.GridWidth - 1); gx++)
                {
                    int cell = gx + gz * Stats.GridWidth;
                    for (int k = CellStart[cell]; k < CellStart[cell + 1]; k++)
                    {
                        int other = Sorted[k];
                        if (other / UnitsPerTeam == team) continue;
                        long d = BattleFixed.DistSq(x, z, PosX[other], PosZ[other]);
                        if (d < bestSq || (d == bestSq && other < best))
                        {
                            bestSq = d;
                            best = other;
                        }
                    }
                }
            }
            return bestSq <= detectSq ? best : -1;
        }

        // Đẩy lính ra khỏi những lính đang chồng lên; duyệt theo thứ tự cố định nên kết quả không phụ thuộc luồng.
        void Separate(int self, int x, int z, in BattleUnitStats type, out int pushX, out int pushZ)
        {
            pushX = 0;
            pushZ = 0;
            int radius = type.SeparationRadius;
            long radiusSq = (long)radius * radius;
            int cx = math.clamp((x + Stats.MapHalf) / Stats.CellSize, 0, Stats.GridWidth - 1);
            int cz = math.clamp((z + Stats.MapHalf) / Stats.CellSize, 0, Stats.GridWidth - 1);
            int counted = 0;

            for (int gz = math.max(cz - 1, 0); gz <= math.min(cz + 1, Stats.GridWidth - 1); gz++)
            {
                for (int gx = math.max(cx - 1, 0); gx <= math.min(cx + 1, Stats.GridWidth - 1); gx++)
                {
                    int cell = gx + gz * Stats.GridWidth;
                    for (int k = CellStart[cell]; k < CellStart[cell + 1]; k++)
                    {
                        int other = Sorted[k];
                        if (other == self) continue;
                        long dx = x - PosX[other];
                        long dz = z - PosZ[other];
                        long d2 = dx * dx + dz * dz;
                        if (d2 >= radiusSq) continue;

                        if (d2 == 0)
                        {
                            // trùng vị trí: tách theo chỉ số để hai bên đẩy ngược chiều
                            pushX += self > other ? 8 : -8;
                        }
                        else
                        {
                            long d = BattleFixed.Isqrt(d2);
                            long overlap = radius - d;
                            pushX += (int)(dx * overlap / d / 4);
                            pushZ += (int)(dz * overlap / d / 4);
                        }
                        if (++counted >= MaxSeparationNeighbors) break;
                    }
                    if (counted >= MaxSeparationNeighbors) break;
                }
                if (counted >= MaxSeparationNeighbors) break;
            }

            int limit = type.Speed;
            pushX = math.clamp(pushX, -limit, limit);
            pushZ = math.clamp(pushZ, -limit, limit);
        }
    }

    // Trừ máu theo thứ tự chỉ số lính để nhiều đòn cùng một mục tiêu cho kết quả cố định; đòn kết liễu tính vào kills/losses.
    [BurstCompile(FloatMode = FloatMode.Deterministic)]
    public struct BattleDamageJob : IJob
    {
        public int UnitCount;
        public int UnitsPerTeam;
        [ReadOnly] public NativeArray<BattleUnitStats> Types;
        [ReadOnly] public NativeArray<byte> UnitType;
        [ReadOnly] public NativeArray<int> DamageTarget;
        public NativeArray<int> Health;
        public NativeArray<int> Kills;  // theo đội
        public NativeArray<int> Losses; // theo đội

        public void Execute()
        {
            for (int i = 0; i < UnitCount; i++)
            {
                int t = DamageTarget[i];
                if (t < 0 || Health[t] <= 0) continue;
                Health[t] -= Types[UnitType[i]].Damage;
                if (Health[t] > 0) continue;
                Kills[i / UnitsPerTeam]++;
                Losses[t / UnitsPerTeam]++;
            }
        }
    }

    // Tâm và số lính còn sống của từng tiểu đội, dùng cho chọn đội hình và hiển thị.
    [BurstCompile(FloatMode = FloatMode.Deterministic)]
    public struct BattleSquadJob : IJob
    {
        public int UnitCount;
        public int UnitsPerTeam;
        public int SquadSize;
        public int SquadsPerTeam;
        [ReadOnly] public NativeArray<int> PosX;
        [ReadOnly] public NativeArray<int> PosZ;
        [ReadOnly] public NativeArray<int> Health;
        public NativeArray<long> SumX;
        public NativeArray<long> SumZ;
        public NativeArray<int> Alive;

        public void Execute()
        {
            for (int s = 0; s < Alive.Length; s++)
            {
                SumX[s] = 0;
                SumZ[s] = 0;
                Alive[s] = 0;
            }
            for (int i = 0; i < UnitCount; i++)
            {
                if (Health[i] <= 0) continue;
                int squad = i / UnitsPerTeam * SquadsPerTeam + i % UnitsPerTeam / SquadSize;
                SumX[squad] += PosX[i];
                SumZ[squad] += PosZ[i];
                Alive[squad]++;
            }
        }
    }

    // Băm toàn bộ trạng thái mô phỏng để các máy so khớp nhau.
    [BurstCompile(FloatMode = FloatMode.Deterministic)]
    public struct BattleChecksumJob : IJob
    {
        public int UnitCount;
        [ReadOnly] public NativeArray<int> PosX;
        [ReadOnly] public NativeArray<int> PosZ;
        [ReadOnly] public NativeArray<int> Health;
        [ReadOnly] public NativeArray<int> Target;
        [ReadOnly] public NativeArray<int> Timer;
        [ReadOnly] public NativeArray<byte> State;
        [ReadOnly] public NativeArray<int> SquadDestX;
        [ReadOnly] public NativeArray<int> SquadDestZ;
        [ReadOnly] public NativeArray<byte> SquadStatus;
        [ReadOnly] public NativeArray<int> SquadReady;
        [ReadOnly] public NativeArray<int> Gold;
        [ReadOnly] public NativeArray<int> GoldFrac;
        [ReadOnly] public NativeArray<int> Kills;
        [ReadOnly] public NativeArray<int> Losses;
        [ReadOnly] public NativeArray<byte> AiTeam;
        public NativeArray<uint> Result;

        public void Execute()
        {
            uint hash = 2166136261u;
            for (int i = 0; i < UnitCount; i++)
            {
                hash = (hash ^ (uint)PosX[i]) * 16777619u;
                hash = (hash ^ (uint)PosZ[i]) * 16777619u;
                hash = (hash ^ (uint)Health[i]) * 16777619u;
                hash = (hash ^ (uint)Target[i]) * 16777619u;
                hash = (hash ^ (uint)Timer[i]) * 16777619u;
                hash = (hash ^ State[i]) * 16777619u;
            }
            for (int s = 0; s < SquadStatus.Length; s++)
            {
                hash = (hash ^ (uint)SquadDestX[s]) * 16777619u;
                hash = (hash ^ (uint)SquadDestZ[s]) * 16777619u;
                hash = (hash ^ SquadStatus[s]) * 16777619u;
                hash = (hash ^ (uint)SquadReady[s]) * 16777619u;
            }
            for (int t = 0; t < Gold.Length; t++)
            {
                hash = (hash ^ (uint)Gold[t]) * 16777619u;
                hash = (hash ^ (uint)GoldFrac[t]) * 16777619u;
                hash = (hash ^ (uint)Kills[t]) * 16777619u;
                hash = (hash ^ (uint)Losses[t]) * 16777619u;
                hash = (hash ^ AiTeam[t]) * 16777619u;
            }
            Result[0] = hash;
        }
    }

    // Đếm lính đang đi và đang đánh trong một vùng trên mặt đất (mét), để chỉnh âm lượng loop đám đông.
    [BurstCompile(FloatMode = FloatMode.Deterministic)]
    public struct BattleDensityJob : IJob
    {
        public int UnitCount;
        public float4 Rect; // minX, minZ, maxX, maxZ
        [ReadOnly] public NativeArray<int> PosX;
        [ReadOnly] public NativeArray<int> PosZ;
        [ReadOnly] public NativeArray<byte> State;
        public NativeArray<int> Result; // 0 = đang đi, 1 = đang đánh

        public void Execute()
        {
            int walking = 0;
            int attacking = 0;
            for (int i = 0; i < UnitCount; i++)
            {
                byte state = State[i];
                if (state != BattleState.Walk && state != BattleState.Attack) continue;
                float x = PosX[i] / (float)BattleFixed.One;
                float z = PosZ[i] / (float)BattleFixed.One;
                if (x < Rect.x || z < Rect.y || x > Rect.z || z > Rect.w) continue;
                if (state == BattleState.Walk) walking++;
                else attacking++;
            }
            Result[0] = walking;
            Result[1] = attacking;
        }
    }

    public static class BattleState
    {
        public const byte Idle = 0;
        public const byte Walk = 1;
        public const byte Attack = 2;
        public const byte Dead = 3;
        public const byte Inactive = 4; // lính dự trữ chưa xuất hiện
    }
}
