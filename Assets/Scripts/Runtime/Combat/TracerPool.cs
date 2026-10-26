using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace SurvivalRts
{
    // Bắn mũi tên bay theo quỹ đạo vòng cung parabol từ điểm bắn tới mục tiêu bằng LineRenderer lấy từ pool.
    public class TracerPool : MonoBehaviour
    {
        const int SegmentPoints = 4;

        [Header("Tham chiếu")]
        [Tooltip("Camera dùng để loại mũi tên ngoài màn hình, mặc định là Main Camera")]
        [SerializeField] Camera cam;

        [Header("Hiển thị")]
        [Tooltip("Material của mũi tên, nên là loại Unlit trong suốt")]
        [SerializeField] Material material;

        [Tooltip("Màu mũi tên khi đang bay")]
        [SerializeField] Color color = new Color(1f, 0.85f, 0.4f, 1f);

        [Tooltip("Độ dày thân mũi tên (m)")]
        [SerializeField] float width = 0.08f;

        [Tooltip("Thời gian bay cơ sở tối thiểu (giây)")]
        [SerializeField] float duration = 0.35f;

        [Tooltip("Tốc độ bay ngang của mũi tên (m/giây)")]
        [SerializeField] float projectileSpeed = 24f;

        [Tooltip("Tỉ lệ độ cao đỉnh vòng cung so với khoảng cách bắn")]
        [SerializeField] float arcHeightFactor = 0.25f;

        [Tooltip("Chiều dài thân mũi tên kèm vệt đuôi trên quỹ đạo (m)")]
        [SerializeField] float arrowLength = 1.35f;

        [Header("Giới hạn")]
        [Tooltip("Số mũi tên tối đa bay cùng lúc. Vượt quá thì phát bắn mới không được vẽ")]
        [SerializeField] int maxTracers = 256;

        class Tracer
        {
            public LineRenderer Line;
            public Vector3 From;
            public Vector3 To;
            public float ArcHeight;
            public float NormalizedLength;
            public float Elapsed;
            public float TotalTime;
        }

        readonly List<Tracer> active = new List<Tracer>();
        readonly Stack<Tracer> free = new Stack<Tracer>();
        readonly Plane[] planes = new Plane[6];
        int planesFrame = -1;
        int created;
        bool subscribed;

        public static TracerPool Instance { get; private set; }

        void Awake()
        {
            Instance = this;
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
                tracer.Elapsed += deltaTime;
                if (tracer.Elapsed >= tracer.TotalTime)
                {
                    tracer.Line.enabled = false;
                    free.Push(tracer);
                    active.RemoveAt(i);
                    continue;
                }

                float u = tracer.Elapsed / tracer.TotalTime;
                UpdateArcPositions(tracer, u);
            }
        }

        void OnDestroy()
        {
            if (subscribed) EnemyWorld.ShotFired -= OnShot;
            if (Instance == this) Instance = null;
        }

        // Bắn một mũi tên từ nguồn khác ngoài kẻ địch ECS, ví dụ lính cung trong trận đại chiến.
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

            float dist = Vector3.Distance(from, to);
            tracer.From = from;
            tracer.To = to;
            tracer.ArcHeight = Mathf.Clamp(dist * arcHeightFactor, 0.65f, 4.5f);
            tracer.TotalTime = Mathf.Clamp(dist / Mathf.Max(4f, projectileSpeed), Mathf.Max(0.20f, duration * 0.7f), 0.75f);
            tracer.NormalizedLength = Mathf.Clamp(arrowLength / Mathf.Max(2f, dist), 0.14f, 0.36f);
            tracer.Elapsed = 0f;

            UpdateArcPositions(tracer, 0f);
            tracer.Line.enabled = true;
            active.Add(tracer);
        }

        void UpdateArcPositions(Tracer tracer, float u)
        {
            float headU = Mathf.Clamp01(Mathf.Max(u, tracer.NormalizedLength * 0.45f));
            float tailU = Mathf.Clamp01(headU - tracer.NormalizedLength);

            tracer.Line.SetPosition(0, EvaluateParabola(tracer.From, tracer.To, tracer.ArcHeight, tailU));
            tracer.Line.SetPosition(1, EvaluateParabola(tracer.From, tracer.To, tracer.ArcHeight, Mathf.Lerp(tailU, headU, 0.35f)));
            tracer.Line.SetPosition(2, EvaluateParabola(tracer.From, tracer.To, tracer.ArcHeight, Mathf.Lerp(tailU, headU, 0.70f)));
            tracer.Line.SetPosition(3, EvaluateParabola(tracer.From, tracer.To, tracer.ArcHeight, headU));

            float fade = u > 0.85f ? Mathf.Clamp01((1f - u) / 0.15f) : 1f;
            var tailColor = color;
            tailColor.a *= fade * 0.35f;
            var headColor = color;
            headColor.a *= fade;
            tracer.Line.startColor = tailColor;
            tracer.Line.endColor = headColor;
        }

        static Vector3 EvaluateParabola(Vector3 from, Vector3 to, float arcHeight, float u)
        {
            var pos = Vector3.Lerp(from, to, u);
            pos.y += 4f * arcHeight * u * (1f - u);
            return pos;
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
            bounds.Encapsulate((from + to) * 0.5f + Vector3.up * 3.5f);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }

        Tracer Create()
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = SegmentPoints;
            line.useWorldSpace = true;
            line.startWidth = width * 0.35f;
            line.endWidth = width * 1.15f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            created++;
            return new Tracer { Line = line };
        }
    }
}
