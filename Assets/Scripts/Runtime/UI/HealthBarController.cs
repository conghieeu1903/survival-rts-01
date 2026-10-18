using UnityEngine;
using UnityEngine.UIElements;

namespace SurvivalRts
{
    [RequireComponent(typeof(UIDocument))]
    public class HealthBarController : MonoBehaviour
    {
        const string HiddenClass = "hidden";
        const string LowClass = "health-fill--low";
        const float LowThreshold = 0.3f;
        const float HeightOffset = 0.6f;

        [Header("Tham chiếu")]
        [Tooltip("Selector báo công trình đang được chọn để hiện thanh máu")]
        [SerializeField] BuildingSelector selector;

        [Tooltip("Camera dùng để đổi vị trí 3D sang vị trí trên màn hình, mặc định là Main Camera")]
        [SerializeField] Camera cam;

        VisualElement bar;
        VisualElement fill;
        Label valueLabel;
        Health target;
        Renderer[] targetRenderers;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            bar = root.Q<VisualElement>("healthBar");
            fill = root.Q<VisualElement>("fill");
            valueLabel = root.Q<Label>("valueLabel");
            if (cam == null) cam = Camera.main;

            selector.SelectionChanged += OnSelectionChanged;
            OnSelectionChanged(selector.Selected);
        }

        void OnDisable()
        {
            if (selector != null) selector.SelectionChanged -= OnSelectionChanged;
            Unsubscribe();
        }

        void OnSelectionChanged(Health health)
        {
            Unsubscribe();
            target = health;

            bar.EnableInClassList(HiddenClass, target == null);
            if (target == null) return;

            targetRenderers = target.GetComponentsInChildren<Renderer>();
            target.Changed += Refresh;
            Refresh(target.Current, target.Max);
        }

        void Unsubscribe()
        {
            if (target != null) target.Changed -= Refresh;
            target = null;
        }

        void Refresh(float current, float max)
        {
            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            fill.style.width = Length.Percent(ratio * 100f);
            fill.EnableInClassList(LowClass, ratio <= LowThreshold);
            valueLabel.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(max);
        }

        void LateUpdate()
        {
            if (target == null) return;

            var bounds = targetRenderers[0].bounds;
            foreach (var r in targetRenderers) bounds.Encapsulate(r.bounds);
            var anchor = new Vector3(bounds.center.x, bounds.max.y + HeightOffset, bounds.center.z);

            var position = RuntimePanelUtils.CameraTransformWorldToPanel(bar.panel, anchor, cam);
            bar.style.left = position.x;
            bar.style.top = position.y;
        }
    }
}
