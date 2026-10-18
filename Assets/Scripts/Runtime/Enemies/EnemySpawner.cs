using UnityEngine;

namespace SurvivalRts
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [Tooltip("Vùng cho phép spawn")]
        [SerializeField] EnemySpawnArea area;

        [Header("Spawn thử")]
        [Tooltip("Tự spawn một đợt khi bắt đầu chơi, dùng để test khi chưa có hệ thống đợt tấn công")]
        [SerializeField] bool spawnOnStart = true;

        [Tooltip("Số kẻ địch trong đợt spawn thử")]
        [SerializeField] int count = 5;

        [Tooltip("Giây giữa hai lần spawn trong đợt. 0 = spawn tất cả cùng lúc")]
        [SerializeField] float interval = 0.5f;

        void Start()
        {
            if (spawnOnStart) SpawnWave(count, interval);
        }

        // Kẻ địch là thực thể ECS; việc tạo thật diễn ra trong EnemySimulationSystem.
        public void SpawnWave(int amount, float gap)
        {
            EnemyWorld.RequestSpawn(area.transform.position, area.Radius, amount, gap);
        }
    }
}
