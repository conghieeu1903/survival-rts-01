using Unity.Mathematics;

namespace SurvivalRts
{
    // Toán số nguyên cố định cho mô phỏng lockstep: mọi máy phải ra đúng từng bit nên không dùng float trong vòng mô phỏng.
    public static class BattleFixed
    {
        // 1 mét = 256 đơn vị cố định
        public const int One = 256;

        public static int FromMeters(float meters)
        {
            return (int)math.round(meters * One);
        }

        public static float ToMeters(int fixedValue)
        {
            return fixedValue / (float)One;
        }

        // Căn bậc hai nguyên (làm tròn xuống); sqrt double theo IEEE nên giống nhau trên mọi máy x64.
        public static long Isqrt(long value)
        {
            if (value <= 0) return 0;
            long root = (long)math.sqrt((double)value);
            while (root * root > value) root--;
            while ((root + 1) * (root + 1) <= value) root++;
            return root;
        }

        public static long DistSq(int ax, int az, int bx, int bz)
        {
            long dx = ax - bx;
            long dz = az - bz;
            return dx * dx + dz * dz;
        }
    }

    // Thông số bản đồ và lưới, dùng chung cho mọi lính.
    public struct BattleStats
    {
        public int MapHalf;
        public int CellSize;
        public int GridWidth;
        public int RetargetInterval;
        public int ArriveRadius;
    }

    // Chỉ số một loại lính đã đổi sang số nguyên cố định, tính một lần khi bắt đầu trận.
    public struct BattleUnitStats
    {
        public int MaxHealth;
        public int Damage;
        public int CooldownSteps;
        public int HitMark;          // giá trị timer lúc đòn đánh trúng
        public int AttackRange;
        public int DetectRange;
        public int Speed;            // đơn vị cố định mỗi bước
        public int SeparationRadius; // hai lính gần hơn mức này thì đẩy nhau
    }
}
