using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Điều phối giao diện trong scene battle: chỉ chứa HUD trận (06) và Kết quả trận (07a/07b);
    // toàn bộ menu, danh sách phòng LAN/Online và Phòng chờ nằm ở scene menu.
    public class BattleUiController : MonoBehaviour
    {
        const float RefreshSeconds = 0.1f;
        const float IgnoreDisconnectSeconds = 1f; // sau khi tự rời trận thì bỏ qua sự kiện Disconnected

        [Header("Tham chiếu")]
        [Tooltip("Font TMP có đủ tiếng Việt (Dynamic); để trống thì dùng font mặc định của TMP")]
        [SerializeField] TMP_FontAsset fontAsset;

        [Tooltip("Mô phỏng trận; để trống thì tự tìm trong scene")]
        [SerializeField] BattleSimulation simulation;

        [Tooltip("Điều khiển chuột của người chơi; để trống thì tự tìm")]
        [SerializeField] BattleInput battleInput;

        BattleHudScreen hudScreen;
        BattleResultScreen resultScreen;
        LockstepNetwork subscribedNet;
        BattleSimulation subscribedSimulation;

        string notice = "";
        float ignoreDisconnectUntil;
        float nextRefresh;
        bool matchEnded;
        int winner = -1;
        bool autoStartMatchOnLobby;

        void Awake()
        {
            if (simulation == null) simulation = FindFirstObjectByType<BattleSimulation>();
            if (battleInput == null) battleInput = FindFirstObjectByType<BattleInput>();

            BattleUiKit.Font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            EnsureEventSystem();
            BuildScreens();
            if (FindFirstObjectByType<SettingsMenuController>() == null)
                gameObject.AddComponent<SettingsMenuController>();
        }

        void Start()
        {
            var net = LockstepNetwork.EnsureInstance();
            Subscribe();
            if (!net.InMatch && !net.IsConnecting && net.LocalTeam < 0)
            {
                autoStartMatchOnLobby = true;
                net.Host();
            }
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
                net.LobbyChanged += OnLobbyChanged;
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
                subscribedNet.LobbyChanged -= OnLobbyChanged;
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

            hudScreen = new BattleHudScreen(this, simulation, battleInput);
            resultScreen = new BattleResultScreen(this, simulation);
            hudScreen.Build(canvasObject.transform);
            resultScreen.Build(canvasObject.transform);
            hudScreen.Root.SetActive(false);
            resultScreen.Root.SetActive(false);
        }

        void Update()
        {
            if (subscribedNet == null || subscribedSimulation == null) Subscribe();
            var net = LockstepNetwork.Instance;

            bool inMatch = net != null && net.InMatch && simulation != null;
            if (!inMatch) matchEnded = false;

            bool showResult = inMatch && (matchEnded || simulation.WinnerTeam >= 0);
            hudScreen.Root.SetActive(inMatch);
            resultScreen.Root.SetActive(showResult);

            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshSeconds;

            if (inMatch)
            {
                hudScreen.Refresh(net, notice);
                if (showResult) resultScreen.Refresh(net, winner >= 0 ? winner : simulation.WinnerTeam);
            }
        }

        public void HostRoom()
        {
            LockstepNetwork.EnsureInstance().Host();
        }

        public void JoinRoom(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            LockstepNetwork.EnsureInstance().Join(address.Trim());
        }

        public void SetNotice(string text)
        {
            notice = text ?? "";
        }

        // Thoát ở màn kết quả: về lại sảnh multiplayer trên scene menu qua loading chuyển cảnh.
        public void LeaveRoom()
        {
            ReturnToMultiplayerMenu("");
        }

        void ReturnToMultiplayerMenu(string message)
        {
            ignoreDisconnectUntil = Time.unscaledTime + IgnoreDisconnectSeconds;
            notice = "";
            GameFlow.BackToMultiplayerMenu(message);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnLobbyChanged()
        {
            if (!autoStartMatchOnLobby) return;
            var net = LockstepNetwork.Instance;
            if (net != null && net.IsHost && !net.InMatch && net.LocalTeam >= 0)
            {
                autoStartMatchOnLobby = false;
                net.StartMatch();
            }
        }

        void OnDisconnected(string reason)
        {
            if (Time.unscaledTime < ignoreDisconnectUntil) return;
            string message = string.IsNullOrEmpty(reason) ? "Mất kết nối với phòng" : "Mất kết nối: " + reason;
            ReturnToMultiplayerMenu(message);
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
