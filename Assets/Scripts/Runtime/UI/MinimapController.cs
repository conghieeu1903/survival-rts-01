using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SurvivalRts
{
    // Điều khiển Minimap góc dưới bên trái: nướng lớp địa hình từ BuildGrid, vẽ tài nguyên, công trình,
    // dân, kẻ địch ECS, khung nhìn camera và cho phép click/kéo chuột để di chuyển camera trên bản đồ.
    [RequireComponent(typeof(UIDocument))]
    public class MinimapController : MonoBehaviour
    {
        const int MapResolution = 180;
        const float RefreshInterval = 0.065f;

        static readonly Color32 GroundBase = new Color32(34, 45, 32, 255);
        static readonly Color32 GroundAlt = new Color32(38, 50, 35, 255);
        static readonly Color32 GroundHigh = new Color32(56, 66, 46, 255);
        static readonly Color32 SteepColor = new Color32(72, 66, 56, 255);
        static readonly Color32 WaterColor = new Color32(36, 78, 108, 255);
        static readonly Color32 BorderGridColor = new Color32(26, 34, 24, 255);

        static readonly Color32 WoodDot = new Color32(68, 148, 58, 255);
        static readonly Color32 StoneDot = new Color32(168, 176, 186, 255);
        static readonly Color32 IronDot = new Color32(116, 168, 218, 255);
        static readonly Color32 FoodDot = new Color32(226, 152, 54, 255);

        static readonly Color32 HqColor = new Color32(245, 202, 68, 255);
        static readonly Color32 BuildingColor = new Color32(78, 214, 138, 255);
        static readonly Color32 ConstructionColor = new Color32(232, 178, 72, 255);
        static readonly Color32 WallColor = new Color32(118, 122, 114, 255);
        static readonly Color32 UnitDot = new Color32(158, 242, 112, 255);
        static readonly Color32 EnemyDot = new Color32(238, 62, 54, 255);
        static readonly Color32 CameraFrameColor = new Color32(242, 245, 240, 245);

        VisualElement minimapPanel;
        VisualElement minimapCanvas;
        Texture2D mapTexture;
        Color32[] terrainPixels;
        Color32[] framePixels;
        bool terrainReady;
        bool draggingMap;
        float nextRefreshTime;

        CameraController camController;
        readonly List<Vector2> enemyPositions = new List<Vector2>(256);
        readonly Vector3[] frustumCorners = new Vector3[4];

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            if (doc == null || doc.rootVisualElement == null) return;

            var root = doc.rootVisualElement;
            minimapPanel = root.Q<VisualElement>("minimapPanel");
            minimapCanvas = root.Q<VisualElement>("minimapCanvas");
            if (minimapCanvas == null) return;

            if (mapTexture == null)
            {
                mapTexture = new Texture2D(MapResolution, MapResolution, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                terrainPixels = new Color32[MapResolution * MapResolution];
                framePixels = new Color32[MapResolution * MapResolution];
            }

            minimapCanvas.style.backgroundImage = new StyleBackground(mapTexture);

            if (minimapPanel != null)
            {
                minimapPanel.RegisterCallback<PointerEnterEvent>(_ => UiPointerState.IsOverUI = true);
                minimapPanel.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (!draggingMap) UiPointerState.IsOverUI = false;
                });
            }

            minimapCanvas.RegisterCallback<PointerDownEvent>(OnPointerDown);
            minimapCanvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            minimapCanvas.RegisterCallback<PointerUpEvent>(OnPointerUp);

            terrainReady = false;
            nextRefreshTime = 0f;
        }

        void OnDisable()
        {
            draggingMap = false;
        }

        void OnDestroy()
        {
            if (mapTexture != null)
            {
                Destroy(mapTexture);
                mapTexture = null;
            }
        }

        void Update()
        {
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

        void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            draggingMap = true;
            UiPointerState.IsOverUI = true;
            minimapCanvas.CapturePointer(evt.pointerId);
            MoveCameraToPointer(evt.localPosition);
            evt.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (!draggingMap || !minimapCanvas.HasPointerCapture(evt.pointerId)) return;
            MoveCameraToPointer(evt.localPosition);
            evt.StopPropagation();
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.button != 0 || !draggingMap) return;
            draggingMap = false;
            if (minimapCanvas.HasPointerCapture(evt.pointerId))
                minimapCanvas.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void MoveCameraToPointer(Vector2 localPos)
        {
            if (camController == null) return;
            float rectW = Mathf.Max(1f, minimapCanvas.contentRect.width);
            float rectH = Mathf.Max(1f, minimapCanvas.contentRect.height);

            // Trong UI Toolkit, gốc (0,0) ở góc trên-trái nên trục Y đi từ trên xuống, cần đảo lại cho trục Z thế giới.
            float u = Mathf.Clamp01(localPos.x / rectW);
            float v = 1f - Mathf.Clamp01(localPos.y / rectH);

            GetWorldBounds(out Vector2 minWorld, out Vector2 sizeWorld);
            float worldX = minWorld.x + u * sizeWorld.x;
            float worldZ = minWorld.y + v * sizeWorld.y;
            camController.FocusOnGroundPoint(new Vector3(worldX, 0f, worldZ));
            RenderMinimap();
        }

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

        Vector2Int WorldToPixel(float worldX, float worldZ, Vector2 minWorld, Vector2 sizeWorld)
        {
            int px = Mathf.Clamp(Mathf.RoundToInt((worldX - minWorld.x) / sizeWorld.x * (MapResolution - 1)), 0, MapResolution - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt((worldZ - minWorld.y) / sizeWorld.y * (MapResolution - 1)), 0, MapResolution - 1);
            return new Vector2Int(px, py);
        }

        void BakeTerrainLayer()
        {
            var grid = BuildGrid.Instance;
            if (grid == null) return;

            var terrain = grid.Terrain;
            var heights = grid.Heights;
            int gxCount = grid.Size.x;
            int gzCount = grid.Size.y;

            // Tìm vị trí hồ nước nếu có trong scene để tô màu nước chính xác.
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

                    // Vẽ đường chia lưới mờ cứ mỗi 10m để dễ định vị khu vực.
                    if ((px % 36 == 0 || py % 36 == 0) && col.r != WaterColor.r)
                    {
                        col = Color32.Lerp(col, BorderGridColor, 0.35f);
                    }

                    terrainPixels[py * MapResolution + px] = col;
                }
            }

            terrainReady = true;
        }

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

            // 1. Vẽ các mỏ tài nguyên (Gỗ, Đá, Sắt, Nông sản)
            var nodes = ResourceNode.All;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null) continue;
                var pos = node.transform.position;
                var p = WorldToPixel(pos.x, pos.z, minWorld, sizeWorld);

                Color32 c = WoodDot;
                int radius = 2;
                if (node.resourceType == ResourceType.Stone) { c = StoneDot; radius = 3; }
                else if (node.resourceType == ResourceType.Iron) { c = IronDot; radius = 3; }
                else if (node.resourceType == ResourceType.Food) { c = FoodDot; radius = 2; }

                DrawFilledRect(p.x, p.y, radius, c);
            }

            // 2. Vẽ công trình phe mình và vật cản tĩnh
            var occupants = FindObjectsByType<GridOccupant>(FindObjectsSortMode.None);
            for (int i = 0; i < occupants.Length; i++)
            {
                var occ = occupants[i];
                if (occ == null || occ.GetComponent<ResourceNode>() != null) continue;

                var pos = occ.transform.position;
                var p = WorldToPixel(pos.x, pos.z, minWorld, sizeWorld);
                int halfSpan = Mathf.Clamp(Mathf.Max(occ.footprint.x, occ.footprint.y), 2, 5);

                if (occ.GetComponent<Headquarters>() != null)
                {
                    DrawFilledRect(p.x, p.y, 4, HqColor);
                }
                else if (occ.isBuilding)
                {
                    var col = occ.GetComponent<ConstructionSite>() != null ? ConstructionColor : BuildingColor;
                    DrawFilledRect(p.x, p.y, halfSpan, col);
                }
                else
                {
                    DrawFilledRect(p.x, p.y, 2, WallColor);
                }
            }

            // 3. Vẽ các đơn vị dân / thợ xây / người thu hoạch
            var villagers = FindObjectsByType<VillagerUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < villagers.Length; i++)
            {
                if (!villagers[i].gameObject.activeInHierarchy) continue;
                var pos = villagers[i].transform.position;
                var p = WorldToPixel(pos.x, pos.z, minWorld, sizeWorld);
                DrawFilledRect(p.x, p.y, 1, UnitDot);
            }

            var builders = FindObjectsByType<BuilderUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < builders.Length; i++)
            {
                var pos = builders[i].transform.position;
                var p = WorldToPixel(pos.x, pos.z, minWorld, sizeWorld);
                DrawFilledRect(p.x, p.y, 1, UnitDot);
            }

            var harvesters = FindObjectsByType<HarvesterUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < harvesters.Length; i++)
            {
                var pos = harvesters[i].transform.position;
                var p = WorldToPixel(pos.x, pos.z, minWorld, sizeWorld);
                DrawFilledRect(p.x, p.y, 1, UnitDot);
            }

            // 4. Vẽ chấm đỏ cho toàn bộ kẻ địch ECS đang sống
            EnemyWorld.CopyAlivePositions(enemyPositions);
            for (int i = 0; i < enemyPositions.Count; i++)
            {
                var ep = enemyPositions[i];
                var p = WorldToPixel(ep.x, ep.y, minWorld, sizeWorld);
                DrawFilledRect(p.x, p.y, 1, EnemyDot);
            }

            // 5. Vẽ khung trắng thể hiện vùng nhìn hiện tại của Camera trên mặt đất
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
