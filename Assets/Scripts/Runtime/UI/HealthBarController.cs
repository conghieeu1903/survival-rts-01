using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Hiển thị thanh máu, tiến độ xây/phá và bảng điều phối thợ bằng uGUI và TextMeshPro trên đầu công trình đang chọn.
    public class HealthBarController : MonoBehaviour
    {
        const float LowThreshold = 0.3f;
        const float HeightOffset = 0.6f;

        static readonly Color FillNormalColor = new Color(0.80f, 0.80f, 0.80f, 1f);
        static readonly Color FillLowColor = new Color(0.48f, 0.48f, 0.48f, 1f);
        static readonly Color StatusNormalColor = new Color(0.94f, 0.94f, 0.94f, 1f);
        static readonly Color StatusDangerColor = new Color(0.78f, 0.78f, 0.78f, 1f);

        [Header("Tham chiếu")]
        [Tooltip("Selector báo công trình đang được chọn để hiện thanh máu và bảng thợ")]
        [SerializeField] BuildingSelector selector;

        [Tooltip("Camera dùng để đổi vị trí 3D sang vị trí trên màn hình, mặc định là Main Camera")]
        [SerializeField] Camera cam;

        [Header("Phần tử uGUI")]
        [SerializeField] RectTransform canvasRect;
        [SerializeField] RectTransform bar;
        [SerializeField] RectTransform fillRect;
        [SerializeField] Image fillImage;
        [SerializeField] TMP_Text valueLabel;
        [SerializeField] GameObject statusBox;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] GameObject workerPanel;
        [SerializeField] TMP_Text workerLabel;
        [SerializeField] Button addWorkerButton;
        [SerializeField] Button removeWorkerButton;
        [SerializeField] GameObject demolishPanel;
        [SerializeField] Button demolishButton;
        [SerializeField] TMP_Text demolishConfirmLabel;
        [SerializeField] Button confirmDemolishBtn;
        [SerializeField] Button cancelDemolishBtn;

        TMP_Text addWorkerButtonText;
        Canvas parentCanvas;
        BuildingPlacer placer;
        Health target;
        ConstructionSite targetSite;
        DemolishSite targetDemolish;
        IWorkerBuilding targetWorkerBuilding;
        Renderer[] targetRenderers;

        // Khởi tạo tham chiếu uGUI và đăng ký sự kiện chọn công trình.
        void OnEnable()
        {
            ResolveReferences();
            UiButtonFeedback.AttachAll(this);

            if (workerPanel != null && workerPanel.GetComponent<UiPointerBlocker>() == null)
                workerPanel.AddComponent<UiPointerBlocker>();
            if (demolishPanel != null && demolishPanel.GetComponent<UiPointerBlocker>() == null)
                demolishPanel.AddComponent<UiPointerBlocker>();

            if (addWorkerButton != null) addWorkerButton.onClick.AddListener(OnAddWorkerClicked);
            if (removeWorkerButton != null) removeWorkerButton.onClick.AddListener(OnRemoveWorkerClicked);
            if (demolishButton != null) demolishButton.onClick.AddListener(OnDemolishClicked);
            if (confirmDemolishBtn != null) confirmDemolishBtn.onClick.AddListener(OnConfirmDemolishClicked);
            if (cancelDemolishBtn != null) cancelDemolishBtn.onClick.AddListener(OnCancelDemolishClicked);

            if (cam == null) cam = Camera.main;
            if (selector == null) selector = FindFirstObjectByType<BuildingSelector>();
            placer = selector != null && selector.Placer != null ? selector.Placer : FindFirstObjectByType<BuildingPlacer>();
            if (placer != null) placer.DemolishStateChanged += OnDemolishStateChanged;

            if (selector != null)
            {
                selector.SelectionChanged += OnSelectionChanged;
                OnSelectionChanged(selector.Selected);
            }
        }

        // Hủy đăng ký sự kiện khi tắt component.
        void OnDisable()
        {
            if (selector != null) selector.SelectionChanged -= OnSelectionChanged;
            if (placer != null) placer.DemolishStateChanged -= OnDemolishStateChanged;
            if (addWorkerButton != null) addWorkerButton.onClick.RemoveListener(OnAddWorkerClicked);
            if (removeWorkerButton != null) removeWorkerButton.onClick.RemoveListener(OnRemoveWorkerClicked);
            if (demolishButton != null) demolishButton.onClick.RemoveListener(OnDemolishClicked);
            if (confirmDemolishBtn != null) confirmDemolishBtn.onClick.RemoveListener(OnConfirmDemolishClicked);
            if (cancelDemolishBtn != null) cancelDemolishBtn.onClick.RemoveListener(OnCancelDemolishClicked);
            Unsubscribe();
            UiPointerState.IsOverUI = false;
        }

        // Tự động tìm các thành phần uGUI theo tên nếu chưa được gán trong Inspector.
        void ResolveReferences()
        {
            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null) parentCanvas = GetComponentInChildren<Canvas>(true);
            if (canvasRect == null && parentCanvas != null)
                canvasRect = parentCanvas.GetComponent<RectTransform>();

            if (bar == null) bar = FindComponent<RectTransform>("healthBar");
            if (fillRect == null) fillRect = FindComponent<RectTransform>("fill");
            if (fillImage == null && fillRect != null) fillImage = fillRect.GetComponent<Image>();
            if (valueLabel == null) valueLabel = FindComponent<TMP_Text>("valueLabel");
            if (statusLabel == null) statusLabel = FindComponent<TMP_Text>("statusLabel");
            if (statusBox == null)
            {
                var t = FindChildRecursive(transform, "statusBox");
                if (t != null) statusBox = t.gameObject;
                else if (statusLabel != null) statusBox = statusLabel.gameObject;
            }
            if (workerPanel == null)
            {
                var t = FindChildRecursive(transform, "workerPanel");
                if (t != null) workerPanel = t.gameObject;
            }
            if (workerLabel == null) workerLabel = FindComponent<TMP_Text>("workerLabel");
            if (addWorkerButton == null) addWorkerButton = FindComponent<Button>("addWorkerButton");
            if (addWorkerButton != null && addWorkerButtonText == null)
                addWorkerButtonText = addWorkerButton.GetComponentInChildren<TMP_Text>(true);
            if (removeWorkerButton == null) removeWorkerButton = FindComponent<Button>("removeWorkerButton");

            if (demolishPanel == null)
            {
                var t = FindChildRecursive(transform, "demolishPanel");
                if (t != null) demolishPanel = t.gameObject;
            }
            if (demolishButton == null) demolishButton = FindComponent<Button>("demolishButton");
            if (demolishConfirmLabel == null) demolishConfirmLabel = FindComponent<TMP_Text>("demolishConfirmLabel");
            if (confirmDemolishBtn == null) confirmDemolishBtn = FindComponent<Button>("confirmDemolishBtn");
            if (cancelDemolishBtn == null) cancelDemolishBtn = FindComponent<Button>("cancelDemolishBtn");
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

        // Cập nhật giao diện khi đổi đối tượng được chọn.
        void OnSelectionChanged(Health health)
        {
            Unsubscribe();
            if (health != null && health.IsDead) health = null;
            target = health;

            if (bar != null) bar.gameObject.SetActive(target != null);
            if (target == null)
            {
                if (statusBox != null) statusBox.SetActive(false);
                if (workerPanel != null) workerPanel.SetActive(false);
                if (demolishPanel != null) demolishPanel.SetActive(false);
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
            UpdatePosition();
        }

        // Làm mới bảng tháo dỡ và trạng thái công trình khi chế độ phá thay đổi.
        void OnDemolishStateChanged()
        {
            if (target == null) return;
            BindDemolishSite();
            BindWorkerBuilding();
            RefreshConstructionStatus();
            RefreshDemolishPanel();
        }

        // Ẩn thanh máu khi mục tiêu bị phá hủy.
        void OnTargetDied()
        {
            OnSelectionChanged(null);
        }

        // Ngắt đăng ký sự kiện với mục tiêu cũ.
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

        // Cập nhật nhãn tiến độ khi công trình đang xây.
        void OnSiteProgress(float progress, int stage)
        {
            RefreshConstructionStatus();
        }

        // Cập nhật renderer và bảng thợ khi công trình xây xong.
        void OnSiteCompleted(ConstructionSite site)
        {
            if (target != null) targetRenderers = target.GetComponentsInChildren<Renderer>(true);
            BindWorkerBuilding();
            RefreshConstructionStatus();
            RefreshDemolishPanel();
        }

        // Liên kết sự kiện với DemolishSite nếu công trình đang trong quá trình tháo dỡ.
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

        // Cập nhật nhãn trạng thái khi tiến độ tháo dỡ thay đổi.
        void OnDemolishProgress(float progress, float remaining, int stage)
        {
            RefreshConstructionStatus();
        }

        // Ẩn thanh máu khi tháo dỡ hoàn tất.
        void OnDemolishCompleted(DemolishSite site)
        {
            OnSelectionChanged(null);
        }

        // Liên kết sự kiện số lượng thợ với công trình có phân công lao động.
        void BindWorkerBuilding()
        {
            if (targetWorkerBuilding != null) targetWorkerBuilding.WorkersChanged -= RefreshWorkers;
            targetWorkerBuilding = target != null && targetDemolish == null ? target.GetComponent<IWorkerBuilding>() : null;
            if (targetWorkerBuilding != null) targetWorkerBuilding.WorkersChanged += RefreshWorkers;
            RefreshWorkers();
        }

        // Hiển thị tiến độ xây dựng, tháo dỡ hoặc thông tin nhà dân.
        void RefreshConstructionStatus()
        {
            if (statusLabel == null || statusBox == null) return;

            if (targetDemolish != null)
            {
                int pct = Mathf.FloorToInt(targetDemolish.Progress * 100f);
                statusLabel.text = $"Đang tháo dỡ: {pct}% ({targetDemolish.Remaining:0.0}s · Khung {targetDemolish.StageNumber}/3)";
                statusLabel.color = StatusDangerColor;
                statusBox.SetActive(true);
                return;
            }

            statusLabel.color = StatusNormalColor;

            if (targetSite != null && !targetSite.IsCompleted)
            {
                int pct = Mathf.FloorToInt(targetSite.Progress * 100f);
                statusLabel.text = $"Đang xây: {pct}% (Khung {targetSite.StageIndex}/3)";
                statusBox.SetActive(true);
                return;
            }

            var house = target != null ? target.GetComponent<HouseBuilding>() : null;
            if (house != null)
            {
                statusLabel.text = $"Nhà dân (+{house.Provided} dân)";
                statusBox.SetActive(true);
                return;
            }

            statusBox.SetActive(false);
        }

        // Cập nhật số lượng thợ/lính và trạng thái nút cộng trừ.
        void RefreshWorkers()
        {
            if (workerPanel == null) return;
            bool show = targetDemolish == null
                && targetWorkerBuilding != null
                && (targetSite == null || targetSite.IsCompleted);
            workerPanel.SetActive(show);
            if (!show) return;

            int free = PopulationManager.Instance != null ? PopulationManager.Instance.FreePopulation : 0;
            if (workerLabel != null)
                workerLabel.text = $"{targetWorkerBuilding.WorkerRoleName}: {targetWorkerBuilding.WorkerCount}/{targetWorkerBuilding.MaxWorkers} (Rỗi: {free})";
            if (addWorkerButton != null) addWorkerButton.interactable = targetWorkerBuilding.CanAddWorker;
            if (removeWorkerButton != null) removeWorkerButton.interactable = targetWorkerBuilding.CanRemoveWorker;
            if (addWorkerButtonText != null) addWorkerButtonText.text = targetWorkerBuilding is Barracks ? "+ Lính" : "+ Thợ";
        }

        // Cập nhật các nút trong bảng tháo dỡ dưới thanh máu.
        void RefreshDemolishPanel()
        {
            if (demolishPanel == null) return;
            bool canDemolish = DemolishSite.CanDemolish(target);
            demolishPanel.SetActive(canDemolish);
            if (!canDemolish) return;

            bool pendingConfirm = placer != null && placer.IsDemolishing && placer.PendingDemolishTarget == target;
            if (demolishButton != null) demolishButton.gameObject.SetActive(!pendingConfirm);
            if (demolishConfirmLabel != null)
            {
                var occ = target.GetComponent<GridOccupant>();
                float time = occ != null ? occ.DemolishTime : 2.5f;
                demolishConfirmLabel.text = $"Phá ({time:0.#}s)?";
                demolishConfirmLabel.gameObject.SetActive(pendingConfirm);
            }
            if (confirmDemolishBtn != null) confirmDemolishBtn.gameObject.SetActive(pendingConfirm);
            if (cancelDemolishBtn != null) cancelDemolishBtn.gameObject.SetActive(pendingConfirm);
            if (bar != null) LayoutRebuilder.ForceRebuildLayoutImmediate(bar);
        }

        // Thêm 1 công nhân hoặc lính vào công trình.
        void OnAddWorkerClicked()
        {
            if (targetWorkerBuilding != null) targetWorkerBuilding.TryAddWorker();
            RefreshWorkers();
        }

        // Bớt 1 công nhân khỏi công trình.
        void OnRemoveWorkerClicked()
        {
            if (targetWorkerBuilding != null) targetWorkerBuilding.TryRemoveWorker();
            RefreshWorkers();
        }

        // Yêu cầu tháo dỡ công trình đang chọn.
        void OnDemolishClicked()
        {
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
            if (placer == null) placer = FindFirstObjectByType<BuildingPlacer>();
            if (placer != null && target != null) placer.RequestDemolish(target);
        }

        // Xác nhận tháo dỡ công trình.
        void OnConfirmDemolishClicked()
        {
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
            if (placer != null) placer.ConfirmDemolish();
        }

        // Hủy yêu cầu tháo dỡ công trình.
        void OnCancelDemolishClicked()
        {
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
            if (placer != null) placer.CancelDemolish();
        }

        // Cập nhật độ rộng thanh máu và nhãn số máu.
        void Refresh(float current, float max)
        {
            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (fillRect != null)
            {
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(ratio, 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
            }
            if (fillImage != null)
            {
                fillImage.color = ratio <= LowThreshold ? FillLowColor : FillNormalColor;
            }
            if (valueLabel != null)
            {
                valueLabel.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(max);
            }
        }

        // Bám vị trí thanh máu theo đỉnh của đối tượng 3D mỗi khung hình.
        void LateUpdate()
        {
            if (!ReferenceEquals(target, null) && (target == null || target.IsDead))
            {
                OnSelectionChanged(null);
                return;
            }
            UpdatePosition();
        }

        // Tính toán hộp giới hạn của mục tiêu và quy đổi sang tọa độ neo trên Canvas.
        void UpdatePosition()
        {
            if (target == null || bar == null || canvasRect == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Bounds bounds = default;
            bool hasBounds = false;
            if (targetRenderers != null)
            {
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
            }
            if (!hasBounds) bounds = new Bounds(target.transform.position + Vector3.up, Vector3.one);

            var anchor = new Vector3(bounds.center.x, bounds.max.y + HeightOffset, bounds.center.z);
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, anchor);
            Camera uiCam = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? parentCanvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCam, out Vector2 localPoint))
            {
                bar.anchoredPosition = localPoint;
            }
        }
    }
}
