using System;
using UnityEngine;

namespace SurvivalRts
{
    public class Health : MonoBehaviour
    {
        [Header("Máu")]
        [Tooltip("Máu tối đa, máu hiện tại bằng giá trị này khi bắt đầu")]
        [SerializeField] float maxHealth = 100f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        public event Action<float, float> Changed;
        public event Action Died;

        void Awake()
        {
            Current = maxHealth;
        }

        public void SetMax(float value)
        {
            maxHealth = value;
            Current = value;
            Changed?.Invoke(Current, maxHealth);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;

            Current = Mathf.Max(0f, Current - amount);
            Changed?.Invoke(Current, maxHealth);
            if (IsDead) Died?.Invoke();
        }
    }
}
