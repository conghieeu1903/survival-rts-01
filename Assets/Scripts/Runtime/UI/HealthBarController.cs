using UnityEngine;
using UnityEngine.UIElements;

namespace SurvivalRts
{
    [RequireComponent(typeof(UIDocument))]
    public class HealthBarController : MonoBehaviour
    {
        const string HiddenClass = "hidden";
        const string LowClass = "health-fill--low";
        const string DangerStatusClass = "health-status--danger";
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
        VisualElement demolishPanel;
        Button demolishButton;
        Label demolishConfirmLabel;
        Button confirmDemolishBtn;
        Button cancelDemolishBtn;

        BuildingPlacer placer;
        Health target;
        ConstructionSite targetSite;
        DemolishSite targetDemolish;
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
            demolishPanel = root.Q<VisualElement>("demolishPanel");
            demolishButton = root.Q<Button>("demolishButton");
            demolishConfirmLabel = root.Q<Label>("demolishConfirmLabel");
            confirmDemolishBtn = root.Q<Button>("confirmDemolishBtn");
            cancelDemolishBtn = root.Q<Button>("cancelDemolishBtn");

            if (workerPanel != null)
            {
                workerPanel.RegisterCallback<PointerEnterEvent>(_ => UiPointerState.IsOverUI = true);
                workerPanel.RegisterCallback<PointerLeaveEvent>(_ => UiPointerState.IsOverUI = false);
            }
            if (demolishPanel != null)
            {
                demolishPanel.RegisterCallback<PointerEnterEvent>(_ => UiPointerState.IsOverUI = true);
                demolishPanel.RegisterCallback<PointerLeaveEvent>(_ => UiPointerState.IsOverUI = false);
            }

            if (addWorkerButton != null) addWorkerButton.clicked += OnAddWorkerClicked;
            if (removeWorkerButton != null) removeWorkerButton.clicked += OnRemoveWorkerClicked;
            if (demolishButton != null) demolishButton.clicked += OnDemolishClicked;
            if (confirmDemolishBtn != null) confirmDemolishBtn.clicked += OnConfirmDemolishClicked;
            if (cancelDemolishBtn != null) cancelDemolishBtn.clicked += OnCancelDemolishClicked;

            if (cam == null) cam = Camera.main;
            placer = selector != null && selector.Placer != null ? selector.Placer : FindFirstObjectByType<BuildingPlacer>();
            if (placer != null) placer.DemolishStateChanged += OnDemolishStateChanged;

            selector.SelectionChanged += OnSelectionChanged;
            OnSelectionChanged(selector.Selected);
        }

        void OnDisable()
        {
            if (selector != null) selector.SelectionChanged -= OnSelectionChanged;
            if (placer != null) placer.DemolishStateChanged -= OnDemolishStateChanged;
            if (addWorkerButton != null) addWorkerButton.clicked -= OnAddWorkerClicked;
            if (removeWorkerButton != null) removeWorkerButton.clicked -= OnRemoveWorkerClicked;
            if (demolishButton != null) demolishButton.clicked -= OnDemolishClicked;
            if (confirmDemolishBtn != null) confirmDemolishBtn.clicked -= OnConfirmDemolishClicked;
            if (cancelDemolishBtn != null) cancelDemolishBtn.clicked -= OnCancelDemolishClicked;
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
                if (demolishPanel != null) demolishPanel.EnableInClassList(HiddenClass, true);
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

            BindDemolishSite();
            BindWorkerBuilding();
            RefreshConstructionStatus();
            RefreshDemolishPanel();
        }

        void OnDemolishStateChanged()
        {
            if (target == null) return;
            BindDemolishSite();
            BindWorkerBuilding();
            RefreshConstructionStatus();
            RefreshDemolishPanel();
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
            if (!ReferenceEquals(targetDemolish, null))
            {
                targetDemolish.ProgressChanged -= OnDemolishProgress;
                targetDemolish.Completed -= OnDemolishCompleted;
            }
            if (targetWorkerBuilding != null)
            {
                targetWorkerBuilding.WorkersChanged -= RefreshWorkers;
            }
            target = null;
            targetSite = null;
            targetDemolish = null;
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
            RefreshDemolishPanel();
        }

