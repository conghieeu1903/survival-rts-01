using Unity.Entities;
using UnityEngine;

namespace SurvivalRts
{
    // Đặt một cái trong scene để báo cho thế giới ECS biết dùng dữ liệu kẻ địch nào.
    public class EnemyEcsConfig : MonoBehaviour
    {
        [Header("Dữ liệu")]
        [Tooltip("Dữ liệu kẻ địch dùng để tạo prefab entity (mesh, material, chỉ số, animation)")]
        [SerializeField] EnemyData enemyData;

        void Awake()
        {
            if (enemyData == null) return;

            EnemyWorld.Configure(enemyData);
            World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<EnemyAnimationSystem>()?.Configure(enemyData);
        }
    }
}
