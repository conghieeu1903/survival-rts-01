using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Điều phối giao diện trận đại chiến: tự dựng Canvas + EventSystem, chuyển giữa màn chính, phòng chờ, HUD và kết quả
    // theo trạng thái của LockstepNetwork.
    public class BattleUiController : MonoBehaviour
    {
        const float RefreshSeconds = 0.1f;
        const float ConnectTimeoutSeconds = 12f;
        const float IgnoreDisconnectSeconds = 1f; // sau khi tự rời phòng thì bỏ qua sự kiện Disconnected

        [Header("Tham chiếu")]
        [Tooltip("Font TMP có đủ tiếng Việt (Dynamic); để trống thì dùng font mặc định của TMP")]
        [SerializeField] TMP_FontAsset fontAsset;

        [Tooltip("Mô phỏng trận; để trống thì tự tìm trong scene")]
        [SerializeField] BattleSimulation simulation;

        [Tooltip("Điều khiển chuột của người chơi; để trống thì tự tìm")]
        [SerializeField] BattleInput battleInput;

        [Tooltip("Dò phòng LAN cho màn hình chính; để trống thì tự tìm, không có thì ẩn danh sách")]
        [SerializeField] LanDiscovery lanDiscovery;

        BattleMainMenuScreen menuScreen;
        BattleLobbyScreen lobbyScreen;
        BattleHudScreen hudScreen;
        BattleResultScreen resultScreen;
        LockstepNetwork subscribedNet;
        BattleSimulation subscribedSimulation;

        string notice = "";
        string connectingText = "";
        float connectingSince;
        float ignoreDisconnectUntil;
        float nextRefresh;
        bool matchEnded;
        int winner = -1;

        void Awake()
        {
            if (simulation == null) simulation = FindFirstObjectByType<BattleSimulation>();
            if (battleInput == null) battleInput = FindFirstObjectByType<BattleInput>();
            if (lanDiscovery == null) lanDiscovery = FindFirstObjectByType<LanDiscovery>();

            BattleUiKit.Font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            EnsureEventSystem();
            BuildScreens();
        }

        void Start()
        {
            Subscribe();
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        // Thứ tự Awake giữa các component không cố định nên đăng ký sự kiện ở Start và đăng ký lại nếu mạng được tạo lại.
        void Subscribe()
        {
            var net = LockstepNetwork.Instance;
            if (net != null && net != subscribedNet)
            {
                Unsubscribe();
                subscribedNet = net;
                net.Disconnected += OnDisconnected;
                net.DesyncDetected += OnDesync;
            }
            if (simulation != null && subscribedSimulation != simulation)
            {
                subscribedSimulation = simulation;
                simulation.MatchStarted += OnMatchStarted;
                simulation.MatchEnded += OnMatchEnded;
            }
        }

        void Unsubscribe()
        {
            if (subscribedNet != null)
            {
                subscribedNet.Disconnected -= OnDisconnected;
                subscribedNet.DesyncDetected -= OnDesync;
            }
            subscribedNet = null;
            if (subscribedSimulation != null)
            {
                subscribedSimulation.MatchStarted -= OnMatchStarted;
                subscribedSimulation.MatchEnded -= OnMatchEnded;
            }
            subscribedSimulation = null;
        }

        // Dùng Input System UI module vì dự án chỉ dùng Input System.
        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        void BuildScreens()
        {
            var canvasObject = new GameObject("BattleCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            menuScreen = new BattleMainMenuScreen(this, lanDiscovery);
            lobbyScreen = new BattleLobbyScreen(this);
            hudScreen = new BattleHudScreen(this, simulation, battleInput);
            resultScreen = new BattleResultScreen(this, simulation);
            hudScreen.Build(canvasObject.transform);
            resultScreen.Build(canvasObject.transform);
            lobbyScreen.Build(canvasObject.transform);
            menuScreen.Build(canvasObject.transform);
            lobbyScreen.Root.SetActive(false);
            hudScreen.Root.SetActive(false);
            resultScreen.Root.SetActive(false);
            menuScreen.SetActive(true);
        }

        void Update()
        {
            if (subscribedNet == null || subscribedSimulation == null) Subscribe();
            var net = LockstepNetwork.Instance;

            bool inMatch = net != null && net.InMatch && simulation != null;
            bool inLobby = net != null && !net.InMatch && net.LocalTeam >= 0;
            if (inLobby || inMatch) connectingText = "";
            if (!inMatch) matchEnded = false;
            if (connectingText.Length > 0 && Time.unscaledTime - connectingSince > ConnectTimeoutSeconds)
            {
                connectingText = "";
                LeaveRoom();
                notice = "Không kết nối được tới phòng";
            }

            bool showResult = inMatch && (matchEnded || simulation.WinnerTeam >= 0);
            lobbyScreen.Root.SetActive(inLobby);
            hudScreen.Root.SetActive(inMatch);
            resultScreen.Root.SetActive(showResult);
            menuScreen.SetActive(!inMatch && !inLobby);

            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshSeconds;

            if (inMatch)
            {
                hudScreen.Refresh(net, notice);
                if (showResult) resultScreen.Refresh(net, winner >= 0 ? winner : simulation.WinnerTeam);
            }
            else if (inLobby) lobbyScreen.Refresh(net, simulation != null ? simulation.Data : null);
            else menuScreen.Refresh(connectingText.Length > 0 ? connectingText : notice);
        }

        public void SetNotice(string text)
        {
            notice = text ?? "";
        }

        public void BeginConnecting(string text)
        {
            connectingText = text;
            connectingSince = Time.unscaledTime;
            notice = "";
        }

        // Tự rời phòng: bỏ qua thông báo mất kết nối do chính hành động này gây ra.
        public void LeaveRoom()
        {
            var net = LockstepNetwork.Instance;
            if (net == null) return;
            ignoreDisconnectUntil = Time.unscaledTime + IgnoreDisconnectSeconds;
            net.Leave();
            notice = "";
            connectingText = "";
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnDisconnected(string reason)
        {
            connectingText = "";
            if (Time.unscaledTime < ignoreDisconnectUntil) return;
            notice = string.IsNullOrEmpty(reason) ? "Mất kết nối với phòng" : "Mất kết nối: " + reason;
        }

        void OnDesync(uint turn)
        {
            notice = $"Phát hiện lệch đồng bộ ở lượt {turn}";
        }

        void OnMatchStarted()
        {
            matchEnded = false;
            winner = -1;
            notice = "";
        }

        void OnMatchEnded(int winnerTeam)
        {
            matchEnded = true;
            winner = winnerTeam;
        }
    }
}
