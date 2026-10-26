using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Menu Esc trong trận: dựng panel cài đặt wireframe bằng code, bật/tắt bằng phím Esc.
    public class SettingsMenuController : MonoBehaviour
    {
        const string LegacyRootName = "settingsRoot";
        const int SortingOrder = 120;

        [Header("Tham chiếu")]
        [Tooltip("Placer, để Esc lúc đang đặt công trình chỉ hủy đặt chứ không mở menu")]
        [SerializeField] BuildingPlacer placer;

        [Tooltip("Font TMP có đủ tiếng Việt; để trống thì dùng font mặc định của TMP")]
        [SerializeField] TMP_FontAsset fontAsset;

        GameObject canvasObject;
        SettingsPanel panel;
        BattleSimulation battleSimulation;
        float previousTimeScale = 1f;
        bool pausedTimeScale;
        bool wasPlacing;

        public bool IsOpen { get; private set; }

        // Dựng panel một lần khi bật component và ẩn giao diện tối cũ nằm sẵn trong prefab.
        void OnEnable()
        {
            if (placer == null) placer = FindFirstObjectByType<BuildingPlacer>();
            if (battleSimulation == null) battleSimulation = FindFirstObjectByType<BattleSimulation>();
            HideLegacyUi();
            if (canvasObject == null) Build();
        }

        // Đóng menu nếu đang mở khi tắt component.
        void OnDisable()
        {
            if (IsOpen) Close();
        }

        void OnDestroy()
        {
            if (canvasObject != null) Destroy(canvasObject);
        }

        void HideLegacyUi()
        {
            var legacy = transform.Find(LegacyRootName);
            if (legacy != null) legacy.gameObject.SetActive(false);
        }

        void Build()
        {
            BattleUiKit.Font = fontAsset != null ? fontAsset : (BattleUiKit.Font != null ? BattleUiKit.Font : TMP_Settings.defaultFontAsset);
            EnsureEventSystem();

            canvasObject = new GameObject("SettingsMenuCanvas", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            panel = new SettingsPanel(Close, "Tiếp tục", OnMainMenu, SettingsPanel.OfflineControls, BattleUiKit.BackdropColor);
            panel.Build(canvasObject.transform);
            panel.Hide();
        }

        // Dùng Input System UI module vì dự án chỉ dùng Input System.
        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        // Lắng nghe phím Esc để đóng/mở menu khi không bận đặt/phá công trình hoặc mở bản đồ lớn.
        void Update()
        {
            if (battleSimulation != null)
            {
                var net = LockstepNetwork.Instance;
                bool matchActive = net != null && net.InMatch && battleSimulation.Started && battleSimulation.WinnerTeam < 0;
                if (!matchActive)
                {
                    if (IsOpen) Close();
                    return;
                }
            }

            var keyboard = Keyboard.current;
            bool busy = (placer != null && (placer.IsPlacing || placer.IsDemolishing)) || MinimapController.IsFullMapOpen;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !busy && !wasPlacing)
            {
                if (IsOpen) Close();
                else Open();
            }

            if (IsOpen) UiPointerState.IsOverUI = true;
        }

        // Ghi nhận trạng thái đang bận thao tác ở cuối frame.
        void LateUpdate()
        {
            wasPlacing = (placer != null && (placer.IsPlacing || placer.IsDemolishing)) || MinimapController.IsFullMapOpen;
        }

        // Mở menu cài đặt và tạm dừng thời gian trong màn chơi offline.
        public void Open()
        {
            if (IsOpen || panel == null) return;
            IsOpen = true;
            panel.Show();
            pausedTimeScale = battleSimulation == null;
            if (pausedTimeScale)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            UiPointerState.IsOverUI = true;
            AudioDirector.Instance?.SetGameplayPaused(true);
            AudioDirector.Post(SoundId.UiOpen, Vector3.zero);
        }

        // Đóng menu cài đặt, lưu âm lượng và khôi phục tốc độ thời gian.
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            panel.Hide();
            if (pausedTimeScale)
            {
                Time.timeScale = previousTimeScale;
                pausedTimeScale = false;
            }
            UiPointerState.IsOverUI = false;
            AudioVolumeSettings.Save();
            AudioDirector.Instance?.SetGameplayPaused(false);
            AudioDirector.Post(SoundId.UiClose, Vector3.zero);
        }

        // Về menu chính: đóng menu, ngắt kết nối nếu đang trong trận mạng rồi mới chuyển màn.
        void OnMainMenu()
        {
            Close();
            var net = LockstepNetwork.Instance;
            if (net != null && (net.InMatch || net.IsConnected || net.IsConnecting)) net.Leave();
            GameFlow.BackToMenu();
        }
    }
}
