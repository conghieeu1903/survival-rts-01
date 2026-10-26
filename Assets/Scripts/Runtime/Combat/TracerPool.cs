using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace SurvivalRts
{
    // Vẽ tia đạn của tháp bằng LineRenderer lấy từ pool. Phát bắn ngoài khung camera bị bỏ qua, không tốn gì.
    public class TracerPool : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [Tooltip("Camera dùng để loại tia đạn ngoài màn hình, mặc định là Main Camera")]
        [SerializeField] Camera cam;

        [Header("Hiển thị")]
        [Tooltip("Material của tia đạn, nên là loại Unlit trong suốt")]
        [SerializeField] Material material;

        [Tooltip("Màu tia đạn lúc vừa bắn, mờ dần về trong suốt")]
        [SerializeField] Color color = new Color(1f, 0.85f, 0.4f, 1f);

        [Tooltip("Độ rộng tia đạn (m)")]
        [SerializeField] float width = 0.06f;

        [Tooltip("Số giây tia đạn còn hiện trên màn hình")]
        [SerializeField] float duration = 0.08f;

        [Header("Giới hạn")]
        [Tooltip("Số tia đạn tối đa hiện cùng lúc. Vượt quá thì phát bắn mới không được vẽ")]
        [SerializeField] int maxTracers = 256;

        class Tracer
        {
            public LineRenderer Line;
            public float Remaining;
        }

        readonly List<Tracer> active = new List<Tracer>();
        readonly Stack<Tracer> free = new Stack<Tracer>();
        readonly Plane[] planes = new Plane[6];
        int planesFrame = -1;
        int created;
        bool subscribed;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        // EnemyWorld chỉ nhận đăng ký khi thế giới ECS đã có, nên thử lại cho tới khi được.
        void Update()
        {
            if (!subscribed && World.DefaultGameObjectInjectionWorld != null)
            {
                EnemyWorld.ShotFired += OnShot;
                subscribed = true;
            }

            float deltaTime = Time.deltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var tracer = active[i];
                tracer.Remaining -= deltaTime;
                if (tracer.Remaining <= 0f)
                {
                    tracer.Line.enabled = false;
                    free.Push(tracer);
                    active.RemoveAt(i);
                    continue;
                }

                var c = color;
                c.a *= tracer.Remaining / duration;
                tracer.Line.startColor = c;
                tracer.Line.endColor = c;
            }
        }

        void OnDestroy()
        {
            if (subscribed) EnemyWorld.ShotFired -= OnShot;
        }

        // Vẽ một tia từ nguồn khác ngoài kẻ địch ECS, ví dụ mũi tên của lính trong trận đại chiến.
        public void Emit(Vector3 from, Vector3 to)
        {
            OnShot(from, to);
        }

        void OnShot(Vector3 from, Vector3 to)
        {
            if (cam == null || !IsVisible(from, to)) return;

            Tracer tracer;
            if (free.Count > 0) tracer = free.Pop();
            else if (created < maxTracers) tracer = Create();
            else return;

            tracer.Line.SetPosition(0, from);
            tracer.Line.SetPosition(1, to);
            tracer.Line.startColor = color;
            tracer.Line.endColor = color;
            tracer.Line.enabled = true;
            tracer.Remaining = duration;
            active.Add(tracer);
        }

        bool IsVisible(Vector3 from, Vector3 to)
        {
            if (planesFrame != Time.frameCount)
            {
                GeometryUtility.CalculateFrustumPlanes(cam, planes);
                planesFrame = Time.frameCount;
            }

            var bounds = new Bounds(from, Vector3.zero);
            bounds.Encapsulate(to);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }

        Tracer Create()
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            created++;
            return new Tracer { Line = line };
        }
    }
}
