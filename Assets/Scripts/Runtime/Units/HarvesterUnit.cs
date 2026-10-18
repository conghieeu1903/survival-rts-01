using UnityEngine;

namespace SurvivalRts
{
    // Điều khiển công nhân khai thác (Tiều phu chặt gỗ, Thợ mỏ đập đá): đi tới điểm tài nguyên -> khai thác -> mang về xưởng.
    public class HarvesterUnit : MonoBehaviour
    {
        enum State
        {
            Idle,
            MovingToResource,
            Harvesting,
            ReturningToCamp
        }

        const float MoveSpeed = 3.2f;
        const float ArriveDistance = 0.25f;

        GpuMeshAnimator animator;
        ResourceCamp camp;
        int slotIndex;
        State state;
        Vector3 workPosition;
        Vector3 lookTarget;
        float timer;

        public void Setup(ResourceCamp ownerCamp, int slot)
        {
            camp = ownerCamp;
            slotIndex = slot;
            animator = GetComponent<GpuMeshAnimator>();
            transform.position = camp.GetDropOffPosition(slotIndex);
            SnapToGround();
            BeginNextCycle();
        }

        void Update()
        {
            if (camp == null) return;

            switch (state)
            {
                case State.Idle:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) BeginNextCycle();
                    break;

                case State.MovingToResource:
                    if (MoveTowards(workPosition))
                    {
                        state = State.Harvesting;
                        timer = camp.HarvestInterval;
                        if (animator != null) animator.Play("Work", true);
                    }
                    break;

                case State.Harvesting:
                    FaceTowards(lookTarget);
                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                    {
                        state = State.ReturningToCamp;
                        if (animator != null) animator.Play("Walk");
                    }
                    break;

                case State.ReturningToCamp:
                    var dropPos = camp.GetDropOffPosition(slotIndex);
                    if (MoveTowards(dropPos))
                    {
                        if (PopulationManager.Instance != null)
                        {
                            PopulationManager.Instance.AddResource(camp.ResourceType, camp.HarvestAmount);
                        }
                        BeginNextCycle();
                    }
                    break;
            }
        }

        void BeginNextCycle()
        {
            if (camp != null && camp.TryGetHarvestPoint(slotIndex, out workPosition, out lookTarget))
            {
                state = State.MovingToResource;
                if (animator != null) animator.Play("Walk");
            }
            else
            {
                state = State.Idle;
                timer = 1.5f;
                if (animator != null) animator.Play("Idle");
            }
        }

        bool MoveTowards(Vector3 destination)
        {
            var diff = destination - transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            if (dist <= ArriveDistance) return true;

            var dir = diff / dist;
            transform.position += dir * (MoveSpeed * Time.deltaTime);
            SnapToGround();
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 12f * Time.deltaTime);
            return false;
        }

        void FaceTowards(Vector3 target)
        {
            var dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), 12f * Time.deltaTime);
            }
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
