using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SurvivalRts
{
    // Panel multiplayer (LAN hoặc Tham gia máy chủ): mở ngay trên scene menu, chỉ chuyển sang scene battle khi bấm Tạo phòng / Vào phòng.
    public class BattleMainMenuScreen
    {
        const int MaxRoomButtons = 6;
        const int MaxServers = 8;
        const string ServersKey = "online.servers";

        readonly PlayMode mode;
        readonly LanDiscovery discovery;
        readonly Action onBack;
        readonly Color backdropColor;
        // Chơi online thì không có danh sách phòng LAN, chỉ nhập IP công khai
        readonly bool showRooms;
        RectTransform root;
        TMP_InputField addressInput;
        TMP_Text statusLabel;
        RectTransform roomList;
        string roomSignature = "";
        // Danh sách máy chủ đã lưu của chế độ online (tên|địa chỉ), nhớ qua PlayerPrefs
        readonly List<(string name, string address)> servers = new List<(string, string)>();
        int selectedServer = -1;
        RectTransform serverList;
        TMP_InputField nameInput;
        Button joinSelectedButton;

        public GameObject Root => root.gameObject;

        public BattleMainMenuScreen(PlayMode mode, LanDiscovery discovery, Action onBack, Color? backdrop = null)
        {
            this.mode = mode;
            this.discovery = discovery;
            this.onBack = onBack ?? GameFlow.BackToMenu;
            backdropColor = backdrop ?? BattleUiKit.BackdropColor;
            showRooms = mode != PlayMode.Online;
        }

        public BattleMainMenuScreen(BattleUiController owner, LanDiscovery discovery)
            : this(GameFlow.Mode == PlayMode.Online ? PlayMode.Online : PlayMode.Lan, discovery, GameFlow.BackToMenu, BattleUiKit.WorldColor)
        {
        }

        public void Build(Transform parent)
        {
            var overlay = BattleUiKit.Background(parent, showRooms ? "LanMultiplayerPanel" : "OnlineServerPanel", backdropColor);
            root = overlay.rectTransform;
            if (!showRooms)
            {
                BuildServerBrowser();
                return;
            }

            var panel = BattleUiKit.Panel(root, "Panel", BattleUiKit.PanelColor);
            BattleUiKit.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(640f, 0f), Vector2.zero);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            BattleUiKit.Vertical(panel.rectTransform, 16f, 24);

            BattleUiKit.Heading(panel.transform, "Đại chiến 4 đội - LAN", 30f);

            BattleUiKit.Size(BattleUiKit.PushButton(panel.transform, "Tạo phòng", OnHost, -1f, 56f, 22f, true), -1f, 56f);

            var joinRow = BattleUiKit.Node(panel.transform, "JoinRow");
            BattleUiKit.Size(joinRow, -1f, 48f);
            var horizontal = BattleUiKit.Horizontal(joinRow, 12f, 0);
            horizontal.childForceExpandWidth = false;
            addressInput = BattleUiKit.Input(joinRow, "127.0.0.1", "Địa chỉ IP hoặc IP:cổng");
            BattleUiKit.Size(addressInput, -1f, 48f, 1f);
            BattleUiKit.PushButton(joinRow, "Vào phòng", OnJoin, 170f, 48f);

            var listTitle = BattleUiKit.Label(panel.transform, "Phòng trong mạng LAN", 18f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor);
            BattleUiKit.Size(listTitle, -1f, 26f);

            roomList = BattleUiKit.Node(panel.transform, "RoomList");
            BattleUiKit.Vertical(roomList, 8f, 0);
            var none = BattleUiKit.Placeholder(roomList, "Chưa thấy phòng nào", 48f, 18f);
            BattleUiKit.Size(none, -1f, 48f);

            statusLabel = BattleUiKit.Label(panel.transform, "", 18f, TextAlignmentOptions.Center);
            BattleUiKit.Size(statusLabel, -1f, 30f);

            BattleUiKit.Separator(panel.transform);
            var actions = BattleUiKit.Node(panel.transform, "Actions");
            BattleUiKit.Size(actions, -1f, 48f);
            BattleUiKit.Horizontal(actions, 12f, 0, TextAnchor.MiddleLeft);
            BattleUiKit.PushButton(actions, "Quay lại", () => onBack?.Invoke(), 160f, 48f);
        }

        // Giao diện "Tham gia máy chủ": bảng máy chủ đã lưu, thêm / xoá, vào máy chủ đang chọn hoặc tạo phòng.
        void BuildServerBrowser()
        {
            LoadServers();

            var panel = BattleUiKit.Panel(root, "Panel", BattleUiKit.PanelColor);
            BattleUiKit.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(860f, 0f), Vector2.zero);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            BattleUiKit.Vertical(panel.rectTransform, 12f, 24);

            BattleUiKit.Heading(panel.transform, "Tham gia máy chủ", 30f);

            var header = BattleUiKit.Node(panel.transform, "ColumnHeader");
            BattleUiKit.Size(header, -1f, 28f);
            var headerLayout = BattleUiKit.Horizontal(header, 10f, 0);
            headerLayout.padding = new RectOffset(16, 16, 0, 0);
            BattleUiKit.Size(BattleUiKit.Label(header, "Tên máy chủ", 18f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor), -1f, -1f, 1f);
            BattleUiKit.Size(BattleUiKit.Label(header, "Địa chỉ", 18f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor), -1f, -1f, 1f);

            serverList = BattleUiKit.Node(panel.transform, "ServerList");
            BattleUiKit.Vertical(serverList, 6f, 0);

            BattleUiKit.Separator(panel.transform);
            var addRow = BattleUiKit.Node(panel.transform, "AddRow");
            BattleUiKit.Size(addRow, -1f, 48f);
            BattleUiKit.Horizontal(addRow, 10f, 0).childForceExpandWidth = false;
            nameInput = BattleUiKit.Input(addRow, "", "Tên máy chủ");
            BattleUiKit.Size(nameInput, -1f, 48f, 1f);
            addressInput = BattleUiKit.Input(addRow, "", "Địa chỉ IP hoặc IP:cổng");
            BattleUiKit.Size(addressInput, -1f, 48f, 1f);
            BattleUiKit.PushButton(addRow, "Thêm", OnAddServer, 110f, 48f);

            var hint = BattleUiKit.Label(panel.transform,
                "Chưa có máy chủ trung tâm: nhập IP công khai của chủ phòng rồi lưu lại. Chủ phòng cần mở cổng UDP 7770 hoặc cùng dùng VPN.",
                16f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor);
            BattleUiKit.Size(hint, -1f, 44f);

            statusLabel = BattleUiKit.Label(panel.transform, "", 18f, TextAlignmentOptions.Center);
            BattleUiKit.Size(statusLabel, -1f, 28f);

            BattleUiKit.Separator(panel.transform);
            var actions = BattleUiKit.Node(panel.transform, "Actions");
            BattleUiKit.Size(actions, -1f, 52f);
            BattleUiKit.Horizontal(actions, 12f, 0, TextAnchor.MiddleLeft);
            BattleUiKit.PushButton(actions, "Quay lại", () => onBack?.Invoke(), 150f, 52f);
            var spacer = BattleUiKit.Node(actions, "Spacer");
            BattleUiKit.Size(spacer, -1f, 52f, 1f);
            BattleUiKit.PushButton(actions, "Xoá", OnRemoveServer, 110f, 52f);
            BattleUiKit.PushButton(actions, "Tạo phòng", OnHost, 160f, 52f);
            joinSelectedButton = BattleUiKit.PushButton(actions, "Vào máy chủ", OnJoinSelected, 190f, 52f, 20f, true);

            RebuildServerRows();
        }

        // Dựng lại các dòng máy chủ 2 cột khớp tiêu đề, dòng đang chọn tô nút chính.
        void RebuildServerRows()
        {
            for (int i = serverList.childCount - 1; i >= 0; i--) Object.Destroy(serverList.GetChild(i).gameObject);
            if (servers.Count == 0)
            {
                var none = BattleUiKit.Placeholder(serverList, "Chưa có máy chủ nào, hãy thêm bên dưới", 56f, 18f);
                BattleUiKit.Size(none, -1f, 56f);
            }
            for (int i = 0; i < servers.Count; i++)
            {
                int index = i;
                bool active = i == selectedServer;
                var row = BattleUiKit.PushButton(serverList, servers[i].name,
                    () => { selectedServer = index; RebuildServerRows(); }, -1f, 44f, 18f, active);
                row.name = "Server - " + servers[i].name;
                var rowLayout = BattleUiKit.Horizontal((RectTransform)row.transform, 10f, 0);
                rowLayout.padding = new RectOffset(16, 16, 0, 0);
                row.Label.alignment = TextAlignmentOptions.Left;
                row.Label.margin = Vector4.zero;
                BattleUiKit.Size(row.Label, -1f, -1f, 1f);
                var addrLabel = BattleUiKit.Label(row.transform, servers[i].address, 18f, TextAlignmentOptions.Left,
                    active ? BattleUiKit.PrimaryTextColor : BattleUiKit.TextColor);
                BattleUiKit.Size(addrLabel, -1f, -1f, 1f);
            }
            if (joinSelectedButton != null) joinSelectedButton.interactable = selectedServer >= 0;
        }

        void OnAddServer()
        {
            string address = addressInput.text.Trim();
            if (address.Length == 0 || servers.Count >= MaxServers) return;
            string name = nameInput.text.Trim();
            servers.Add((name.Length > 0 ? name : address, address));
            selectedServer = servers.Count - 1;
            nameInput.text = "";
            addressInput.text = "";
            SaveServers();
            RebuildServerRows();
        }

        void OnRemoveServer()
        {
            if (selectedServer < 0 || selectedServer >= servers.Count) return;
            servers.RemoveAt(selectedServer);
            selectedServer = -1;
            SaveServers();
            RebuildServerRows();
        }

        void OnJoinSelected()
        {
            if (selectedServer >= 0 && selectedServer < servers.Count) JoinAddress(servers[selectedServer].address);
        }

        void LoadServers()
        {
            servers.Clear();
            foreach (var line in PlayerPrefs.GetString(ServersKey, "").Split('\n'))
            {
                var parts = line.Split(new[] { '|' }, 2);
                if (parts.Length == 2 && parts[1].Length > 0 && servers.Count < MaxServers) servers.Add((parts[0], parts[1]));
            }
        }

        void SaveServers()
        {
            var builder = new StringBuilder();
            foreach (var server in servers) builder.Append(server.name.Replace('|', ' ')).Append('|').Append(server.address).Append('\n');
            PlayerPrefs.SetString(ServersKey, builder.ToString());
            PlayerPrefs.Save();
        }

        // Hiện panel và bắt đầu lắng nghe phòng LAN nếu đang ở chế độ LAN.
        public void Show(string notice = "")
        {
            SetActive(true);
            if (statusLabel != null) BattleUiKit.SetText(statusLabel, notice ?? "");
            if (showRooms) RefreshRooms();
        }

        public void Hide()
        {
            SetActive(false);
        }

        // Bật hoặc tắt màn hình, kèm việc dò phòng LAN chỉ khi đang hiện.
        public void SetActive(bool active)
        {
            root.gameObject.SetActive(active);
            if (discovery == null) return;
            if (active && showRooms)
            {
                discovery.enabled = true;
                discovery.StartListening();
            }
            else if (!active && showRooms)
            {
                discovery.StopListening();
            }
        }

        void OnHost()
        {
            GameFlow.HostMultiplayer(mode);
        }

        void OnJoin()
        {
            JoinAddress(addressInput.text.Trim());
        }

        void JoinAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            GameFlow.JoinMultiplayer(mode, address.Trim());
        }

        public void Refresh(string status = null)
        {
            if (status != null && statusLabel != null) BattleUiKit.SetText(statusLabel, status);
            RefreshRooms();
        }

        // Dựng lại danh sách nút phòng LAN khi nội dung đổi.
        void RefreshRooms()
        {
            if (discovery == null || roomList == null) return;

            var rooms = new List<(string name, int players, string displayAddress, string joinAddress)>();
            var builder = new StringBuilder();
            foreach (var room in discovery.Rooms)
            {
                if (rooms.Count >= MaxRoomButtons) break;
                string joinAddr = room.Port == 7770 ? room.Address : room.JoinAddress;
                rooms.Add((room.Name, room.PlayerCount, room.Address, joinAddr));
                builder.Append(room.Name).Append('|').Append(room.PlayerCount).Append('|').Append(joinAddr).Append(';');
            }
            string signature = builder.ToString();
            if (signature == roomSignature) return;
            roomSignature = signature;

            for (int i = roomList.childCount - 1; i >= 0; i--) Object.Destroy(roomList.GetChild(i).gameObject);
            if (rooms.Count == 0)
            {
                var none = BattleUiKit.Placeholder(roomList, "Chưa thấy phòng nào", 48f, 18f);
                BattleUiKit.Size(none, -1f, 48f);
                return;
            }
            foreach (var room in rooms)
            {
                string joinAddress = room.joinAddress;
                BattleUiKit.PushButton(roomList, $"{room.name}  ({room.players}/{BattleSimulation.TeamCount})  {room.displayAddress}",
                    () => JoinAddress(joinAddress), -1f, 44f, 18f);
            }
        }
    }
}
