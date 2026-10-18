using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

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
        public int TargetId;
        public byte Engaged;
        public byte Moving;
    }

    // Giá trị gửi thẳng lên shader GpuSkinnedLit cho từng thực thể: x = hàng frame A, y = hàng frame B, z = tỉ lệ nội suy.
    [MaterialProperty("_AnimData")]
    public struct EnemyAnimData : IComponentData
    {
        public float4 Value;
    }

    public struct AnimClip
    {
        public int Start;
        public int Frames;
        public float Fps;
        public byte Loop;

        public float Length => Loop != 0 ? Frames / Fps : (Frames - 1) / Fps;
    }

    public enum EnemyAnimClip : byte
    {
        Idle,
        Walk,
        Attack,
        Death
    }

    public struct EnemyAnimation : IComponentData
    {
        public AnimClip Idle;
        public AnimClip Walk;
        public AnimClip Attack;
        public AnimClip Death;
        public EnemyAnimClip Current;
        public float Time;
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
