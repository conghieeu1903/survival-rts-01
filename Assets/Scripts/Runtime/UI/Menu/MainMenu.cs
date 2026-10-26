using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Menu chính của scene menu: chứa cả Panel LAN, Panel Tham gia máy chủ và Phòng chờ ngay trên cùng một scene;
    // chỉ chuyển scene khi bấm Chơi offline hoặc khi chủ phòng bấm Bắt đầu trong Phòng chờ.
    public class MainMenu : MonoBehaviour
    {
        const float RefreshSeconds = 0.15f;
        const float ConnectTimeoutSeconds = 12f;
        const float IgnoreDisconnectSeconds = 1f;

        [Header("Giao diện")]
        [Tooltip("Font TMP có đủ tiếng Việt (Dynamic); để trống thì dùng font mặc định của TMP")]
        [SerializeField] TMP_FontAsset fontAsset;

        [Tooltip("Tên game hiện trong ô logo")]
        [SerializeField] string title = "SURVIVAL RTS";

        [Tooltip("Dòng phiên bản nhỏ ở cuối thanh bên")]
        [SerializeField] string versionNote = "v0.1";

        LanDiscovery lanDiscovery;
        LockstepNetwork subscribedNet;
        GameObject exitDialog;
        BattleMainMenuScreen lanPanel;
        BattleMainMenuScreen onlinePanel;
        BattleLobbyScreen lobbyPanel;
        SaveSlotsPanel saveSlots;
        SettingsPanel settings;
        CreditsPanel credits;
        GameObject openPanel;
        Action closeOpenPanel;

        string notice = "";
        string connectingText = "";
        float connectingSince;
        float ignoreDisconnectUntil;
        float nextRefresh;

        void Awake()
        {
            BattleUiKit.Font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            AudioDirector.EnsureInstance();
            EnsureEventSystem();

            var net = LockstepNetwork.EnsureInstance();
            lanDiscovery = net.GetComponent<LanDiscovery>();
            if (lanDiscovery == null) lanDiscovery = FindFirstObjectByType<LanDiscovery>();
            if (lanDiscovery == null) lanDiscovery = gameObject.AddComponent<LanDiscovery>();

            Build();
        }

        void Start()
        {
            Subscribe();
            GameFlow.ConsumeMenuState(out var openLobby, out var openMultiplayerPanel, out var mode, out var pendingNotice);
            notice = pendingNotice ?? "";

            var net = LockstepNetwork.Instance;
            if (openLobby || (net != null && !net.InMatch && net.LocalTeam >= 0))
            {
                OpenLobbyPanel();
            }
            else if (openMultiplayerPanel)
            {
                if (mode == PlayMode.Online) OpenOnlinePanel(notice);
                else OpenLanPanel(notice);
            }
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        void Subscribe()
        {
            var net = LockstepNetwork.Instance;
            if (net == null || net == subscribedNet) return;
            Unsubscribe();
            subscribedNet = net;
            net.LobbyChanged += OnLobbyChanged;
            net.Disconnected += OnDisconnected;
        }

        void Unsubscribe()
        {
            if (subscribedNet == null) return;
            subscribedNet.LobbyChanged -= OnLobbyChanged;
            subscribedNet.Disconnected -= OnDisconnected;
            subscribedNet = null;
        }

        // Dùng Input System UI module vì dự án chỉ dùng Input System.
        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        void Build()
        {
            var canvasObject = new GameObject("MenuCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var background = BattleUiKit.Background(canvasObject.transform, "Background", BattleUiKit.WorldColor);
            var backgroundLabel = BattleUiKit.Label(background.transform, "HÌNH NỀN", 28f, TextAlignmentOptions.Center, BattleUiKit.DimTextColor);
            backgroundLabel.rectTransform.offsetMin = new Vector2(520f, 0f);

            var sidebar = BattleUiKit.Node(background.transform, "Sidebar");
            sidebar.anchorMin = new Vector2(0f, 0f);
            sidebar.anchorMax = new Vector2(0f, 1f);
            sidebar.pivot = new Vector2(0f, 0.5f);
            sidebar.sizeDelta = new Vector2(380f, -280f);
            sidebar.anchoredPosition = new Vector2(80f, -60f);
            BattleUiKit.Vertical(sidebar, 12f, 0);

            BattleUiKit.Placeholder(sidebar, title, 176f, 30f);
            BattleUiKit.Size(BattleUiKit.Node(sidebar, "LogoGap"), -1f, 8f);

            BattleUiKit.PushButton(sidebar, "Chơi offline", GameFlow.StartOffline, -1f, 56f, 24f, true);
            BattleUiKit.PushButton(sidebar, "Chơi LAN", () => OpenLanPanel(), -1f, 56f, 24f);
            BattleUiKit.PushButton(sidebar, "Chơi online", () => OpenOnlinePanel(), -1f, 56f, 24f);
            BattleUiKit.PushButton(sidebar, "Bản lưu", () => OpenPanel(saveSlots.Root, saveSlots.Show, saveSlots.Hide), -1f, 56f, 24f);
            BattleUiKit.PushButton(sidebar, "Cài đặt", () => OpenPanel(settings.Root, settings.Show, settings.Hide), -1f, 56f, 24f);
            BattleUiKit.PushButton(sidebar, "Credits", () => OpenPanel(credits.Root, credits.Show, credits.Hide), -1f, 56f, 24f);
            BattleUiKit.PushButton(sidebar, "Thoát", () => exitDialog.SetActive(true), -1f, 56f, 24f);

            var spacer = BattleUiKit.Node(sidebar, "Spacer");
            spacer.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var version = BattleUiKit.Label(sidebar, versionNote, 14f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor);
            BattleUiKit.Size(version, -1f, 22f);

            exitDialog = BattleUiKit.ConfirmDialog(canvasObject.transform, "Thoát game?", "Tiến trình chưa lưu sẽ bị mất.",
                "Không", "Thoát", Quit, () => exitDialog.SetActive(false));
            exitDialog.SetActive(false);

            lanPanel = new BattleMainMenuScreen(PlayMode.Lan, lanDiscovery, CloseMultiplayerPanel);
            lanPanel.Build(canvasObject.transform);
            lanPanel.Hide();

            onlinePanel = new BattleMainMenuScreen(PlayMode.Online, null, CloseMultiplayerPanel);
            onlinePanel.Build(canvasObject.transform);
            onlinePanel.Hide();

            lobbyPanel = new BattleLobbyScreen(LeaveRoomFromLobby);
            lobbyPanel.Build(canvasObject.transform);
            lobbyPanel.Hide();

            saveSlots = new SaveSlotsPanel(ClosePanel);
            saveSlots.Build(canvasObject.transform);
            settings = new SettingsPanel(ClosePanel);
            settings.Build(canvasObject.transform);
            credits = new CreditsPanel(ClosePanel);
            credits.Build(canvasObject.transform);
            saveSlots.Hide();
            settings.Hide();
            credits.Hide();
        }

        // Tạo phòng ngay trên scene menu rồi chuyển sang panel Phòng chờ.
        public void BeginHostRoom()
        {
            Subscribe();
            var net = LockstepNetwork.EnsureInstance();
            Subscribe();
            connectingText = "Đang tạo phòng...";
            connectingSince = Time.unscaledTime;
            notice = "";
            RefreshActiveMultiplayerPanel();
            net.Host();
        }

        // Kết nối tới phòng ngay trên scene menu; khi vào được phòng thì tự mở panel Phòng chờ.
        public void BeginJoinRoom(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            Subscribe();
            var net = LockstepNetwork.EnsureInstance();
            Subscribe();
            string trimmed = address.Trim();
            connectingText = "Đang kết nối tới " + trimmed + "...";
            connectingSince = Time.unscaledTime;
            notice = "";
            RefreshActiveMultiplayerPanel();
            net.Join(trimmed);
        }

        // Rời phòng chờ ngay trên scene menu: quay lại panel LAN hoặc Tham gia máy chủ, không đổi scene.
        void LeaveRoomFromLobby()
        {
            var net = LockstepNetwork.Instance;
            if (net != null)
            {
                ignoreDisconnectUntil = Time.unscaledTime + IgnoreDisconnectSeconds;
                net.Leave();
            }
            connectingText = "";
            notice = "";
            if (GameFlow.Mode == PlayMode.Online) OpenOnlinePanel();
            else OpenLanPanel();
        }

        void CloseMultiplayerPanel()
        {
            if (connectingText.Length > 0)
            {
                var net = LockstepNetwork.Instance;
                if (net != null)
                {
                    ignoreDisconnectUntil = Time.unscaledTime + IgnoreDisconnectSeconds;
                    net.Leave();
                }
                connectingText = "";
            }
            notice = "";
            ClosePanel();
        }

        void OpenLanPanel(string initialNotice = "")
        {
            GameFlow.SetMode(PlayMode.Lan);
            notice = initialNotice ?? "";
            OpenPanel(lanPanel.Root, () => lanPanel.Show(connectingText.Length > 0 ? connectingText : notice), lanPanel.Hide);
        }

        void OpenOnlinePanel(string initialNotice = "")
        {
            GameFlow.SetMode(PlayMode.Online);
            notice = initialNotice ?? "";
            OpenPanel(onlinePanel.Root, () => onlinePanel.Show(connectingText.Length > 0 ? connectingText : notice), onlinePanel.Hide);
        }

        void OpenLobbyPanel()
        {
            connectingText = "";
            notice = "";
            OpenPanel(lobbyPanel.Root, lobbyPanel.Show, lobbyPanel.Hide);
        }

        // Mở một panel phủ lên menu; Esc hoặc nút Quay lại để đóng.
        void OpenPanel(GameObject panel, Action show, Action hide = null)
        {
            ClosePanel();
            show?.Invoke();
            openPanel = panel;
            closeOpenPanel = hide;
        }

        void ClosePanel()
        {
            if (openPanel == null) return;
            if (closeOpenPanel != null) closeOpenPanel.Invoke();
            else openPanel.SetActive(false);
            openPanel = null;
            closeOpenPanel = null;
        }

        void OnLobbyChanged()
        {
            var net = LockstepNetwork.Instance;
            if (net == null || net.InMatch) return;
            if (net.LocalTeam >= 0)
            {
                if (openPanel != lobbyPanel.Root) OpenLobbyPanel();
                else lobbyPanel.Refresh(net);
            }
        }

        void OnDisconnected(string reason)
        {
            connectingText = "";
            if (Time.unscaledTime < ignoreDisconnectUntil) return;
            notice = string.IsNullOrEmpty(reason) ? "Mất kết nối với phòng" : reason;
            if (GameFlow.Mode == PlayMode.Online) OpenOnlinePanel(notice);
            else OpenLanPanel(notice);
        }

        void RefreshActiveMultiplayerPanel()
        {
            string status = connectingText.Length > 0 ? connectingText : notice;
            if (openPanel == lanPanel.Root) lanPanel.Refresh(status);
            else if (openPanel == onlinePanel.Root) onlinePanel.Refresh(status);
        }

        void Update()
        {
            if (subscribedNet == null) Subscribe();
            var net = LockstepNetwork.Instance;
            bool inLobby = net != null && !net.InMatch && net.LocalTeam >= 0;

            if (inLobby && openPanel != lobbyPanel.Root)
            {
                OpenLobbyPanel();
            }

            if (connectingText.Length > 0 && Time.unscaledTime - connectingSince > ConnectTimeoutSeconds)
            {
                connectingText = "";
                if (net != null)
                {
                    ignoreDisconnectUntil = Time.unscaledTime + IgnoreDisconnectSeconds;
                    net.Leave();
                }
                notice = "Không kết nối được tới phòng";
                RefreshActiveMultiplayerPanel();
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (openPanel == lobbyPanel.Root) LeaveRoomFromLobby();
                else if (openPanel == lanPanel.Root || openPanel == onlinePanel.Root) CloseMultiplayerPanel();
                else ClosePanel();
            }

            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshSeconds;

            if (openPanel == lobbyPanel.Root && net != null) lobbyPanel.Refresh(net);
            else if (openPanel == lanPanel.Root || openPanel == onlinePanel.Root) RefreshActiveMultiplayerPanel();
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
