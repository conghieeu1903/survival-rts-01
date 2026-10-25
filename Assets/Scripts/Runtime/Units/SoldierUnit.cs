using Unity.Entities;
using UnityEngine;

namespace SurvivalRts
{
    // Lính tự canh gác: đứng ở vị trí trước trại, SoldierCombatSystem báo kẻ địch gần nhất trong tầm,
    // lính chạy tới chém, kẻ địch chết hoặc chạy xa thì quay về chỗ canh gác.
    [RequireComponent(typeof(Health))]
    public class SoldierUnit : MonoBehaviour
    {
        const float ArriveDistance = 0.15f;
        const float RotationSharpness = 12f;

        Barracks barracks;
        SoldierData data;
        GpuMeshAnimator animator;
        Health health;

        Vector3 guardPosition;
        Vector3 barracksSlot;
        bool customPost;
        Entity target = Entity.Null;
        Vector3 targetPosition;
        float attackTimer;
        float strikeTimer;
        bool strikePending;
        bool dismissed;

        public SoldierData Data => data;
        public Vector3 GuardPosition => guardPosition;
        public float DetectRange => data != null ? data.detectRange : 0f;
        public bool HasTarget => target != Entity.Null;
        public bool IsOnCustomPost => customPost;

        public void Setup(Barracks owner, SoldierData soldierData, Vector3 guard)
        {
            barracks = owner;
            data = soldierData;
            barracksSlot = guard;
            guardPosition = guard;
            animator = GetComponent<GpuMeshAnimator>();
            health = GetComponent<Health>();
            health.SetMax(data.maxHealth);
            if (animator != null) animator.Play("Walk");
        }

        // Vị trí trong hàng canh gác trước trại; chỉ áp dụng khi lính không bị điều đi chỗ khác.
        public void SetBarracksSlot(Vector3 slot)
        {
            barracksSlot = slot;
            if (!customPost) guardPosition = slot;
        }

        // Lệnh của người chơi: chạy tới điểm này và đóng quân canh gác tại đó.
        public void CommandMove(Vector3 destination)
        {
            customPost = true;
            guardPosition = destination;
            target = Entity.Null;
            strikePending = false;
        }

        public void ReturnToBarracks()
        {
            customPost = false;
            guardPosition = barracksSlot;
            target = Entity.Null;
            strikePending = false;
        }

        // Gọi bởi SoldierCombatSystem mỗi frame: kẻ địch gần nhất trong tầm canh gác (Entity.Null nếu không có).
        public void SetTarget(Entity enemy, Vector3 enemyPosition)
        {
            target = enemy;
            targetPosition = enemyPosition;
        }

        void OnEnable()
        {
            SoldierCombatSystem.Register(this);
        }

        void OnDisable()
        {
            SoldierCombatSystem.Unregister(this);
        }

        void Update()
        {
            if (data == null || health == null || health.IsDead) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (target != Entity.Null && FlatDistance(targetPosition, guardPosition) > data.leashRange)
                target = Entity.Null;

            if (target != Entity.Null)
            {
                if (FlatDistance(transform.position, targetPosition) <= data.attackRange)
                {
                    Face(targetPosition, dt);
                    TickAttack(dt);
                }
                else
                {
                    strikePending = false;
                    MoveTowards(targetPosition, dt);
                }
                return;
            }

            strikePending = false;
            attackTimer = 0f;
            if (FlatDistance(transform.position, guardPosition) > ArriveDistance)
            {
                MoveTowards(guardPosition, dt);
            }
            else if (animator != null)
            {
                animator.Play("Idle");
            }
        }

        void TickAttack(float dt)
        {
            attackTimer -= dt;
            if (attackTimer <= 0f)
            {
                attackTimer = data.attackInterval;
                if (animator != null) animator.Play("Attack", true);
                strikePending = true;
                strikeTimer = data.strikeDelay;
            }

            if (!strikePending) return;
            strikeTimer -= dt;
            if (strikeTimer > 0f) return;

            strikePending = false;
            SoldierCombatSystem.QueueDamage(target, data.damage);
            AudioDirector.Post(SoundId.EnemyHit, transform.position);
        }

        void MoveTowards(Vector3 destination, float dt)
        {
            if (animator != null) animator.Play("Walk");

            var diff = destination - transform.position;
            diff.y = 0f;
            float distance = diff.magnitude;
            if (distance < 1e-4f) return;

            var direction = diff / distance;
            transform.position += direction * Mathf.Min(distance, data.moveSpeed * dt);
            SnapToGround();
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), RotationSharpness * dt);
        }

        void Face(Vector3 point, float dt)
        {
            var diff = point - transform.position;
            diff.y = 0f;
            if (diff.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(diff), RotationSharpness * dt);
        }

        void SnapToGround()
        {
            var grid = BuildGrid.Instance;
            if (grid == null) return;
            var cell = grid.MinCell(transform.position, Vector2Int.one);
            if (!grid.InBounds(cell)) return;
            var position = transform.position;
            position.y = grid.Heights[grid.Index(cell)];
            transform.position = position;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // Trại gọi khi giải ngũ hoặc bị phá: lính biến mất và không bị tính là chết.
        public void Dismiss()
        {
            dismissed = true;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (!dismissed && barracks != null) barracks.OnSoldierDied(this);
        }
    }
}
