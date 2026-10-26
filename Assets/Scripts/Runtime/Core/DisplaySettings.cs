using System.Collections.Generic;
using UnityEngine;

namespace SurvivalRts
{
    // Cài đặt hiển thị của người chơi: độ phân giải, toàn màn hình, VSync, giới hạn FPS, chất lượng đồ họa.
    // Lưu bằng PlayerPrefs và áp dụng lại mỗi lần mở game.
    public static class DisplaySettings
    {
        const string Prefix = "display.";

        // 0 là không giới hạn
        public static readonly int[] FpsOptions = { 30, 60, 120, 144, 0 };

        public static int Width;
        public static int Height;
        public static bool FullScreen;
        public static bool VSync;
        public static int FpsLimit;
        public static int Quality;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Startup()
        {
            Load();
            // Chưa từng chỉnh thì giữ nguyên mặc định của Unity
            if (PlayerPrefs.HasKey(Prefix + "width")) Apply();
        }

        public static void Load()
        {
            Width = PlayerPrefs.GetInt(Prefix + "width", Screen.width);
            Height = PlayerPrefs.GetInt(Prefix + "height", Screen.height);
            FullScreen = PlayerPrefs.GetInt(Prefix + "fullscreen", Screen.fullScreen ? 1 : 0) != 0;
            VSync = PlayerPrefs.GetInt(Prefix + "vsync", QualitySettings.vSyncCount > 0 ? 1 : 0) != 0;
            FpsLimit = PlayerPrefs.GetInt(Prefix + "fps", 0);
            Quality = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(Prefix + "width", Width);
            PlayerPrefs.SetInt(Prefix + "height", Height);
            PlayerPrefs.SetInt(Prefix + "fullscreen", FullScreen ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "fps", FpsLimit);
            PlayerPrefs.SetInt(Prefix + "quality", Quality);
            PlayerPrefs.Save();
        }

        public static void Apply()
        {
            QualitySettings.SetQualityLevel(Quality, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = FpsLimit > 0 ? FpsLimit : -1;
            Screen.SetResolution(Width, Height, FullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        // Về mặc định: độ phân giải hiện tại của màn hình, bật toàn màn hình và VSync, không giới hạn FPS.
        public static void ResetToDefault()
        {
            var native = Screen.currentResolution;
            Width = native.width;
            Height = native.height;
            FullScreen = true;
            VSync = true;
            FpsLimit = 0;
            Quality = Mathf.Min(2, QualitySettings.names.Length - 1);
        }

        // Danh sách độ phân giải khác nhau của màn hình, lớn trước, luôn có độ phân giải hiện tại.
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!list.Contains(size)) list.Add(size);
            }
            var current = new Vector2Int(Width, Height);
            if (!list.Contains(current)) list.Add(current);
            list.Sort((a, b) => b.x != a.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));
            return list;
        }

        public static int FpsIndex(int fps)
        {
            int index = System.Array.IndexOf(FpsOptions, fps);
            return index < 0 ? FpsOptions.Length - 1 : index;
        }
    }
}
