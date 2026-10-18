using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace SurvivalRts
{
    public class BuildingPlacer : MonoBehaviour
    {
        [Header("Dữ liệu")]
        [Tooltip("Danh sách công trình có thể xây, phím 1-9 chọn theo thứ tự")]
        [SerializeField] BuildingData[] buildings;

        [Tooltip("Camera dùng để bắn tia từ chuột xuống mặt đất, mặc định là Main Camera")]
        [SerializeField] Camera cam;

        [Header("Bóng mờ khi đặt")]
        [Tooltip("Material của bóng mờ khi vị trí hợp lệ")]
        [SerializeField] Material ghostValid;

        [Tooltip("Material của bóng mờ khi vị trí không hợp lệ")]
        [SerializeField] Material ghostInvalid;

        [Header("Ô lưới hiển thị")]
        [Tooltip("Prefab một ô lưới, hiện dưới chân bóng mờ")]
        [SerializeField] GameObject gridCellPrefab;

        [Tooltip("Material của ô còn trống")]
        [SerializeField] Material cellFree;

        [Tooltip("Material của ô bị chặn")]
        [SerializeField] Material cellBlocked;

        readonly Dictionary<BuildingData, int> savedTurns = new Dictionary<BuildingData, int>();
        readonly List<Renderer> cellRenderers = new List<Renderer>();
        readonly List<GameObject> cellObjects = new List<GameObject>();

        CameraController cameraController;
        BuildingData current;
        GameObject ghost;
        Renderer[] ghostRenderers;
        Transform cellRoot;
        int turns;
        bool lastValid;
        bool dirty;
        Vector3 lastCenter;
        int lastTurns;

        const string FrontMarkerName = "FrontMarker";

        public event System.Action<BuildingData> CurrentChanged;

        public bool IsPlacing => current != null;
        public BuildingData Current => current;
        public IReadOnlyList<BuildingData> Buildings => buildings;

        public void Toggle(BuildingData data)
        {
            if (current == data) Cancel();
            else Begin(data);
        }

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            cameraController = cam.GetComponent<CameraController>();
            cellRoot = new GameObject("PlacementCells").transform;
            cellRoot.SetParent(transform, false);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            for (int i = 0; i < buildings.Length && i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame) Begin(buildings[i]);
            }

            if (current == null) return;

            bool rightClicked = mouse.rightButton.wasReleasedThisFrame
                && !UiPointerState.IsOverUI
                && (cameraController == null || !cameraController.RotatedWithRightButton);
            if (keyboard.escapeKey.wasPressedThisFrame || rightClicked)
            {
                Cancel();
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                turns = (turns + 1) & 3;
                savedTurns[current] = turns;
                dirty = true;
            }

            UpdateGhost(mouse.position.ReadValue());

            if (mouse.leftButton.wasPressedThisFrame && lastValid) Place();
        }

        void Begin(BuildingData data)
        {
            Cancel();
            if (data == null || data.prefab == null) return;

            current = data;
            savedTurns.TryGetValue(data, out turns);
            dirty = true;

            ghost = Instantiate(data.prefab);
            ghost.name = data.displayName + " (Ghost)";
            foreach (var collider in ghost.GetComponentsInChildren<Collider>())
                Destroy(collider);
            ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
            ghost.SetActive(false);
            CurrentChanged?.Invoke(current);
        }

        void Cancel()
        {
            bool wasPlacing = current != null;
            if (ghost != null) Destroy(ghost);
            ghost = null;
            current = null;
            HideCells();
            if (wasPlacing) CurrentChanged?.Invoke(null);
        }

        void HideCells()
        {
            foreach (var cell in cellObjects) cell.SetActive(false);
        }

        void UpdateGhost(Vector2 screenPosition)
        {
            if (UiPointerState.IsOverUI)
            {
                ghost.SetActive(false);
                HideCells();
                lastValid = false;
                dirty = true;
                return;
            }

            var grid = BuildGrid.Instance;
            var ray = cam.ScreenPointToRay(screenPosition);
            if (grid == null || !new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance))
            {
                ghost.SetActive(false);
                return;
            }

            var footprint = BuildGrid.RotatedFootprint(current.footprint, turns);
            var center = grid.Snap(ray.GetPoint(distance), footprint);
            center.y = 0f;

            // Kẻ địch luôn di chuyển, nên khi còn kẻ địch thì kiểm tra lại mỗi frame.
            if (!dirty && center == lastCenter && turns == lastTurns && EnemyWorld.AliveCount == 0) return;
            dirty = false;
            lastCenter = center;
            lastTurns = turns;

            var states = grid.Evaluate(center, footprint, out float baseY);
            bool enemyInside = EnemyInside(grid, center, footprint);
            lastValid = !enemyInside;
            foreach (var state in states)
                if (state != CellState.Free) lastValid = false;

            ghost.SetActive(true);
            ghost.transform.SetPositionAndRotation(
                new Vector3(center.x, baseY, center.z),
                Quaternion.Euler(0f, 90f * turns, 0f));
            ApplyGhostMaterial(lastValid ? ghostValid : ghostInvalid);
            ShowCells(grid, center, footprint, states, baseY, enemyInside);
        }

        bool EnemyInside(BuildGrid grid, Vector3 center, Vector2Int footprint)
        {
            var half = new Vector3(footprint.x, 0f, footprint.y) * grid.CellSize * 0.5f + new Vector3(-0.05f, 1f, -0.05f);
            if (EnemyWorld.AnyInRect(new Vector2(center.x, center.z), new Vector2(half.x, half.z))) return true;

            foreach (var hit in Physics.OverlapBox(center + Vector3.up, half, Quaternion.identity))
            {
                var unit = hit.GetComponentInParent<UnitMarker>();
                if (unit != null && unit.isEnemy) return true;
            }
            return false;
        }

        void ApplyGhostMaterial(Material material)
        {
            foreach (var r in ghostRenderers)
            {
                if (r.name == FrontMarkerName) continue;
                var materials = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                r.sharedMaterials = materials;
            }
        }

        void ShowCells(BuildGrid grid, Vector3 center, Vector2Int footprint, CellState[] states, float baseY, bool enemyInside)
        {
            int count = states.Length;
            while (cellObjects.Count < count)
            {
                var cell = Instantiate(gridCellPrefab, cellRoot);
                cellObjects.Add(cell);
                cellRenderers.Add(cell.GetComponentInChildren<Renderer>());
            }

            var min = grid.MinCell(center, footprint);
            for (int i = 0; i < cellObjects.Count; i++)
            {
                bool used = i < count;
                cellObjects[i].SetActive(used);
                if (!used) continue;

                var cellPos = grid.CellCenter(new Vector2Int(min.x + i % footprint.x, min.y + i / footprint.x));
                cellObjects[i].transform.position = new Vector3(cellPos.x, baseY + 0.05f, cellPos.z);
                bool blocked = states[i] != CellState.Free || enemyInside;
                cellRenderers[i].sharedMaterial = blocked ? cellBlocked : cellFree;
            }
        }

        void Place()
        {
            var grid = BuildGrid.Instance;
            var footprint = BuildGrid.RotatedFootprint(current.footprint, turns);

            var building = Instantiate(current.prefab, ghost.transform.position, ghost.transform.rotation);
            building.name = current.displayName;

            var occupant = building.AddComponent<GridOccupant>();
            occupant.footprint = footprint;
            occupant.isBuilding = true;
            occupant.Register();

            building.AddComponent<Health>().SetMax(current.maxHealth);
            building.AddComponent<PlayerTarget>();

            var obstacle = building.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(current.footprint.x, current.height, current.footprint.y) * grid.CellSize;
            obstacle.center = new Vector3(0f, current.height * 0.5f, 0f);
            obstacle.carving = true;

            Cancel();
        }
    }
}
