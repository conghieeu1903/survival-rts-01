using Unity.Entities;
using Unity.Mathematics;

namespace SurvivalRts
{
    public struct EnemyTag : IComponentData
    {
    }

    public struct EnemyHealth : IComponentData
    {
        public float Current;
        public float Max;
    }

    public struct EnemyStats : IComponentData
    {
        public float MoveSpeed;
        public float Damage;
        public float AttackRange;
        public float AttackCooldown;
        public float DetectRange;
        public float Radius;
        public float HeightOffset;
    }

    public struct EnemyBrain : IComponentData
    {
        public float AttackTimer;
        public byte Engaged;
    }

    public struct DamageEvent
    {
        public int TargetId;
        public float Amount;
    }

    public struct TargetInfo
    {
        public int Id;
        public float2 Center;
        public float2 HalfExtents;
        public int Priority;
    }

    // Bản sao lưới xây dựng dùng được trong Burst.
    public struct GridInfo
    {
        public int2 Size;
        public float2 Origin;
        public float CellSize;

        public int CellCount => Size.x * Size.y;

        public int2 CellCoord(float2 position)
        {
            return (int2)math.floor((position - Origin) / CellSize);
        }

        public bool InBounds(int2 cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Size.x && cell.y < Size.y;
        }

        public int Index(int2 cell)
        {
            return cell.x + cell.y * Size.x;
        }

        public int CellIndex(float2 position)
        {
            var cell = CellCoord(position);
            return InBounds(cell) ? Index(cell) : -1;
        }

        public int CellIndexClamped(float2 position)
        {
            return Index(math.clamp(CellCoord(position), int2.zero, Size - 1));
        }
    }
}
