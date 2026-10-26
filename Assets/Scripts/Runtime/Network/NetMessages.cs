using FishNet.Broadcast;

namespace SurvivalRts
{
    // Một lệnh của người chơi, chỉ dùng số nguyên để mọi máy đọc ra kết quả giống hệt nhau.
    public struct LockstepCommand
    {
        public byte Team;
        public ushort Type;
        public int X; // tọa độ nhân 100
        public int Z;
        public int Param;
    }

    // Client gửi lên host.
    public struct CommandMessage : IBroadcast
    {
        public LockstepCommand Command;
    }

    // Host gửi cho từng client khi trận bắt đầu.
    public struct StartMatchMessage : IBroadcast
    {
        public int Seed;
        public byte TeamCount;
        public byte LocalTeam;
        public byte AiMask;
    }

    // Host gom lệnh của một lượt rồi gửi cho tất cả; lượt rỗng vẫn gửi để các máy cùng bước tiếp.
    public struct TurnMessage : IBroadcast
    {
        public uint Turn;
        public LockstepCommand[] Commands;
    }

    // Client báo hash trạng thái mô phỏng để host phát hiện lệch.
    public struct ChecksumMessage : IBroadcast
    {
        public uint Turn;
        public uint Hash;
    }

    public struct DesyncMessage : IBroadcast
    {
        public uint Turn;
    }

    // Client báo đã chạy xong tới lượt nào (Turn = số lượt đã xử lý) để host biết máy nào đang tụt.
    public struct AckMessage : IBroadcast
    {
        public uint Turn;
    }

    // Host báo cả phòng đang chờ máy chậm (Waiting) và đội gây chậm.
    public struct PauseMessage : IBroadcast
    {
        public bool Waiting;
        public byte SlowTeam;
    }

    // Host kết thúc trận, mọi máy về phòng chờ.
    public struct EndMatchMessage : IBroadcast
    {
        public byte Reason;
    }

    // Danh sách người trong phòng chờ.
    public struct LobbyMessage : IBroadcast
    {
        public byte PlayerCount;
        public byte LocalTeam;
    }
}
