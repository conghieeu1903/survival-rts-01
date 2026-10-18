using Unity.Entities;
using UnityEngine;

namespace SurvivalRts
{
    // Phần GameObject của một tháp: chỉ giữ entity tháp trong ECS, việc chọn mục tiêu và bắn nằm ở TowerFireJob.
    public class Tower : MonoBehaviour
    {
        [Header("Dữ liệu")]
        [Tooltip("Chỉ số bắn của tháp. Với tháp đặt bằng menu, BuildingPlacer tự gán từ BuildingData")]
        [SerializeField] TowerData data;

        Entity entity = Entity.Null;

        public TowerData Data => data;

        void Start()
        {
            Register();
        }

        public void Setup(TowerData towerData)
        {
            data = towerData;
            Register();
        }

        void Register()
        {
            if (entity != Entity.Null || data == null) return;
            entity = EnemyWorld.CreateTower(data, transform.position);
        }

        void OnDestroy()
        {
            EnemyWorld.DestroyTower(entity);
            entity = Entity.Null;
        }
    }
}
