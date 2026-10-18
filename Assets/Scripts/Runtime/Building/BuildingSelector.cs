using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SurvivalRts
{
    public class BuildingSelector : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [Tooltip("Placer dùng để bỏ qua click khi đang đặt công trình")]
        [SerializeField] BuildingPlacer placer;

        [Tooltip("Camera dùng để bắn tia từ chuột, mặc định là Main Camera")]
        [SerializeField] Camera cam;

        bool wasPlacing;

        public Health Selected { get; private set; }

        public event Action<Health> SelectionChanged;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        void OnDisable()
        {
            if (!ReferenceEquals(Selected, null)) Selected.Died -= OnSelectedDied;
        }

        void Update()
        {
            if (!ReferenceEquals(Selected, null) && (Selected == null || Selected.IsDead))
            {
                Select(null);
            }

            var mouse = Mouse.current;
            bool blocked = wasPlacing || placer.IsPlacing || UiPointerState.IsOverUI;
            if (mouse == null || blocked || !mouse.leftButton.wasPressedThisFrame) return;

            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            Health target = null;
            if (Physics.Raycast(ray, out var hit, 500f))
                target = hit.collider.GetComponentInParent<Health>();
            if (target != null && target.IsDead) target = null;
            Select(target);
        }

        void LateUpdate()
        {
            wasPlacing = placer.IsPlacing;
        }

        void OnSelectedDied()
        {
            Select(null);
        }

        void Select(Health target)
        {
            if (ReferenceEquals(Selected, target)) return;
            if (!ReferenceEquals(Selected, null)) Selected.Died -= OnSelectedDied;
            Selected = target;
            if (Selected != null) Selected.Died += OnSelectedDied;
            SelectionChanged?.Invoke(target);
        }
    }
}
