using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace SurvivalRts
{
    // Một phòng tìm thấy trong mạng LAN.
    public class LanRoom
    {
        public string Name;
        public string Address; // IP của host
        public ushort Port; // cổng game
        public int PlayerCount;
        public int MaxPlayers;
        internal float LastSeen;

        // chuỗi đưa thẳng vào LockstepNetwork.Join
        public string JoinAddress => Address + ":" + Port;
    }

    // Tìm phòng trong LAN bằng UDP broadcast: host phát gói nhỏ mỗi giây khi đang ở phòng chờ,
    // client bật lắng nghe để lấy danh sách phòng. Không đi qua internet/NAT.
    public class LanDiscovery : MonoBehaviour
    {
        public static LanDiscovery Instance { get; private set; }

        const string Magic = "SRTS1";
        const float BroadcastInterval = 1f;
        const float RoomTimeout = 3f;

        [SerializeField] int discoveryPort = 7771;

        public string RoomName { get; set; } = Environment.MachineName;
        public bool IsListening => listener != null;
        public IReadOnlyList<LanRoom> Rooms => rooms;

        public event Action RoomsChanged;

        readonly List<LanRoom> rooms = new List<LanRoom>();
        UdpClient listener;
        UdpClient sender;
        List<IPEndPoint> targets;
        float broadcastTimer;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDisable()
        {
            StopListening();
            CloseSender();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Bắt đầu nghe phòng của các host trong LAN.
        public void StartListening()
        {
            if (listener != null) return;
            try
            {
                listener = new UdpClient(AddressFamily.InterNetwork);
                listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Không mở được cổng tìm phòng {discoveryPort}: {e.Message}");
                CloseListener();
            }
        }

        public void StopListening()
        {
            CloseListener();
            if (rooms.Count == 0) return;
            rooms.Clear();
            RoomsChanged?.Invoke();
        }

        void CloseListener()
        {
            if (listener == null) return;
            try { listener.Close(); }
            catch (Exception) { }
            listener = null;
        }

        void CloseSender()
        {
            if (sender == null) return;
            try { sender.Close(); }
            catch (Exception) { }
            sender = null;
            targets = null;
        }

        void Update()
        {
            UpdateBroadcast();
            UpdateListening();
        }

        // Host ở phòng chờ thì phát thông tin phòng, vào trận hoặc rời phòng thì ngừng.
        void UpdateBroadcast()
        {
            var net = LockstepNetwork.Instance;
            if (net == null || !net.IsHost || net.InMatch || !net.IsConnected)
            {
                CloseSender();
                broadcastTimer = 0f;
                return;
            }

            broadcastTimer -= Time.unscaledDeltaTime;
            if (broadcastTimer > 0f) return;
            broadcastTimer = BroadcastInterval;

            try
            {
                if (sender == null)
                {
                    sender = new UdpClient(AddressFamily.InterNetwork);
                    sender.EnableBroadcast = true;
                    targets = BuildTargets();
                }
                string text = $"{Magic}|{net.Port}|{net.PlayerCount}|{LockstepNetwork.MaxTeams}|{RoomName}";
                byte[] data = Encoding.UTF8.GetBytes(text);
                foreach (var target in targets) sender.Send(data, data.Length, target);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Phát thông tin phòng lỗi: {e.Message}");
                CloseSender();
            }
        }

        // Địa chỉ broadcast chung, broadcast riêng từng mạng con và máy này (để tìm phòng cùng máy).
        List<IPEndPoint> BuildTargets()
        {
            var list = new List<IPEndPoint>
            {
                new IPEndPoint(IPAddress.Broadcast, discoveryPort),
                new IPEndPoint(IPAddress.Loopback, discoveryPort),
            };
            foreach (var (address, mask) in GetInterfaces())
            {
                var a = address.GetAddressBytes();
                var m = mask.GetAddressBytes();
                var b = new byte[4];
                for (int i = 0; i < 4; i++) b[i] = (byte)(a[i] | ~m[i]);
                list.Add(new IPEndPoint(new IPAddress(b), discoveryPort));
            }
            return list;
        }

        void UpdateListening()
        {
            if (listener == null) return;

            bool changed = false;
            try
            {
                while (listener.Available > 0)
                {
                    IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = listener.Receive(ref from);
                    if (ParsePacket(data, from)) changed = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Nhận thông tin phòng lỗi: {e.Message}");
                CloseListener();
            }

            float now = Time.unscaledTime;
            for (int i = rooms.Count - 1; i >= 0; i--)
            {
                if (now - rooms[i].LastSeen <= RoomTimeout) continue;
                rooms.RemoveAt(i);
                changed = true;
            }
            if (changed) RoomsChanged?.Invoke();
        }

        // Gói: SRTS1|cổng|số người|tối đa|tên phòng. Trả true nếu danh sách phòng thay đổi.
        bool ParsePacket(byte[] data, IPEndPoint from)
        {
            string[] parts = Encoding.UTF8.GetString(data).Split(new[] { '|' }, 5);
            if (parts.Length < 5 || parts[0] != Magic) return false;
            if (!ushort.TryParse(parts[1], out var gamePort)) return false;
            if (!int.TryParse(parts[2], out var players)) return false;
            if (!int.TryParse(parts[3], out var maxPlayers)) return false;

            string address = from.Address.ToString();
            LanRoom room = null;
            foreach (var r in rooms)
            {
                if (r.Address != address || r.Port != gamePort) continue;
                room = r;
                break;
            }

            bool changed = false;
            if (room == null)
            {
                room = new LanRoom { Address = address, Port = gamePort };
                rooms.Add(room);
                changed = true;
            }
            if (room.Name != parts[4] || room.PlayerCount != players || room.MaxPlayers != maxPlayers) changed = true;
            room.Name = parts[4];
            room.PlayerCount = players;
            room.MaxPlayers = maxPlayers;
            room.LastSeen = Time.unscaledTime;
            return changed;
        }

        static List<(IPAddress address, IPAddress mask)> GetInterfaces()
        {
            var result = new List<(IPAddress, IPAddress)>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var info in nic.GetIPProperties().UnicastAddresses)
                    {
                        var ip = info.Address;
                        if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip)) continue;
                        // bỏ địa chỉ tự cấp 169.254.x.x
                        var bytes = ip.GetAddressBytes();
                        if (bytes[0] == 169 && bytes[1] == 254) continue;
                        if (info.IPv4Mask == null) continue;
                        result.Add((ip, info.IPv4Mask));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Không đọc được card mạng: {e.Message}");
            }
            return result;
        }

        // Danh sách IP LAN (IPv4, không loopback) của máy này.
        public static List<string> GetLocalAddresses()
        {
            var list = new List<string>();
            foreach (var (address, _) in GetInterfaces()) list.Add(address.ToString());
            return list;
        }
    }
}
