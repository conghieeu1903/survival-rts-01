using System.Collections.Generic;
using UnityEngine;

namespace SurvivalRts
{
    public struct VegetationRecord
    {
        public int WorldX;
        public int WorldZ;
        public int HeightSub;
        public int YawDegrees;
        public bool IsLargeBlock;
        public bool Cleared;
    }

    // Sinh tự động cây lớn (Block) và cây nhỏ/bụi (Clear) theo từng chunk 50m từ hạt giống số nguyên tất định.
    public class ProceduralVegetation : MonoBehaviour
    {
        public static ProceduralVegetation Instance { get; private set; }

        [Header("Hạt giống & Phạm vi")]
        [Tooltip("Hạt giống số nguyên để sinh cây đồng nhất trên mọi máy")]
        [SerializeField] int seed = 12345;

        [Tooltip("Bán kính số chunk quanh tâm bản đồ được sinh cây ngay khi vào trận (1 = 2x2 chunk = 100x100 m)")]
        [SerializeField] int activeChunkRadius = 1;

        [Tooltip("Độ dốc tối đa (độ nguyên) mà cây có thể mọc")]
        [SerializeField] int maxTreeSlopeDegrees = 25;

        [Tooltip("Bán kính (ô) quanh gốc (0,0) để trống cho Nhà chính và dân khởi đầu")]
        [SerializeField] int spawnClearRadius = 11;

        [Header("Dữ liệu quy tắc đặt")]
        [Tooltip("Cấu hình placementRule = Block cho cây lớn")]
        [SerializeField] ObjectPlacementData blockTreeData;

        [Tooltip("Cấu hình placementRule = Clear cho cây nhỏ và bụi cây")]
        [SerializeField] ObjectPlacementData clearBushData;

        readonly List<VegetationRecord> records = new List<VegetationRecord>();
        readonly List<GameObject> spawnedObjects = new List<GameObject>();
        Transform container;
        bool generated;

        static Mesh cylinderMesh;
        static Mesh sphereMesh;
        static Material trunkMaterial;
        static Material largeCrownMaterial;
        static Material smallBushMaterial;

        public int Seed => seed;
        public int TotalCount => records.Count;
        public int BlockCount { get; private set; }
        public int ClearCount { get; private set; }
        public IReadOnlyList<VegetationRecord> Records => records;

        public ObjectPlacementData BlockTreeData
        {
            get => blockTreeData;
            set => blockTreeData = value;
        }

        public ObjectPlacementData ClearBushData
        {
            get => clearBushData;
            set => clearBushData = value;
        }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (!generated)
                Generate(seed);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Xoá toàn bộ cây đã sinh trước đó và sinh lại từ hạt giống số nguyên mới.
        public void RespawnWithSeed(int newSeed)
        {
            seed = newSeed;
            Generate(seed);
        }

        public void Generate(int mapSeed)
        {
            ClearSpawned();
            seed = mapSeed;
            generated = true;

            var hf = HeightField.Instance != null ? HeightField.Instance : FindFirstObjectByType<HeightField>();
            var grid = BuildGrid.Instance != null ? BuildGrid.Instance : FindFirstObjectByType<BuildGrid>();
            if (hf == null) return;
            hf.EnsureInitialized();

            if (container == null)
            {
                var existing = transform.Find("SpawnedVegetation");
                if (existing != null) container = existing;
                else
                {
                    var go = new GameObject("SpawnedVegetation");
                    go.transform.SetParent(transform, false);
                    container = go.transform;
                }
            }

            int centerChunkX = hf.MapChunks.x / 2;
            int centerChunkZ = hf.MapChunks.y / 2;
            int minChunkX = Mathf.Max(0, centerChunkX - activeChunkRadius);
            int maxChunkX = Mathf.Min(hf.MapChunks.x - 1, centerChunkX + activeChunkRadius - 1);
            int minChunkZ = Mathf.Max(0, centerChunkZ - activeChunkRadius);
            int maxChunkZ = Mathf.Min(hf.MapChunks.y - 1, centerChunkZ + activeChunkRadius - 1);

            for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
            {
                for (int cx = minChunkX; cx <= maxChunkX; cx++)
                {
                    GenerateChunk(hf, grid, cx, cz);
                }
            }
        }

