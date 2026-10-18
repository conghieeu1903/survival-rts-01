using System.Collections.Generic;
using UnityEngine;

namespace SurvivalRts
{
    public enum TargetPriority
    {
        Unit = 0,
        Building = 1,
        Headquarters = 2
    }

    // Đối tượng phe mình mà kẻ địch (ECS) có thể nhắm và gây sát thương.
    [RequireComponent(typeof(Health))]
    public class PlayerTarget : MonoBehaviour
    {
        static readonly List<PlayerTarget> all = new List<PlayerTarget>();
        static readonly Dictionary<int, PlayerTarget> byId = new Dictionary<int, PlayerTarget>();
        static int nextId = 1;

        [Header("Mục tiêu")]
        [Tooltip("Bán kính (m) khi không có GridOccupant, ví dụ cho đơn vị")]
        [SerializeField] float radius = 0.5f;

        GridOccupant occupant;

        public static IReadOnlyList<PlayerTarget> All => all;

        public int Id { get; private set; }
        public Health Health { get; private set; }
        public GridOccupant Occupant => occupant;
        public TargetPriority Priority { get; private set; }

        public Vector2 HalfExtents
        {
            get
            {
                if (occupant == null || BuildGrid.Instance == null) return new Vector2(radius, radius);
                return (Vector2)occupant.footprint * (BuildGrid.Instance.CellSize * 0.5f);
            }
        }

        public static bool TryGet(int id, out PlayerTarget target)
        {
            return byId.TryGetValue(id, out target);
        }

        void Awake()
        {
            Id = nextId++;
            Health = GetComponent<Health>();
            occupant = GetComponent<GridOccupant>();
            Priority = GetComponent<Headquarters>() != null ? TargetPriority.Headquarters
                : occupant != null ? TargetPriority.Building
                : TargetPriority.Unit;
        }

        void OnEnable()
        {
            all.Add(this);
            byId[Id] = this;
            Health.Died += OnDied;
        }

        void OnDisable()
        {
            all.Remove(this);
            byId.Remove(Id);
            Health.Died -= OnDied;
        }

        void OnDied()
        {
            // Nhà chính ở lại để GameManager xử lý thua; các công trình khác bị phá.
            if (Priority != TargetPriority.Headquarters) Destroy(gameObject);
        }
    }
}