        void BindDemolishSite()
        {
            if (!ReferenceEquals(targetDemolish, null))
            {
                targetDemolish.ProgressChanged -= OnDemolishProgress;
                targetDemolish.Completed -= OnDemolishCompleted;
            }
            targetDemolish = target != null ? target.GetComponent<DemolishSite>() : null;
            if (targetDemolish != null)
            {
                targetDemolish.ProgressChanged += OnDemolishProgress;
                targetDemolish.Completed += OnDemolishCompleted;
                if (target != null) targetRenderers = target.GetComponentsInChildren<Renderer>(true);
            }
        }

        void OnDemolishProgress(float progress, float remaining, int stage)
        {
            RefreshConstructionStatus();
        }

        void OnDemolishCompleted(DemolishSite site)
        {
            OnSelectionChanged(null);
        }

        void BindWorkerBuilding()
        {
            if (targetWorkerBuilding != null) targetWorkerBuilding.WorkersChanged -= RefreshWorkers;
            targetWorkerBuilding = target != null && targetDemolish == null ? target.GetComponent<IWorkerBuilding>() : null;
            if (targetWorkerBuilding != null) targetWorkerBuilding.WorkersChanged += RefreshWorkers;
            RefreshWorkers();
        }

        void RefreshConstructionStatus()
        {
            if (statusLabel == null) return;

            if (targetDemolish != null)
            {
                int pct = Mathf.FloorToInt(targetDemolish.Progress * 100f);
                statusLabel.text = $"Đang tháo dỡ: {pct}% ({targetDemolish.Remaining:0.0}s · Khung {targetDemolish.StageNumber}/3)";
                statusLabel.EnableInClassList(DangerStatusClass, true);
                statusLabel.EnableInClassList(HiddenClass, false);
                return;
            }

            statusLabel.EnableInClassList(DangerStatusClass, false);

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
            bool show = targetDemolish == null
                && targetWorkerBuilding != null
                && (targetSite == null || targetSite.IsCompleted);
            workerPanel.EnableInClassList(HiddenClass, !show);
            if (!show) return;

            int free = PopulationManager.Instance != null ? PopulationManager.Instance.FreePopulation : 0;
            workerLabel.text = $"{targetWorkerBuilding.WorkerRoleName}: {targetWorkerBuilding.WorkerCount}/{targetWorkerBuilding.MaxWorkers} (Rỗi: {free})";
            addWorkerButton.SetEnabled(targetWorkerBuilding.CanAddWorker);
            removeWorkerButton.SetEnabled(targetWorkerBuilding.CanRemoveWorker);
        }

        void RefreshDemolishPanel()
        {
            if (demolishPanel == null) return;
            bool canDemolish = DemolishSite.CanDemolish(target);
            demolishPanel.EnableInClassList(HiddenClass, !canDemolish);
            if (!canDemolish) return;

            bool pendingConfirm = placer != null && placer.IsDemolishing && placer.PendingDemolishTarget == target;
            if (demolishButton != null) demolishButton.EnableInClassList(HiddenClass, pendingConfirm);
            if (demolishConfirmLabel != null)
            {
                var occ = target.GetComponent<GridOccupant>();
                float time = occ != null ? occ.DemolishTime : 2.5f;
                demolishConfirmLabel.text = $"Phá ({time:0.#}s)?";
                demolishConfirmLabel.EnableInClassList(HiddenClass, !pendingConfirm);
            }
            if (confirmDemolishBtn != null) confirmDemolishBtn.EnableInClassList(HiddenClass, !pendingConfirm);
            if (cancelDemolishBtn != null) cancelDemolishBtn.EnableInClassList(HiddenClass, !pendingConfirm);
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

        void OnDemolishClicked()
        {
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
            if (placer == null) placer = FindFirstObjectByType<BuildingPlacer>();
            if (placer != null && target != null) placer.RequestDemolish(target);
        }

        void OnConfirmDemolishClicked()
        {
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
            if (placer != null) placer.ConfirmDemolish();
        }

        void OnCancelDemolishClicked()
        {
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
            if (placer != null) placer.CancelDemolish();
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
