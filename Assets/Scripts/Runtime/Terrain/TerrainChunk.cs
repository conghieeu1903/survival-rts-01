using System;

namespace SurvivalRts
{
    // Lưu dữ liệu độ cao, trạng thái địa hình và chiếm ô cho một chunk 50x50 m.
    public sealed class TerrainChunk
    {
        public const int Size = 50;
        public const int CellCount = Size * Size;

        public readonly int ChunkX;
        public readonly int ChunkZ;
        public int Version { get; private set; }

        readonly short[] heights;
        readonly CellOccupancy[] occupancy;
        readonly CellState[] terrain;

        public TerrainChunk(int chunkX, int chunkZ, short defaultHeightSub)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            heights = new short[CellCount];
            occupancy = new CellOccupancy[CellCount];
            terrain = new CellState[CellCount];

            if (defaultHeightSub != 0)
            {
                for (int i = 0; i < CellCount; i++)
                    heights[i] = defaultHeightSub;
            }
        }

        public static int LocalIndex(int localX, int localZ) => localX + localZ * Size;

        public short GetHeight(int localX, int localZ) => heights[localX + localZ * Size];

        public short GetHeightByIndex(int localIndex) => heights[localIndex];

        public void SetHeight(int localX, int localZ, short value)
        {
            int idx = localX + localZ * Size;
            if (heights[idx] == value) return;
            heights[idx] = value;
            Version++;
        }

        public CellOccupancy GetOccupancy(int localX, int localZ) => occupancy[localX + localZ * Size];

        public void SetOccupancy(int localX, int localZ, CellOccupancy value)
        {
            int idx = localX + localZ * Size;
            if (occupancy[idx] == value) return;
            occupancy[idx] = value;
            Version++;
        }

        public CellState GetTerrain(int localX, int localZ) => terrain[localX + localZ * Size];

        public void SetTerrain(int localX, int localZ, CellState value)
        {
            int idx = localX + localZ * Size;
            if (terrain[idx] == value) return;
            terrain[idx] = value;
            Version++;
        }

        public void BumpVersion() => Version++;
    }
}
