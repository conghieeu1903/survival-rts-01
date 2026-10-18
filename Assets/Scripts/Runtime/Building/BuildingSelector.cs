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

        void Update()
        {
            if (Selected != null && Selected.IsDead) Select(null);

            var mouse = Mouse.current;
            bool blocked = wasPlacing || placer.IsPlacing || UiPointerState.IsOverUI;
            if (mouse == null || blocked || !mouse.leftButton.wasPressedThisFrame) return;

            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            Health target = null;
            if (Physics.Raycast(ray, out var hit, 500f))
                target = hit.collider.GetComponentInParent<Health>();
            Select(target);
        }

        void LateUpdate()
        {
            wasPlacing = placer.IsPlacing;
        }

        void Select(Health target)
        {
            if (Selected == target) return;
            Selected = target;
            SelectionChanged?.Invoke(target);
        }
    }
}
