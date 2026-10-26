using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalRts
{
    public enum PlayMode
    {
        Offline,
        Lan,
        Online
    }

    // Luồng chuyển màn của game:
    // - Scene menu chứa Menu chính, Panel LAN, Panel Máy chủ, Phòng chờ, Bản lưu, Cài đặt, Credits (cùng scene, không loading).
    // - Chỉ chuyển scene (qua SceneTransitionLoader) khi vào trận/màn chơi hoặc từ màn chơi rút về menu/phòng chờ.
    public static class GameFlow
    {
        public const string InitScene = "init";
        public const string MenuScene = "menu";
        public const string OfflineScene = "prototype";
        public const string MultiplayerScene = "battle";

        // Chế độ người chơi chọn ở menu
        public static PlayMode Mode { get; private set; } = PlayMode.Offline;

        // Cờ báo cho MainMenu mở lại panel LAN/Online khi từ trận rút về vì thoát hoặc mất kết nối
        public static bool OpenMultiplayerPanelOnMenu { get; private set; }

        // Cờ báo cho MainMenu mở thẳng Phòng chờ khi chủ phòng chọn Về phòng chờ từ màn kết quả
        public static bool OpenLobbyOnMenu { get; private set; }

        // Thông báo (như mất kết nối) gửi kèm khi quay về panel multiplayer ở menu
        public static string PendingNotice { get; private set; } = "";

        public static void SetMode(PlayMode mode)
        {
            Mode = mode;
        }

        // Vào màn chơi offline từ menu chính qua màn loading chuyển cảnh (11c).
        public static void StartOffline()
        {
            Mode = PlayMode.Offline;
            OpenMultiplayerPanelOnMenu = false;
            OpenLobbyOnMenu = false;
            PendingNotice = "";
            SceneTransitionLoader.LoadScene(OfflineScene);
        }

        // Tạo phòng ngay trên scene hiện tại (scene menu): mở host và chuyển sang panel Phòng chờ, không đổi scene.
        public static void HostMultiplayer(PlayMode mode)
        {
            Mode = mode;
            OpenMultiplayerPanelOnMenu = false;
            OpenLobbyOnMenu = false;
            PendingNotice = "";

            var menu = Object.FindFirstObjectByType<MainMenu>();
            if (menu != null)
            {
                menu.BeginHostRoom();
                return;
            }

            var controller = Object.FindFirstObjectByType<BattleUiController>();
            if (controller != null)
            {
                controller.HostRoom();
                return;
            }

            LockstepNetwork.EnsureInstance().Host();
        }

        // Vào phòng theo địa chỉ ngay trên scene hiện tại (scene menu): kết nối xong thì mở panel Phòng chờ, không đổi scene.
        public static void JoinMultiplayer(PlayMode mode, string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            string trimmed = address.Trim();
            Mode = mode;
            OpenMultiplayerPanelOnMenu = false;
            OpenLobbyOnMenu = false;
            PendingNotice = "";

            var menu = Object.FindFirstObjectByType<MainMenu>();
            if (menu != null)
            {
                menu.BeginJoinRoom(trimmed);
                return;
            }

            var controller = Object.FindFirstObjectByType<BattleUiController>();
            if (controller != null)
            {
                controller.JoinRoom(trimmed);
                return;
            }

            LockstepNetwork.EnsureInstance().Join(trimmed);
        }

        public static void StartMultiplayer(PlayMode mode)
        {
            HostMultiplayer(mode);
        }

        // Khi chủ phòng bấm Bắt đầu ở Phòng chờ (hoặc bấm Chơi lại ở Kết quả): chuyển sang scene battle qua màn loading chuyển cảnh (11c).
        public static void EnterMultiplayerMatch()
        {
            OpenMultiplayerPanelOnMenu = false;
            OpenLobbyOnMenu = false;
            PendingNotice = "";

            if (SceneManager.GetActiveScene().name == MultiplayerScene)
            {
                SceneTransitionLoader.ShowOverlay(0.45f);
                return;
            }

            SceneTransitionLoader.LoadScene(MultiplayerScene, null, IsMatchReady);
        }

        // Chủ phòng chọn Về phòng chờ ở màn Kết quả: chuyển từ scene battle về scene menu qua màn loading chuyển cảnh (11a) và mở Phòng chờ.
        public static void ReturnToMultiplayerLobby()
        {
            OpenLobbyOnMenu = true;
            OpenMultiplayerPanelOnMenu = false;
            PendingNotice = "";
            if (SceneManager.GetActiveScene().name != MenuScene)
                SceneTransitionLoader.LoadScene(MenuScene);
        }

        // Thoát trận hoặc mất kết nối khi đang ở scene battle: rời mạng, chuyển về scene menu qua màn loading chuyển cảnh (11e) và mở panel LAN/Online.
        public static void BackToMultiplayerMenu(string notice = "")
        {
            var net = LockstepNetwork.Instance;
            if (net != null) net.Leave();
            OpenLobbyOnMenu = false;
            OpenMultiplayerPanelOnMenu = true;
            PendingNotice = notice ?? "";
            if (SceneManager.GetActiveScene().name != MenuScene)
                SceneTransitionLoader.LoadScene(MenuScene);
        }

        // Về thẳng menu chính từ màn chơi (11f).
        public static void BackToMenu()
        {
            var net = LockstepNetwork.Instance;
            if (net != null) net.Leave();
            OpenLobbyOnMenu = false;
            OpenMultiplayerPanelOnMenu = false;
            PendingNotice = "";
            if (SceneManager.GetActiveScene().name != MenuScene)
                SceneTransitionLoader.LoadScene(MenuScene);
        }

        // Lấy và xóa trạng thái mở panel khi MainMenu khởi chạy.
        public static void ConsumeMenuState(out bool openLobby, out bool openMultiplayerPanel, out PlayMode mode, out string notice)
        {
            openLobby = OpenLobbyOnMenu;
            openMultiplayerPanel = OpenMultiplayerPanelOnMenu;
            mode = Mode;
            notice = PendingNotice;
            OpenLobbyOnMenu = false;
            OpenMultiplayerPanelOnMenu = false;
            PendingNotice = "";
        }

        static bool IsMatchReady()
        {
            var net = LockstepNetwork.Instance;
            if (net == null || !net.InMatch) return true;
            return BattleSimulation.Current != null && BattleSimulation.Current.Started;
        }
    }
}
