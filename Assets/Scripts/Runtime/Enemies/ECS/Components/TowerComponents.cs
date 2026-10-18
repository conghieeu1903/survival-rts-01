using Unity.Entities;
using Unity.Mathematics;

namespace SurvivalRts
{
    // Tháp phe mình trong thế giới ECS. Không có LocalTransform vì tháp đứng yên và không được vẽ bằng ECS.
    public struct TowerStats : IComponentData
    {
        public float3 Muzzle;
        public float Damage;
        public float Range;
        public float FireInterval;
    }

    public struct TowerState : IComponentData
    {
        public float Cooldown;
    }

    // EnemyIndex là chỉ số trong ảnh chụp vị trí kẻ địch của frame hiện tại.
    public struct TowerShot
    {
        public int EnemyIndex;
        public float Damage;
        public float3 From;
        public float3 To;
    }
}