        void GenerateChunk(HeightField hf, BuildGrid grid, int chunkX, int chunkZ)
        {
            int baseMapX = chunkX * HeightField.ChunkSize;
            int baseMapZ = chunkZ * HeightField.ChunkSize;

            for (int lz = 0; lz < HeightField.ChunkSize; lz++)
            {
                for (int lx = 0; lx < HeightField.ChunkSize; lx++)
                {
                    var wc = hf.MapCellToWorldCell(baseMapX + lx, baseMapZ + lz);
                    int wx = wc.x;
                    int wz = wc.y;

                    if (ShouldSkipCell(hf, grid, wx, wz)) continue;

                    // Tính mật độ cụm rừng bằng nhiễu số nguyên nội suy trên lưới 8x8 ô (0..1023).
                    int density = SampleForestDensity(seed, wx, wz);
                    if (density < 530) continue;

                    uint cellHash = HashCell(seed ^ 0x6C8E9CF5, wx, wz);
                    if ((cellHash & 7u) != 0u) continue;

                    bool isLargeBlock = density >= 680 && ((cellHash >> 3) & 1u) == 0u;
                    int yaw = (int)((cellHash >> 8) % 360u);
                    int hSub = hf.HeightAtWorldCell(wx, wz);

                    int recordIndex = records.Count;
                    records.Add(new VegetationRecord
                    {
                        WorldX = wx,
                        WorldZ = wz,
                        HeightSub = hSub,
                        YawDegrees = yaw,
                        IsLargeBlock = isLargeBlock,
                        Cleared = false
                    });

                    if (isLargeBlock) BlockCount++;
                    else ClearCount++;

                    hf.SetOccupancyAtWorldCell(wx, wz, isLargeBlock ? CellOccupancy.Static : CellOccupancy.Clearable);
                    SpawnTreeObject(grid, recordIndex, wx, wz, hSub, yaw, isLargeBlock);
                }
            }
        }

        bool ShouldSkipCell(HeightField hf, BuildGrid grid, int wx, int wz)
        {
            // Giữ trống vùng quanh gốc (Nhà chính) và các khu vực địa hình mẫu trong scene prototype.
            if (wx * wx + wz * wz <= spawnClearRadius * spawnClearRadius) return true;
            if (wx >= -2 && wx <= 2 && wz >= -12 && wz <= -6) return true;
            if (wx >= 8 && wx <= 16 && wz >= 8 && wz <= 16) return true;
            if (wx >= 8 && wx <= 16 && wz >= -16 && wz <= -8) return true;
            if (wx >= -16 && wx <= -8 && wz >= -16 && wz <= -8) return true;
            if (wx >= -18 && wx <= -10 && wz >= 8 && wz <= 16) return true;
            if (wx >= -12 && wx <= -5 && wz >= -5 && wz <= 11) return true;
            if (wx >= 2 && wx <= 13 && wz >= -7 && wz <= 9) return true;

            if (hf.IsWaterAtWorldCell(wx, wz)) return true;
            if (hf.IsSteepAtWorldCell(wx, wz, maxTreeSlopeDegrees)) return true;
            if (hf.GetOccupancyAtWorldCell(wx, wz) != CellOccupancy.None) return true;

            if (grid != null)
            {
                int ox = Mathf.RoundToInt(grid.Origin.x / grid.CellSize);
                int oz = Mathf.RoundToInt(grid.Origin.z / grid.CellSize);
                var cell = new Vector2Int(wx - ox, wz - oz);
                if (grid.InBounds(cell) && grid.GetOccupancy(cell) != CellOccupancy.None)
                    return true;
            }

            return false;
        }

