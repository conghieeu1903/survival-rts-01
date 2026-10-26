using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SurvivalRts
{
    // Điều khiển của người chơi: kéo chuột trái chọn tiểu đội (Shift thêm vào), nhấp đúp chọn cùng loại lính,
    // Ctrl+1..9 gán nhóm, 1..9 gọi nhóm, Q chọn cả quân, chuột phải ra lệnh di chuyển.
    [RequireComponent(typeof(BattleSimulation))]
    public class BattleInput : MonoBehaviour
    {
        const float ClickRadius = 30f;      // pixel quanh tâm tiểu đội để click chọn
        const float DragThreshold = 6f;     // quãng kéo (pixel) tối thiểu để tính là kéo khung
        const float MarkerSize = 12f;
        const float DoubleClickSeconds = 0.35f;
        const float MoveMarkSeconds = 1.2f; // thời gian giữ dấu điểm đích sau khi ra lệnh
        const int GroupCount = 9;

        [Header("Tham chiếu")]
        [Tooltip("Camera nhìn bản đồ")]
        [SerializeField] Camera viewCamera;

        [Tooltip("Để biết lần giữ chuột phải vừa rồi là xoay camera hay là ra lệnh")]
        [SerializeField] CameraController cameraController;

        [Header("Ra lệnh")]
        [Tooltip("Khoảng cách giữa các tiểu đội khi cùng được điều tới một điểm (m)")]
        [SerializeField] float squadSpacing = 16f;

        BattleSimulation simulation;
        readonly HashSet<int> selected = new HashSet<int>();
        readonly List<int> scratch = new List<int>();
        readonly List<int>[] groups = new List<int>[GroupCount];
        readonly List<Vector3> moveMarks = new List<Vector3>(); // (x, z) đích và thời điểm ra lệnh ở thành phần y
        Vector2 dragStart;
        bool dragging;
        bool pressStartedOnUi; // lần nhấn chuột trái này bắt đầu trên giao diện nên bỏ qua
        int lastClickSquad = -1;
        float lastClickTime = -10f;
        int hoverSquad = -1;
        GUIStyle labelStyle;

        public int SelectedCount => selected.Count;

        void Awake()
        {
            simulation = GetComponent<BattleSimulation>();
            if (viewCamera == null) viewCamera = Camera.main;
            for (int i = 0; i < GroupCount; i++) groups[i] = new List<int>();
        }

        void Update()
        {
            var net = LockstepNetwork.Instance;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (net == null || !net.InMatch || !simulation.Started || simulation.LocalTeam < 0)
            {
                if (selected.Count > 0) selected.Clear();
                dragging = false;
                return;
            }
            if (mouse == null || keyboard == null) return;
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return;

            PruneDead();
            bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            bool ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            var position = mouse.position.ReadValue();
            bool overUi = UiPointerState.IsOverUI;

            hoverSquad = overUi ? -1 : FindNearest(position);

            if (keyboard.qKey.wasPressedThisFrame) SelectAll();
            HandleGroups(keyboard, ctrl, shift);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                dragStart = position;
                dragging = false;
                pressStartedOnUi = overUi;
            }
            if (pressStartedOnUi)
            {
                if (mouse.leftButton.wasReleasedThisFrame) pressStartedOnUi = false;
            }
            else
            {
                if (mouse.leftButton.isPressed && (position - dragStart).magnitude > DragThreshold) dragging = true;
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    if (dragging) SelectInRect(Rect.MinMaxRect(
                        Mathf.Min(dragStart.x, position.x), Mathf.Min(dragStart.y, position.y),
                        Mathf.Max(dragStart.x, position.x), Mathf.Max(dragStart.y, position.y)), shift);
                    else Click(position, shift);
                    dragging = false;
                }
            }

            if (mouse.rightButton.wasReleasedThisFrame && !overUi
                && simulation.WinnerTeam < 0
                && (cameraController == null || !cameraController.RotatedWithRightButton)
                && selected.Count > 0)
            {
                IssueMove(position);
            }
        }

        // Ctrl+số gán nhóm, số gọi nhóm (Shift giữ lựa chọn hiện có).
        void HandleGroups(Keyboard keyboard, bool ctrl, bool shift)
        {
            for (int i = 0; i < GroupCount; i++)
            {
                if (!keyboard[Key.Digit1 + i].wasPressedThisFrame) continue;
                var group = groups[i];
                if (ctrl)
                {
                    group.Clear();
                    group.AddRange(selected);
                    continue;
                }
                if (group.Count == 0) continue;
                if (!shift) selected.Clear();
                foreach (int s in group)
                    if (simulation.TryGetSquad(simulation.LocalTeam, s, out _, out _)) selected.Add(s);
            }
        }

        // Bỏ các tiểu đội đã hết lính khỏi lựa chọn.
        void PruneDead()
        {
            if (selected.Count == 0) return;
            scratch.Clear();
            foreach (int s in selected)
                if (!simulation.TryGetSquad(simulation.LocalTeam, s, out _, out _)) scratch.Add(s);
            foreach (int s in scratch) selected.Remove(s);
        }

        // Chọn cả quân, giữ nguyên cách hoạt động cũ.
        public void SelectAll()
        {
            selected.Clear();
            for (int s = 0; s < simulation.SquadsPerTeam; s++)
                if (simulation.TryGetSquad(simulation.LocalTeam, s, out _, out _)) selected.Add(s);
        }

        // Chọn mọi tiểu đội còn sống thuộc một loại lính (chỉ số trong BattleData.unitTypes).
        public void SelectType(int type, bool add = false)
        {
            if (!add) selected.Clear();
            for (int s = 0; s < simulation.SquadsPerTeam; s++)
            {
                if (simulation.SquadType(simulation.LocalTeam, s) != type) continue;
                if (simulation.TryGetSquad(simulation.LocalTeam, s, out _, out _)) selected.Add(s);
            }
        }

        void SelectInRect(Rect rect, bool add)
        {
            if (!add) selected.Clear();
            for (int s = 0; s < simulation.SquadsPerTeam; s++)
            {
                if (!TryScreenPosition(s, out var screen)) continue;
                if (rect.Contains(screen)) selected.Add(s);
            }
        }

        // Nhấp một lần chọn tiểu đội gần nhất; nhấp đúp lên cùng tiểu đội thì chọn mọi tiểu đội cùng loại.
        void Click(Vector2 position, bool add)
        {
            int best = FindNearest(position);
            if (best < 0)
            {
                if (!add) selected.Clear();
                lastClickSquad = -1;
                return;
            }

            bool doubleClick = best == lastClickSquad && Time.unscaledTime - lastClickTime <= DoubleClickSeconds;
            lastClickSquad = best;
            lastClickTime = Time.unscaledTime;

            if (doubleClick)
            {
                SelectType(simulation.SquadType(simulation.LocalTeam, best), add);
                return;
            }
            if (!add) selected.Clear();
            if (add && selected.Contains(best)) selected.Remove(best);
            else selected.Add(best);
        }

        int FindNearest(Vector2 position)
        {
            int best = -1;
            float bestDistance = ClickRadius;
            for (int s = 0; s < simulation.SquadsPerTeam; s++)
            {
                if (!TryScreenPosition(s, out var screen)) continue;
                float distance = Vector2.Distance(screen, position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = s;
                }
            }
            return best;
        }

        bool TryScreenPosition(int squad, out Vector2 screen)
        {
            screen = default;
            if (!simulation.TryGetSquad(simulation.LocalTeam, squad, out var center, out _)) return false;
            var point = viewCamera.WorldToScreenPoint(new Vector3(center.x, 0f, center.y));
            if (point.z <= 0f) return false;
            screen = new Vector2(point.x, point.y);
            return true;
        }

        // Các tiểu đội được chọn xếp thành lưới quanh điểm bấm.
        void IssueMove(Vector2 screenPosition)
        {
            var ray = viewCamera.ScreenPointToRay(screenPosition);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return;
            var point = ray.GetPoint(distance);

            var squads = new List<int>(selected);
            squads.Sort();
            int columns = Mathf.CeilToInt(Mathf.Sqrt(squads.Count));
            int rows = (squads.Count + columns - 1) / columns;
            var net = LockstepNetwork.Instance;
            moveMarks.Clear();
            for (int k = 0; k < squads.Count; k++)
            {
                float offsetX = (k % columns - (columns - 1) * 0.5f) * squadSpacing;
                float offsetZ = (k / columns - (rows - 1) * 0.5f) * squadSpacing;
                net.SendCommand(new LockstepCommand
                {
                    Type = BattleCommand.MoveSquad,
                    Param = squads[k],
                    X = Mathf.RoundToInt((point.x + offsetX) * 100f),
                    Z = Mathf.RoundToInt((point.z + offsetZ) * 100f)
                });
                moveMarks.Add(new Vector3(point.x + offsetX, Time.unscaledTime, point.z + offsetZ));
            }
        }

        void OnGUI()
        {
            var net = LockstepNetwork.Instance;
            if (net == null || !net.InMatch || simulation == null || !simulation.Started || simulation.LocalTeam < 0) return;
            if (viewCamera == null || Event.current.type != EventType.Repaint) return;
            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };

            var previous = GUI.color;
            DrawMoveMarks();
            for (int s = 0; s < simulation.SquadsPerTeam; s++)
            {
                if (!TryScreenPosition(s, out var screen)) continue;
                if (screen.x < -MarkerSize || screen.y < -MarkerSize || screen.x > Screen.width + MarkerSize || screen.y > Screen.height + MarkerSize) continue;
                simulation.TryGetSquad(simulation.LocalTeam, s, out _, out int alive);
                var rect = new Rect(screen.x - MarkerSize, Screen.height - screen.y - MarkerSize, MarkerSize * 2f, MarkerSize * 2f);
                bool isSelected = selected.Contains(s);
                GUI.color = isSelected ? Color.yellow : Color.white;
                GUI.Box(rect, GUIContent.none);
                if (isSelected) DrawOutline(Rect.MinMaxRect(rect.xMin - 4f, rect.yMin - 4f, rect.xMax + 4f, rect.yMax + 4f), Color.yellow, 2f);
                else if (s == hoverSquad) DrawOutline(Rect.MinMaxRect(rect.xMin - 3f, rect.yMin - 3f, rect.xMax + 3f, rect.yMax + 3f), Color.white, 1f);
                var type = simulation.Data.unitTypes[simulation.SquadType(simulation.LocalTeam, s)].displayName;
                GUI.color = Color.white;
                GUI.Label(rect, (type.Length > 0 ? type.Substring(type.LastIndexOf(' ') + 1, 1) : "") + alive, labelStyle);
            }

            if (dragging)
            {
                var position = Mouse.current.position.ReadValue();
                var rect = Rect.MinMaxRect(
                    Mathf.Min(dragStart.x, position.x), Screen.height - Mathf.Max(dragStart.y, position.y),
                    Mathf.Max(dragStart.x, position.x), Screen.height - Mathf.Min(dragStart.y, position.y));
                GUI.color = new Color(1f, 1f, 0.2f, 0.25f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                DrawOutline(rect, new Color(1f, 1f, 0.2f, 0.9f), 1f);
            }
            GUI.color = previous;
        }

        // Dấu điểm đích vừa ra lệnh: vòng nhỏ mờ dần.
        void DrawMoveMarks()
        {
            for (int i = moveMarks.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - moveMarks[i].y;
                if (age > MoveMarkSeconds)
                {
                    moveMarks.RemoveAt(i);
                    continue;
                }
                var point = viewCamera.WorldToScreenPoint(new Vector3(moveMarks[i].x, 0f, moveMarks[i].z));
                if (point.z <= 0f) continue;
                float size = 10f + age * 14f;
                var rect = new Rect(point.x - size, Screen.height - point.y - size, size * 2f, size * 2f);
                DrawOutline(rect, new Color(0.6f, 1f, 0.6f, 1f - age / MoveMarkSeconds), 2f);
            }
        }

        // Vẽ viền chữ nhật bằng bốn đường mảnh.
        static void DrawOutline(Rect rect, Color color, float thickness)
        {
            var previous = GUI.color;
            GUI.color = color;
            var white = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, thickness), white);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), white);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, thickness, rect.height), white);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), white);
            GUI.color = previous;
        }
    }
}
