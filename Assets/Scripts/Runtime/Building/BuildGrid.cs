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
        Static,
        Building
    }

    public class BuildGrid : MonoBehaviour
    {
        public static BuildGrid Instance { get; private set; }

        [Header("Lưới")]
        [Tooltip("Cạnh mỗi ô lưới (m)")]
        [SerializeField] float cellSize = 1f;

        [Tooltip("Số ô của lưới (rộng x sâu), lưới căn giữa quanh vị trí của object này")]
        [SerializeField] Vector2Int size = new Vector2Int(50, 50);

        [Header("Điều kiện đặt")]
        [Tooltip("Layer của mặt đất, dùng để đo độ cao và độ dốc")]
        [SerializeField] LayerMask groundMask;

        [Tooltip("Độ dốc tối đa (độ) của một ô để được phép đặt")]
        [SerializeField] float maxSlopeAngle = 30f;

        [Tooltip("Chênh lệch độ cao tối đa (m) giữa các ô trong cùng một công trình")]
        [SerializeField] float maxHeightDelta = 0.5f;

        [Tooltip("Khoảng cách tối đa (m) từ tâm ô tới NavMesh để ô được coi là đi được")]
        [SerializeField] float navMeshSampleDistance = 0.6f;

        // Ba mảng cùng chỉ số Index(cell) = x + y * size.x, dùng NativeArray để job Burst đọc được.
        NativeArray<CellOccupancy> occupancy;
        NativeArray<CellState> terrain;
        NativeArray<float> heights;
        bool terrainBaked;
        Vector3 origin;

        public float CellSize => cellSize;
        public Vector2Int Size => size;
        public Vector3 Origin => origin;
        public NativeArray<CellOccupancy> Occupancy => occupancy;

        // Trạng thái địa hình của từng ô: Free, OffGround, Steep hoặc NoNavMesh. Không tính việc bị chiếm.
        public NativeArray<CellState> Terrain
        {
            get { EnsureTerrain(); return terrain; }
        }

        // Độ cao mặt đất tại tâm từng ô, bằng 0 với ô OffGround.
        public NativeArray<float> Heights
        {
            get { EnsureTerrain(); return heights; }
        }

        // Tăng mỗi khi có ô đổi trạng thái chiếm, để flow field của kẻ địch biết cần tính lại.
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
            if (Instance == this) Instance = null;
            if (occupancy.IsCreated) occupancy.Dispose();
            if (terrain.IsCreated) terrain.Dispose();
            if (heights.IsCreated) heights.Dispose();
        }

        public static Vector2Int RotatedFootprint(Vector2Int footprint, int quarterTurns)
        {
            return (quarterTurns & 1) == 1 ? new Vector2Int(footprint.y, footprint.x) : footprint;
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

        public bool InBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < size.x && cell.y < size.y;
        }

        public int Index(Vector2Int cell)
        {
            return cell.x + cell.y * size.x;
        }

        public void SetOccupancy(Vector2Int min, Vector2Int footprint, CellOccupancy value)
        {
            for (int y = 0; y < footprint.y; y++)
            {
                for (int x = 0; x < footprint.x; x++)
                {
                    var cell = new Vector2Int(min.x + x, min.y + y);
                    if (InBounds(cell)) occupancy[Index(cell)] = value;
                }
            }
            Version++;
        }

        public CellOccupancy GetOccupancy(Vector2Int cell)
        {
            return InBounds(cell) ? occupancy[Index(cell)] : CellOccupancy.Static;
        }

        // Địa hình không đổi trong ván nên chỉ đo một lần, ở lần dùng đầu tiên
        // (lúc đó NavMesh của scene chắc chắn đã được nạp).
        void EnsureTerrain()
        {
            if (terrainBaked) return;
            terrainBaked = true;

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    int i = Index(cell);
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

        // Kết quả được xếp theo thứ tự x + y * footprint.x, bắt đầu từ ô MinCell.
        public CellState[] Evaluate(Vector3 center, Vector2Int footprint, out float baseY)
        {
            EnsureTerrain();
            var min = MinCell(center, footprint);
            var result = new CellState[footprint.x * footprint.y];
            float lowest = float.MaxValue;
            float highest = float.MinValue;

            for (int y = 0; y < footprint.y; y++)
            {
                for (int x = 0; x < footprint.x; x++)
                {
                    int i = x + y * footprint.x;
                    var cell = new Vector2Int(min.x + x, min.y + y);

                    if (!InBounds(cell))
                    {
                        result[i] = CellState.OutOfBounds;
                        continue;
                    }

                    int c = Index(cell);
                    var ground = terrain[c];
                    if (ground != CellState.OffGround)
                    {
                        lowest = Mathf.Min(lowest, heights[c]);
                        highest = Mathf.Max(highest, heights[c]);
                    }

                    result[i] = occupancy[c] != CellOccupancy.None ? CellState.Occupied : ground;
                }
            }

            baseY = lowest <= highest ? lowest : 0f;
            if (lowest <= highest && highest - lowest > maxHeightDelta)
            {
                for (int i = 0; i < result.Length; i++)
                    if (result[i] == CellState.Free) result[i] = CellState.Uneven;
            }

            return result;
        }
    }
}