        void SpawnTreeObject(BuildGrid grid, int recordIndex, int wx, int wz, int hSub, int yaw, bool isLargeBlock)
        {
            EnsureMeshesAndMaterials();
            float worldY = hSub * (1f / HeightField.HeightScale);
            var pos = new Vector3(wx + 0.5f, worldY, wz + 0.5f);

            var go = new GameObject(isLargeBlock ? $"ProcTree_Block_{recordIndex}" : $"ProcBush_Clear_{recordIndex}");
            go.transform.SetParent(container, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            if (isLargeBlock)
            {
                var trunk = new GameObject("Trunk");
                trunk.transform.SetParent(go.transform, false);
                trunk.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                trunk.transform.localScale = new Vector3(0.36f, 0.7f, 0.36f);
                trunk.AddComponent<MeshFilter>().sharedMesh = cylinderMesh;
                trunk.AddComponent<MeshRenderer>().sharedMaterial = trunkMaterial;

                var crown = new GameObject("Crown");
                crown.transform.SetParent(go.transform, false);
                crown.transform.localPosition = new Vector3(0f, 1.85f, 0f);
                crown.transform.localScale = new Vector3(1.25f, 1.25f, 1.25f);
                crown.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
                crown.AddComponent<MeshRenderer>().sharedMaterial = largeCrownMaterial;
            }
            else
            {
                var bush = new GameObject("Bush");
                bush.transform.SetParent(go.transform, false);
                bush.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                bush.transform.localScale = new Vector3(0.75f, 0.65f, 0.75f);
                bush.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
                bush.AddComponent<MeshRenderer>().sharedMaterial = smallBushMaterial;
            }

            var occ = go.AddComponent<GridOccupant>();
            occ.footprint = Vector2Int.one;
            occ.baseFootprint = Vector2Int.one;
            occ.angle = yaw;
            occ.isBuilding = false;
            occ.placementData = isLargeBlock ? GetOrCreateDefaultBlockData() : GetOrCreateDefaultClearData();

            var node = go.AddComponent<ResourceNode>();
            node.resourceType = ResourceType.Wood;

            int capturedIndex = recordIndex;
            occ.ClearedByBuilding += () =>
            {
                if (capturedIndex >= 0 && capturedIndex < records.Count)
                {
                    var rec = records[capturedIndex];
                    rec.Cleared = true;
                    records[capturedIndex] = rec;
                }
            };

            if (grid != null)
                occ.Register();

            spawnedObjects.Add(go);
        }

        public void ClearSpawned()
        {
            var hf = HeightField.Instance != null ? HeightField.Instance : FindFirstObjectByType<HeightField>();
            if (hf != null)
            {
                for (int i = 0; i < records.Count; i++)
                {
                    var r = records[i];
                    if (!r.Cleared)
                        hf.SetOccupancyAtWorldCell(r.WorldX, r.WorldZ, CellOccupancy.None);
                }
            }

            for (int i = 0; i < spawnedObjects.Count; i++)
            {
                var go = spawnedObjects[i];
                if (go == null) continue;
                var occ = go.GetComponent<GridOccupant>();
                if (occ != null) occ.Unregister();
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
            spawnedObjects.Clear();

            if (container != null)
            {
                for (int i = container.childCount - 1; i >= 0; i--)
                {
                    var child = container.GetChild(i).gameObject;
                    var occ = child.GetComponent<GridOccupant>();
                    if (occ != null) occ.Unregister();
                    if (Application.isPlaying) Destroy(child);
                    else DestroyImmediate(child);
                }
            }

            records.Clear();
            BlockCount = 0;
            ClearCount = 0;
        }

        // Tính mã băm FNV-1a 32-bit trên toàn bộ danh sách cây đã sinh để kiểm chứng tính tất định.
        public uint ComputeDeterministicHash()
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)seed) * 16777619u;
            hash = (hash ^ (uint)records.Count) * 16777619u;
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                hash = (hash ^ (uint)r.WorldX) * 16777619u;
                hash = (hash ^ (uint)r.WorldZ) * 16777619u;
                hash = (hash ^ (uint)r.HeightSub) * 16777619u;
                hash = (hash ^ (uint)r.YawDegrees) * 16777619u;
                hash = (hash ^ (r.IsLargeBlock ? 1u : 0u)) * 16777619u;
            }
            return hash;
        }

        // Nội suy song tuyến số nguyên trên lưới cụm 8x8 ô, trả về giá trị mật độ rừng 0..1023.
        public static int SampleForestDensity(int mapSeed, int wx, int wz)
        {
            const int clusterSize = 8;
            int gx = FloorDiv(wx, clusterSize);
            int gz = FloorDiv(wz, clusterSize);
            int tx = wx - gx * clusterSize;
            int tz = wz - gz * clusterSize;

            int v00 = (int)(HashCell(mapSeed, gx, gz) & 1023u);
            int v10 = (int)(HashCell(mapSeed, gx + 1, gz) & 1023u);
            int v01 = (int)(HashCell(mapSeed, gx, gz + 1) & 1023u);
            int v11 = (int)(HashCell(mapSeed, gx + 1, gz + 1) & 1023u);

            int vx0 = (v00 * (clusterSize - tx) + v10 * tx) >> 3;
            int vx1 = (v01 * (clusterSize - tx) + v11 * tx) >> 3;
            return (vx0 * (clusterSize - tz) + vx1 * tz) >> 3;
        }

        // Hàm băm số nguyên 32-bit tất định theo (seed, x, z).
        public static uint HashCell(int mapSeed, int x, int z)
        {
            uint h = (uint)mapSeed;
            h ^= (uint)x * 0x9E3779B9u;
            h = (h << 13) | (h >> 19);
            h = h * 5u + 0xE6546B64u;
            h ^= (uint)z * 0x85EBCA6Bu;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : (a - b + 1) / b;

        ObjectPlacementData GetOrCreateDefaultBlockData()
        {
            if (blockTreeData != null) return blockTreeData;
            blockTreeData = ScriptableObject.CreateInstance<ObjectPlacementData>();
            blockTreeData.displayName = "Cây cổ thụ";
            blockTreeData.placementRule = PlacementRule.Block;
            blockTreeData.footprint = Vector2Int.one;
            blockTreeData.clearable = true;
            blockTreeData.clearRewardType = ResourceType.Wood;
            blockTreeData.clearRewardAmount = 10;
            return blockTreeData;
        }

        ObjectPlacementData GetOrCreateDefaultClearData()
        {
            if (clearBushData != null) return clearBushData;
            clearBushData = ScriptableObject.CreateInstance<ObjectPlacementData>();
            clearBushData.displayName = "Bụi cây nhỏ";
            clearBushData.placementRule = PlacementRule.Clear;
            clearBushData.footprint = Vector2Int.one;
            clearBushData.clearable = true;
            clearBushData.clearRewardType = ResourceType.Wood;
            clearBushData.clearRewardAmount = 2;
            return clearBushData;
        }

        static void EnsureMeshesAndMaterials()
        {
            if (cylinderMesh == null)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cylinderMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(temp);
            }
            if (sphereMesh == null)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(temp);
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (trunkMaterial == null)
                trunkMaterial = CreateGrayboxMat(shader, "TrunkGraybox", new Color(0.36f, 0.34f, 0.32f, 1f));
            if (largeCrownMaterial == null)
                largeCrownMaterial = CreateGrayboxMat(shader, "LargeCrownGraybox", new Color(0.46f, 0.50f, 0.46f, 1f));
            if (smallBushMaterial == null)
                smallBushMaterial = CreateGrayboxMat(shader, "SmallBushGraybox", new Color(0.56f, 0.60f, 0.55f, 1f));
        }

        static Material CreateGrayboxMat(Shader shader, string matName, Color col)
        {
            var mat = new Material(shader) { name = matName, color = col };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            return mat;
        }
    }
}
