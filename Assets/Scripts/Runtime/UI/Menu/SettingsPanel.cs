using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace SurvivalRts
{
    // Màn cài đặt theo panel của dự án Godot: thẻ có tiêu đề, thanh tab, danh sách cuộn, chân trang Mặc định / Quay lại / Áp dụng.
    public class SettingsPanel
    {
        static readonly string[] TabNames = { "Đồ họa", "Âm thanh", "Điều khiển" };

        // Phím và thao tác có thật của chế độ đại chiến
        public static readonly string[,] BattleControls =
        {
            { "Chọn tiểu đội", "Kéo chuột trái để chọn khung, Shift để thêm vào" },
            { "Chọn cùng loại", "Nhấp đúp vào một tiểu đội" },
            { "Chọn cả quân", "Q" },
            { "Gán nhóm", "Ctrl + 1..9" },
            { "Gọi nhóm", "1..9" },
            { "Di chuyển", "Chuột phải" },
        };

        // Phím và thao tác có thật của màn chơi offline
        public static readonly string[,] OfflineControls =
        {
            { "Di chuyển camera", "W A S D" },
            { "Phóng to / thu nhỏ", "Cuộn chuột" },
            { "Đặt công trình", "Chuột trái" },
            { "Xoay công trình 90°", "R" },
            { "Xoay mịn từng độ", "Q / E (Shift: nhanh)" },
            { "Hủy đặt / hủy phá", "Chuột phải" },
            { "Chế độ tháo dỡ", "X" },
            { "Bản đồ lớn", "M" },
            { "Menu cài đặt", "Esc" },
        };

        readonly System.Action onBack;
        readonly string backLabel;
        readonly System.Action onMainMenu;
        readonly string[,] controls;
        readonly Color? backdrop;
        RectTransform root;
        RectTransform tabBar;
        RectTransform list;
        TMP_Text noteLabel;
        int tab;

        public GameObject Root => root.gameObject;

        // onMainMenu khác null thì chân trang có thêm nút "Về menu chính"; backdrop khác null thì phủ lớp mờ thay nền xám (dùng khi mở đè lên game).
        public SettingsPanel(System.Action onBack, string backLabel = "Quay lại", System.Action onMainMenu = null,
            string[,] controls = null, Color? backdrop = null)
        {
            this.onBack = onBack;
            this.backLabel = backLabel;
            this.onMainMenu = onMainMenu;
            this.controls = controls ?? BattleControls;
            this.backdrop = backdrop;
        }

        public void Build(Transform parent)
        {
            DisplaySettings.Load();
            var card = BattleUiKit.ScreenCard(parent, "Settings", "Cài đặt", "", 180f, 48f, backdrop);
            root = card.parent as RectTransform;
            noteLabel = card.GetChild(0).GetChild(1).GetComponent<TMP_Text>();

            tabBar = BattleUiKit.Node(card, "Tabs");
            BattleUiKit.Size(tabBar, -1f, 48f);
            BattleUiKit.Horizontal(tabBar, 8f, 0);
            BattleUiKit.Separator(card);

            list = BattleUiKit.ScrollList(card, 20f);

            var footer = BattleUiKit.Footer(card);
            BattleUiKit.PushButton(footer, "Mặc định", OnDefault, 160f, 48f);
            BattleUiKit.Size(BattleUiKit.Node(footer, "Spacer"), -1f, -1f, 1f);
            if (onMainMenu != null) BattleUiKit.PushButton(footer, "Về menu chính", () => onMainMenu(), 220f, 48f);
            BattleUiKit.PushButton(footer, backLabel, OnBack, 160f, 48f);
            BattleUiKit.PushButton(footer, "Áp dụng", OnApply, 160f, 48f, 20f, true);

            ShowTab(0);
        }

        public void Show()
        {
            DisplaySettings.Load();
            ShowTab(tab);
            root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            root.gameObject.SetActive(false);
        }

        // Dựng lại thanh tab (tab đang chọn là nút chính) và nội dung của tab.
        void ShowTab(int index)
        {
            tab = index;
            Clear(tabBar);
            for (int i = 0; i < TabNames.Length; i++)
            {
                int target = i;
                BattleUiKit.PushButton(tabBar, TabNames[i], () => ShowTab(target), 180f, 44f, 20f, i == tab);
            }
            BattleUiKit.SetText(noteLabel, "Cài đặt · " + TabNames[tab]);

            Clear(list);
            if (tab == 0) BuildGraphics();
            else if (tab == 1) BuildAudio();
            else BuildControls();
        }

        static void Clear(RectTransform target)
        {
            for (int i = target.childCount - 1; i >= 0; i--) Object.Destroy(target.GetChild(i).gameObject);
        }

        RectTransform Group(string title)
        {
            var group = BattleUiKit.Node(list, "Group");
            BattleUiKit.Vertical(group, 2f, 0);
            BattleUiKit.GroupTitle(group, title);
            return group;
        }

        void BuildGraphics()
        {
            var display = Group("Hiển thị màn hình");
            var resolutions = DisplaySettings.Resolutions();
            var names = new string[resolutions.Count];
            int selected = 0;
            for (int i = 0; i < resolutions.Count; i++)
            {
                names[i] = resolutions[i].x + " x " + resolutions[i].y;
                if (resolutions[i].x == DisplaySettings.Width && resolutions[i].y == DisplaySettings.Height) selected = i;
            }
            BattleUiKit.ChoiceRow(display, "Độ phân giải", names, selected, i =>
            {
                DisplaySettings.Width = resolutions[i].x;
                DisplaySettings.Height = resolutions[i].y;
            });
            BattleUiKit.ToggleRow(display, "Toàn màn hình", DisplaySettings.FullScreen, on => DisplaySettings.FullScreen = on);
            BattleUiKit.ToggleRow(display, "Đồng bộ dọc (VSync)", DisplaySettings.VSync, on => DisplaySettings.VSync = on);

            var fpsNames = new string[DisplaySettings.FpsOptions.Length];
            for (int i = 0; i < fpsNames.Length; i++)
                fpsNames[i] = DisplaySettings.FpsOptions[i] > 0 ? DisplaySettings.FpsOptions[i] + " FPS" : "Không giới hạn";
            BattleUiKit.ChoiceRow(display, "Giới hạn FPS", fpsNames, DisplaySettings.FpsIndex(DisplaySettings.FpsLimit),
                i => DisplaySettings.FpsLimit = DisplaySettings.FpsOptions[i]);

            var quality = Group("Chất lượng");
            BattleUiKit.ChoiceRow(quality, "Chất lượng đồ họa", QualitySettings.names, DisplaySettings.Quality,
                i => DisplaySettings.Quality = i);
        }

        void BuildAudio()
        {
            var volume = Group("Âm lượng các kênh (%)");
            AddVolume(volume, "Âm lượng tổng (Master)", VolumeChannel.Master);
            AddVolume(volume, "Nhạc nền", VolumeChannel.Music);
            AddVolume(volume, "Hiệu ứng thế giới (SFX)", VolumeChannel.Sfx);
            AddVolume(volume, "Âm thanh giao diện (UI)", VolumeChannel.Ui);
        }

        static void AddVolume(Transform parent, string title, VolumeChannel channel)
        {
            BattleUiKit.SliderRow(parent, title, 0f, 100f, true, AudioVolumeSettings.Get(channel) * 100f,
                v => Mathf.RoundToInt(v) + "%", v => AudioVolumeSettings.Set(channel, v / 100f));
        }

        // Chỉ xem: dự án chưa có hệ thống đổi phím.
        void BuildControls()
        {
            var group = Group(controls == BattleControls ? "Điều khiển trong trận đại chiến" : "Phím và thao tác");
            for (int i = 0; i < controls.GetLength(0); i++)
            {
                var row = BattleUiKit.SettingRow(group, controls[i, 0]);
                var key = BattleUiKit.Label(row, controls[i, 1], 18f, TextAlignmentOptions.Right, BattleUiKit.DimTextColor);
                BattleUiKit.Size(key, -1f, -1f, 1f);
            }
            var note = BattleUiKit.Label(group, "Chưa hỗ trợ đổi phím.", 16f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor);
            BattleUiKit.Size(note, -1f, 32f);
        }

        void OnDefault()
        {
            DisplaySettings.ResetToDefault();
            AudioVolumeSettings.ResetToDefault();
            ShowTab(tab);
        }

        void OnApply()
        {
            DisplaySettings.Apply();
            DisplaySettings.Save();
            AudioVolumeSettings.Save();
        }

        void OnBack()
        {
            AudioVolumeSettings.Save();
            onBack?.Invoke();
        }
    }
}
