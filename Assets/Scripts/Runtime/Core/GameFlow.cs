using UnityEngine.SceneManagement;

namespace SurvivalRts
{
    public enum PlayMode
    {
        Offline,
        Lan,
        Online
    }

    // Luồng chuyển màn của game: init (loading) -> menu -> chơi offline hoặc vào phần multiplayer.
    public static class GameFlow
    {
        public const string InitScene = "init";
        public const string MenuScene = "menu";
        public const string OfflineScene = "prototype";
        public const string MultiplayerScene = "battle";

        // Chế độ người chơi chọn ở menu, màn multiplayer đọc để biết hiện giao diện LAN hay online
        public static PlayMode Mode { get; private set; } = PlayMode.Offline;

        public static void StartOffline()
        {
            Mode = PlayMode.Offline;
            SceneManager.LoadScene(OfflineScene);
        }

        public static void StartMultiplayer(PlayMode mode)
        {
            Mode = mode;
            SceneManager.LoadScene(MultiplayerScene);
        }

        // Rời phòng (nếu đang ở trong) rồi về menu chính.
        public static void BackToMenu()
        {
            var net = LockstepNetwork.Instance;
            if (net != null) net.Leave();
            SceneManager.LoadScene(MenuScene);
        }
    }
}
