using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Hiệu ứng phóng to khi rê chuột, thu nhỏ khi nhấn và phát âm thanh cho nút bấm uGUI.
    [DisallowMultipleComponent]
    public class UiButtonFeedback : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler
    {
        const float HoverScale = 1.06f;
        const float PressScale = 0.94f;
        const float ScaleSpeed = 18f;
        const float HoverSoundParam = 1f;

        Selectable selectable;
        Vector3 baseScale = Vector3.one;
        bool scaleInitialized;
        bool hovered;
        bool pressed;

        // Gắn hiệu ứng cho một nút nếu chưa có.
        public static void Attach(Component target)
        {
            if (target == null) return;
            if (target.GetComponent<UiButtonFeedback>() == null)
                target.gameObject.AddComponent<UiButtonFeedback>();
        }

        // Gắn hiệu ứng cho toàn bộ Button con bên dưới một gốc giao diện.
        public static void AttachAll(Component root)
        {
            if (root == null) return;
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                Attach(buttons[i]);
        }

        void Awake()
        {
            selectable = GetComponent<Selectable>();
            InitScale();
        }

        void OnEnable()
        {
            if (selectable == null) selectable = GetComponent<Selectable>();
            InitScale();
            hovered = false;
            pressed = false;
            transform.localScale = baseScale;
        }

        void OnDisable()
        {
            hovered = false;
            pressed = false;
            if (scaleInitialized) transform.localScale = baseScale;
        }

        void InitScale()
        {
            if (scaleInitialized) return;
            baseScale = transform.localScale;
            if (baseScale.sqrMagnitude < 0.0001f) baseScale = Vector3.one;
            scaleInitialized = true;
        }

        void Update()
        {
            bool interactable = selectable == null || selectable.IsInteractable();
            float factor = 1f;
            if (interactable)
            {
                if (pressed) factor = PressScale;
                else if (hovered) factor = HoverScale;
            }

            Vector3 target = baseScale * factor;
            if ((transform.localScale - target).sqrMagnitude > 0.000001f)
            {
                float t = 1f - Mathf.Exp(-ScaleSpeed * Time.unscaledDeltaTime);
                transform.localScale = Vector3.Lerp(transform.localScale, target, t);
            }
            else
            {
                transform.localScale = target;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            if (selectable == null || selectable.IsInteractable())
                AudioDirector.Post(SoundId.UiClick, Vector3.zero, HoverSoundParam);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (selectable != null && !selectable.IsInteractable()) return;
            pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            pressed = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (selectable != null && !selectable.IsInteractable()) return;
            AudioDirector.Post(SoundId.UiClick, Vector3.zero, 0f);
        }
    }
}
