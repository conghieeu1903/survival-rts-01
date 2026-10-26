using System;
using UnityEngine;

namespace SurvivalRts
{
    // Nguồn dữ liệu độ cao số nguyên chia theo chunk 50x50 m (đơn vị 1/256 m) và đồng bộ ra Unity Terrain.
    public class HeightField : MonoBehaviour
    {
        public static HeightField Instance { get; private set; }

        public const int ChunkSize = TerrainChunk.Size;
        public const int HeightScale = 256;

        [Header("Kích thước bản đồ")]
        [Tooltip("Số lượng chunk theo trục X và Z (mỗi chunk 50x50 m; 20x20 = 1000x1000 m)")]
        [SerializeField] Vector2Int mapChunks = new Vector2Int(20, 20);

        [Tooltip("Độ cao mặc định (1/256 m) cho các chunk chưa cấp phát")]
        [SerializeField] short defaultHeightSub;

        [Tooltip("Ngưỡng độ cao mặt nước (1/256 m); ô thấp hơn hoặc bằng ngưỡng này tính là vùng nước")]
        [SerializeField] short waterLevelSub = -32;

        [Header("Hiển thị Unity Terrain")]
        [Tooltip("Terrain chỉ dùng để vẽ mặt đất và bắt tia chuột, không dùng làm nguồn mô phỏng")]
        [SerializeField] Terrain unityTerrain;

        [Tooltip("Độ cao Y thế giới ứng với giá trị 0 của Terrain heightmap")]
        [SerializeField] float terrainBaseY = -10f;

        [Tooltip("Tổng chiều cao Y tối đa của TerrainData")]
        [SerializeField] float terrainMaxHeight = 60f;

        [Header("Dữ liệu độ cao nướng sẵn")]
        [SerializeField] Vector2Int bakedMinWorldCell = new Vector2Int(-25, -25);
        [SerializeField] Vector2Int bakedSizeCells = new Vector2Int(50, 50);
        [SerializeField] short[] bakedHeightsSub;

        TerrainChunk[] chunks;
        int allocatedChunkCount;
        bool initialized;

        public int Version { get; private set; }
        public Vector2Int MapChunks => mapChunks;
        public Vector2Int MapSizeCells => new Vector2Int(mapChunks.x * ChunkSize, mapChunks.y * ChunkSize);
        public short DefaultHeightSub => defaultHeightSub;
        public short WaterLevelSub => waterLevelSub;
        public Terrain UnityTerrain
        {
            get => unityTerrain;
            set => unityTerrain = value;
        }
        public float TerrainBaseY => terrainBaseY;
        public float TerrainMaxHeight => terrainMaxHeight;

        public int AllocatedChunkCount
        {
            get { EnsureInitialized(); return allocatedChunkCount; }
        }

        // Phát sự kiện khi một vùng ô thế giới thay đổi độ cao (minX, minZ, maxX, maxZ).
        public event Action<int, int, int, int> RegionHeightsChanged;

