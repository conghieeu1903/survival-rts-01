namespace SurvivalRts
{
    // Mô phỏng chạy theo lượt: mọi máy nhận cùng danh sách lệnh nên phải ra cùng kết quả.
    public interface ILockstepSimulation
    {
        // aiMask: bit i bật nghĩa là đội i do máy tính điều khiển (đội không có người chơi)
        void StartMatch(int seed, int teamCount, int localTeam, int aiMask);
        void ApplyTurn(uint turn, LockstepCommand[] commands);
        uint ComputeChecksum();
    }
}
