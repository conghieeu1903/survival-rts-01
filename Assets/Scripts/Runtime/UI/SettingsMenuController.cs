using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Menu cài đặt uGUI và TextMeshPro bật/tắt bằng phím Esc và tạm dừng game khi mở.
    public class SettingsMenuController : MonoBehaviour
    {
        const float ClickSoundInterval = 0.08f;

        [Header("Tham chiếu")]
        [Tooltip("Placer, để Esc lúc đang đặt công trình chỉ hủy đặt chứ không mở menu")]
        [SerializeField] BuildingPlacer placer;

        [Header("Phần tử uGUI")]
        [SerializeField] GameObject root;
        [SerializeField] Slider masterSlider;
        [SerializeField] TMP_Text masterValue;
        [SerializeField] Slider musicSlider;
        [SerializeField] TMP_Text musicValue;
        [SerializeField] Slider sfxSlider;
        [SerializeField] TMP_Text sfxValue;
        [SerializeField] Slider uiSlider;
        [SerializeField] TMP_Text uiValue;
        [SerializeField] Button resumeButton;
        [SerializeField] Button defaultButton;

        float previousTimeScale = 1f;
        bool wasPlacing;
        float lastClickSound;

        public bool IsOpen { get; private set; }

        // Liên kết các thanh trượt âm lượng và nút bấm khi bật component.
        void OnEnable()
        {
            ResolveReferences();
            UiButtonFeedback.AttachAll(this);

            if (root != null && root.GetComponent<UiPointerBlocker>() == null)
            {
                root.AddComponent<UiPointerBlocker>();
            }

            Bind(masterSlider, masterValue, VolumeChannel.Master);
            Bind(musicSlider, musicValue, VolumeChannel.Music);
            Bind(sfxSlider, sfxValue, VolumeChannel.Sfx);
            Bind(uiSlider, uiValue, VolumeChannel.Ui);

            if (resumeButton != null) resumeButton.onClick.AddListener(Close);
            if (defaultButton != null) defaultButton.onClick.AddListener(ResetDefaults);
            AudioVolumeSettings.Changed += RefreshSliders;
        }

        // Hủy đăng ký sự kiện và đóng menu nếu đang mở khi tắt component.
        void OnDisable()
        {
            AudioVolumeSettings.Changed -= RefreshSliders;
            if (resumeButton != null) resumeButton.onClick.RemoveListener(Close);
            if (defaultButton != null) defaultButton.onClick.RemoveListener(ResetDefaults);
            if (IsOpen) Close();
        }

        // Tự động tìm các đối tượng uGUI con theo tên nếu chưa được gán trong Inspector.
        void ResolveReferences()
        {
            if (placer == null) placer = FindFirstObjectByType<BuildingPlacer>();
            if (root == null)
            {
                var t = FindChildRecursive(transform, "settingsRoot");
                if (t != null) root = t.gameObject;
            }
            if (masterSlider == null) masterSlider = FindComponent<Slider>("masterSlider");
            if (masterValue == null) masterValue = FindComponent<TMP_Text>("masterValue");
            if (musicSlider == null) musicSlider = FindComponent<Slider>("musicSlider");
            if (musicValue == null) musicValue = FindComponent<TMP_Text>("musicValue");
            if (sfxSlider == null) sfxSlider = FindComponent<Slider>("sfxSlider");
            if (sfxValue == null) sfxValue = FindComponent<TMP_Text>("sfxValue");
            if (uiSlider == null) uiSlider = FindComponent<Slider>("uiSlider");
            if (uiValue == null) uiValue = FindComponent<TMP_Text>("uiValue");
            if (resumeButton == null) resumeButton = FindComponent<Button>("resumeButton");
            if (defaultButton == null) defaultButton = FindComponent<Button>("defaultButton");
        }

        // Tìm component trên đối tượng con theo tên.
        T FindComponent<T>(string childName) where T : Component
        {
            var t = FindChildRecursive(transform, childName);
            return t != null ? t.GetComponent<T>() : null;
        }

        // Đệ quy tìm Transform con theo tên.
        static Transform FindChildRecursive(Transform parent, string targetName)
        {
            if (parent.name == targetName) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindChildRecursive(parent.GetChild(i), targetName);
                if (found != null) return found;
            }
            return null;
        }

        // Gắn sự kiện thay đổi giá trị cho một thanh trượt âm lượng.
        void Bind(Slider slider, TMP_Text valueLabel, VolumeChannel channel)
        {
            if (slider == null) return;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            float pct = AudioVolumeSettings.Get(channel) * 100f;
            slider.SetValueWithoutNotify(pct);
            if (valueLabel != null) valueLabel.text = Mathf.RoundToInt(pct) + "%";

            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(val =>
            {
                if (valueLabel != null) valueLabel.text = Mathf.RoundToInt(val) + "%";
                AudioVolumeSettings.Set(channel, val / 100f);
                PlayClickThrottled();
            });
        }

        // Đồng bộ lại toàn bộ thanh trượt và nhãn phần trăm theo cài đặt âm lượng.
        void RefreshSliders()
        {
            RefreshSingle(masterSlider, masterValue, VolumeChannel.Master);
            RefreshSingle(musicSlider, musicValue, VolumeChannel.Music);
            RefreshSingle(sfxSlider, sfxValue, VolumeChannel.Sfx);
            RefreshSingle(uiSlider, uiValue, VolumeChannel.Ui);
        }

        // Cập nhật giá trị hiển thị của một kênh âm lượng.
        static void RefreshSingle(Slider slider, TMP_Text valueLabel, VolumeChannel channel)
        {
            float pct = AudioVolumeSettings.Get(channel) * 100f;
            if (slider != null) slider.SetValueWithoutNotify(pct);
            if (valueLabel != null) valueLabel.text = Mathf.RoundToInt(pct) + "%";
        }

        // Lắng nghe phím Esc để đóng/mở menu cài đặt khi không bận đặt/phá công trình hoặc mở bản đồ lớn.
        void Update()
        {
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

        // Mở menu cài đặt và tạm dừng thời gian trong game.
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            if (root != null) root.SetActive(true);
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            UiPointerState.IsOverUI = true;
            AudioDirector.Instance?.SetGameplayPaused(true);
            AudioDirector.Post(SoundId.UiOpen, Vector3.zero);
        }

        // Đóng menu cài đặt, lưu âm lượng và khôi phục tốc độ thời gian.
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (root != null) root.SetActive(false);
            Time.timeScale = previousTimeScale;
            UiPointerState.IsOverUI = false;
            AudioVolumeSettings.Save();
            AudioDirector.Instance?.SetGameplayPaused(false);
            AudioDirector.Post(SoundId.UiClose, Vector3.zero);
        }

        // Khôi phục âm lượng về mức mặc định.
        void ResetDefaults()
        {
            AudioVolumeSettings.ResetToDefault();
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
        }

        // Phát âm thanh click có giới hạn tần suất khi kéo thanh trượt.
        void PlayClickThrottled()
        {
            if (Time.unscaledTime - lastClickSound < ClickSoundInterval) return;
            lastClickSound = Time.unscaledTime;
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
        }
    }
}
