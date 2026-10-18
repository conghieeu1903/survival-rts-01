using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SurvivalRts
{
    // Chọn và phát clip animation (bone texture) theo trạng thái của kẻ địch, rồi dọn xác khi clip Death xong.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemySimulationSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class EnemyAnimationSystem : SystemBase
    {
        const float CorpseLinger = 2f;
        // Thời điểm (giây) trong clip Attack mà gậy đập xuống; phát tiếng đánh trúng đúng lúc này.
        const float AttackStrikeTime = 0.5f;

        // Gắn component animation vào prefab entity mà EnemySimulationSystem đã tạo từ cùng EnemyData.
        public void Configure(EnemyData data)
        {
            if (data.animation == null) return;

            var animation = new EnemyAnimation
            {
                Idle = ToClip(data.animation, "Idle"),
                Walk = ToClip(data.animation, "Walk"),
                Attack = ToClip(data.animation, "Attack"),
                Death = ToClip(data.animation, "Death"),
                Current = EnemyAnimClip.Idle
            };

            var query = EntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<EnemyTag>(), ComponentType.ReadOnly<Prefab>() },
                Options = EntityQueryOptions.IncludePrefab
            });
            var prefabs = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var prefab in prefabs)
            {
                if (EntityManager.HasComponent<EnemyAnimation>(prefab)) EntityManager.SetComponentData(prefab, animation);
                else EntityManager.AddComponentData(prefab, animation);
                if (!EntityManager.HasComponent<EnemyAnimData>(prefab)) EntityManager.AddComponentData(prefab, new EnemyAnimData());
            }
            prefabs.Dispose();
            query.Dispose();
        }

        static AnimClip ToClip(GpuAnimationData data, string clipName)
        {
            if (!data.TryGetClip(clipName, out var clip) && !data.TryGetClip("Walk", out clip)) clip = data.clips[0];
            return new AnimClip { Start = clip.startRow, Frames = clip.frameCount, Fps = clip.fps, Loop = (byte)(clip.loop ? 1 : 0) };
        }

        protected override void OnUpdate()
        {
            var commands = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(World.Unmanaged);

            var audio = World.GetExistingSystemManaged<AudioEventSystem>();
            Dependency = Unity.Jobs.JobHandle.CombineDependencies(Dependency, audio.ProducerDependency);
            Dependency = new EnemyAnimationJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                StrikeTime = AttackStrikeTime,
                Audio = audio.Writer
            }.ScheduleParallel(Dependency);
            audio.AddProducer(Dependency);
            Dependency = new EnemyCorpseJob { Commands = commands.AsParallelWriter(), Linger = CorpseLinger }
                .ScheduleParallel(Dependency);
        }
    }

    [BurstCompile]
    public partial struct EnemyAnimationJob : IJobEntity
    {
        // Lệch pha tối đa (giây) cho clip lặp, để đám đông không cử động đồng loạt.
        const float PhaseSpread = 10f;

        public float DeltaTime;
        public float StrikeTime;
        public NativeQueue<AudioEvent>.ParallelWriter Audio;

        void Execute(Entity entity, ref EnemyAnimation animation, ref EnemyAnimData data,
            in EnemyBrain brain, in EnemyHealth health, in LocalTransform transform)
        {
            var next = health.Current <= 0f ? EnemyAnimClip.Death
                : brain.Engaged != 0 ? EnemyAnimClip.Attack
                : brain.Moving != 0 ? EnemyAnimClip.Walk
                : EnemyAnimClip.Idle;

            if (next != animation.Current)
            {
                // Đánh và chết luôn phát từ đầu; đứng và đi giữ nhịp để chuyển qua lại không giật.
                if (next == EnemyAnimClip.Attack || next == EnemyAnimClip.Death) animation.Time = 0f;
                if (next == EnemyAnimClip.Death) Audio.Enqueue(new AudioEvent { Id = SoundId.EnemyDeath, Position = transform.Position });
                animation.Current = next;
            }

            float previousTime = animation.Time;
            animation.Time += DeltaTime;

            if (next == EnemyAnimClip.Attack)
            {
                // Mỗi vòng clip Attack có một cú đập; phát tiếng khi thời gian vừa vượt qua mốc đập của vòng đó.
                float length = animation.Attack.Length;
                if (math.floor((animation.Time - StrikeTime) / length) > math.floor((previousTime - StrikeTime) / length))
                    Audio.Enqueue(new AudioEvent { Id = SoundId.EnemyHit, Position = transform.Position });
            }

            var clip = next == EnemyAnimClip.Death ? animation.Death
                : next == EnemyAnimClip.Attack ? animation.Attack
                : next == EnemyAnimClip.Walk ? animation.Walk
                : animation.Idle;

            float time = animation.Time;
            if (next == EnemyAnimClip.Idle || next == EnemyAnimClip.Walk)
                time += (math.hash(new int2(entity.Index, 0x2F6B)) & 0xFFFF) / 65535f * PhaseSpread;

            float frame = time * clip.Fps;
            int a;
            int b;
            if (clip.Loop != 0)
            {
                frame = frame - math.floor(frame / clip.Frames) * clip.Frames;
                a = (int)frame;
                b = (a + 1) % clip.Frames;
            }
            else
            {
                frame = math.min(frame, clip.Frames - 1);
                a = (int)frame;
                b = math.min(a + 1, clip.Frames - 1);
            }

            data.Value = new float4(clip.Start + a, clip.Start + b, frame - a, 0f);
        }
    }

    [BurstCompile]
    public partial struct EnemyCorpseJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter Commands;
        public float Linger;

        void Execute(Entity entity, [ChunkIndexInQuery] int sortKey, in EnemyAnimation animation, in EnemyHealth health)
        {
            if (health.Current <= 0f && animation.Current == EnemyAnimClip.Death
                && animation.Time >= animation.Death.Length + Linger)
                Commands.DestroyEntity(sortKey, entity);
        }
    }
}
