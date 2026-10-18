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
        [Tooltip("Selector báo công trình đang được chọn để hiện thanh máu và bảng thợ")]
        [SerializeField] BuildingSelector selector;

        [Tooltip("Camera dùng để đổi vị trí 3D sang vị trí trên màn hình, mặc định là Main Camera")]
        [SerializeField] Camera cam;

        VisualElement bar;
        VisualElement fill;
        Label valueLabel;
        Label statusLabel;
        VisualElement workerPanel;
        Label workerLabel;
        Button addWorkerButton;
        Button removeWorkerButton;

        Health target;
        ConstructionSite targetSite;
        IWorkerBuilding targetWorkerBuilding;
        Renderer[] targetRenderers;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            bar = root.Q<VisualElement>("healthBar");
            fill = root.Q<VisualElement>("fill");
            valueLabel = root.Q<Label>("valueLabel");
            statusLabel = root.Q<Label>("statusLabel");
            workerPanel = root.Q<VisualElement>("workerPanel");
            workerLabel = root.Q<Label>("workerLabel");
            addWorkerButton = root.Q<Button>("addWorkerButton");
            removeWorkerButton = root.Q<Button>("removeWorkerButton");

            if (workerPanel != null)
            {
                workerPanel.RegisterCallback<PointerEnterEvent>(_ => UiPointerState.IsOverUI = true);
                workerPanel.RegisterCallback<PointerLeaveEvent>(_ => UiPointerState.IsOverUI = false);
            }
            if (addWorkerButton != null) addWorkerButton.clicked += OnAddWorkerClicked;
            if (removeWorkerButton != null) removeWorkerButton.clicked += OnRemoveWorkerClicked;

            if (cam == null) cam = Camera.main;

            selector.SelectionChanged += OnSelectionChanged;
            OnSelectionChanged(selector.Selected);
        }

        void OnDisable()
        {
            if (selector != null) selector.SelectionChanged -= OnSelectionChanged;
            if (addWorkerButton != null) addWorkerButton.clicked -= OnAddWorkerClicked;
            if (removeWorkerButton != null) removeWorkerButton.clicked -= OnRemoveWorkerClicked;
            Unsubscribe();
            UiPointerState.IsOverUI = false;
        }

        void OnSelectionChanged(Health health)
        {
            Unsubscribe();
            if (health != null && health.IsDead) health = null;
            target = health;

            bar.EnableInClassList(HiddenClass, target == null);
            if (target == null)
            {
                if (statusLabel != null) statusLabel.EnableInClassList(HiddenClass, true);
                if (workerPanel != null) workerPanel.EnableInClassList(HiddenClass, true);
                UiPointerState.IsOverUI = false;
                return;
            }

            targetRenderers = target.GetComponentsInChildren<Renderer>(true);
            target.Changed += Refresh;
            target.Died += OnTargetDied;
            Refresh(target.Current, target.Max);

            targetSite = target.GetComponent<ConstructionSite>();
            if (targetSite != null && !targetSite.IsCompleted)
            {
                targetSite.ProgressChanged += OnSiteProgress;
                targetSite.Completed += OnSiteCompleted;
            }

            BindWorkerBuilding();
            RefreshConstructionStatus();
        }

        void OnTargetDied()
        {
            OnSelectionChanged(null);
        }

        void Unsubscribe()
        {
            if (!ReferenceEquals(target, null))
            {
                target.Changed -= Refresh;
                target.Died -= OnTargetDied;
            }
            if (!ReferenceEquals(targetSite, null))
            {
                targetSite.ProgressChanged -= OnSiteProgress;
                targetSite.Completed -= OnSiteCompleted;
            }
            if (targetWorkerBuilding != null)
            {
                targetWorkerBuilding.WorkersChanged -= RefreshWorkers;
            }
            target = null;
            targetSite = null;
            targetWorkerBuilding = null;
        }

        void OnSiteProgress(float progress, int stage)
        {
            RefreshConstructionStatus();
        }

        void OnSiteCompleted(ConstructionSite site)
        {
            if (target != null) targetRenderers = target.GetComponentsInChildren<Renderer>(true);
            BindWorkerBuilding();
            RefreshConstructionStatus();
        }

        void BindWorkerBuilding()
        {
            if (targetWorkerBuilding != null) targetWorkerBuilding.WorkersChanged -= RefreshWorkers;
            targetWorkerBuilding = target != null ? target.GetComponent<IWorkerBuilding>() : null;
            if (targetWorkerBuilding != null) targetWorkerBuilding.WorkersChanged += RefreshWorkers;
            RefreshWorkers();
        }

        void RefreshConstructionStatus()
        {
            if (statusLabel == null) return;
            if (targetSite != null && !targetSite.IsCompleted)
            {
                int pct = Mathf.FloorToInt(targetSite.Progress * 100f);
                statusLabel.text = $"Đang xây: {pct}% (Khung {targetSite.StageIndex}/3)";
                statusLabel.EnableInClassList(HiddenClass, false);
                return;
            }

            var house = target != null ? target.GetComponent<HouseBuilding>() : null;
            if (house != null)
            {
                statusLabel.text = $"Nhà dân (+{house.Provided} dân)";
                statusLabel.EnableInClassList(HiddenClass, false);
                return;
            }

            statusLabel.EnableInClassList(HiddenClass, true);
        }

        void RefreshWorkers()
        {
            if (workerPanel == null) return;
            bool show = targetWorkerBuilding != null && (targetSite == null || targetSite.IsCompleted);
            workerPanel.EnableInClassList(HiddenClass, !show);
            if (!show) return;

            int free = PopulationManager.Instance != null ? PopulationManager.Instance.FreePopulation : 0;
            workerLabel.text = $"{targetWorkerBuilding.WorkerRoleName}: {targetWorkerBuilding.WorkerCount}/{targetWorkerBuilding.MaxWorkers} (Rỗi: {free})";
            addWorkerButton.SetEnabled(targetWorkerBuilding.CanAddWorker);
            removeWorkerButton.SetEnabled(targetWorkerBuilding.CanRemoveWorker);
        }

        void OnAddWorkerClicked()
        {
            if (targetWorkerBuilding != null) targetWorkerBuilding.TryAddWorker();
            RefreshWorkers();
        }

        void OnRemoveWorkerClicked()
        {
            if (targetWorkerBuilding != null) targetWorkerBuilding.TryRemoveWorker();
            RefreshWorkers();
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
            if (!ReferenceEquals(target, null) && (target == null || target.IsDead))
            {
                OnSelectionChanged(null);
                return;
            }
            if (target == null || targetRenderers == null || targetRenderers.Length == 0) return;

            Bounds bounds = default;
            bool hasBounds = false;
            foreach (var r in targetRenderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
            if (!hasBounds) bounds = new Bounds(target.transform.position + Vector3.up, Vector3.one);

            var anchor = new Vector3(bounds.center.x, bounds.max.y + HeightOffset, bounds.center.z);
            var position = RuntimePanelUtils.CameraTransformWorldToPanel(bar.panel, anchor, cam);
            bar.style.left = position.x;
            bar.style.top = position.y;
        }
    }
}
