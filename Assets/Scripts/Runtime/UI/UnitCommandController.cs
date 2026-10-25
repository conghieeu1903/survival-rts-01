using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Bảng lệnh uGUI và TextMeshPro khi chọn một lính: điều di chuyển tới điểm canh gác hoặc quay về trại.
    public class UnitCommandController : MonoBehaviour
    {
        static readonly Color BtnNormalBg = new Color(0.22f, 0.22f, 0.22f, 1f);
        static readonly Color BtnActiveBg = new Color(0.88f, 0.88f, 0.88f, 1f);
        static readonly Color BtnNormalText = new Color(0.94f, 0.94f, 0.94f, 1f);
        static readonly Color BtnActiveText = new Color(0.08f, 0.08f, 0.08f, 1f);

        [Header("Tham chiếu")]
        [Tooltip("Selector báo đối tượng đang được chọn; bảng chỉ hiện khi chọn một lính")]
        [SerializeField] BuildingSelector selector;

        [Tooltip("Camera dùng để bắn tia từ chuột xuống mặt đất, mặc định là Main Camera")]
        [SerializeField] Camera cam;

        [Header("Phần tử uGUI")]
        [SerializeField] GameObject root;
        [SerializeField] RectTransform commandPanel;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text status;
        [SerializeField] Button moveButton;
        [SerializeField] Button returnButton;
        [SerializeField] Button cancelButton;

        static bool targeting;
        static int consumedClickFrame = -1; // Frame vừa tiêu thụ click đặt điểm đến để tránh bỏ chọn lính

        Image moveButtonBg;
        TMP_Text moveButtonText;
        SoldierUnit soldier;

        public static bool BlocksSelection => targeting || consumedClickFrame == Time.frameCount;

        // Khởi tạo tham chiếu uGUI và đăng ký sự kiện nút bấm.
        void OnEnable()
        {
            ResolveReferences();

            if (commandPanel != null && commandPanel.GetComponent<UiPointerBlocker>() == null)
            {
                commandPanel.gameObject.AddComponent<UiPointerBlocker>();
            }

            if (moveButton != null) moveButton.onClick.AddListener(StartTargeting);
            if (returnButton != null) returnButton.onClick.AddListener(ReturnToBarracks);
            if (cancelButton != null) cancelButton.onClick.AddListener(StopTargeting);

            if (cam == null) cam = Camera.main;
            if (selector == null) selector = FindFirstObjectByType<BuildingSelector>();
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
            if (moveButton != null) moveButton.onClick.RemoveListener(StartTargeting);
            if (returnButton != null) returnButton.onClick.RemoveListener(ReturnToBarracks);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(StopTargeting);
            targeting = false;
        }

        // Tự động tìm các đối tượng uGUI con theo tên nếu chưa được gán trong Inspector.
        void ResolveReferences()
        {
            if (root == null)
            {
                var t = FindChildRecursive(transform, "commandRoot");
                if (t != null) root = t.gameObject;
            }
            if (commandPanel == null) commandPanel = FindComponent<RectTransform>("commandPanel");
            if (root == null && commandPanel != null) root = commandPanel.gameObject;

            if (title == null) title = FindComponent<TMP_Text>("commandTitle");
            if (status == null) status = FindComponent<TMP_Text>("commandStatus");
            if (moveButton == null) moveButton = FindComponent<Button>("moveButton");
            if (returnButton == null) returnButton = FindComponent<Button>("returnButton");
            if (cancelButton == null) cancelButton = FindComponent<Button>("cancelButton");

            if (moveButton != null)
            {
                moveButtonBg = moveButton.GetComponent<Image>();
                moveButtonText = moveButton.GetComponentInChildren<TMP_Text>(true);
            }
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

        // Đổi lính đang điều khiển khi người chơi chọn đối tượng khác.
        void OnSelectionChanged(Health health)
        {
            soldier = health != null ? health.GetComponent<SoldierUnit>() : null;
            targeting = false;
            Refresh();
        }

        // Bật chế độ chọn điểm đến trên bản đồ.
        void StartTargeting()
        {
            if (soldier == null) return;
            targeting = true;
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
            Refresh();
        }

        // Tắt chế độ chọn điểm đến.
        void StopTargeting()
        {
            targeting = false;
            Refresh();
        }

        // Lệnh cho lính quay về vị trí tập kết trước trại lính.
        void ReturnToBarracks()
        {
            if (soldier == null) return;
            soldier.ReturnToBarracks();
            targeting = false;
            AudioDirector.Post(SoundId.UiClick, Vector3.zero);
            Refresh();
        }

        // Xử lý click chuột lên mặt đất khi đang chọn điểm di chuyển cho lính.
        void Update()
        {
            if (soldier == null)
            {
                if (root != null && root.activeSelf) OnSelectionChanged(null);
                return;
            }
            if (!targeting) return;

            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame)
            {
                StopTargeting();
                return;
            }
            if (!mouse.leftButton.wasPressedThisFrame || UiPointerState.IsOverUI) return;

            if (TryGetGroundPoint(mouse.position.ReadValue(), out var point))
            {
                soldier.CommandMove(point);
                targeting = false;
                consumedClickFrame = Time.frameCount;
                AudioDirector.Post(SoundId.UiPlace, point);
                Refresh();
            }
        }

        // Lấy điểm trên mặt đất dưới con trỏ chuột, bỏ qua ô đã bị chiếm.
        bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 point)
        {
            point = default;
            if (cam == null) cam = Camera.main;
            if (cam == null) return false;

            var ray = cam.ScreenPointToRay(screenPosition);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return false;
            point = ray.GetPoint(distance);

            var grid = BuildGrid.Instance;
            if (grid == null) return true;
            var cell = grid.MinCell(point, Vector2Int.one);
            if (!grid.InBounds(cell) || grid.GetOccupancy(cell) != CellOccupancy.None) return false;
            point.y = grid.Heights[grid.Index(cell)];
            return true;
        }

        // Cập nhật nội dung và trạng thái hiển thị của bảng lệnh.
        void Refresh()
        {
            bool show = soldier != null;
            if (root != null) root.SetActive(show);
            if (!show) return;

            if (title != null) title.text = soldier.Data != null ? soldier.Data.displayName : soldier.name;
            if (status != null)
            {
                status.text = targeting ? "Click lên bản đồ để chọn điểm đến"
                    : soldier.IsOnCustomPost ? "Đang canh gác tại điểm được điều tới"
                    : "Đang canh gác trước trại";
            }

            if (moveButtonBg != null) moveButtonBg.color = targeting ? BtnActiveBg : BtnNormalBg;
            if (moveButtonText != null)
            {
                moveButtonText.color = targeting ? BtnActiveText : BtnNormalText;
                moveButtonText.fontStyle = targeting ? FontStyles.Bold : FontStyles.Normal;
            }

            if (cancelButton != null) cancelButton.gameObject.SetActive(targeting);
            if (returnButton != null) returnButton.interactable = soldier.IsOnCustomPost && !targeting;
            if (commandPanel != null) LayoutRebuilder.ForceRebuildLayoutImmediate(commandPanel);
        }
    }
}
