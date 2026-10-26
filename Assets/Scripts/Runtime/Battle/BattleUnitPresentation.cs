using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurvivalRts
{
    public struct BattleUnit : IComponentData
    {
        public int Index;
        public byte Type;
    }

    public struct BattleUnitAnim : IComponentData
    {
        public byte State;
        public float Time;
        public float Heading;
    }

    [MaterialProperty("_BaseColor")]
    public struct BattleUnitColor : IComponentData
    {
        public float4 Value;
    }

    // Clip animation và kích thước hiển thị của một loại lính.
    public struct BattleClips
    {
        public AnimClip Idle;
        public AnimClip Walk;
        public AnimClip Attack;
        public AnimClip Death;
        public float Linger;
        public float Scale;
        public float HeightOffset;
    }

    // Vẽ lính của BattleSimulation bằng Entities Graphics: mỗi lính (kể cả lính dự trữ, ẩn tới lúc xuất hiện) một entity, vị trí nội suy giữa hai lượt mô phỏng.
    // Mô phỏng chạy ngoài ECS để kết quả cố định, hệ thống này chỉ đọc kết quả.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class BattleUnitPresentation : SystemBase
    {
        NativeArray<Entity> units;
        NativeArray<BattleClips> clips;
        bool ready;

        protected override void OnDestroy()
        {
            CompleteDependency();
            if (units.IsCreated) units.Dispose();
            if (clips.IsCreated) clips.Dispose();
        }

        protected override void OnUpdate()
        {
            var sim = BattleSimulation.Current;
            if (!ready || sim == null || !sim.Started) return;

            double sinceTurn = UnityEngine.Time.timeAsDouble - sim.TurnAppliedTime;
            float alpha = math.saturate((float)(sinceTurn / LockstepNetwork.TurnInterval));

            Dependency = new BattlePresentationJob
            {
                PrevX = sim.PrevX,
                PrevZ = sim.PrevZ,
                CurX = sim.PosX,
                CurZ = sim.PosZ,
                Target = sim.Target,
                State = sim.State,
                Clips = clips,
                Alpha = alpha,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(Dependency);
        }

        // Mô phỏng gọi trước khi sửa mảng dữ liệu để job hiển thị đang đọc xong đã.
        public void CompleteJobs()
        {
            CompleteDependency();
        }

        // Tạo (lại) entity cho toàn bộ lính của trận vừa bắt đầu.
        public void Build(BattleData data, BattleSimulation sim)
        {
            CompleteDependency();
            ready = false;
            if (units.IsCreated)
            {
                EntityManager.DestroyEntity(units);
                units.Dispose();
            }
            if (clips.IsCreated) clips.Dispose();

            int typeCount = data.unitTypes.Length;
            int unitCount = sim.UnitCount;
            clips = new NativeArray<BattleClips>(typeCount, Allocator.Persistent);
            units = new NativeArray<Entity>(unitCount, Allocator.Persistent);
            var unitType = sim.UnitType;

            for (int t = 0; t < typeCount; t++)
            {
                var type = data.unitTypes[t];
                clips[t] = new BattleClips
                {
                    Idle = ToClip(type.animation, "Idle"),
                    Walk = ToClip(type.animation, "Walk"),
                    Attack = ToClip(type.animation, "Attack"),
                    Death = ToClip(type.animation, "Death"),
                    Linger = data.corpseLinger,
                    Scale = type.scale,
                    HeightOffset = -type.mesh.bounds.min.y * type.scale
                };

                var prefab = EntityManager.CreateEntity();
                var description = new RenderMeshDescription(
                    type.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows: true);
                var meshArray = new RenderMeshArray(new[] { type.material }, new[] { type.mesh });
                RenderMeshUtility.AddComponents(prefab, EntityManager, description, meshArray,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
                EntityManager.AddComponentData(prefab, LocalTransform.FromScale(type.scale));
                EntityManager.AddComponentData(prefab, new BattleUnit());
                EntityManager.AddComponentData(prefab, new BattleUnitAnim { State = byte.MaxValue });
                EntityManager.AddComponentData(prefab, new BattleUnitColor { Value = new float4(1f) });
                EntityManager.AddComponentData(prefab, new EnemyAnimData());
                EntityManager.AddComponent<Prefab>(prefab);

                int count = 0;
                for (int i = 0; i < unitCount; i++)
                    if (unitType[i] == t) count++;

                var created = new NativeArray<Entity>(count, Allocator.Temp);
                EntityManager.Instantiate(prefab, created);
                EntityManager.DestroyEntity(prefab);

                int next = 0;
                for (int i = 0; i < unitCount; i++)
                {
                    if (unitType[i] != t) continue;
                    var entity = created[next++];
                    var team = data.teamColors[Mathf.Min(i / sim.UnitsPerTeam, data.teamColors.Length - 1)] * type.tint;
                    units[i] = entity;
                    EntityManager.SetComponentData(entity, new BattleUnit { Index = i, Type = (byte)t });
                    EntityManager.SetComponentData(entity, new BattleUnitColor { Value = new float4(team.r, team.g, team.b, 1f) });
                }
                created.Dispose();
            }
            ready = true;
        }

        static AnimClip ToClip(GpuAnimationData data, string clipName)
        {
            if (!data.TryGetClip(clipName, out var clip) && !data.TryGetClip("Walk", out clip)) clip = data.clips[0];
            return new AnimClip { Start = clip.startRow, Frames = clip.frameCount, Fps = clip.fps, Loop = (byte)(clip.loop ? 1 : 0) };
        }
    }

    [BurstCompile]
    public partial struct BattlePresentationJob : IJobEntity
    {
        // Lệch pha tối đa (giây) cho clip lặp, để đám đông không cử động đồng loạt.
        const float PhaseSpread = 10f;

        [ReadOnly] public NativeArray<int> PrevX;
        [ReadOnly] public NativeArray<int> PrevZ;
        [ReadOnly] public NativeArray<int> CurX;
        [ReadOnly] public NativeArray<int> CurZ;
        [ReadOnly] public NativeArray<int> Target;
        [ReadOnly] public NativeArray<byte> State;
        [ReadOnly] public NativeArray<BattleClips> Clips;
        public float Alpha;
        public float DeltaTime;

        void Execute(ref LocalTransform transform, ref EnemyAnimData animData, ref BattleUnitAnim anim, in BattleUnit unit)
        {
            int i = unit.Index;
            // lính dự trữ chưa xuất hiện: thu nhỏ về 0, không chạy animation
            if (State[i] == BattleState.Inactive)
            {
                transform.Scale = 0f;
                anim.State = byte.MaxValue;
                return;
            }
            var clips = Clips[unit.Type];
            float cx = CurX[i] / (float)BattleFixed.One;
            float cz = CurZ[i] / (float)BattleFixed.One;
            float px = PrevX[i] / (float)BattleFixed.One;
            float pz = PrevZ[i] / (float)BattleFixed.One;
            float x = math.lerp(px, cx, Alpha);
            float z = math.lerp(pz, cz, Alpha);

            byte state = State[i];
            float2 face = new float2(cx - px, cz - pz);
            if (state == BattleState.Attack && Target[i] >= 0)
            {
                int t = Target[i];
                face = new float2(CurX[t] / (float)BattleFixed.One - cx, CurZ[t] / (float)BattleFixed.One - cz);
            }
            if (math.lengthsq(face) > 1e-4f) anim.Heading = math.atan2(face.x, face.y);

            if (state != anim.State)
            {
                // Đánh và chết luôn phát từ đầu; đứng và đi giữ nhịp để chuyển qua lại không giật.
                if (state == BattleState.Attack || state == BattleState.Dead || anim.State == byte.MaxValue) anim.Time = 0f;
                anim.State = state;
            }
            anim.Time += DeltaTime;

            var clip = state == BattleState.Dead ? clips.Death
                : state == BattleState.Attack ? clips.Attack
                : state == BattleState.Walk ? clips.Walk
                : clips.Idle;

            float time = anim.Time;
            if (state == BattleState.Idle || state == BattleState.Walk)
                time += (math.hash(new int2(i, 0x2F6B)) & 0xFFFF) / 65535f * PhaseSpread;

            float frame = time * clip.Fps;
            int a;
            int b;
            if (clip.Loop != 0)
            {
                frame -= math.floor(frame / clip.Frames) * clip.Frames;
                a = (int)frame;
                b = (a + 1) % clip.Frames;
            }
            else
            {
                frame = math.min(frame, clip.Frames - 1);
                a = (int)frame;
                b = math.min(a + 1, clip.Frames - 1);
            }
            animData.Value = new float4(clip.Start + a, clip.Start + b, frame - a, 0f);

            bool hidden = state == BattleState.Dead && anim.Time > clips.Death.Length + clips.Linger;
            transform = LocalTransform.FromPositionRotationScale(
                new float3(x, clips.HeightOffset, z), quaternion.RotateY(anim.Heading), hidden ? 0f : clips.Scale);
        }
    }
}
