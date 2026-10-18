using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace SurvivalRts
{
    // Đếm số kẻ địch đang đi và đang đánh trong khung nhìn camera, để AudioDirector chỉnh âm lượng các loop đám đông.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemyAnimationSystem))]
    public partial class AudioDensitySystem : SystemBase
    {
        NativeArray<int> counts;
        JobHandle handle;

        // Khung nhìn trên mặt đất: (minX, minZ, maxX, maxZ). AudioDirector cập nhật mỗi frame.
        public float4 ViewRect { get; set; }

        protected override void OnCreate()
        {
            counts = new NativeArray<int>(2, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            handle.Complete();
            if (counts.IsCreated) counts.Dispose();
        }

        protected override void OnUpdate()
        {
            handle.Complete();
            counts[0] = 0;
            counts[1] = 0;
            handle = new CountJob { Rect = ViewRect, Counts = counts }.Schedule(Dependency);
            Dependency = handle;
        }

        // x = số con đang đi, y = số con đang đánh (của frame gần nhất).
        public int2 GetCounts()
        {
            handle.Complete();
            return new int2(counts[0], counts[1]);
        }

        [BurstCompile]
        [WithAll(typeof(EnemyTag))]
        partial struct CountJob : IJobEntity
        {
            public float4 Rect;
            public NativeArray<int> Counts;

            void Execute(in LocalTransform transform, in EnemyBrain brain, in EnemyHealth health)
            {
                if (health.Current <= 0f) return;
                var p = transform.Position.xz;
                if (p.x < Rect.x || p.y < Rect.y || p.x > Rect.z || p.y > Rect.w) return;

                if (brain.Engaged != 0) Counts[1]++;
                else if (brain.Moving != 0) Counts[0]++;
            }
        }
    }
}
