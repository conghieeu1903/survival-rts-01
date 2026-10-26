using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Điều khiển bản đồ thu nhỏ góc dưới trái và bản đồ lớn giữa màn hình (mở bằng phím M).
    public class MinimapController : MonoBehaviour
    {
        const int MapResolution = 240;
        const float RefreshInterval = 0.065f;

        static readonly Color32 GroundBase = new Color32(42, 42, 42, 255);
        static readonly Color32 GroundAlt = new Color32(48, 48, 48, 255);
        static readonly Color32 GroundHigh = new Color32(72, 72, 72, 255);
        static readonly Color32 SteepColor = new Color32(92, 92, 92, 255);
        static readonly Color32 WaterColor = new Color32(26, 26, 26, 255);
        static readonly Color32 BorderGridColor = new Color32(32, 32, 32, 255);

        static readonly Color32 WoodDot = new Color32(140, 140, 140, 255);
        static readonly Color32 StoneDot = new Color32(185, 185, 185, 255);
        static readonly Color32 IronDot = new Color32(160, 160, 160, 255);
        static readonly Color32 FoodDot = new Color32(205, 205, 205, 255);

        static readonly Color32 HqColor = new Color32(245, 245, 245, 255);
        static readonly Color32 BuildingColor = new Color32(210, 210, 210, 255);
        static readonly Color32 ConstructionColor = new Color32(150, 150, 150, 255);
        static readonly Color32 WallColor = new Color32(115, 115, 115, 255);
        static readonly Color32 UnitDot = new Color32(230, 230, 230, 255);
        static readonly Color32 EnemyDot = new Color32(95, 95, 95, 255);
        static readonly Color32 CameraFrameColor = new Color32(250, 250, 250, 245);

        [Header("Tham chiếu uGUI")]
        [SerializeField] RawImage minimapCanvas;
        [SerializeField] RawImage fullMapCanvas;

        Texture2D mapTexture;
        Color32[] terrainPixels;
        Color32[] framePixels;
        bool terrainReady;
        bool draggingMap;
        RawImage activeDragImage;
        float nextRefreshTime;

        CameraController camController;
        readonly List<Vector2> enemyPositions = new List<Vector2>(256); // Danh sách vị trí kẻ địch ECS còn sống
        readonly Vector3[] frustumCorners = new Vector3[4]; // 4 góc khung nhìn camera chiếu xuống mặt đất

        public static bool IsFullMapOpen { get; private set; }

        // Khởi tạo texture bản đồ và đăng ký sự kiện chuột trên cả bản đồ nhỏ và bản đồ lớn.
        void OnEnable()
        {
            ResolveReferences();
            if (minimapCanvas == null) return;

            if (mapTexture == null)
            {
                mapTexture = new Texture2D(MapResolution, MapResolution, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                terrainPixels = new Color32[MapResolution * MapResolution];
                framePixels = new Color32[MapResolution * MapResolution];
            }

            SetupMapImage(minimapCanvas);
            if (fullMapCanvas != null)
            {
                SetupMapImage(fullMapCanvas);
                fullMapCanvas.gameObject.SetActive(false);
            }

            IsFullMapOpen = false;
            terrainReady = false;
            nextRefreshTime = 0f;
        }

        // Đặt lại trạng thái khi tắt component.
        void OnDisable()
        {
            draggingMap = false;
            activeDragImage = null;
            IsFullMapOpen = false;
        }

        // Giải phóng bộ nhớ Texture2D khi hủy đối tượng.
        void OnDestroy()
        {
            if (mapTexture != null)
            {
                Destroy(mapTexture);
                mapTexture = null;
            }
        }

        // Gán texture, bộ chặn chuột và sự kiện click/kéo cho một RawImage bản đồ.
        void SetupMapImage(RawImage img)
        {
            img.texture = mapTexture;
            if (img.GetComponent<UiPointerBlocker>() == null)
            {
                img.gameObject.AddComponent<UiPointerBlocker>();
            }

            var trigger = img.GetComponent<EventTrigger>();
            if (trigger == null) trigger = img.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            AddTrigger(trigger, EventTriggerType.PointerDown, data => OnPointerDown(img, data));
            AddTrigger(trigger, EventTriggerType.Drag, data => OnDrag(img, data));
            AddTrigger(trigger, EventTriggerType.PointerUp, OnPointerUp);
        }

        // Tự động tìm hoặc chuẩn hóa các thành phần UI bản đồ nếu chưa được gán trong Inspector.
        void ResolveReferences()
        {
            if (minimapCanvas == null)
            {
                var t = FindChildRecursive(transform, "minimapCanvas");
                if (t != null) minimapCanvas = t.GetComponent<RawImage>();
            }

            var oldHeader = FindChildRecursive(transform, "minimapHeader");
            if (oldHeader != null) Destroy(oldHeader.gameObject);

            var oldPanel = FindChildRecursive(transform, "minimapPanel");
            if (oldPanel != null && minimapCanvas != null && minimapCanvas.transform.parent == oldPanel)
            {
                minimapCanvas.transform.SetParent(oldPanel.parent, false);
                var rect = minimapCanvas.rectTransform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                rect.anchoredPosition = new Vector2(16f, 16f);
                rect.sizeDelta = new Vector2(170f, 170f);
                Destroy(oldPanel.gameObject);
            }

            if (fullMapCanvas == null)
            {
                var t = FindChildRecursive(transform, "fullMapCanvas");
                if (t != null) fullMapCanvas = t.GetComponent<RawImage>();
            }

            if (fullMapCanvas == null && minimapCanvas != null)
            {
                var go = new GameObject("fullMapCanvas", typeof(RectTransform), typeof(RawImage), typeof(Outline), typeof(UiPointerBlocker));
                go.transform.SetParent(minimapCanvas.transform.parent, false);
                var fmRect = go.GetComponent<RectTransform>();
                fmRect.anchorMin = new Vector2(0.5f, 0.5f);
                fmRect.anchorMax = new Vector2(0.5f, 0.5f);
                fmRect.pivot = new Vector2(0.5f, 0.5f);
                fmRect.anchoredPosition = new Vector2(0f, 36f);
                fmRect.sizeDelta = new Vector2(460f, 460f);
                go.GetComponent<Outline>().effectColor = new Color(0.48f, 0.48f, 0.48f, 1f);
                fullMapCanvas = go.GetComponent<RawImage>();
                go.SetActive(false);
            }
        }

        // Tìm Transform con theo tên không phân biệt cấp bậc.
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

        // Thêm sự kiện chuột vào EventTrigger của RawImage.
        static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
        }

        // Lắng nghe phím M để bật/tắt bản đồ lớn và cập nhật hình ảnh bản đồ theo chu kỳ.
        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && Time.timeScale > 0f && fullMapCanvas != null)
            {
                bool currentlyOpen = fullMapCanvas.gameObject.activeSelf;
                if (kb.mKey.wasPressedThisFrame)
                {
                    SetFullMapOpen(!currentlyOpen);
                }
                else if (currentlyOpen && kb.escapeKey.wasPressedThisFrame)
                {
                    SetFullMapOpen(false);
                }
            }

            if (minimapCanvas == null || mapTexture == null) return;
            if (camController == null) camController = FindAnyObjectByType<CameraController>();

            if (!terrainReady && BuildGrid.Instance != null)
            {
                BakeTerrainLayer();
            }

            if (Time.unscaledTime >= nextRefreshTime)
            {
                nextRefreshTime = Time.unscaledTime + RefreshInterval;
                RenderMinimap();
            }
        }

        // Bật hoặc tắt hiển thị bản đồ lớn ở giữa màn hình.
        public void SetFullMapOpen(bool open)
        {
            if (fullMapCanvas == null) return;
            IsFullMapOpen = open;
            if (fullMapCanvas.gameObject.activeSelf == open) return;
            fullMapCanvas.gameObject.SetActive(open);
            AudioDirector.Post(open ? SoundId.UiOpen : SoundId.UiClose, Vector3.zero);
        }

        // Bắt đầu di chuyển camera khi nhấn chuột trái lên bản đồ.
        void OnPointerDown(RawImage targetImage, BaseEventData data)
        {
            var evt = (PointerEventData)data;
            if (evt.button != PointerEventData.InputButton.Left) return;
            draggingMap = true;
            activeDragImage = targetImage;
            UiPointerState.IsOverUI = true;
            MoveCameraToPointer(targetImage, evt);
        }

        // Di chuyển camera theo chuột khi kéo trên bản đồ.
        void OnDrag(RawImage targetImage, BaseEventData data)
        {
            if (!draggingMap || activeDragImage != targetImage) return;
            MoveCameraToPointer(targetImage, (PointerEventData)data);
        }

        // Kết thúc kéo chuột trên bản đồ.
        void OnPointerUp(BaseEventData data)
        {
            var evt = (PointerEventData)data;
            if (evt.button != PointerEventData.InputButton.Left || !draggingMap) return;
            draggingMap = false;
            activeDragImage = null;
            UiPointerState.IsOverUI = false;
        }

        // Chuyển tọa độ chuột trên RawImage sang tọa độ mặt đất để đưa camera tới.
        void MoveCameraToPointer(RawImage targetImage, PointerEventData evt)
        {
            if (camController == null || targetImage == null) return;
            var rectTransform = targetImage.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, evt.position, evt.pressEventCamera, out Vector2 localPos))
                return;

            var rect = rectTransform.rect;
            float u = Mathf.Clamp01((localPos.x - rect.xMin) / Mathf.Max(1f, rect.width));
            float v = Mathf.Clamp01((localPos.y - rect.yMin) / Mathf.Max(1f, rect.height));

            GetWorldBounds(out Vector2 minWorld, out Vector2 sizeWorld);
            float worldX = minWorld.x + u * sizeWorld.x;
            float worldZ = minWorld.y + v * sizeWorld.y;
            camController.FocusOnGroundPoint(new Vector3(worldX, 0f, worldZ));
            RenderMinimap();
        }

        // Lấy tọa độ góc dưới trái và kích thước bản đồ thế giới từ BuildGrid.
        void GetWorldBounds(out Vector2 min, out Vector2 size)
        {
            var grid = BuildGrid.Instance;
            if (grid != null)
            {
                min = new Vector2(grid.Origin.x, grid.Origin.z);
                size = new Vector2(grid.Size.x * grid.CellSize, grid.Size.y * grid.CellSize);
                return;
            }
            min = new Vector2(-25f, -25f);
            size = new Vector2(50f, 50f);
        }

        // Quy đổi tọa độ thế giới sang tọa độ điểm ảnh trên texture bản đồ.
        Vector2Int WorldToPixel(float worldX, float worldZ, Vector2 minWorld, Vector2 sizeWorld)
        {
            int px = Mathf.Clamp(Mathf.RoundToInt((worldX - minWorld.x) / sizeWorld.x * (MapResolution - 1)), 0, MapResolution - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt((worldZ - minWorld.y) / sizeWorld.y * (MapResolution - 1)), 0, MapResolution - 1);
            return new Vector2Int(px, py);
        }

        // Nướng dữ liệu mặt đất, độ dốc và vùng nước từ BuildGrid vào mảng điểm ảnh nền.
        void BakeTerrainLayer()
        {
            var grid = BuildGrid.Instance;
            if (grid == null) return;

            var terrain = grid.Terrain;
            var heights = grid.Heights;
            int gxCount = grid.Size.x;
            int gzCount = grid.Size.y;

            Bounds waterBounds = default;
            bool hasWater = false;
            var waterGo = GameObject.Find("Water_Pond");
            if (waterGo != null)
            {
                var r = waterGo.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    waterBounds = r.bounds;
                    hasWater = true;
                }
            }

            GetWorldBounds(out Vector2 minWorld, out Vector2 sizeWorld);

            for (int py = 0; py < MapResolution; py++)
            {
                float v = py / (float)(MapResolution - 1);
                float wz = minWorld.y + v * sizeWorld.y;
                int cz = Mathf.Clamp(Mathf.FloorToInt(v * gzCount), 0, gzCount - 1);

                for (int px = 0; px < MapResolution; px++)
                {
                    float u = px / (float)(MapResolution - 1);
                    float wx = minWorld.x + u * sizeWorld.x;
                    int cx = Mathf.Clamp(Mathf.FloorToInt(u * gxCount), 0, gxCount - 1);

                    int cellIdx = cx + cz * gxCount;
                    var state = terrain[cellIdx];
                    float h = heights[cellIdx];

                    Color32 col = ((cx + cz) & 1) == 0 ? GroundBase : GroundAlt;

                    if (hasWater && wx >= waterBounds.min.x && wx <= waterBounds.max.x && wz >= waterBounds.min.z && wz <= waterBounds.max.z)
                    {
                        col = WaterColor;
                    }
                    else if (state == CellState.Steep)
                    {
                        col = SteepColor;
                    }
                    else if (h > 0.25f)
                    {
                        float t = Mathf.Clamp01(h / 1.6f);
                        col = Color32.Lerp(GroundBase, GroundHigh, t);
                    }
                    else if (state == CellState.NoNavMesh)
                    {
                        col = WaterColor;
                    }

                    if ((px % 48 == 0 || py % 48 == 0) && col.r != WaterColor.r)
                    {
                        col = Color32.Lerp(col, BorderGridColor, 0.35f);
                    }

                    terrainPixels[py * MapResolution + px] = col;
                }
            }

            terrainReady = true;
        }

        // Vẽ toàn bộ tài nguyên, công trình, đơn vị và khung nhìn camera lên texture.
        void RenderMinimap()
        {
            if (!terrainReady)
            {
                for (int i = 0; i < framePixels.Length; i++) framePixels[i] = GroundBase;
            }
            else
            {
                System.Array.Copy(terrainPixels, framePixels, terrainPixels.Length);
            }

            GetWorldBounds(out Vector2 minWorld, out Vector2 sizeWorld);

            var nodes = ResourceNode.All;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null) continue;
                var pos = node.transform.position;
                var p = WorldToPixel(pos.x, pos.z, minWorld, sizeWorld);

                Color32 c = WoodDot;
                int radius = 3;
                if (node.resourceType == ResourceType.Stone) { c = StoneDot; radius = 4; }
                else if (node.resourceType == ResourceType.Iron) { c = IronDot; radius = 4; }
                else if (node.resourceType == ResourceType.Food) { c = FoodDot; radius = 3; }

                DrawFilledRect(p.x, p.y, radius, c);
            }

            var occupants = FindObjectsByType<GridOccupant>(FindObjectsSortMode.None);
            for (int i = 0; i < occupants.Length; i++)
            {
                var occ = occupants[i];
                if (occ == null || occ.GetComponent<ResourceNode>() != null) continue;

                var pos = occ.transform.position;
                var p = WorldToPixel(pos.x, pos.z, minWorld, sizeWorld);
                int halfSpan = Mathf.Clamp(Mathf.Max(occ.footprint.x, occ.footprint.y) + 1, 3, 6);

                if (occ.GetComponent<Headquarters>() != null)
                {
                    DrawFilledRect(p.x, p.y, 5, HqColor);
                }
                else if (occ.isBuilding)
                {
                    var col = occ.GetComponent<DemolishSite>() != null ? EnemyDot
                        : occ.GetComponent<ConstructionSite>() != null ? ConstructionColor
                        : BuildingColor;
                    DrawFilledRect(p.x, p.y, halfSpan, col);
                }
                else
                {
                    DrawFilledRect(p.x, p.y, 2, WallColor);
                }
            }

            var villagers = FindObjectsByType<VillagerUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < villagers.Length; i++)
            {
                if (!villagers[i].gameObject.activeInHierarchy) continue;
                var pos = villagers[i].transform.position;
                var p = WorldToPixel(pos.x, pos.z, minWorld, sizeWorld);
                DrawFilledRect(p.x, p.y, 2, UnitDot);
            }

            var builders = FindObjectsByType<BuilderUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < builders.Length; i++)
            {
                var pos = builders[i].transform.position;
                var p = WorldToPixel(pos.x, pos.z, minWorld, sizeWorld);
                DrawFilledRect(p.x, p.y, 2, UnitDot);
            }

            var harvesters = FindObjectsByType<HarvesterUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < harvesters.Length; i++)
            {
                var pos = harvesters[i].transform.position;
                var p = WorldToPixel(pos.x, pos.z, minWorld, sizeWorld);
                DrawFilledRect(p.x, p.y, 2, UnitDot);
            }

            EnemyWorld.CopyAlivePositions(enemyPositions);
            for (int i = 0; i < enemyPositions.Count; i++)
            {
                var ep = enemyPositions[i];
                var p = WorldToPixel(ep.x, ep.y, minWorld, sizeWorld);
                DrawFilledRect(p.x, p.y, 2, EnemyDot);
            }

            if (camController != null && camController.GetGroundFrustumCorners(frustumCorners))
            {
                var c0 = WorldToPixel(frustumCorners[0].x, frustumCorners[0].z, minWorld, sizeWorld);
                var c1 = WorldToPixel(frustumCorners[1].x, frustumCorners[1].z, minWorld, sizeWorld);
                var c2 = WorldToPixel(frustumCorners[2].x, frustumCorners[2].z, minWorld, sizeWorld);
                var c3 = WorldToPixel(frustumCorners[3].x, frustumCorners[3].z, minWorld, sizeWorld);

                DrawLine(c0.x, c0.y, c1.x, c1.y, CameraFrameColor);
                DrawLine(c1.x, c1.y, c2.x, c2.y, CameraFrameColor);
                DrawLine(c2.x, c2.y, c3.x, c3.y, CameraFrameColor);
                DrawLine(c3.x, c3.y, c0.x, c0.y, CameraFrameColor);
            }

            mapTexture.SetPixels32(framePixels);
            mapTexture.Apply(false);
        }

        // Tô một hình vuông đặc tâm (cx, cy) bán kính radius trên mảng điểm ảnh.
        void DrawFilledRect(int cx, int cy, int radius, Color32 color)
        {
            int xMin = Mathf.Max(0, cx - radius);
            int xMax = Mathf.Min(MapResolution - 1, cx + radius);
            int yMin = Mathf.Max(0, cy - radius);
            int yMax = Mathf.Min(MapResolution - 1, cy + radius);

            for (int y = yMin; y <= yMax; y++)
            {
                int row = y * MapResolution;
                for (int x = xMin; x <= xMax; x++)
                {
                    framePixels[row + x] = color;
                }
            }
        }

        // Vẽ đoạn thẳng nối 2 điểm ảnh bằng thuật toán Bresenham.
        void DrawLine(int x0, int y0, int x1, int y1, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            int maxSteps = MapResolution * 2;
            while (maxSteps-- > 0)
            {
                if (x0 >= 0 && x0 < MapResolution && y0 >= 0 && y0 < MapResolution)
                {
                    framePixels[y0 * MapResolution + x0] = color;
                }
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }
    }
}
