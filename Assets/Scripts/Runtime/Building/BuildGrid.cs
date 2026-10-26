using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace SurvivalRts
{
    public enum CellState : byte
    {
        Free,
        OutOfBounds,
        Occupied,
        OffGround,
        NoNavMesh,
        Steep,
        Uneven
    }

    public enum CellOccupancy : byte
    {
        None,
        Clearable,
        Static,
        Building
    }

    public enum PlacementBlockReason : byte
    {
        None = 0,
        OutOfBounds,
        Water,
        SteepOrUneven,
        OccupiedBuilding,
        OccupiedObstacle,
        EnemyOrUnitInside,
        MissingAdjacentResource,
        MissingNearbyResource,
        InsufficientResources
    }

    // Quản lý lưới xây dựng, trạng thái chiếm ô và độ cao mặt đất đọc từ HeightField.
    public class BuildGrid : MonoBehaviour
    {
        public static BuildGrid Instance { get; private set; }

        // Toạ độ trong hàm chiếm ô tính bằng 1/256 ô; sin/cos tra bảng hằng số nguyên nhân 65536 để đồng nhất trên mọi máy.
        public const int SubCell = 256;
        public const int TrigShift = 16;
        const int EdgeMargin = SubCell / 4;
        static readonly int[] CosTable =
        {
            65536, 65526, 65496, 65446, 65376, 65287, 65177, 65048, 64898, 64729, 64540, 64332,
            64104, 63856, 63589, 63303, 62997, 62672, 62328, 61966, 61584, 61183, 60764, 60326,
            59870, 59396, 58903, 58393, 57865, 57319, 56756, 56175, 55578, 54963, 54332, 53684,
            53020, 52339, 51643, 50931, 50203, 49461, 48703, 47930, 47143, 46341, 45525, 44695,
            43852, 42995, 42126, 41243, 40348, 39441, 38521, 37590, 36647, 35693, 34729, 33754,
            32768, 31772, 30767, 29753, 28729, 27697, 26656, 25607, 24550, 23486, 22415, 21336,
            20252, 19161, 18064, 16962, 15855, 14742, 13626, 12505, 11380, 10252, 9121, 7987,
            6850, 5712, 4572, 3430, 2287, 1144, 0, -1144, -2287, -3430, -4572, -5712,
            -6850, -7987, -9121, -10252, -11380, -12505, -13626, -14742, -15855, -16962, -18064, -19161,
            -20252, -21336, -22415, -23486, -24550, -25607, -26656, -27697, -28729, -29753, -30767, -31772,
            -32768, -33754, -34729, -35693, -36647, -37590, -38521, -39441, -40348, -41243, -42126, -42995,
            -43852, -44695, -45525, -46341, -47143, -47930, -48703, -49461, -50203, -50931, -51643, -52339,
            -53020, -53684, -54332, -54963, -55578, -56175, -56756, -57319, -57865, -58393, -58903, -59396,
            -59870, -60326, -60764, -61183, -61584, -61966, -62328, -62672, -62997, -63303, -63589, -63856,
            -64104, -64332, -64540, -64729, -64898, -65048, -65177, -65287, -65376, -65446, -65496, -65526,
            -65536, -65526, -65496, -65446, -65376, -65287, -65177, -65048, -64898, -64729, -64540, -64332,
            -64104, -63856, -63589, -63303, -62997, -62672, -62328, -61966, -61584, -61183, -60764, -60326,
            -59870, -59396, -58903, -58393, -57865, -57319, -56756, -56175, -55578, -54963, -54332, -53684,
            -53020, -52339, -51643, -50931, -50203, -49461, -48703, -47930, -47143, -46341, -45525, -44695,
            -43852, -42995, -42126, -41243, -40348, -39441, -38521, -37590, -36647, -35693, -34729, -33754,
            -32768, -31772, -30767, -29753, -28729, -27697, -26656, -25607, -24550, -23486, -22415, -21336,
            -20252, -19161, -18064, -16962, -15855, -14742, -13626, -12505, -11380, -10252, -9121, -7987,
            -6850, -5712, -4572, -3430, -2287, -1144, 0, 1144, 2287, 3430, 4572, 5712,
            6850, 7987, 9121, 10252, 11380, 12505, 13626, 14742, 15855, 16962, 18064, 19161,
            20252, 21336, 22415, 23486, 24550, 25607, 26656, 27697, 28729, 29753, 30767, 31772,
            32768, 33754, 34729, 35693, 36647, 37590, 38521, 39441, 40348, 41243, 42126, 42995,
            43852, 44695, 45525, 46341, 47143, 47930, 48703, 49461, 50203, 50931, 51643, 52339,
            53020, 53684, 54332, 54963, 55578, 56175, 56756, 57319, 57865, 58393, 58903, 59396,
            59870, 60326, 60764, 61183, 61584, 61966, 62328, 62672, 62997, 63303, 63589, 63856,
            64104, 64332, 64540, 64729, 64898, 65048, 65177, 65287, 65376, 65446, 65496, 65526
        };

        [Header("Lưới")]
        [Tooltip("Cạnh mỗi ô lưới (m)")]
        [SerializeField] float cellSize = 1f;

        [Tooltip("Số ô của lưới khu vực hoạt động (rộng x sâu), căn giữa quanh vị trí của object này")]
        [SerializeField] Vector2Int size = new Vector2Int(50, 50);

        [Tooltip("Nguồn dữ liệu độ cao chia chunk 50x50 m")]
        [SerializeField] HeightField heightField;

        [Header("Điều kiện đặt")]
        [Tooltip("Layer của mặt đất, dùng làm dự phòng khi chưa gắn HeightField")]
        [SerializeField] LayerMask groundMask;

        [Tooltip("Độ dốc tối đa (độ) của một ô để được phép đặt")]
        [SerializeField] float maxSlopeAngle = 30f;

        [Tooltip("Chênh lệch độ cao tối đa (m) giữa các ô trong cùng một công trình")]
        [SerializeField] float maxHeightDelta = 1.0f;

        [Tooltip("Khoảng cách tối đa (m) từ tâm ô tới NavMesh để ô được coi là đi được")]
        [SerializeField] float navMeshSampleDistance = 0.6f;

        // Ba mảng cùng chỉ số Index(cell) = x + y * size.x, dùng NativeArray để job Burst đọc được.
        NativeArray<CellOccupancy> occupancy;
        NativeArray<CellState> terrain;
        NativeArray<float> heights;
        bool terrainBaked;
        bool subscribedHeightField;
        Vector3 origin;

        public float CellSize => cellSize;
        public Vector2Int Size => size;
        public Vector3 Origin => origin;
        public float MaxSlopeAngle => maxSlopeAngle;
        public float MaxHeightDelta => maxHeightDelta;
        public NativeArray<CellOccupancy> Occupancy => occupancy;

        public HeightField HeightField
        {
            get
            {
                if (heightField == null)
                    heightField = HeightField.Instance != null ? HeightField.Instance : FindFirstObjectByType<HeightField>();
                return heightField;
            }
            set => heightField = value;
        }

        // Trạng thái địa hình của từng ô: Free, OffGround, Steep hoặc NoNavMesh. Không tính việc bị chiếm.
        public NativeArray<CellState> Terrain
        {
            get { EnsureTerrain(); return terrain; }
        }

        // Độ cao mặt đất tại tâm từng ô, đọc từ HeightField.
        public NativeArray<float> Heights
        {
            get { EnsureTerrain(); return heights; }
        }

        // Tăng mỗi khi có ô đổi trạng thái chiếm hoặc địa hình, để flow field của kẻ địch biết cần tính lại.
        public int Version { get; private set; }

        void Awake()
        {
            Instance = this;
            int count = size.x * size.y;
            occupancy = new NativeArray<CellOccupancy>(count, Allocator.Persistent);
            terrain = new NativeArray<CellState>(count, Allocator.Persistent);
            heights = new NativeArray<float>(count, Allocator.Persistent);
            origin = transform.position - new Vector3(size.x, 0f, size.y) * cellSize * 0.5f;
        }

        void OnDestroy()
        {
            if (subscribedHeightField && heightField != null)
                heightField.RegionHeightsChanged -= OnRegionHeightsChanged;
            if (Instance == this) Instance = null;
            if (occupancy.IsCreated) occupancy.Dispose();
            if (terrain.IsCreated) terrain.Dispose();
            if (heights.IsCreated) heights.Dispose();
        }

        // Trả về cos(degrees) nhân 65536 từ bảng hằng số nguyên.
        public static int Cos(int degrees) => CosTable[NormalizeAngle(degrees)];

        // Trả về sin(degrees) nhân 65536 từ bảng hằng số nguyên.
        public static int Sin(int degrees) => CosTable[(NormalizeAngle(degrees) + 270) % 360];

        public static int NormalizeAngle(int degrees)
        {
            return ((degrees % 360) + 360) % 360;
        }

        public static Vector2Int RotatedFootprint(Vector2Int footprint, int quarterTurns)
        {
            return (quarterTurns & 1) == 1 ? new Vector2Int(footprint.y, footprint.x) : footprint;
        }

        // Chuyển mã lý do chặn đặt công trình sang thông báo tiếng Việt trên giao diện.
        public static string FormatBlockReason(PlacementBlockReason reason)
        {
            switch (reason)
            {
                case PlacementBlockReason.OutOfBounds:
                    return "Ngoài biên bản đồ";
                case PlacementBlockReason.Water:
                    return "Không thể đặt trên mặt nước";
                case PlacementBlockReason.SteepOrUneven:
                    return "Địa hình quá dốc";
                case PlacementBlockReason.OccupiedBuilding:
                    return "Trùng công trình khác";
                case PlacementBlockReason.OccupiedObstacle:
                    return "Vướng vật cản";
                case PlacementBlockReason.EnemyOrUnitInside:
                    return "Vướng đơn vị trong vùng đặt";
                case PlacementBlockReason.MissingAdjacentResource:
                    return "Cần đặt sát cạnh mỏ tài nguyên";
                case PlacementBlockReason.MissingNearbyResource:
                    return "Không có tài nguyên trong tầm";
                case PlacementBlockReason.InsufficientResources:
                    return "Không đủ tài nguyên";
                default:
                    return string.Empty;
            }
        }

        public Vector3 Snap(Vector3 world, Vector2Int footprint)
        {
            float x = SnapAxis(world.x - origin.x, footprint.x) + origin.x;
            float z = SnapAxis(world.z - origin.z, footprint.y) + origin.z;
            return new Vector3(x, world.y, z);
        }

        float SnapAxis(float value, int cells)
        {
            return cells % 2 == 0
                ? Mathf.Round(value / cellSize) * cellSize
                : (Mathf.Floor(value / cellSize) + 0.5f) * cellSize;
        }

        public Vector2Int MinCell(Vector3 center, Vector2Int footprint)
        {
            float x = (center.x - origin.x) / cellSize - footprint.x * 0.5f;
            float z = (center.z - origin.z) / cellSize - footprint.y * 0.5f;
            return new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(z));
        }

        public Vector3 CellCenter(Vector2Int cell)
        {
            return origin + new Vector3(cell.x + 0.5f, 0f, cell.y + 0.5f) * cellSize;
        }

        public Vector2Int CellToWorldCell(Vector2Int cell)
        {
            int ox = Mathf.RoundToInt(origin.x / cellSize);
            int oz = Mathf.RoundToInt(origin.z / cellSize);
            return new Vector2Int(ox + cell.x, oz + cell.y);
        }

        public bool InBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < size.x && cell.y < size.y;
        }

        public int Index(Vector2Int cell)
        {
            return cell.x + cell.y * size.x;
        }

        public void SetOccupancy(IReadOnlyList<int> cells, CellOccupancy value)
        {
            var hf = HeightField;
            for (int i = 0; i < cells.Count; i++)
            {
                int idx = cells[i];
                if (idx < 0) continue;
                if (occupancy.IsCreated && idx < occupancy.Length)
                    occupancy[idx] = value;
                if (hf != null)
                {
                    var cell = new Vector2Int(idx % size.x, idx / size.x);
                    var wc = CellToWorldCell(cell);
                    hf.SetOccupancyAtWorldCell(wc.x, wc.y, value);
                }
            }
            Version++;
        }

        public Vector3 CellCenter(int index)
        {
            return CellCenter(new Vector2Int(index % size.x, index / size.x));
        }

        // Lấy độ cao mặt đất nội suy mượt tại vị trí thế giới bất kỳ.
        public float SampleWorldHeight(Vector3 worldPos)
        {
            EnsureTerrain();
            var hf = HeightField;
            if (hf != null) return hf.SampleWorldHeight(worldPos);
            var cell = MinCell(worldPos, Vector2Int.one);
            return InBounds(cell) && heights.IsCreated ? heights[Index(cell)] : 0f;
        }

        // Liệt kê các ô mà hình chữ nhật xoay angle độ phủ lên theo toạ độ thế giới.
        public void CollectCells(Vector3 center, Vector2Int baseFootprint, int angle, List<int> result)
        {
            result.Clear();
            long cx = (long)Math.Round((center.x - origin.x) / cellSize * SubCell);
            long cz = (long)Math.Round((center.z - origin.z) / cellSize * SubCell);
            CollectCellsAtSubCell(cx, cz, baseFootprint, angle, result);
        }

        // Liệt kê chỉ số ô mà hình chữ nhật xoay angle độ phủ lên quanh tâm (cx, cz) tính bằng 1/256 ô.
        public void CollectCellsAtSubCell(long cx, long cz, Vector2Int baseFootprint, int angle, List<int> result)
        {
            result.Clear();
            angle = NormalizeAngle(angle);
            long halfW = baseFootprint.x * (SubCell / 2) + EdgeMargin;
            long halfD = baseFootprint.y * (SubCell / 2) + EdgeMargin;
            long cos = Cos(angle);
            long sin = Sin(angle);

            long reach = (halfW + halfD) + SubCell;
            int minX = FloorDiv(cx - reach, SubCell);
            int maxX = CeilDiv(cx + reach, SubCell);
            int minZ = FloorDiv(cz - reach, SubCell);
            int maxZ = CeilDiv(cz + reach, SubCell);

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    long dx = x * SubCell + SubCell / 2 - cx;
                    long dz = z * SubCell + SubCell / 2 - cz;
                    long u = (dx * cos - dz * sin) >> TrigShift;
                    long v = (dx * sin + dz * cos) >> TrigShift;
                    if (u < -halfW || u > halfW || v < -halfD || v > halfD) continue;
                    var cell = new Vector2Int(x, z);
                    result.Add(InBounds(cell) ? Index(cell) : -1);
                }
            }
        }

        // Liệt kê toạ độ ô nguyên (x, z) mà hình chữ nhật xoay angle độ phủ lên quanh tâm (cx, cz) tính bằng 1/256 ô.
        public static void CollectCellCoords(long cx, long cz, Vector2Int baseFootprint, int angle, List<Vector2Int> result)
        {
            result.Clear();
            angle = NormalizeAngle(angle);
            long halfW = baseFootprint.x * (SubCell / 2) + EdgeMargin;
            long halfD = baseFootprint.y * (SubCell / 2) + EdgeMargin;
            long cos = Cos(angle);
            long sin = Sin(angle);

            long reach = (halfW + halfD) + SubCell;
            int minX = FloorDiv(cx - reach, SubCell);
            int maxX = CeilDiv(cx + reach, SubCell);
            int minZ = FloorDiv(cz - reach, SubCell);
            int maxZ = CeilDiv(cz + reach, SubCell);

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    long dx = x * SubCell + SubCell / 2 - cx;
                    long dz = z * SubCell + SubCell / 2 - cz;
                    long u = (dx * cos - dz * sin) >> TrigShift;
                    long v = (dx * sin + dz * cos) >> TrigShift;
                    if (u < -halfW || u > halfW || v < -halfD || v > halfD) continue;
                    result.Add(new Vector2Int(x, z));
                }
            }
        }

        static int FloorDiv(long a, int b) => (int)(a >= 0 ? a / b : (a - b + 1) / b);

        static int CeilDiv(long a, int b) => (int)(a >= 0 ? (a + b - 1) / b : a / b);

        public CellOccupancy GetOccupancy(Vector2Int cell)
        {
            if (InBounds(cell) && occupancy.IsCreated)
                return occupancy[Index(cell)];
            var hf = HeightField;
            if (hf != null)
            {
                var wc = CellToWorldCell(cell);
                return hf.GetOccupancyAtWorldCell(wc.x, wc.y);
            }
            return InBounds(cell) ? CellOccupancy.None : CellOccupancy.Static;
        }

        // Đọc độ cao và độ dốc của toàn bộ ô từ HeightField bằng số nguyên (hoặc raycast dự phòng nếu chưa có HeightField).
        void EnsureTerrain()
        {
            if (terrainBaked) return;
            terrainBaked = true;

            var hf = HeightField;
            if (hf != null && !subscribedHeightField)
            {
                hf.RegionHeightsChanged += OnRegionHeightsChanged;
                subscribedHeightField = true;
            }

            BakeRegionCells(0, 0, size.x - 1, size.y - 1);
        }

        void OnRegionHeightsChanged(int minWorldX, int minWorldZ, int maxWorldX, int maxWorldZ)
        {
            if (!terrainBaked || !terrain.IsCreated) return;
            int ox = Mathf.RoundToInt(origin.x / cellSize);
            int oz = Mathf.RoundToInt(origin.z / cellSize);
            int minX = Mathf.Clamp(minWorldX - ox - 1, 0, size.x - 1);
            int maxX = Mathf.Clamp(maxWorldX - ox + 1, 0, size.x - 1);
            int minZ = Mathf.Clamp(minWorldZ - oz - 1, 0, size.y - 1);
            int maxZ = Mathf.Clamp(maxWorldZ - oz + 1, 0, size.y - 1);
            if (minX <= maxX && minZ <= maxZ)
            {
                BakeRegionCells(minX, minZ, maxX, maxZ);
                Version++;
            }
        }

        void BakeRegionCells(int minX, int minZ, int maxX, int maxZ)
        {
            var hf = HeightField;
            int maxSlopeInt = Mathf.RoundToInt(maxSlopeAngle);

            for (int y = minZ; y <= maxZ; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = new Vector2Int(x, y);
                    int i = Index(cell);

                    if (hf != null)
                    {
                        var wc = CellToWorldCell(cell);
                        if (!hf.InWorldBounds(wc.x, wc.y))
                        {
                            terrain[i] = CellState.OffGround;
                            heights[i] = 0f;
                            continue;
                        }

                        int hSub = hf.HeightAtWorldCell(wc.x, wc.y);
                        heights[i] = hSub * (1f / HeightField.HeightScale);
                        terrain[i] = hf.IsWaterAtWorldCell(wc.x, wc.y) ? CellState.NoNavMesh
                            : hf.IsSteepAtWorldCell(wc.x, wc.y, maxSlopeInt) ? CellState.Steep
                            : CellState.Free;
                        continue;
                    }

                    var origin3 = CellCenter(cell) + Vector3.up * 100f;
                    if (!Physics.Raycast(origin3, Vector3.down, out var hit, 200f, groundMask))
                    {
                        terrain[i] = CellState.OffGround;
                        heights[i] = 0f;
                        continue;
                    }

                    heights[i] = hit.point.y;
                    terrain[i] = Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle ? CellState.Steep
                        : !NavMesh.SamplePosition(hit.point, out _, navMeshSampleDistance, NavMesh.AllAreas) ? CellState.NoNavMesh
                        : CellState.Free;
                }
            }
        }

        // Buộc tính lại trạng thái mặt đất từ HeightField.
        public void RebuildTerrainFromHeightField()
        {
            if (!terrain.IsCreated || !heights.IsCreated) return;
            terrainBaked = true;
            BakeRegionCells(0, 0, size.x - 1, size.y - 1);
            Version++;
        }

        // Lấy mẫu độ cao số nguyên từ HeightField trên lưới 0.5 m trong hình chữ nhật xoay; tính sàn phẳng (trung bình 4 góc), điểm thấp nhất và cao nhất.
        public void SampleFootprintHeights(
            Vector3 center,
            Vector2Int baseFootprint,
            int angle,
            out float baseY,
            out float lowestY,
            out float highestY,
            out bool anySampleSteep,
            int customMaxSlopeDeg = -1)
        {
            EnsureTerrain();
            var hf = HeightField;
            int slopeLimit = customMaxSlopeDeg > 0 ? customMaxSlopeDeg : Mathf.RoundToInt(maxSlopeAngle);

            if (hf == null)
            {
                baseY = 0f;
                lowestY = 0f;
                highestY = 0f;
                anySampleSteep = false;
                return;
            }

            angle = NormalizeAngle(angle);
            long cos = Cos(angle);
            long sin = Sin(angle);
            long worldCx = (long)Math.Round(center.x * HeightField.HeightScale);
            long worldCz = (long)Math.Round(center.z * HeightField.HeightScale);

            long halfW = Math.Max(64L, (long)baseFootprint.x * (HeightField.HeightScale / 2) - 8L);
            long halfD = Math.Max(64L, (long)baseFootprint.y * (HeightField.HeightScale / 2) - 8L);

            int c0 = SampleRotatedPoint(hf, worldCx, worldCz, -halfW, -halfD, cos, sin);
            int c1 = SampleRotatedPoint(hf, worldCx, worldCz, halfW, -halfD, cos, sin);
            int c2 = SampleRotatedPoint(hf, worldCx, worldCz, -halfW, halfD, cos, sin);
            int c3 = SampleRotatedPoint(hf, worldCx, worldCz, halfW, halfD, cos, sin);

            int minSub = Math.Min(Math.Min(c0, c1), Math.Min(c2, c3));
            int maxSub = Math.Max(Math.Max(c0, c1), Math.Max(c2, c3));
            bool steep = false;

            // Quét lưới điểm cách nhau 0.5 m (128 đơn vị) trên toàn bộ đế công trình đã xoay.
            const long stepSub = 128;
            int stepsX = Math.Max(2, (int)((halfW * 2 + stepSub - 1) / stepSub));
            int stepsZ = Math.Max(2, (int)((halfD * 2 + stepSub - 1) / stepSub));
            for (int iz = 0; iz <= stepsZ; iz++)
            {
                long v = -halfD + iz * (halfD * 2) / stepsZ;
                for (int ix = 0; ix <= stepsX; ix++)
                {
                    long u = -halfW + ix * (halfW * 2) / stepsX;
                    long wx = worldCx + ((u * cos + v * sin) >> TrigShift);
                    long wz = worldCz + ((-u * sin + v * cos) >> TrigShift);
                    int hSub = hf.SampleBilinearWorld(wx, wz);
                    if (hSub < minSub) minSub = hSub;
                    if (hSub > maxSub) maxSub = hSub;
                    if (!steep && hf.IsSteepSampleWorld(wx, wz, slopeLimit))
                        steep = true;
                }
            }

            // Đặt mặt sàn ở điểm cao nhất dưới đế để mặt đất sườn dốc không xuyên qua sàn nhà; móng đá nối từ sàn xuống lowestY.
            baseY = maxSub * (1f / HeightField.HeightScale);
            lowestY = minSub * (1f / HeightField.HeightScale);
            highestY = maxSub * (1f / HeightField.HeightScale);
            anySampleSteep = steep;
        }

        static int SampleRotatedPoint(HeightField hf, long worldCx, long worldCz, long u, long v, long cos, long sin)
        {
            long wx = worldCx + ((u * cos + v * sin) >> TrigShift);
            long wz = worldCz + ((-u * sin + v * cos) >> TrigShift);
            return hf.SampleBilinearWorld(wx, wz);
        }

        // Kết quả cùng thứ tự với cells; trả về độ cao sàn phẳng baseY, độ cao thấp nhất lowestY và lý do chặn cụ thể.
        public CellState[] Evaluate(
            Vector3 center,
            Vector2Int baseFootprint,
            int angle,
            BuildingData buildingData,
            List<int> cells,
            out float baseY,
            out float lowestY,
            out PlacementBlockReason blockReason)
        {
            EnsureTerrain();
            CollectCells(center, baseFootprint, angle, cells);

            float allowedDelta = buildingData != null && buildingData.maxHeightDelta > 0f
                ? buildingData.maxHeightDelta
                : maxHeightDelta;
            int allowedSlope = buildingData != null && buildingData.maxSlopeAngle > 0f
                ? Mathf.RoundToInt(buildingData.maxSlopeAngle)
                : Mathf.RoundToInt(maxSlopeAngle);

            var result = new CellState[cells.Count];
            blockReason = PlacementBlockReason.None;

            float cellLowest = float.MaxValue;
            float cellHighest = float.MinValue;
            var hf = HeightField;

            for (int i = 0; i < cells.Count; i++)
            {
                int c = cells[i];
                if (c < 0)
                {
                    result[i] = CellState.OutOfBounds;
                    if (blockReason == PlacementBlockReason.None)
                        blockReason = PlacementBlockReason.OutOfBounds;
                    continue;
                }

                var ground = terrain[c];
                if (hf != null && ground == CellState.Free && allowedSlope != Mathf.RoundToInt(maxSlopeAngle))
                {
                    var wc = CellToWorldCell(new Vector2Int(c % size.x, c / size.x));
                    if (hf.IsSteepAtWorldCell(wc.x, wc.y, allowedSlope))
                        ground = CellState.Steep;
                }

                if (ground != CellState.OffGround)
                {
                    cellLowest = Mathf.Min(cellLowest, heights[c]);
                    cellHighest = Mathf.Max(cellHighest, heights[c]);
                }

                var occ = occupancy[c];
                if (occ == CellOccupancy.Building)
                {
                    result[i] = CellState.Occupied;
                    if (blockReason == PlacementBlockReason.None)
                        blockReason = PlacementBlockReason.OccupiedBuilding;
                }
                else if (occ == CellOccupancy.Static)
                {
                    result[i] = CellState.Occupied;
                    if (blockReason == PlacementBlockReason.None)
                        blockReason = PlacementBlockReason.OccupiedObstacle;
                }
                else
                {
                    result[i] = ground;
                    if (blockReason == PlacementBlockReason.None)
                    {
                        if (ground == CellState.NoNavMesh || ground == CellState.OffGround)
                            blockReason = PlacementBlockReason.Water;
                        else if (ground == CellState.Steep)
                            blockReason = PlacementBlockReason.SteepOrUneven;
                    }
                }
            }

            if (hf != null)
            {
                SampleFootprintHeights(center, baseFootprint, angle, out baseY, out lowestY, out float highestY, out bool sampleSteep, allowedSlope);
                lowestY = Mathf.Min(lowestY, cellLowest <= cellHighest ? cellLowest : lowestY);
                highestY = Mathf.Max(highestY, cellLowest <= cellHighest ? cellHighest : highestY);

                if (sampleSteep || highestY - lowestY > allowedDelta)
                {
                    for (int i = 0; i < result.Length; i++)
                        if (result[i] == CellState.Free) result[i] = CellState.Uneven;
                    if (blockReason == PlacementBlockReason.None)
                        blockReason = PlacementBlockReason.SteepOrUneven;
                }
            }
            else
            {
                lowestY = cellLowest <= cellHighest ? cellLowest : 0f;
                baseY = lowestY;
                if (cellLowest <= cellHighest && cellHighest - cellLowest > allowedDelta)
                {
                    for (int i = 0; i < result.Length; i++)
                        if (result[i] == CellState.Free) result[i] = CellState.Uneven;
                    if (blockReason == PlacementBlockReason.None)
                        blockReason = PlacementBlockReason.SteepOrUneven;
                }
            }

            return result;
        }

        // Phiên bản tương thích ngược cho nơi gọi không truyền BuildingData.
        public CellState[] Evaluate(Vector3 center, Vector2Int baseFootprint, int angle, List<int> cells, out float baseY)
        {
            return Evaluate(center, baseFootprint, angle, null, cells, out baseY, out _, out _);
        }
    }
}
