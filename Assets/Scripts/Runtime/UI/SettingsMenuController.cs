using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SurvivalRts
{
    // Menu cài đặt bật/tắt bằng Esc; tạm dừng game khi mở. Lúc đang đặt công trình thì Esc để hủy đặt, không mở menu.
    [RequireComponent(typeof(UIDocument))]
    public class SettingsMenuController : MonoBehaviour
    {
        const string HiddenClass = "hidden";
        const float ClickSoundInterval = 0.08f;

        [Header("Tham chiếu")]
        [Tooltip("Placer, để Esc lúc đang đặt công trình chỉ hủy đặt chứ không mở menu")]
        [SerializeField] BuildingPlacer placer;

        VisualElement root;
        float previousTimeScale = 1f;
        bool wasPlacing;
        float lastClickSound;

        public bool IsOpen { get; private set; }

        void OnEnable()
        {
            var document = GetComponent<UIDocument>().rootVisualElement;
            root = document.Q<VisualElement>("settingsRoot");

            Bind(document, "masterSlider", "masterValue", VolumeChannel.Master);
            Bind(document, "musicSlider", "musicValue", VolumeChannel.Music);
            Bind(document, "sfxSlider", "sfxValue", VolumeChannel.Sfx);
            Bind(document, "uiSlider", "uiValue", VolumeChannel.Ui);

            document.Q<Button>("resumeButton").clicked += Close;
            document.Q<Button>("defaultButton").clicked += ResetDefaults;
            AudioVolumeSettings.Changed += RefreshSliders;
        }

        void OnDisable()
        {
            AudioVolumeSettings.Changed -= RefreshSliders;
            if (IsOpen) Close();
        }

        void Bind(VisualElement document, string sliderName, string valueName, VolumeChannel channel)
        {
            var slider = document.Q<Slider>(sliderName);
            var value = document.Q<Label>(valueName);
            slider.userData = channel;
            value.userData = channel;
            slider.SetValueWithoutNotify(AudioVolumeSettings.Get(channel) * 100f);
            value.text = Mathf.RoundToInt(slider.value) + "%";

            slider.RegisterValueChangedCallback(evt =>
            {
                value.text = Mathf.RoundToInt(evt.newValue) + "%";
                AudioVolumeSettings.Set(channel, evt.newValue / 100f);
                PlayClickThrottled();
            });
        }

        void RefreshSliders()
        {
            root.Query<Slider>().ForEach(slider =>
            {
                var channel = (VolumeChannel)slider.userData;
                slider.SetValueWithoutNotify(AudioVolumeSettings.Get(channel) * 100f);
            });
            root.Query<Label>(className: "settings-row-value").ForEach(label =>
            {
                var channel = (VolumeChannel)label.userData;
                label.text = Mathf.RoundToInt(AudioVolumeSettings.Get(channel) * 100f) + "%";
            });
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            bool placing = placer != null && placer.IsPlacing;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !placing && !wasPlacing)
            {
                if (IsOpen) Close();
                else Open();
            }

            // Menu khác (menu xây dựng) có thể đặt lại cờ này khi chuột rời khỏi chúng; giữ chặn click xuống bản đồ khi menu đang mở.
            if (IsOpen) UiPointerState.IsOverUI = true;
        }

        void LateUpdate()
        {
            wasPlacing = placer != null && placer.IsPlacing;
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            root.RemoveFromClassList(HiddenClass);
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            UiPointerState.IsOverUI = true;
            AudioDirector.Instance?.SetGameplayPaused(true);
            AudioDirector.Post(SoundId.UiOpen, Vector3.zero);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            root.AddToClassList(HiddenClass);
            Time.timeScale = previousTimeScale;
            UiPointerState.IsOverUI = false;
            AudioVolumeSettings.Save();
            AudioDirector.Instance?.SetGameplayPaused(false);
            AudioDirector.Post(SoundId.UiClose, Vector3.zero);
        }

        void ResetDefaults()
        {
            AudioVolumeSettings.ResetToDefault();
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
        }

        void PlayClickThrottled()
        {
            if (Time.unscaledTime - lastClickSound < ClickSoundInterval) return;
            lastClickSound = Time.unscaledTime;
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
        }
    }
}
