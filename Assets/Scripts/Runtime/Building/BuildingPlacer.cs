using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace SurvivalRts
{
    public class BuildingPlacer : MonoBehaviour
    {
        [Header("Dữ liệu")]
        [Tooltip("Danh sách công trình có thể xây, hiện trên menu xây dựng theo nhóm và đúng thứ tự này")]
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

        [Header("Khung xây dựng")]
        [Tooltip("3 prefab khung giàn giáo theo tiến độ (Stage 1, Stage 2, Stage 3)")]
        [SerializeField] GameObject[] constructionStages;

        readonly Dictionary<BuildingData, int> savedTurns = new Dictionary<BuildingData, int>();
        readonly List<Renderer> cellRenderers = new List<Renderer>();
        readonly List<GameObject> cellObjects = new List<GameObject>();
        readonly List<BuildingData> visible = new List<BuildingData>();

        CameraController cameraController;
        BuildingCategory category;
        bool visibleDirty = true;
        BuildingData current;
        GameObject ghost;
        Renderer[] ghostRenderers;
        Transform cellRoot;
        LineRenderer radiusRing;
        int turns;
        bool lastValid;
        bool dirty;
        Vector3 lastCenter;
        int lastTurns;

        const string FrontMarkerName = "FrontMarker";
        const int RingSegments = 48;

        public event System.Action<BuildingData> CurrentChanged;
        public event System.Action<BuildingCategory> CategoryChanged;

        public bool IsPlacing => current != null;
        public BuildingData Current => current;
        public IReadOnlyList<BuildingData> Buildings => buildings;
        public BuildingCategory Category => category;

        // Công trình thuộc nhóm đang mở, theo thứ tự trong danh sách.
        public IReadOnlyList<BuildingData> Visible
        {
            get
            {
                if (visibleDirty)
                {
                    visible.Clear();
                    foreach (var data in buildings)
                        if (data != null && data.category == category) visible.Add(data);
                    visibleDirty = false;
                }
                return visible;
            }
        }

        public int CountIn(BuildingCategory value)
        {
            int count = 0;
            foreach (var data in buildings)
                if (data != null && data.category == value) count++;
            return count;
        }

        public void Toggle(BuildingData data)
        {
            if (current == data) Cancel();
            else Begin(data);
        }

        public void SetCategory(BuildingCategory value)
        {
            if (category == value) return;
            category = value;
            visibleDirty = true;
            CategoryChanged?.Invoke(category);
        }

        // Xoay bóng mờ 90°, hướng được nhớ cho lần đặt sau cùng loại công trình.
        public void Rotate()
        {
            if (current == null) return;
            turns = (turns + 1) & 3;
            savedTurns[current] = turns;
            dirty = true;
        }

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            cameraController = cam != null ? cam.GetComponent<CameraController>() : null;
            cellRoot = new GameObject("PlacementCells").transform;
            cellRoot.SetParent(transform, false);
            InitRadiusRing();
        }

        void InitRadiusRing()
        {
            var ringGo = new GameObject("HarvestRadiusRing");
            ringGo.transform.SetParent(transform, false);
            radiusRing = ringGo.AddComponent<LineRenderer>();
            radiusRing.useWorldSpace = true;
            radiusRing.loop = true;
            radiusRing.positionCount = RingSegments;
            radiusRing.startWidth = 0.08f;
            radiusRing.endWidth = 0.08f;
            radiusRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            radiusRing.receiveShadows = false;
            if (cellFree != null) radiusRing.sharedMaterial = cellFree;
            radiusRing.enabled = false;
        }

        void Update()
        {
            if (current == null) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) Rotate();
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    Cancel();
                    return;
                }
            }

            var mouse = Mouse.current;
            if (mouse == null) return;

            bool rightClicked = mouse.rightButton.wasReleasedThisFrame
                && !UiPointerState.IsOverUI
                && (cameraController == null || !cameraController.RotatedWithRightButton);
            if (rightClicked)
            {
                Cancel();
                return;
            }

            UpdateGhost(mouse.position.ReadValue());

            if (mouse.leftButton.wasPressedThisFrame && lastValid) Place();
        }

        void Begin(BuildingData data)
        {
            Cancel();
            if (data == null || data.prefab == null) return;

            ResourceNode.EnsureSceneNodesRegistered();
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

        public void Cancel()
        {
            bool wasPlacing = current != null;
            if (ghost != null) Destroy(ghost);
            ghost = null;
            current = null;
            HideCells();
            if (radiusRing != null) radiusRing.enabled = false;
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
                if (radiusRing != null) radiusRing.enabled = false;
                lastValid = false;
                dirty = true;
                return;
            }

            var grid = BuildGrid.Instance;
            var ray = cam.ScreenPointToRay(screenPosition);
            if (grid == null || !new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance))
            {
                ghost.SetActive(false);
                if (radiusRing != null) radiusRing.enabled = false;
                return;
            }

            var footprint = BuildGrid.RotatedFootprint(current.footprint, turns);
            var center = grid.Snap(ray.GetPoint(distance), footprint);
            center.y = 0f;

            // Với Xưởng đá: tự động xoay hướng mặt trước về phía mỏ đá tiếp giáp nếu có.
            if (current.harvestResource != ResourceType.None && current.requireAdjacentResource)
            {
                if (TryFindAdjacentResourceTurn(grid, center, current.footprint, current.harvestResource, turns, out int autoTurn))
                {
                    turns = autoTurn;
                    footprint = BuildGrid.RotatedFootprint(current.footprint, turns);
                }
            }

            // Kẻ địch luôn di chuyển, nên khi còn kẻ địch thì kiểm tra lại mỗi frame.
            if (!dirty && center == lastCenter && turns == lastTurns && EnemyWorld.AliveCount == 0) return;
            dirty = false;
            lastCenter = center;
            lastTurns = turns;

            var states = grid.Evaluate(center, footprint, out float baseY);
            bool enemyInside = EnemyInside(grid, center, footprint);
            bool resourceValid = EvaluateResourceRule(grid, center, footprint, turns);

            lastValid = !enemyInside && resourceValid;
            foreach (var state in states)
                if (state != CellState.Free) lastValid = false;

            ghost.SetActive(true);
            ghost.transform.SetPositionAndRotation(
                new Vector3(center.x, baseY, center.z),
                Quaternion.Euler(0f, 90f * turns, 0f));
            ApplyGhostMaterial(lastValid ? ghostValid : ghostInvalid);
            ShowCells(grid, center, footprint, states, baseY, enemyInside || !resourceValid);
            UpdateRadiusRing(center, baseY, lastValid);
        }

        bool EvaluateResourceRule(BuildGrid grid, Vector3 center, Vector2Int footprint, int currentTurn)
        {
            if (current.harvestResource == ResourceType.None) return true;

            if (current.requireAdjacentResource)
            {
                var min = grid.MinCell(center, footprint);
                return IsFacingAdjacentResource(min, footprint, currentTurn, current.harvestResource);
            }

            // Nếu harvestRadius <= 0 (như Trang trại tự canh tác trên đất), không yêu cầu có mỏ tài nguyên xung quanh.
            if (current.harvestRadius <= 0f) return true;

            return HasResourceInRadius(center, current.harvestResource, current.harvestRadius);
        }

        bool HasResourceInRadius(Vector3 center, ResourceType type, float radius)
        {
            float rSq = radius * radius;
            var all = ResourceNode.All;
            for (int i = 0; i < all.Count; i++)
            {
                var node = all[i];
                if (node == null || node.resourceType != type) continue;
                var diff = node.transform.position - center;
                diff.y = 0f;
                if (diff.sqrMagnitude <= rSq) return true;
            }
            return false;
        }

        bool TryFindAdjacentResourceTurn(BuildGrid grid, Vector3 center, Vector2Int baseFootprint,
            ResourceType type, int preferredTurn, out int matchedTurn)
        {
            for (int i = 0; i < 4; i++)
            {
                int t = (preferredTurn + i) & 3;
                var fp = BuildGrid.RotatedFootprint(baseFootprint, t);
                var min = grid.MinCell(center, fp);
                if (IsFacingAdjacentResource(min, fp, t, type))
                {
                    matchedTurn = t;
                    return true;
                }
            }
            matchedTurn = preferredTurn;
            return false;
        }

        // Kiểm tra cạnh phía trước (theo góc xoay turn) của công trình có nằm sát cạnh mỏ tài nguyên không.
        bool IsFacingAdjacentResource(Vector2Int min, Vector2Int footprint, int turn, ResourceType type)
        {
            var all = ResourceNode.All;
            for (int i = 0; i < all.Count; i++)
            {
                var node = all[i];
                if (node == null || node.resourceType != type || node.Occupant == null) continue;

                var nMin = node.Occupant.Min;
                var nFp = node.Occupant.footprint;

                bool overlapX = min.x < nMin.x + nFp.x && min.x + footprint.x > nMin.x;
                bool overlapY = min.y < nMin.y + nFp.y && min.y + footprint.y > nMin.y;

                // turn 0 (+Z): cạnh Bắc của nhà chạm cạnh Nam của mỏ
                if (turn == 0 && overlapX && min.y + footprint.y == nMin.y) return true;
                // turn 1 (+X): cạnh Đông của nhà chạm cạnh Tây của mỏ
                if (turn == 1 && overlapY && min.x + footprint.x == nMin.x) return true;
                // turn 2 (-Z): cạnh Nam của nhà chạm cạnh Bắc của mỏ
                if (turn == 2 && overlapX && min.y == nMin.y + nFp.y) return true;
                // turn 3 (-X): cạnh Tây của nhà chạm cạnh Đông của mỏ
                if (turn == 3 && overlapY && min.x == nMin.x + nFp.x) return true;
            }
            return false;
        }

        void UpdateRadiusRing(Vector3 center, float baseY, bool valid)
        {
            if (radiusRing == null) return;
            float r = GetDisplayRadius(current);
            bool show = r > 0f;

            radiusRing.enabled = show;
            if (!show) return;

            radiusRing.sharedMaterial = valid ? cellFree : cellBlocked;
            float y = baseY + 0.08f;
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i / (float)RingSegments * Mathf.PI * 2f;
                radiusRing.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * r, y, center.z + Mathf.Sin(angle) * r));
            }
        }

        static float GetDisplayRadius(BuildingData data)
        {
            if (data == null) return 0f;
            if (data.tower != null && data.tower.range > 0f) return data.tower.range;
            if (data.harvestResource != ResourceType.None && !data.requireAdjacentResource && data.harvestRadius > 0f)
                return data.harvestRadius;
            return 0f;
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

        void ShowCells(BuildGrid grid, Vector3 center, Vector2Int footprint, CellState[] states, float baseY, bool invalid)
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
                bool blocked = states[i] != CellState.Free || invalid;
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

            if (current.buildTime > 0f)
            {
                building.AddComponent<ConstructionSite>().Setup(current, constructionStages);
            }
            else
            {
                if (current.tower != null) building.AddComponent<Tower>().Setup(current.tower);
                if (current.populationProvided > 0) building.AddComponent<HouseBuilding>().Setup(current.populationProvided);
                if (current.harvestResource != ResourceType.None)
                {
                    building.AddComponent<ResourceCamp>().Setup(current);
                }
                else if (current.maxWorkers > 0)
                {
                    building.AddComponent<BuilderHut>().Setup(current.maxWorkers);
                }
            }

            var obstacle = building.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(current.footprint.x, current.height, current.footprint.y) * grid.CellSize;
            obstacle.center = new Vector3(0f, current.height * 0.5f, 0f);
            obstacle.carving = true;

            Cancel();
        }
    }
}
