using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace SurvivalRts
{
    // Giữ hàng đợi sự kiện âm thanh suốt vòng đời world.
    // Hệ thống ECS ghi bằng Writer (song song được, nhớ gọi AddProducer với handle của job),
    // AudioDirector đọc hết mỗi frame bằng Drain. Không ai đọc thì sự kiện cũ bị xoá ở đầu frame sau.
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class AudioEventSystem : SystemBase
    {
        NativeQueue<AudioEvent> queue;
        JobHandle producers;
        int lastDrainFrame = -1;

        public NativeQueue<AudioEvent>.ParallelWriter Writer => queue.AsParallelWriter();
        public JobHandle ProducerDependency => producers;

        protected override void OnCreate()
        {
            queue = new NativeQueue<AudioEvent>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            producers.Complete();
            if (queue.IsCreated) queue.Dispose();
        }

        protected override void OnUpdate()
        {
            if (lastDrainFrame >= UnityEngine.Time.frameCount - 1) return;
            producers.Complete();
            queue.Clear();
        }

        public void AddProducer(JobHandle handle)
        {
            producers = JobHandle.CombineDependencies(producers, handle);
        }

        // Dùng từ main thread (MonoBehaviour).
        public void Post(SoundId id, UnityEngine.Vector3 position, float parameter = 0f)
        {
            producers.Complete();
            queue.Enqueue(new AudioEvent { Id = id, Position = position, Parameter = parameter });
        }

        public void Drain(List<AudioEvent> into)
        {
            producers.Complete();
            producers = default;
            while (queue.TryDequeue(out var e)) into.Add(e);
            lastDrainFrame = UnityEngine.Time.frameCount;
        }
    }
}
