using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace SurvivalRts
{
    // Dijkstra từ các ô đích (Nhà chính) ra toàn lưới, rồi mỗi ô chỉ về ô lân cận gần đích nhất.
    [BurstCompile]
    public struct FlowFieldJob : IJob
    {
        const int Unreached = int.MaxValue;
        const int StraightStep = 10;
        const int DiagonalStep = 14;

        public int2 Size;
        public float MaxStep;

        // 0 = không đi được, còn lại là chi phí bước vào ô.
        [ReadOnly] public NativeArray<byte> Cost;
        [ReadOnly] public NativeArray<float> Height;
        [ReadOnly] public NativeArray<int> Goals;

        public NativeArray<int> Integration;
        public NativeArray<float2> Flow;

        public void Execute()
        {
            for (int i = 0; i < Integration.Length; i++)
            {
                Integration[i] = Unreached;
                Flow[i] = float2.zero;
            }

            var heap = new NativeList<int2>(Integration.Length, Allocator.Temp);
            for (int i = 0; i < Goals.Length; i++)
            {
                Integration[Goals[i]] = 0;
                Push(ref heap, new int2(0, Goals[i]));
            }

            while (heap.Length > 0)
            {
                var top = Pop(ref heap);
                int current = top.y;
                if (top.x > Integration[current]) continue;

                var cell = new int2(current % Size.x, current / Size.x);
                for (int k = 0; k < 8; k++)
                {
                    var offset = Offset(k);
                    if (!CanStep(cell, offset, out int next)) continue;

                    int step = (k < 4 ? StraightStep : DiagonalStep) * Cost[next];
                    int distance = top.x + step;
                    if (distance >= Integration[next]) continue;

                    Integration[next] = distance;
                    Push(ref heap, new int2(distance, next));
                }
            }

            for (int i = 0; i < Integration.Length; i++)
            {
                if (Integration[i] == Unreached || Integration[i] == 0) continue;

                var cell = new int2(i % Size.x, i / Size.x);
                int best = Integration[i];
                var direction = float2.zero;
                for (int k = 0; k < 8; k++)
                {
                    var offset = Offset(k);
                    if (!CanStep(cell, offset, out int next) || Integration[next] >= best) continue;
                    best = Integration[next];
                    direction = math.normalize((float2)offset);
                }
                Flow[i] = direction;
            }
        }

        bool CanStep(int2 cell, int2 offset, out int next)
        {
            var target = cell + offset;
            next = -1;
            if (!InBounds(target)) return false;

            next = target.x + target.y * Size.x;
            int from = cell.x + cell.y * Size.x;
            if (Cost[next] == 0 || math.abs(Height[next] - Height[from]) > MaxStep) return false;

            // Đi chéo chỉ khi hai ô cạnh đều đi được, tránh cắt góc vật cản.
            if (offset.x != 0 && offset.y != 0)
            {
                var sideA = new int2(cell.x + offset.x, cell.y);
                var sideB = new int2(cell.x, cell.y + offset.y);
                if (Cost[sideA.x + sideA.y * Size.x] == 0 || Cost[sideB.x + sideB.y * Size.x] == 0) return false;
            }
            return true;
        }

        bool InBounds(int2 cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Size.x && cell.y < Size.y;
        }

        static int2 Offset(int k)
        {
            switch (k)
            {
                case 0: return new int2(1, 0);
                case 1: return new int2(-1, 0);
                case 2: return new int2(0, 1);
                case 3: return new int2(0, -1);
                case 4: return new int2(1, 1);
                case 5: return new int2(1, -1);
                case 6: return new int2(-1, 1);
                default: return new int2(-1, -1);
            }
        }

        // Min-heap theo x (khoảng cách), y là chỉ số ô.
        static void Push(ref NativeList<int2> heap, int2 item)
        {
            heap.Add(item);
            int i = heap.Length - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heap[parent].x <= heap[i].x) break;
                (heap[parent], heap[i]) = (heap[i], heap[parent]);
                i = parent;
            }
        }

        static int2 Pop(ref NativeList<int2> heap)
        {
            var top = heap[0];
            int last = heap.Length - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);

            int i = 0;
            while (true)
            {
                int left = i * 2 + 1;
                int right = left + 1;
                int smallest = i;
                if (left < heap.Length && heap[left].x < heap[smallest].x) smallest = left;
                if (right < heap.Length && heap[right].x < heap[smallest].x) smallest = right;
                if (smallest == i) break;
                (heap[smallest], heap[i]) = (heap[i], heap[smallest]);
                i = smallest;
            }
            return top;
        }
    }
}
