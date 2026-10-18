using UnityEngine;

namespace SurvivalRts
{
    // Điều khiển thợ xây: tự tìm công trường đang xây gần nhất để tới gõ búa, xong việc thì về nhà chờ.
    public class BuilderUnit : MonoBehaviour
    {
        const float MoveSpeed = 3.8f;
        const float ArriveThreshold = 0.25f;
        const float HammerInterval = 0.55f;

        GpuMeshAnimator animator;
        Transform homeTransform;
        Vector3 idlePosition;
        ConstructionSite targetSite;
        int workSlot;
        float hammerTimer;

        public void Setup(Transform home, int slot)
        {
            homeTransform = home;
            workSlot = slot;
            animator = GetComponent<GpuMeshAnimator>();
            idlePosition = ComputeIdlePosition();
            transform.position = idlePosition;
            SnapToGround();
            if (animator != null) animator.Play("Idle");
        }

        void Update()
        {
            if (targetSite == null || targetSite.IsCompleted)
            {
                targetSite = FindNearestSite();
            }

            if (targetSite != null)
            {
                UpdateBuildTask();
            }
            else
            {
                UpdateReturnHome();
            }
        }

        void UpdateBuildTask()
        {
            var workPos = targetSite.GetWorkSlotPosition(workSlot);
            var toSpot = workPos - transform.position;
            toSpot.y = 0f;
            float dist = toSpot.magnitude;

            if (dist > ArriveThreshold)
            {
                var dir = toSpot / dist;
                transform.position += dir * (MoveSpeed * Time.deltaTime);
                SnapToGround();
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 12f * Time.deltaTime);
                if (animator != null) animator.Play("Walk");
                hammerTimer = 0.18f + (workSlot % 3) * 0.12f;
                return;
            }

            // Đã tới rìa công trường: quay mặt vào tâm công trình và vung búa xây dựng.
            var lookDir = targetSite.transform.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir.normalized), 12f * Time.deltaTime);
            }

            if (animator != null) animator.Play("Build");
            targetSite.AddWork(Time.deltaTime);

            hammerTimer -= Time.deltaTime;
            if (hammerTimer <= 0f)
            {
                hammerTimer = HammerInterval;
                AudioDirector.Post(SoundId.BuilderHammer, transform.position);
            }
        }

        void UpdateReturnHome()
        {
            if (homeTransform != null) idlePosition = ComputeIdlePosition();

            var toHome = idlePosition - transform.position;
            toHome.y = 0f;
            float dist = toHome.magnitude;
            if (dist > ArriveThreshold)
            {
                var dir = toHome / dist;
                transform.position += dir * (MoveSpeed * Time.deltaTime);
                SnapToGround();
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
                if (animator != null) animator.Play("Walk");
            }
            else
            {
                if (animator != null) animator.Play("Idle");
            }
        }

        ConstructionSite FindNearestSite()
        {
            var sites = ConstructionSite.ActiveSites;
            ConstructionSite best = null;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < sites.Count; i++)
            {
                var s = sites[i];
                if (s == null || s.IsCompleted) continue;
                float dSq = (s.transform.position - transform.position).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = s;
                }
            }
            return best;
        }

        Vector3 ComputeIdlePosition()
        {
            if (homeTransform == null) return transform.position;
            float angle = (workSlot - 1) * 0.65f;
            var localOffset = new Vector3(Mathf.Sin(angle) * 1.2f, 0f, 1.6f + Mathf.Cos(angle) * 0.3f);
            return homeTransform.position + homeTransform.rotation * localOffset;
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
