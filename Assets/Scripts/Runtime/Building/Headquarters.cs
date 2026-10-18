using System;
using UnityEngine;

namespace SurvivalRts
{
    [RequireComponent(typeof(Health))]
    public class Headquarters : MonoBehaviour
    {
        // Thua ván khi sự kiện này phát ra; GameManager sẽ đăng ký khi có.
        public static event Action<Headquarters> Destroyed;

        Health health;

        void Awake()
        {
            health = GetComponent<Health>();
        }

        void OnEnable()
        {
            health.Died += OnDied;
        }

        void OnDisable()
        {
            health.Died -= OnDied;
        }

        void OnDied()
        {
            Debug.Log("Headquarters destroyed");
            Destroyed?.Invoke(this);
        }
    }
}
