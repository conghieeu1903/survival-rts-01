using Unity.Entities;
using UnityEngine;

namespace SurvivalRts
{
    // Lối vào duy nhất từ code GameObject tới kẻ địch ECS.
    public static class EnemyWorld
    {
        static EnemySimulationSystem System
        {
            get
            {
                var world = World.DefaultGameObjectInjectionWorld;
                return world != null && world.IsCreated ? world.GetExistingSystemManaged<EnemySimulationSystem>() : null;
            }
        }

        public static int AliveCount => System?.AliveCount ?? 0;

        public static void Configure(EnemyData data)
        {
            System?.Configure(data);
        }

        public static void RequestSpawn(Vector3 center, float radius, int count, float interval)
        {
            System?.RequestSpawn(center, radius, count, interval);
        }

        public static bool AnyInRect(Vector2 center, Vector2 halfExtents)
        {
            var system = System;
            return system != null && system.AnyInRect(center, halfExtents);
        }
    }
}
