using UnityEngine;

namespace SurvivalRts
{
    // Vùng tròn trong scene; điểm spawn cụ thể do EnemySimulationSystem chọn ngẫu nhiên bên trong.
    public class EnemySpawnArea : MonoBehaviour
    {
        const int GizmoSegments = 48;

        [Header("Vùng spawn")]
        [Tooltip("Bán kính vùng spawn (m), tâm là vị trí của object này")]
        [SerializeField] float radius = 4f;

        public float Radius => radius;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.9f);
            var previous = transform.position + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= GizmoSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / GizmoSegments;
                var next = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
