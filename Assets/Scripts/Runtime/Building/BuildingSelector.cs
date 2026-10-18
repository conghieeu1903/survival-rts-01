using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SurvivalRts
{
    public class BuildingSelector : MonoBehaviour
    {
        const int RingSegments = 48;

        [Header("Tham chiếu")]
        [Tooltip("Placer dùng để bỏ qua click khi đang đặt công trình")]
        [SerializeField] BuildingPlacer placer;

        [Tooltip("Camera dùng để bắn tia từ chuột, mặc định là Main Camera")]
        [SerializeField] Camera cam;

        LineRenderer rangeRing;
        Material ringMaterial;
        bool wasPlacing;

        public BuildingPlacer Placer => placer;
        public Health Selected { get; private set; }

        public event Action<Health> SelectionChanged;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            InitRangeRing();
        }

        void InitRangeRing()
        {
            var go = new GameObject("SelectedRangeRing");
            go.transform.SetParent(transform, false);
            rangeRing = go.AddComponent<LineRenderer>();
            rangeRing.useWorldSpace = true;
            rangeRing.loop = true;
            rangeRing.positionCount = RingSegments;
            rangeRing.startWidth = 0.08f;
            rangeRing.endWidth = 0.08f;
            rangeRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rangeRing.receiveShadows = false;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            ringMaterial = new Material(shader);
            ringMaterial.color = new Color(0.95f, 0.78f, 0.22f, 0.9f);
            if (ringMaterial.HasProperty("_BaseColor"))
                ringMaterial.SetColor("_BaseColor", new Color(0.95f, 0.78f, 0.22f, 0.9f));
            rangeRing.sharedMaterial = ringMaterial;
            rangeRing.enabled = false;
        }

        void OnDisable()
        {
            if (!ReferenceEquals(Selected, null)) Selected.Died -= OnSelectedDied;
            if (rangeRing != null) rangeRing.enabled = false;
        }

        void OnDestroy()
        {
            if (ringMaterial != null) Destroy(ringMaterial);
        }

        void Update()
        {
            if (!ReferenceEquals(Selected, null) && (Selected == null || Selected.IsDead))
            {
                Select(null);
            }

            UpdateRangeRing();

            var mouse = Mouse.current;
            bool activeTool = placer != null && (placer.IsPlacing || placer.IsDemolishing);
            bool blocked = wasPlacing || activeTool || UiPointerState.IsOverUI;
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
            wasPlacing = placer != null && (placer.IsPlacing || placer.IsDemolishing);
        }

        void OnSelectedDied()
        {
            Select(null);
        }

        public void Select(Health target)
        {
            if (ReferenceEquals(Selected, target)) return;
            if (!ReferenceEquals(Selected, null)) Selected.Died -= OnSelectedDied;
            Selected = target;
            if (Selected != null) Selected.Died += OnSelectedDied;
            UpdateRangeRing();
            SelectionChanged?.Invoke(target);
        }

        void UpdateRangeRing()
        {
            if (rangeRing == null) return;
            float r = GetSelectedRadius(Selected);
            if (r <= 0f)
            {
                rangeRing.enabled = false;
                return;
            }

            rangeRing.enabled = true;
            var center = Selected.transform.position;
            float y = center.y + 0.08f;
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i / (float)RingSegments * Mathf.PI * 2f;
                rangeRing.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * r, y, center.z + Mathf.Sin(angle) * r));
            }
        }

        static float GetSelectedRadius(Health target)
        {
            if (target == null || target.IsDead) return 0f;
            if (target.GetComponent<DemolishSite>() != null) return 0f;

            var tower = target.GetComponent<Tower>();
            if (tower != null && tower.Data != null && tower.Data.range > 0f)
                return tower.Data.range;

            var site = target.GetComponent<ConstructionSite>();
            if (site != null && site.Data != null)
            {
                if (site.Data.tower != null && site.Data.tower.range > 0f) return site.Data.tower.range;
                if (site.Data.harvestResource != ResourceType.None && !site.Data.requireAdjacentResource && site.Data.harvestRadius > 0f)
                    return site.Data.harvestRadius;
            }

            var camp = target.GetComponent<ResourceCamp>();
            if (camp != null && camp.HarvestRadius > 0f)
                return camp.HarvestRadius;

            return 0f;
        }
    }
}
