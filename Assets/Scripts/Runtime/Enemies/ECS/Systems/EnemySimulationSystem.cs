using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurvivalRts
{
    // Điều phối toàn bộ kẻ địch: giữ lưới flow field, chụp vị trí, chạy job mô phỏng,
    // và là cầu nối với thế giới GameObject (mục tiêu phe mình, sát thương, yêu cầu spawn).
    // Tháp phe mình cũng chạy ở đây vì cần dùng chung spatial hash của kẻ địch trong cùng frame.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class EnemySimulationSystem : SystemBase
    {
        const byte FreeCost = 1;
        const byte BuildingCost = 20;
        const float MaxStep = 0.5f;
        const int MaxNeighbors = 16;
        const float SeparationStrength = 6f;
        const int SpawnAttempts = 10;

        class SpawnRequest
        {
            public float2 Center;
            public float Radius;
            public int Remaining;
            public float Interval;
            public float Timer;
        }

        readonly List<SpawnRequest> requests = new List<SpawnRequest>();
        readonly Dictionary<int, float> damageByTarget = new Dictionary<int, float>();

        EntityQuery enemyQuery;
        EntityQuery towerQuery;
        Entity prefab;
        float heightOffset;
        Unity.Mathematics.Random random;

        GridInfo grid;
        bool gridReady;
        int gridVersion = -1;
        NativeArray<float> height;
        NativeArray<byte> cost;
        NativeArray<byte> walk;
        NativeArray<int> integration;
        NativeArray<float2> flow;
        NativeArray<int> cellStart;
        NativeArray<int> cellCount;

        NativeArray<float3> positions;
        NativeArray<int> cells;
        NativeArray<int> sorted;
        NativeArray<Entity> entities;
        NativeArray<byte> alive;
        int positionCount;

        NativeList<TargetInfo> targets;
        NativeQueue<DamageEvent> damage;
        NativeQueue<TowerShot> towerShots;
        NativeList<TowerShot> firedShots;
        JobHandle lastHandle;

        public int AliveCount { get; private set; }

        // Gọi trên main thread cho mỗi phát bắn của frame trước, dùng để vẽ tia đạn.
        public event System.Action<Vector3, Vector3> ShotFired;

        protected override void OnCreate()
        {
            enemyQuery = GetEntityQuery(
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<EnemyBrain>(),
                ComponentType.ReadOnly<EnemyStats>(),
                ComponentType.ReadOnly<EnemyHealth>(),
                ComponentType.ReadOnly<EnemyTag>());
            towerQuery = GetEntityQuery(
                ComponentType.ReadWrite<TowerState>(),
                ComponentType.ReadOnly<TowerStats>());
            targets = new NativeList<TargetInfo>(64, Allocator.Persistent);
            damage = new NativeQueue<DamageEvent>(Allocator.Persistent);
            towerShots = new NativeQueue<TowerShot>(Allocator.Persistent);
            firedShots = new NativeList<TowerShot>(64, Allocator.Persistent);
            random = new Unity.Mathematics.Random(0x5EED1234u);
        }

        protected override void OnDestroy()
        {
            lastHandle.Complete();
            if (targets.IsCreated) targets.Dispose();
            if (damage.IsCreated) damage.Dispose();
            if (towerShots.IsCreated) towerShots.Dispose();
            if (firedShots.IsCreated) firedShots.Dispose();
            DisposeGrid();
            DisposePositions();
        }

        public void Configure(EnemyData data)
        {
            if (prefab != Entity.Null) EntityManager.DestroyEntity(prefab);

            prefab = EntityManager.CreateEntity();
            var description = new RenderMeshDescription(
                data.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows: true);
            var meshArray = new RenderMeshArray(new[] { data.material }, new[] { data.mesh });
            RenderMeshUtility.AddComponents(prefab, EntityManager, description, meshArray,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));

            // Khoảng cách từ pivot của mesh xuống đáy (chân); mesh có pivot ở chân thì bằng 0.
            heightOffset = -data.mesh.bounds.min.y * data.scale;
            EntityManager.AddComponentData(prefab, LocalTransform.FromScale(data.scale));
            EntityManager.AddComponentData(prefab, new EnemyHealth { Current = data.maxHealth, Max = data.maxHealth });
            EntityManager.AddComponentData(prefab, new EnemyStats
            {
                MoveSpeed = data.moveSpeed,
                Damage = data.damage,
                AttackRange = data.attackRange,
                AttackCooldown = data.attackCooldown,
                DetectRange = data.detectRange,
                Radius = data.radius,
                HeightOffset = heightOffset
            });
            EntityManager.AddComponentData(prefab, new EnemyBrain());
            EntityManager.AddComponent<EnemyTag>(prefab);
            EntityManager.AddComponent<Prefab>(prefab);
        }

        public void RequestSpawn(Vector3 center, float radius, int count, float interval)
        {
            if (count <= 0) return;
            requests.Add(new SpawnRequest
            {
                Center = new float2(center.x, center.z),
                Radius = radius,
                Remaining = count,
                Interval = interval,
                Timer = interval
            });
        }

        public Entity CreateTower(TowerData data, Vector3 basePosition)
        {
            var tower = EntityManager.CreateEntity(typeof(TowerStats), typeof(TowerState));
            EntityManager.SetComponentData(tower, new TowerStats
            {
                Muzzle = new float3(basePosition.x, basePosition.y + data.muzzleHeight, basePosition.z),
                Damage = data.damage,
                Range = data.range,
                FireInterval = data.fireInterval
            });
            return tower;
        }

        public void DestroyTower(Entity tower)
        {
            if (EntityManager.Exists(tower)) EntityManager.DestroyEntity(tower);
        }

        public bool AnyInRect(Vector2 center, Vector2 halfExtents)
        {
            lastHandle.Complete();
            if (!positions.IsCreated) return false;

            var min = new float2(center.x - halfExtents.x, center.y - halfExtents.y);
            var max = new float2(center.x + halfExtents.x, center.y + halfExtents.y);
            for (int i = 0; i < positionCount; i++)
            {
                var p = positions[i].xz;
                if (math.all(p >= min) && math.all(p <= max)) return true;
            }
            return false;
        }

        public void CopyAlivePositions(List<Vector2> output)
        {
            output.Clear();
            lastHandle.Complete();
            if (!positions.IsCreated || !alive.IsCreated) return;

            for (int i = 0; i < positionCount; i++)
            {
                if (alive[i] != 0)
                {
                    var p = positions[i];
                    output.Add(new Vector2(p.x, p.z));
                }
            }
        }

        protected override void OnUpdate()
        {
            lastHandle.Complete();
            ApplyDamageToPlayer();
            PublishShots();

            var buildGrid = BuildGrid.Instance;
            if (buildGrid == null)
            {
                AliveCount = 0;
                positionCount = 0;
                return;
            }

            if (!gridReady) InitGrid(buildGrid);
            if (buildGrid.Version != gridVersion) RebuildFlowField(buildGrid);
            SyncTargets();

            float deltaTime = SystemAPI.Time.DeltaTime;
            ProcessSpawns(deltaTime);

            int count = enemyQuery.CalculateEntityCount();
            AliveCount = count;
            positionCount = count;
            if (count == 0) return;
            EnsurePositionCapacity(count);

            var handle = new EnemySnapshotJob
            {
                Grid = grid,
                Positions = positions,
                Cells = cells,
                Entities = entities,
                Alive = alive
            }.ScheduleParallel(enemyQuery, Dependency);

            handle = new EnemySortJob
            {
                Count = count,
                Cells = cells,
                CellStart = cellStart,
                CellCount = cellCount,
                Sorted = sorted
            }.Schedule(handle);

            // Tháp bắn trước khi kẻ địch di chuyển, để con vừa chết không còn đánh trong frame này.
            handle = new TowerFireJob
            {
                Grid = grid,
                DeltaTime = deltaTime,
                Positions = positions,
                Alive = alive,
                CellStart = cellStart,
                CellCount = cellCount,
                Sorted = sorted,
                Shots = towerShots.AsParallelWriter()
            }.ScheduleParallel(towerQuery, handle);

            handle = new TowerApplyShotsJob
            {
                Shots = towerShots,
                Entities = entities,
                Health = GetComponentLookup<EnemyHealth>(),
                Fired = firedShots
            }.Schedule(handle);

            handle = new EnemySimulateJob
            {
                Grid = grid,
                DeltaTime = deltaTime,
                MaxStep = MaxStep,
                MaxNeighbors = MaxNeighbors,
                SeparationStrength = SeparationStrength,
                Flow = flow,
                Walk = walk,
                Height = height,
                Targets = targets.AsArray(),
                Positions = positions,
                CellStart = cellStart,
                CellCount = cellCount,
                Sorted = sorted,
                Damage = damage.AsParallelWriter()
            }.ScheduleParallel(enemyQuery, handle);

            var commands = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(World.Unmanaged);
            handle = new EnemyDeathJob { Commands = commands.AsParallelWriter() }.ScheduleParallel(handle);

            Dependency = handle;
            lastHandle = handle;
        }

        void ApplyDamageToPlayer()
        {
            if (damage.Count == 0) return;

            damageByTarget.Clear();
            while (damage.TryDequeue(out var hit))
            {
                damageByTarget.TryGetValue(hit.TargetId, out float total);
                damageByTarget[hit.TargetId] = total + hit.Amount;
            }

            foreach (var pair in damageByTarget)
            {
                if (PlayerTarget.TryGet(pair.Key, out var target)) target.Health.TakeDamage(pair.Value);
            }
        }

        void PublishShots()
        {
            if (firedShots.Length == 0) return;
            if (ShotFired != null)
            {
                foreach (var shot in firedShots) ShotFired((Vector3)shot.From, (Vector3)shot.To);
            }
            firedShots.Clear();
        }

        void InitGrid(BuildGrid buildGrid)
        {
            grid = new GridInfo
            {
                Size = new int2(buildGrid.Size.x, buildGrid.Size.y),
                Origin = new float2(buildGrid.Origin.x, buildGrid.Origin.z),
                CellSize = buildGrid.CellSize
            };

            int cellTotal = grid.CellCount;
            // Bản sao riêng vì job đọc mảng này, không để phụ thuộc vào vòng đời của BuildGrid.
            height = new NativeArray<float>(buildGrid.Heights, Allocator.Persistent);
            cost = new NativeArray<byte>(cellTotal, Allocator.Persistent);
            walk = new NativeArray<byte>(cellTotal, Allocator.Persistent);
            integration = new NativeArray<int>(cellTotal, Allocator.Persistent);
            flow = new NativeArray<float2>(cellTotal, Allocator.Persistent);
            cellStart = new NativeArray<int>(cellTotal, Allocator.Persistent);
            cellCount = new NativeArray<int>(cellTotal, Allocator.Persistent);
            gridReady = true;
        }

        void RebuildFlowField(BuildGrid buildGrid)
        {
            // BuildGrid dùng cùng cách đánh chỉ số x + y * Size.x nên đọc thẳng theo i.
            buildGrid.Heights.CopyTo(height);
            var terrain = buildGrid.Terrain;
            var occupancyGrid = buildGrid.Occupancy;
            for (int i = 0; i < grid.CellCount; i++)
            {
                var occupancy = occupancyGrid[i];
                bool ground = terrain[i] == CellState.Free;
                bool blockedByObstacle = occupancy == CellOccupancy.Static || occupancy == CellOccupancy.Clearable;
                cost[i] = !ground || blockedByObstacle ? (byte)0
                    : occupancy == CellOccupancy.Building ? BuildingCost
                    : FreeCost;
                walk[i] = (byte)(ground && occupancy == CellOccupancy.None ? 1 : 0);
            }

            var goals = new NativeList<int>(16, Allocator.TempJob);
            foreach (var target in PlayerTarget.All)
            {
                if (target.Priority != TargetPriority.Headquarters || target.Occupant == null) continue;
                foreach (int index in target.Occupant.Cells) goals.Add(index);
            }

            new FlowFieldJob
            {
                Size = grid.Size,
                MaxStep = MaxStep,
                Cost = cost,
                Height = height,
                Goals = goals.AsArray(),
                Integration = integration,
                Flow = flow
            }.Schedule().Complete();

            goals.Dispose();
            gridVersion = buildGrid.Version;
        }

        void SyncTargets()
        {
            targets.Clear();
            foreach (var target in PlayerTarget.All)
            {
                if (target.Health.IsDead) continue;
                var position = target.transform.position;
                var half = target.HalfExtents;
                // Mọi công trình (kể cả Nhà chính) có cùng mức ưu tiên để quái đánh công trình gần nhất.
                targets.Add(new TargetInfo
                {
                    Id = target.Id,
                    Center = new float2(position.x, position.z),
                    HalfExtents = new float2(half.x, half.y),
                    Priority = target.Priority == TargetPriority.Unit ? 0 : 1
                });
            }
        }

        void ProcessSpawns(float deltaTime)
        {
            if (prefab == Entity.Null) return;

            for (int r = requests.Count - 1; r >= 0; r--)
            {
                var request = requests[r];
                int amount;
                if (request.Interval <= 0f)
                {
                    amount = request.Remaining;
                }
                else
                {
                    request.Timer += deltaTime;
                    amount = math.min(request.Remaining, (int)(request.Timer / request.Interval));
                    request.Timer -= amount * request.Interval;
                }

                if (amount > 0) SpawnBatch(request, amount);
                if (request.Remaining <= 0) requests.RemoveAt(r);
            }
        }

        void SpawnBatch(SpawnRequest request, int amount)
        {
            var entities = EntityManager.Instantiate(prefab, amount, Allocator.Temp);
            var scale = EntityManager.GetComponentData<LocalTransform>(prefab).Scale;
            for (int i = 0; i < entities.Length; i++)
            {
                var point = RandomPoint(request);
                var heading = quaternion.RotateY(random.NextFloat(math.PI * 2f));
                EntityManager.SetComponentData(entities[i], LocalTransform.FromPositionRotationScale(point, heading, scale));
            }
            entities.Dispose();
            request.Remaining -= amount;
        }

        float3 RandomPoint(SpawnRequest request)
        {
            for (int attempt = 0; attempt < SpawnAttempts; attempt++)
            {
                float angle = random.NextFloat(math.PI * 2f);
                float distance = request.Radius * math.sqrt(random.NextFloat());
                var p = request.Center + new float2(math.cos(angle), math.sin(angle)) * distance;
                int cell = grid.CellIndex(p);
                if (cell >= 0 && walk[cell] == 1) return new float3(p.x, height[cell] + heightOffset, p.y);
            }

            int centerCell = grid.CellIndexClamped(request.Center);
            return new float3(request.Center.x, height[centerCell] + heightOffset, request.Center.y);
        }

        void EnsurePositionCapacity(int count)
        {
            if (positions.IsCreated && positions.Length >= count) return;

            int capacity = math.max(count, positions.IsCreated ? positions.Length * 2 : 1024);
            DisposePositions();
            positions = new NativeArray<float3>(capacity, Allocator.Persistent);
            cells = new NativeArray<int>(capacity, Allocator.Persistent);
            sorted = new NativeArray<int>(capacity, Allocator.Persistent);
            entities = new NativeArray<Entity>(capacity, Allocator.Persistent);
            alive = new NativeArray<byte>(capacity, Allocator.Persistent);
        }

        void DisposePositions()
        {
            if (positions.IsCreated) positions.Dispose();
            if (cells.IsCreated) cells.Dispose();
            if (sorted.IsCreated) sorted.Dispose();
            if (entities.IsCreated) entities.Dispose();
            if (alive.IsCreated) alive.Dispose();
        }

        void DisposeGrid()
        {
            if (height.IsCreated) height.Dispose();
            if (cost.IsCreated) cost.Dispose();
            if (walk.IsCreated) walk.Dispose();
            if (integration.IsCreated) integration.Dispose();
            if (flow.IsCreated) flow.Dispose();
            if (cellStart.IsCreated) cellStart.Dispose();
            if (cellCount.IsCreated) cellCount.Dispose();
            gridReady = false;
        }
    }
}
