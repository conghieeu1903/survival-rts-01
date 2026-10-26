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
            SnapToGround();
            homePosition.y = transform.position.y;
            PickNextWait();
        }

        void Update()
        {
            if (!moving)
            {
                SnapToGround();
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0f) PickNextTarget();
                return;
            }

            var diff = targetPosition - transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            if (dist <= 0.1f)
            {
                SnapToGround();
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
            var grid = BuildGrid.Instance;
            for (int i = 0; i < 8; i++)
            {
                var offset = Random.insideUnitCircle * WanderRadius;
                var candidate = homePosition + new Vector3(offset.x, 0f, offset.y);
                if (grid == null)
                {
                    targetPosition = candidate;
                    moving = true;
                    if (animator != null) animator.Play("Walk");
                    return;
                }

                var cell = grid.MinCell(candidate, Vector2Int.one);
                if (!grid.InBounds(cell) || grid.GetOccupancy(cell) != CellOccupancy.None) continue;
                int idx = grid.Index(cell);
                if (grid.Terrain[idx] != CellState.Free) continue;
                float candidateY = grid.SampleWorldHeight(candidate);
                if (Mathf.Abs(candidateY - homePosition.y) > 0.65f) continue;

                targetPosition = new Vector3(candidate.x, candidateY, candidate.z);
                moving = true;
                if (animator != null) animator.Play("Walk");
                return;
            }
            PickNextWait();
        }

        void SnapToGround()
        {
            var grid = BuildGrid.Instance;
            if (grid == null) return;
            var pos = transform.position;
            pos.y = grid.SampleWorldHeight(pos);
            transform.position = pos;
        }
    }
}
