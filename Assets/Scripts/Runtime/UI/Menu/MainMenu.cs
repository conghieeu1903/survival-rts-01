using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Menu chính của scene menu: chơi offline vào luôn, LAN hoặc online thì sang phần multiplayer.
    // Bố cục theo menu wireframe: thanh bên trái có ô logo và các nút lớn, hộp thoại xác nhận khi thoát.
    public class MainMenu : MonoBehaviour
    {
        [Header("Giao diện")]
        [Tooltip("Font TMP có đủ tiếng Việt (Dynamic); để trống thì dùng font mặc định của TMP")]
        [SerializeField] TMP_FontAsset fontAsset;

        [Tooltip("Tên game hiện trong ô logo")]
        [SerializeField] string title = "SURVIVAL RTS";

        [Tooltip("Dòng phiên bản nhỏ ở cuối thanh bên")]
        [SerializeField] string versionNote = "v0.1";

        GameObject exitDialog;
        SaveSlotsPanel saveSlots;
        SettingsPanel settings;
        CreditsPanel credits;
        GameObject openPanel;

        void Awake()
        {
            BattleUiKit.Font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            AudioDirector.EnsureInstance();
            EnsureEventSystem();
            Build();
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
            BattleUiKit.PushButton(sidebar, "Chơi LAN", () => GameFlow.StartMultiplayer(PlayMode.Lan), -1f, 56f, 24f);
            BattleUiKit.PushButton(sidebar, "Chơi online", () => GameFlow.StartMultiplayer(PlayMode.Online), -1f, 56f, 24f);
            BattleUiKit.PushButton(sidebar, "Bản lưu", () => OpenPanel(saveSlots.Root, saveSlots.Show), -1f, 56f, 24f);
            BattleUiKit.PushButton(sidebar, "Cài đặt", () => OpenPanel(settings.Root, settings.Show), -1f, 56f, 24f);
            BattleUiKit.PushButton(sidebar, "Credits", () => OpenPanel(credits.Root, credits.Show), -1f, 56f, 24f);
            BattleUiKit.PushButton(sidebar, "Thoát", () => exitDialog.SetActive(true), -1f, 56f, 24f);

            var spacer = BattleUiKit.Node(sidebar, "Spacer");
            spacer.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var version = BattleUiKit.Label(sidebar, versionNote, 14f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor);
            BattleUiKit.Size(version, -1f, 22f);

            exitDialog = BattleUiKit.ConfirmDialog(canvasObject.transform, "Thoát game?", "Tiến trình chưa lưu sẽ bị mất.",
                "Không", "Thoát", Quit, () => exitDialog.SetActive(false));
            exitDialog.SetActive(false);

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

        // Mở một panel phủ lên menu; Esc hoặc nút Quay lại để đóng.
        void OpenPanel(GameObject panel, System.Action show)
        {
            show();
            openPanel = panel;
        }

        void ClosePanel()
        {
            if (openPanel == null) return;
            openPanel.SetActive(false);
            openPanel = null;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ClosePanel();
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
