using UnityEngine;

namespace SurvivalRts
{
    // Đặt một cái trong scene để báo cho thế giới ECS biết dùng dữ liệu kẻ địch nào.
    public class EnemyEcsConfig : MonoBehaviour
    {
        [Header("Dữ liệu")]
        [Tooltip("Dữ liệu kẻ địch dùng để tạo prefab entity (mesh, material, chỉ số)")]
        [SerializeField] EnemyData enemyData;

        void Awake()
        {
            if (enemyData != null) EnemyWorld.Configure(enemyData);
        }
    }
}
