using UnityEngine;

namespace SurvivalRts
{
    // Điều khiển người dân tự do đi dạo quanh nhà dân hoặc Nhà chính khi chưa được gán nghề.
    public class VillagerUnit : MonoBehaviour
    {
        const float MoveSpeed = 1.8f;
        const float WanderRadius = 2.8f;

        GpuMeshAnimator animator;
        Vector3 homePosition;
        Vector3 targetPosition;
        float waitTimer;
        bool moving;

        public void Setup(Vector3 home)
        {
            homePosition = home;
            animator = GetComponent<GpuMeshAnimator>();
            PickNextWait();
        }

        void Update()
        {
            if (!moving)
            {
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0f) PickNextTarget();
                return;
            }

            var diff = targetPosition - transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            if (dist <= 0.1f)
            {
                PickNextWait();
                return;
            }

            var dir = diff / dist;
            transform.position += dir * (MoveSpeed * Time.deltaTime);
            SnapToGround();
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
        }

        void PickNextWait()
        {
            moving = false;
            waitTimer = Random.Range(2f, 4.5f);
            if (animator != null) animator.Play("Idle");
        }

        void PickNextTarget()
        {
            for (int i = 0; i < 6; i++)
            {
                var offset = Random.insideUnitCircle * WanderRadius;
                var candidate = homePosition + new Vector3(offset.x, 0f, offset.y);
                if (BuildGrid.Instance == null)
                {
                    targetPosition = candidate;
                    moving = true;
                    if (animator != null) animator.Play("Walk");
                    return;
                }

                var cell = BuildGrid.Instance.MinCell(candidate, Vector2Int.one);
                if (BuildGrid.Instance.InBounds(cell) && BuildGrid.Instance.GetOccupancy(cell) == CellOccupancy.None)
                {
                    targetPosition = candidate;
                    moving = true;
                    if (animator != null) animator.Play("Walk");
                    return;
                }
            }
            PickNextWait();
        }

        void SnapToGround()
        {
            if (BuildGrid.Instance == null) return;
            var cell = BuildGrid.Instance.MinCell(transform.position, Vector2Int.one);
            if (!BuildGrid.Instance.InBounds(cell)) return;
            int idx = BuildGrid.Instance.Index(cell);
            var pos = transform.position;
            pos.y = BuildGrid.Instance.Heights[idx];
            transform.position = pos;
        }
    }
}
