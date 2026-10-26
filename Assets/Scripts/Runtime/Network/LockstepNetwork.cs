using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace SurvivalRts
{
    // Khung mạng lockstep trên FishNet: host đặt đội, gom lệnh theo lượt rồi phát cho mọi máy.
    // Chưa dùng NetworkObject, toàn bộ đi qua Broadcast.
    public class LockstepNetwork : MonoBehaviour
    {
        public static LockstepNetwork Instance { get; private set; }

        // Thời gian một lượt (giây): lệnh được gom và phát mỗi lượt, mô phỏng chạy cố định số bước mỗi lượt
        public const float TurnInterval = 0.1f;

        public const int MaxTeams = 4;
        const int ChecksumEveryTurns = 10;
        const int AckEveryTurns = 10;

        // host tạm dừng phát lượt khi có máy tụt quá PauseLagTurns, chạy lại khi còn dưới ResumeLagTurns
        const int PauseLagTurns = 30;
        const int ResumeLagTurns = 20;
        const float StuckSeconds = 20f; // máy tụt mà không tiến quá lâu thì coi như rời

        // client xử lý nhiều lượt mỗi frame theo ngân sách thời gian khi bị tụt
        const float TurnBudgetMs = 8f;
        const int MaxTurnsPerFrame = 64;

        // chống lạm dụng
        const int MaxCommandsPerTeamPerTurn = 64;
        const int MaxCommandsPerTurn = 256;

        [Header("Mạng")]
        [SerializeField] ushort port = 7770;

        public ushort Port => port;
        public bool IsHost { get; private set; }
        public bool InMatch { get; private set; }
        public int LocalTeam { get; private set; } = -1;
        public int PlayerCount { get; private set; }
        public uint CurrentTurn { get; private set; }

        // số lượt đã nhận từ host nhưng máy này chưa xử lý (độ tụt của máy local)
        public int LagTurns => incoming.Count;
        // host đang chờ máy chậm; WaitingTeam là đội gây chậm (-1 nếu không có)
        public bool IsWaitingForPlayers { get; private set; }
        public int WaitingTeam { get; private set; } = -1;
        public bool IsConnecting => connecting;
        public bool IsConnected => wasConnected;

        public event Action LobbyChanged;
        public event Action<uint> DesyncDetected;
        public event Action MatchStarted;
        public event Action MatchEnded;
        public event Action WaitingChanged;
        // mất kết nối với phòng (tham số là lý do hiển thị cho người chơi)
        public event Action<string> Disconnected;

        public ILockstepSimulation Simulation { get; set; }

        NetworkManager manager;
        bool connecting; // đang kết nối, chưa lên được
        bool wasConnected; // đã kết nối thành công, dùng để phân biệt tự rời với bị mất kết nối
        bool hasPendingStart; // gói bắt đầu trận đã nhận từ phòng chờ ở scene menu, chờ scene battle nạp xong mới chạy StartMatch
        StartMatchMessage pendingStartMessage;

        // chỉ host dùng
        readonly Dictionary<int, byte> teamByConnection = new Dictionary<int, byte>();
        readonly Dictionary<int, uint> ackedTurn = new Dictionary<int, uint>(); // số lượt mỗi máy đã xử lý
        readonly Dictionary<int, float> lastProgress = new Dictionary<int, float>(); // lúc ack tiến lần cuối
        readonly List<LockstepCommand> pending = new List<LockstepCommand>();
        readonly Dictionary<uint, uint> checksums = new Dictionary<uint, uint>();
        readonly int[] commandCounts = new int[MaxTeams]; // số lệnh mỗi đội trong lượt đang gom
        readonly List<int> kickList = new List<int>();
        float turnTimer;
        uint hostTurn;
        bool hostWaiting;
        int hostSlowTeam = -1;

        // chỉ client dùng
        readonly Queue<TurnMessage> incoming = new Queue<TurnMessage>();

        public static LockstepNetwork EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<LockstepNetwork>();
            if (existing != null) return existing;
            var go = new GameObject("LockstepNetwork");
            DontDestroyOnLoad(go);
            return go.AddComponent<LockstepNetwork>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            if (transform.parent == null && GetComponent<BattleSimulation>() == null) DontDestroyOnLoad(gameObject);
            if (GetComponent<LanDiscovery>() == null) gameObject.AddComponent<LanDiscovery>();
        }

        // Tham số dòng lệnh: -host để mở phòng, -join <địa chỉ> để vào phòng của máy khác.
        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-host") Host();
                else if (args[i] == "-join" && i + 1 < args.Length) Join(args[i + 1]);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
        }

        bool EnsureManager()
        {
            if (manager != null) return true;
            manager = InstanceFinder.NetworkManager;
            if (manager == null)
            {
                var prefab = Resources.Load<GameObject>("NetworkManager");
                if (prefab == null)
                {
                    Debug.LogError("Thiếu Resources/NetworkManager.prefab, tạo bằng menu Tools/Network/Create NetworkManager Prefab");
                    return false;
                }
                manager = Instantiate(prefab).GetComponent<NetworkManager>();
            }
            Subscribe();
            return true;
        }

        // Danh sách IP LAN (IPv4, không loopback) của máy để host đọc cho người khác nhập.
        public static List<string> GetLocalAddresses()
        {
            return LanDiscovery.GetLocalAddresses();
        }

        public void Host()
        {
            if (!EnsureManager()) return;
            if (IsHost || connecting || wasConnected) Leave();
            IsHost = true;
            connecting = true;
            manager.ServerManager.StartConnection(port);
            manager.ClientManager.StartConnection("127.0.0.1", port);
        }

        // Nhận "ip", "ip:port" hoặc tên máy.
        public void Join(string address)
        {
            if (!EnsureManager()) return;
            if (IsHost || connecting || wasConnected) Leave();
            ParseAddress(address, port, out var host, out var hostPort);
            IsHost = false;
            connecting = true;
            manager.ClientManager.StartConnection(host, hostPort);
        }

        static void ParseAddress(string input, ushort defaultPort, out string host, out ushort port)
        {
            host = string.IsNullOrWhiteSpace(input) ? "127.0.0.1" : input.Trim();
            port = defaultPort;
            int colon = host.IndexOf(':');
            // chỉ tách khi có đúng một dấu ':' (IPv4 hoặc tên máy)
            if (colon <= 0 || colon != host.LastIndexOf(':')) return;
            if (ushort.TryParse(host.Substring(colon + 1), out var parsed) && parsed > 0) port = parsed;
            host = host.Substring(0, colon);
        }

        // Rời phòng hoặc trận một cách sạch sẽ (nút Thoát). Chủ phòng rời thì phòng đóng.
        public void Leave()
        {
            bool host = IsHost;
            ResetState();
            if (manager == null) return;
            manager.ClientManager.StopConnection();
            if (host) manager.ServerManager.StopConnection(true);
        }

        // Tên cũ, giữ cho các chỗ đã gọi.
        public void Disconnect()
        {
            Leave();
        }

        void ResetState()
        {
            bool wasInMatch = InMatch;
            IsHost = false;
            InMatch = false;
            hasPendingStart = false;
            connecting = false;
            wasConnected = false;
            LocalTeam = -1;
            PlayerCount = 0;
            CurrentTurn = 0;
            hostTurn = 0;
            turnTimer = 0f;
            hostWaiting = false;
            hostSlowTeam = -1;
            IsWaitingForPlayers = false;
            WaitingTeam = -1;
            teamByConnection.Clear();
            ackedTurn.Clear();
            lastProgress.Clear();
            pending.Clear();
            checksums.Clear();
            incoming.Clear();
            Array.Clear(commandCounts, 0, commandCounts.Length);
            LobbyChanged?.Invoke();
            if (wasInMatch) MatchEnded?.Invoke();
        }

        void Subscribe()
        {
            manager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            manager.ServerManager.OnAuthenticationResult += OnAuthenticationResult;
            manager.ServerManager.RegisterBroadcast<CommandMessage>(OnServerCommand);
            manager.ServerManager.RegisterBroadcast<ChecksumMessage>(OnServerChecksum);
            manager.ServerManager.RegisterBroadcast<AckMessage>(OnServerAck);
            manager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            manager.ClientManager.RegisterBroadcast<LobbyMessage>(OnClientLobby);
            manager.ClientManager.RegisterBroadcast<StartMatchMessage>(OnClientStart);
            manager.ClientManager.RegisterBroadcast<TurnMessage>(OnClientTurn);
            manager.ClientManager.RegisterBroadcast<DesyncMessage>(OnClientDesync);
            manager.ClientManager.RegisterBroadcast<PauseMessage>(OnClientPause);
            manager.ClientManager.RegisterBroadcast<EndMatchMessage>(OnClientEndMatch);
        }

        void Unsubscribe()
        {
            if (manager == null) return;
            manager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            manager.ServerManager.OnAuthenticationResult -= OnAuthenticationResult;
            manager.ServerManager.UnregisterBroadcast<CommandMessage>(OnServerCommand);
            manager.ServerManager.UnregisterBroadcast<ChecksumMessage>(OnServerChecksum);
            manager.ServerManager.UnregisterBroadcast<AckMessage>(OnServerAck);
            manager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            manager.ClientManager.UnregisterBroadcast<LobbyMessage>(OnClientLobby);
            manager.ClientManager.UnregisterBroadcast<StartMatchMessage>(OnClientStart);
            manager.ClientManager.UnregisterBroadcast<TurnMessage>(OnClientTurn);
            manager.ClientManager.UnregisterBroadcast<DesyncMessage>(OnClientDesync);
            manager.ClientManager.UnregisterBroadcast<PauseMessage>(OnClientPause);
            manager.ClientManager.UnregisterBroadcast<EndMatchMessage>(OnClientEndMatch);
        }

        // ---- Host ----

        // Chỉ gửi được broadcast sau khi client đã xác thực, nên đặt đội ở bước này.
        void OnAuthenticationResult(NetworkConnection connection, bool passed)
        {
            if (!passed) return;
            if (!IsHost || InMatch || teamByConnection.Count >= MaxTeams)
            {
                connection.Disconnect(true);
                return;
            }
            teamByConnection[connection.ClientId] = (byte)teamByConnection.Count;
            BroadcastLobby();
        }

        void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started) return;

            if (!InMatch)
            {
                if (!teamByConnection.Remove(connection.ClientId)) return;
                CompactTeams();
                BroadcastLobby();
            }
            else
            {
                HandlePlayerLeft(connection.ClientId);
            }
        }

        // Dồn lại số đội cho liền mạch theo thứ tự id kết nối.
        void CompactTeams()
        {
            var ids = new List<int>(teamByConnection.Keys);
            ids.Sort();
            byte next = 0;
            foreach (var id in ids) teamByConnection[id] = next++;
        }

        // Người chơi rời giữa trận: chèn lệnh PlayerLeft để mọi máy cùng chuyển đội đó cho máy tính. Gọi lặp lại không sao.
        void HandlePlayerLeft(int connectionId)
        {
            if (!teamByConnection.TryGetValue(connectionId, out var team)) return;
            teamByConnection.Remove(connectionId);
            ackedTurn.Remove(connectionId);
            lastProgress.Remove(connectionId);
            pending.Add(new LockstepCommand { Team = team, Type = BattleCommand.PlayerLeft });
            Debug.LogWarning($"Client {connectionId} (đội {team}) rời giữa trận");
        }

        void KickPlayer(int connectionId)
        {
            Debug.LogWarning($"Client {connectionId} tụt quá lâu, ngắt kết nối");
            HandlePlayerLeft(connectionId);
            if (manager.ServerManager.Clients.TryGetValue(connectionId, out var connection)) connection.Disconnect(true);
        }

        void BroadcastLobby()
        {
            foreach (var pair in teamByConnection)
            {
                if (!manager.ServerManager.Clients.TryGetValue(pair.Key, out var connection)) continue;
                manager.ServerManager.Broadcast(connection,
                    new LobbyMessage { PlayerCount = (byte)teamByConnection.Count, LocalTeam = pair.Value });
            }
        }

        // Host bắt đầu trận từ phòng chờ.
        public void StartMatch()
        {
            if (!IsHost || InMatch || manager == null || teamByConnection.Count == 0) return;
            BeginMatch();
        }

        // Host bắt đầu lại trận với những người đang ở phòng: đặt lại lượt, hàng đợi, checksum.
        public void RestartMatch()
        {
            if (!IsHost || manager == null || teamByConnection.Count == 0) return;
            CompactTeams();
            BeginMatch();
        }

        // Host kết thúc trận, mọi máy về phòng chờ và người mới có thể vào lại.
        public void ReturnToLobby()
        {
            if (!IsHost || !InMatch || manager == null) return;
            InMatch = false;
            hostWaiting = false;
            hostSlowTeam = -1;
            pending.Clear();
            checksums.Clear();
            ackedTurn.Clear();
            lastProgress.Clear();
            manager.ServerManager.Broadcast(new EndMatchMessage { Reason = 0 });
            // phía host đã tắt cờ InMatch nên tự dọn và báo sự kiện ở đây
            hasPendingStart = false;
            CurrentTurn = 0;
            incoming.Clear();
            IsWaitingForPlayers = false;
            WaitingTeam = -1;
            MatchEnded?.Invoke();
            CompactTeams();
            BroadcastLobby();
            GameFlow.ReturnToMultiplayerLobby();
        }

        void BeginMatch()
        {
            InMatch = true;
            hostTurn = 0;
            turnTimer = 0f;
            hostWaiting = false;
            hostSlowTeam = -1;
            pending.Clear();
            checksums.Clear();
            Array.Clear(commandCounts, 0, commandCounts.Length);
            ackedTurn.Clear();
            lastProgress.Clear();
            float now = Time.unscaledTime;
            foreach (var id in teamByConnection.Keys)
            {
                ackedTurn[id] = 0;
                lastProgress[id] = now;
            }

            int seed = Environment.TickCount;
            // đội nào không có người chơi thì máy tính điều khiển
            int humanMask = 0;
            foreach (var team in teamByConnection.Values) humanMask |= 1 << team;
            byte aiMask = (byte)(((1 << MaxTeams) - 1) & ~humanMask);
            foreach (var pair in teamByConnection)
            {
                if (!manager.ServerManager.Clients.TryGetValue(pair.Key, out var connection)) continue;
                manager.ServerManager.Broadcast(connection,
                    new StartMatchMessage { Seed = seed, TeamCount = (byte)teamByConnection.Count, LocalTeam = pair.Value, AiMask = aiMask });
            }
        }

        // Chỉ nhận lệnh hợp lệ: đúng người trong trận, kiểu lệnh cho phép, không vượt hạn mức mỗi lượt.
        void OnServerCommand(NetworkConnection connection, CommandMessage message, Channel channel)
        {
            if (!InMatch || !teamByConnection.TryGetValue(connection.ClientId, out var team)) return;
            var command = message.Command;
            // kiểu từ PlayerLeft trở lên là lệnh riêng của host
            if (command.Type == 0 || command.Type >= BattleCommand.PlayerLeft) return;
            if (team >= MaxTeams || commandCounts[team] >= MaxCommandsPerTeamPerTurn) return;
            commandCounts[team]++;
            command.Team = team; // không tin team do client gửi
            pending.Add(command);
        }

        void OnServerChecksum(NetworkConnection connection, ChecksumMessage message, Channel channel)
        {
            if (!InMatch || !teamByConnection.ContainsKey(connection.ClientId)) return;
            if (!checksums.TryGetValue(message.Turn, out var expected))
            {
                checksums[message.Turn] = message.Hash;
                if (checksums.Count > 64) checksums.Clear();
                return;
            }
            if (expected != message.Hash)
                manager.ServerManager.Broadcast(new DesyncMessage { Turn = message.Turn });
        }

        void OnServerAck(NetworkConnection connection, AckMessage message, Channel channel)
        {
            if (!ackedTurn.TryGetValue(connection.ClientId, out var old)) return;
            uint turn = message.Turn > hostTurn ? hostTurn : message.Turn;
            if (turn <= old) return;
            ackedTurn[connection.ClientId] = turn;
            lastProgress[connection.ClientId] = Time.unscaledTime;
        }

        // Kiểm tra máy chậm nhất: tụt nhiều thì dừng phát lượt, tụt quá lâu thì đuổi.
        void UpdateLag()
        {
            uint worst = 0;
            int slowId = -1;
            float now = Time.unscaledTime;
            int ownId = manager.ClientManager.Connection != null ? manager.ClientManager.Connection.ClientId : -1;
            kickList.Clear();
            foreach (var pair in ackedTurn)
            {
                uint lag = hostTurn - pair.Value;
                if (lag > worst)
                {
                    worst = lag;
                    slowId = pair.Key;
                }
                if (lag > PauseLagTurns && pair.Key != ownId && now - lastProgress[pair.Key] > StuckSeconds)
                    kickList.Add(pair.Key);
            }
            foreach (var id in kickList) KickPlayer(id);

            bool waiting = worst > (hostWaiting ? ResumeLagTurns : PauseLagTurns);
            int slowTeam = -1;
            if (waiting && slowId >= 0 && teamByConnection.TryGetValue(slowId, out var team)) slowTeam = team;
            if (waiting == hostWaiting && slowTeam == hostSlowTeam) return;

            hostWaiting = waiting;
            hostSlowTeam = slowTeam;
            manager.ServerManager.Broadcast(new PauseMessage { Waiting = waiting, SlowTeam = (byte)(slowTeam < 0 ? 255 : slowTeam) });
        }

        void TickHost(float dt)
        {
            UpdateLag();
            if (hostWaiting)
            {
                turnTimer = 0f;
                return;
            }

            turnTimer += dt;
            while (turnTimer >= TurnInterval)
            {
                turnTimer -= TurnInterval;
                int count = Math.Min(pending.Count, MaxCommandsPerTurn);
                var commands = count == 0 ? Array.Empty<LockstepCommand>() : pending.GetRange(0, count).ToArray();
                pending.RemoveRange(0, count);
                Array.Clear(commandCounts, 0, commandCounts.Length);
                manager.ServerManager.Broadcast(new TurnMessage { Turn = hostTurn++, Commands = commands });
            }
        }

        // ---- Client ----

        void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                wasConnected = true;
                connecting = false;
                return;
            }
            if (args.ConnectionState != LocalConnectionState.Stopped) return;
            // đã chủ động rời thì cờ đã xóa, bỏ qua
            if (!wasConnected && !connecting) return;

            bool joined = wasConnected;
            bool host = IsHost;
            ResetState();
            if (host) manager.ServerManager.StopConnection(true);
            Disconnected?.Invoke(joined ? (host ? "Phòng đã đóng" : "Mất kết nối với chủ phòng") : "Không thể kết nối tới phòng");
        }

        void OnClientLobby(LobbyMessage message, Channel channel)
        {
            PlayerCount = message.PlayerCount;
            LocalTeam = message.LocalTeam;
            LobbyChanged?.Invoke();
        }

        void OnClientStart(StartMatchMessage message, Channel channel)
        {
            InMatch = true;
            CurrentTurn = 0;
            incoming.Clear();
            IsWaitingForPlayers = false;
            WaitingTeam = -1;
            LocalTeam = message.LocalTeam;
            hasPendingStart = true;
            pendingStartMessage = message;
            MatchStarted?.Invoke();
            GameFlow.EnterMultiplayerMatch();
        }

        void OnClientTurn(TurnMessage message, Channel channel)
        {
            incoming.Enqueue(message);
        }

        void OnClientDesync(DesyncMessage message, Channel channel)
        {
            Debug.LogError($"Desync ở lượt {message.Turn}");
            DesyncDetected?.Invoke(message.Turn);
        }

        void OnClientPause(PauseMessage message, Channel channel)
        {
            IsWaitingForPlayers = message.Waiting;
            WaitingTeam = message.Waiting && message.SlowTeam != 255 ? message.SlowTeam : -1;
            WaitingChanged?.Invoke();
        }

        // Host kết thúc trận: bỏ các lượt còn lại, về phòng chờ.
        void OnClientEndMatch(EndMatchMessage message, Channel channel)
        {
            bool wasInMatch = InMatch;
            InMatch = false;
            hasPendingStart = false;
            CurrentTurn = 0;
            incoming.Clear();
            IsWaitingForPlayers = false;
            WaitingTeam = -1;
            if (wasInMatch) MatchEnded?.Invoke();
            LobbyChanged?.Invoke();
            GameFlow.ReturnToMultiplayerLobby();
        }

        public void SendCommand(LockstepCommand command)
        {
            if (!InMatch || manager == null) return;
            manager.ClientManager.Broadcast(new CommandMessage { Command = command });
        }

        void Update()
        {
            if (manager == null) return;
            if (!InMatch || Simulation == null) return;
            if (hasPendingStart)
            {
                hasPendingStart = false;
                Simulation.StartMatch(pendingStartMessage.Seed, pendingStartMessage.TeamCount,
                    pendingStartMessage.LocalTeam, pendingStartMessage.AiMask);
            }
            if (IsHost) TickHost(Time.unscaledDeltaTime);

            // chạy nhiều lượt mỗi frame trong ngân sách thời gian để bắt kịp khi bị trễ mà không khựng
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < MaxTurnsPerFrame && incoming.Count > 0; i++)
            {
                var turn = incoming.Dequeue();
                Simulation.ApplyTurn(turn.Turn, turn.Commands ?? Array.Empty<LockstepCommand>());
                CurrentTurn = turn.Turn + 1;

                if (turn.Turn % ChecksumEveryTurns == 0)
                {
                    uint hash = Simulation.ComputeChecksum();
                    manager.ClientManager.Broadcast(new ChecksumMessage { Turn = turn.Turn, Hash = hash });
                    if (Debug.isDebugBuild && turn.Turn % 100 == 0) Debug.Log($"[Lockstep] lượt {turn.Turn} checksum {hash}");
                }
                if (CurrentTurn % AckEveryTurns == 0)
                    manager.ClientManager.Broadcast(new AckMessage { Turn = CurrentTurn });

                if (watch.Elapsed.TotalMilliseconds >= TurnBudgetMs) break;
            }
        }
    }
}