        void Awake()
        {
            Instance = this;
            EnsureInitialized();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Khởi tạo mảng tham chiếu chunk và nạp vùng độ cao đã nướng.
        public void EnsureInitialized()
        {
            if (initialized && chunks != null && chunks.Length == mapChunks.x * mapChunks.y) return;

            if (mapChunks.x < 1) mapChunks.x = 1;
            if (mapChunks.y < 1) mapChunks.y = 1;

            chunks = new TerrainChunk[mapChunks.x * mapChunks.y];
            allocatedChunkCount = 0;
            initialized = true;

            if (bakedHeightsSub != null && bakedHeightsSub.Length == bakedSizeCells.x * bakedSizeCells.y)
            {
                for (int z = 0; z < bakedSizeCells.y; z++)
                {
                    for (int x = 0; x < bakedSizeCells.x; x++)
                    {
                        short h = bakedHeightsSub[x + z * bakedSizeCells.x];
                        if (h == defaultHeightSub) continue;
                        var mapCell = WorldCellToMapCell(bakedMinWorldCell.x + x, bakedMinWorldCell.y + z);
                        if (InMapBounds(mapCell.x, mapCell.y))
                            SetHeightInternal(mapCell.x, mapCell.y, h);
                    }
                }
            }

            Version++;
        }

        // Đổi kích thước số lượng chunk của bản đồ (ví dụ 20x20 hoặc 200x200) và khởi tạo lại.
        public void ResizeMapChunks(Vector2Int newMapChunks)
        {
            mapChunks = new Vector2Int(Mathf.Max(1, newMapChunks.x), Mathf.Max(1, newMapChunks.y));
            initialized = false;
            EnsureInitialized();
        }

        // Lưu vùng độ cao nướng sẵn vào dữ liệu tuần tự hoá của component.
        public void SetBakedData(Vector2Int minWorldCell, Vector2Int sizeCells, short[] heightsSub)
        {
            bakedMinWorldCell = minWorldCell;
            bakedSizeCells = sizeCells;
            bakedHeightsSub = heightsSub;
            initialized = false;
            EnsureInitialized();
        }

        public Vector2Int WorldCellToMapCell(int worldCellX, int worldCellZ)
        {
            int halfX = (mapChunks.x * ChunkSize) / 2;
            int halfZ = (mapChunks.y * ChunkSize) / 2;
            return new Vector2Int(worldCellX + halfX, worldCellZ + halfZ);
        }

        public Vector2Int MapCellToWorldCell(int mapCellX, int mapCellZ)
        {
            int halfX = (mapChunks.x * ChunkSize) / 2;
            int halfZ = (mapChunks.y * ChunkSize) / 2;
            return new Vector2Int(mapCellX - halfX, mapCellZ - halfZ);
        }

        public bool InMapBounds(int mapCellX, int mapCellZ)
        {
            return mapCellX >= 0 && mapCellZ >= 0
                && mapCellX < mapChunks.x * ChunkSize
                && mapCellZ < mapChunks.y * ChunkSize;
        }

        public bool InWorldBounds(int worldCellX, int worldCellZ)
        {
            var m = WorldCellToMapCell(worldCellX, worldCellZ);
            return InMapBounds(m.x, m.y);
        }

        public TerrainChunk GetChunk(int chunkX, int chunkZ)
        {
            EnsureInitialized();
            if (chunkX < 0 || chunkZ < 0 || chunkX >= mapChunks.x || chunkZ >= mapChunks.y)
                return null;
            return chunks[chunkX + chunkZ * mapChunks.x];
        }

        public TerrainChunk GetOrCreateChunk(int chunkX, int chunkZ)
        {
            EnsureInitialized();
            if (chunkX < 0 || chunkZ < 0 || chunkX >= mapChunks.x || chunkZ >= mapChunks.y)
                return null;

            int idx = chunkX + chunkZ * mapChunks.x;
            var chunk = chunks[idx];
            if (chunk == null)
            {
                chunk = new TerrainChunk(chunkX, chunkZ, defaultHeightSub);
                chunks[idx] = chunk;
                allocatedChunkCount++;
            }
            return chunk;
        }

        // Trả về độ cao số nguyên (1/256 m) tại tâm ô bản đồ (mapCellX, mapCellZ).
        public int HeightAt(int mapCellX, int mapCellZ)
        {
            EnsureInitialized();
            if (!InMapBounds(mapCellX, mapCellZ)) return defaultHeightSub;

            int cx = mapCellX / ChunkSize;
            int cz = mapCellZ / ChunkSize;
            var chunk = chunks[cx + cz * mapChunks.x];
            if (chunk == null) return defaultHeightSub;

            int lx = mapCellX - cx * ChunkSize;
            int lz = mapCellZ - cz * ChunkSize;
            return chunk.GetHeight(lx, lz);
        }

        // Trả về độ cao số nguyên (1/256 m) tại tâm ô thế giới (worldCellX, worldCellZ).
        public int HeightAtWorldCell(int worldCellX, int worldCellZ)
        {
            var m = WorldCellToMapCell(worldCellX, worldCellZ);
            return HeightAt(m.x, m.y);
        }

        public float HeightMetersAtWorldCell(int worldCellX, int worldCellZ)
        {
            return HeightAtWorldCell(worldCellX, worldCellZ) * (1f / HeightScale);
        }

        public bool IsWaterAtWorldCell(int worldCellX, int worldCellZ)
        {
            return HeightAtWorldCell(worldCellX, worldCellZ) <= waterLevelSub;
        }

        // Lấy mẫu độ cao nội suy song tuyến bằng số nguyên tại toạ độ (mapSubX, mapSubZ) tính bằng 1/256 m từ gốc bản đồ.
        public int SampleBilinear(long mapSubX, long mapSubZ)
        {
            long px = mapSubX - HeightScale / 2;
            long pz = mapSubZ - HeightScale / 2;
            int x0 = FloorDiv(px, HeightScale);
            int z0 = FloorDiv(pz, HeightScale);
            int tx = (int)(px - (long)x0 * HeightScale);
            int tz = (int)(pz - (long)z0 * HeightScale);

            int h00 = HeightAt(x0, z0);
            int h10 = HeightAt(x0 + 1, z0);
            int h01 = HeightAt(x0, z0 + 1);
            int h11 = HeightAt(x0 + 1, z0 + 1);

            int hx0 = (h00 * (HeightScale - tx) + h10 * tx) >> 8;
            int hx1 = (h01 * (HeightScale - tx) + h11 * tx) >> 8;
            return (hx0 * (HeightScale - tz) + hx1 * tz) >> 8;
        }

        // Lấy mẫu độ cao nội suy song tuyến bằng số nguyên tại toạ độ thế giới (worldSubX, worldSubZ) tính bằng 1/256 m.
        public int SampleBilinearWorld(long worldSubX, long worldSubZ)
        {
            long halfXSub = (long)(mapChunks.x * ChunkSize / 2) * HeightScale;
            long halfZSub = (long)(mapChunks.y * ChunkSize / 2) * HeightScale;
            return SampleBilinear(worldSubX + halfXSub, worldSubZ + halfZSub);
        }

        // Trả về độ cao mặt đất nội suy (m) tại vị trí thế giới bất kỳ.
        public float SampleWorldHeight(Vector3 worldPos)
        {
            long wx = (long)Math.Round(worldPos.x * HeightScale);
            long wz = (long)Math.Round(worldPos.z * HeightScale);
            return SampleBilinearWorld(wx, wz) * (1f / HeightScale);
        }

        // Kiểm tra độ dốc tại ô thế giới (worldCellX, worldCellZ) có vượt ngưỡng maxSlopeDegrees hay không bằng số nguyên.
        public bool IsSteepAtWorldCell(int worldCellX, int worldCellZ, int maxSlopeDegrees)
        {
            long cx = (long)worldCellX * HeightScale + HeightScale / 2;
            long cz = (long)worldCellZ * HeightScale + HeightScale / 2;
            const long q = 64;
            return IsSteepSampleWorld(cx, cz, maxSlopeDegrees)
                || IsSteepSampleWorld(cx - q, cz - q, maxSlopeDegrees)
                || IsSteepSampleWorld(cx + q, cz - q, maxSlopeDegrees)
                || IsSteepSampleWorld(cx - q, cz + q, maxSlopeDegrees)
                || IsSteepSampleWorld(cx + q, cz + q, maxSlopeDegrees);
        }

        // Kiểm tra độ dốc nội suy tại điểm bất kỳ (worldSubX, worldSubZ) tính bằng 1/256 m.
        public bool IsSteepSampleWorld(long worldSubX, long worldSubZ, int maxSlopeDegrees)
        {
            const long step = 64;
            long dhx = SampleBilinearWorld(worldSubX + step, worldSubZ) - SampleBilinearWorld(worldSubX - step, worldSubZ);
            long dhz = SampleBilinearWorld(worldSubX, worldSubZ + step) - SampleBilinearWorld(worldSubX, worldSubZ - step);
            return IsGradientSteep(dhx, dhz, step * 2, maxSlopeDegrees);
        }

        static bool IsGradientSteep(long dhx, long dhz, long stepSub, int maxSlopeDegrees)
        {
            maxSlopeDegrees = Mathf.Clamp(maxSlopeDegrees, 1, 89);
            long sin = BuildGrid.Sin(maxSlopeDegrees) >> 4;
            long cos = BuildGrid.Cos(maxSlopeDegrees) >> 4;
            long gradSq = dhx * dhx + dhz * dhz;
            return gradSq * cos * cos > stepSub * stepSub * sin * sin;
        }

        // Ghi độ cao cho một ô thế giới và cập nhật version của chunk chứa ô đó.
        public void SetWorldCellHeight(int worldCellX, int worldCellZ, short heightSub, bool syncTerrain = true)
        {
            EnsureInitialized();
            var m = WorldCellToMapCell(worldCellX, worldCellZ);
            if (!InMapBounds(m.x, m.y)) return;
            SetHeightInternal(m.x, m.y, heightSub);
            Version++;
            if (syncTerrain)
                SyncRegionToUnityTerrain(worldCellX - 1, worldCellZ - 1, worldCellX + 1, worldCellZ + 1);
            RegionHeightsChanged?.Invoke(worldCellX, worldCellZ, worldCellX, worldCellZ);
        }

        // Ghi độ cao cho một vùng chữ nhật các ô thế giới, cập nhật version các chunk bị ảnh hưởng và đồng bộ Terrain.
        public void SetWorldRegionHeights(int minWorldCellX, int minWorldCellZ, int width, int depth, short heightSub, bool syncTerrain = true)
        {
            EnsureInitialized();
            int maxWorldCellX = minWorldCellX + width - 1;
            int maxWorldCellZ = minWorldCellZ + depth - 1;
            for (int z = minWorldCellZ; z <= maxWorldCellZ; z++)
            {
                for (int x = minWorldCellX; x <= maxWorldCellX; x++)
                {
                    var m = WorldCellToMapCell(x, z);
                    if (InMapBounds(m.x, m.y))
                        SetHeightInternal(m.x, m.y, heightSub);
                }
            }
            Version++;
            if (syncTerrain)
                SyncRegionToUnityTerrain(minWorldCellX - 1, minWorldCellZ - 1, maxWorldCellX + 1, maxWorldCellZ + 1);
            RegionHeightsChanged?.Invoke(minWorldCellX, minWorldCellZ, maxWorldCellX, maxWorldCellZ);
        }

        void SetHeightInternal(int mapCellX, int mapCellZ, short heightSub)
        {
            int cx = mapCellX / ChunkSize;
            int cz = mapCellZ / ChunkSize;
            var chunk = GetOrCreateChunk(cx, cz);
            if (chunk == null) return;
            chunk.SetHeight(mapCellX - cx * ChunkSize, mapCellZ - cz * ChunkSize, heightSub);
        }

        // Đọc và ghi trạng thái chiếm ô theo chunk.
        public CellOccupancy GetOccupancyAtWorldCell(int worldCellX, int worldCellZ)
        {
            var m = WorldCellToMapCell(worldCellX, worldCellZ);
            if (!InMapBounds(m.x, m.y)) return CellOccupancy.Static;
            int cx = m.x / ChunkSize;
            int cz = m.y / ChunkSize;
            var chunk = GetChunk(cx, cz);
            if (chunk == null) return CellOccupancy.None;
            return chunk.GetOccupancy(m.x - cx * ChunkSize, m.y - cz * ChunkSize);
        }

        public void SetOccupancyAtWorldCell(int worldCellX, int worldCellZ, CellOccupancy value)
        {
            var m = WorldCellToMapCell(worldCellX, worldCellZ);
            if (!InMapBounds(m.x, m.y)) return;
            int cx = m.x / ChunkSize;
            int cz = m.y / ChunkSize;
            if (value == CellOccupancy.None && GetChunk(cx, cz) == null) return;
            var chunk = GetOrCreateChunk(cx, cz);
            chunk.SetOccupancy(m.x - cx * ChunkSize, m.y - cz * ChunkSize, value);
        }

        // Đồng bộ toàn bộ dữ liệu độ cao từ HeightField sang Unity Terrain để hiển thị.
        public void SyncToUnityTerrain()
        {
            EnsureInitialized();
            if (unityTerrain == null || unityTerrain.terrainData == null) return;

            var td = unityTerrain.terrainData;
            int res = td.heightmapResolution;
            Vector3 tPos = unityTerrain.transform.position;
            Vector3 tSize = td.size;
            float invMaxH = tSize.y > 0.001f ? 1f / tSize.y : 1f / 60f;

            var heights = new float[res, res];
            for (int hz = 0; hz < res; hz++)
            {
                float normZ = hz / (float)(res - 1);
                long worldSubZ = (long)Math.Round((tPos.z + normZ * tSize.z) * HeightScale);
                for (int hx = 0; hx < res; hx++)
                {
                    float normX = hx / (float)(res - 1);
                    long worldSubX = (long)Math.Round((tPos.x + normX * tSize.x) * HeightScale);
                    int hSub = SampleBilinearWorld(worldSubX, worldSubZ);
                    float worldY = hSub * (1f / HeightScale);
                    heights[hz, hx] = Mathf.Clamp01((worldY - tPos.y) * invMaxH);
                }
            }
            td.SetHeights(0, 0, heights);
        }

        // Đồng bộ một vùng nhỏ từ HeightField sang Unity Terrain khi độ cao thay đổi cục bộ.
        public void SyncRegionToUnityTerrain(int minWorldCellX, int minWorldCellZ, int maxWorldCellX, int maxWorldCellZ)
        {
            EnsureInitialized();
            if (unityTerrain == null || unityTerrain.terrainData == null) return;

            var td = unityTerrain.terrainData;
            int res = td.heightmapResolution;
            Vector3 tPos = unityTerrain.transform.position;
            Vector3 tSize = td.size;
            if (tSize.x <= 0.001f || tSize.z <= 0.001f || tSize.y <= 0.001f) return;

            int minHx = Mathf.Clamp(Mathf.FloorToInt((minWorldCellX - tPos.x) / tSize.x * (res - 1)) - 1, 0, res - 1);
            int maxHx = Mathf.Clamp(Mathf.CeilToInt((maxWorldCellX + 1 - tPos.x) / tSize.x * (res - 1)) + 1, 0, res - 1);
            int minHz = Mathf.Clamp(Mathf.FloorToInt((minWorldCellZ - tPos.z) / tSize.z * (res - 1)) - 1, 0, res - 1);
            int maxHz = Mathf.Clamp(Mathf.CeilToInt((maxWorldCellZ + 1 - tPos.z) / tSize.z * (res - 1)) + 1, 0, res - 1);

            int w = maxHx - minHx + 1;
            int h = maxHz - minHz + 1;
            if (w <= 0 || h <= 0) return;

            float invMaxH = 1f / tSize.y;
            var patch = new float[h, w];
            for (int dz = 0; dz < h; dz++)
            {
                int hz = minHz + dz;
                float normZ = hz / (float)(res - 1);
                long worldSubZ = (long)Math.Round((tPos.z + normZ * tSize.z) * HeightScale);
                for (int dx = 0; dx < w; dx++)
                {
                    int hx = minHx + dx;
                    float normX = hx / (float)(res - 1);
                    long worldSubX = (long)Math.Round((tPos.x + normX * tSize.x) * HeightScale);
                    int hSub = SampleBilinearWorld(worldSubX, worldSubZ);
                    float worldY = hSub * (1f / HeightScale);
                    patch[dz, dx] = Mathf.Clamp01((worldY - tPos.y) * invMaxH);
                }
            }
            td.SetHeights(minHx, minHz, patch);
        }

        // Tính mã băm FNV-1a 32-bit xác định trên toàn bộ dữ liệu độ cao để kiểm tra đồng bộ giữa các lần chạy.
        public uint ComputeDeterministicHash()
        {
            EnsureInitialized();
            uint hash = 2166136261u;
            hash = (hash ^ (uint)mapChunks.x) * 16777619u;
            hash = (hash ^ (uint)mapChunks.y) * 16777619u;
            hash = (hash ^ (uint)(ushort)defaultHeightSub) * 16777619u;

            for (int cz = 0; cz < mapChunks.y; cz++)
            {
                for (int cx = 0; cx < mapChunks.x; cx++)
                {
                    var chunk = chunks[cx + cz * mapChunks.x];
                    if (chunk == null) continue;
                    for (int i = 0; i < TerrainChunk.CellCount; i++)
                    {
                        short v = chunk.GetHeightByIndex(i);
                        if (v == defaultHeightSub) continue;
                        hash = (hash ^ (uint)cx) * 16777619u;
                        hash = (hash ^ (uint)cz) * 16777619u;
                        hash = (hash ^ (uint)i) * 16777619u;
                        hash = (hash ^ (uint)(ushort)v) * 16777619u;
                    }
                }
            }
            return hash;
        }

        static int FloorDiv(long a, int b) => (int)(a >= 0 ? a / b : (a - b + 1) / b);
    }
}
