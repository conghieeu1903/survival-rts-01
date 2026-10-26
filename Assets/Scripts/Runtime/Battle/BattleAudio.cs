using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace SurvivalRts
{
    // Đưa tiếng của trận đại chiến vào AudioDirector: kiếm chém, tên bắn, lính chết, và mật độ lính cho loop đám đông, giao tranh và nhạc trận.
    [RequireComponent(typeof(BattleSimulation))]
    public class BattleAudio : MonoBehaviour
    {
        BattleSimulation simulation;
        NativeArray<int> counts;

        void Awake()
        {
            simulation = GetComponent<BattleSimulation>();
            counts = new NativeArray<int>(2, Allocator.Persistent);
        }

        void OnEnable()
        {
            simulation.MeleeHit += OnMeleeHit;
            simulation.UnitKilled += OnUnitKilled;
            simulation.ShotFired += OnShotFired;
        }

        void OnDisable()
        {
            simulation.MeleeHit -= OnMeleeHit;
            simulation.UnitKilled -= OnUnitKilled;
            simulation.ShotFired -= OnShotFired;
            var director = AudioDirector.Instance;
            if (director != null) director.DensityProvider = null;
        }

        void OnDestroy()
        {
            if (counts.IsCreated) counts.Dispose();
        }

        void Start()
        {
            var director = AudioDirector.Instance;
            if (director != null) director.DensityProvider = CountUnits;
        }

        void OnMeleeHit(Vector3 position)
        {
            Post(SoundId.EnemyHit, position);
        }

        void OnUnitKilled(Vector3 position)
        {
            Post(SoundId.EnemyDeath, position);
        }

        void OnShotFired(Vector3 from, Vector3 to)
        {
            Post(SoundId.TowerShot, from);
        }

        static void Post(SoundId id, Vector3 position)
        {
            var director = AudioDirector.Instance;
            if (director != null && director.IsAudible(position)) AudioDirector.Post(id, position);
        }

        int2 CountUnits(float4 rect)
        {
            if (!simulation.Started) return int2.zero;
            new BattleDensityJob
            {
                UnitCount = simulation.UnitCount,
                Rect = rect,
                PosX = simulation.PosX,
                PosZ = simulation.PosZ,
                State = simulation.State,
                Result = counts
            }.Run();
            return new int2(counts[0], counts[1]);
        }
    }
}
