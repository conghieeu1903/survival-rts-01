using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SurvivalRts
{
    // Nhận mọi sự kiện âm thanh của frame, rồi lọc trước khi phát:
    // bỏ tiếng ngoài khung nhìn, gộp tiếng cùng loại trong một ô, giới hạn số tiếng mới mỗi giây,
    // và điều khiển các loop đám đông theo số kẻ địch đang đi/đánh trong khung nhìn.
    public class AudioDirector : MonoBehaviour
    {
        struct Bucket
        {
            public SoundId Id;
            public Vector3 Sum;
            public float ParamSum;
            public int Count;
        }

        [Header("Tham chiếu")]
        [Tooltip("Quy tắc phát cho từng loại âm thanh")]
        [SerializeField] SoundLibrary library;

        [Tooltip("Camera RTS, dùng để lấy khung nhìn và điểm nhìn xuống mặt đất (đặt listener)")]
        [SerializeField] CameraController cameraController;

        [Header("Lọc theo camera & độ zoom")]
        [Tooltip("Lề (m) cộng thêm quanh khung nhìn; sự kiện ngoài vùng này bị bỏ")]
        [SerializeField] float viewMargin = 2f;

        [Tooltip("Mức orthographicSize tối đa còn nghe thấy tiếng tại chỗ (zoom xa hơn mức này sẽ tắt tiếng 3D, zoom gần vào sẽ nghe rõ dần)")]
        [SerializeField] float maxAudibleOrthoSize = 18f;

        [Tooltip("Bán kính (m) quanh tâm điểm nhìn trên mặt đất được phép phát tiếng khi zoom vào")]
        [SerializeField] float audibleGroundRadius = 16f;

        [Header("Loop đám đông")]
        [Tooltip("Số kẻ địch đang đi trong vùng nghe để loop bước chân đạt âm lượng tối đa")]
        [SerializeField] int marchFullCount = 40;

        [Tooltip("Số kẻ địch đang đánh trong vùng nghe để loop giao tranh đạt âm lượng tối đa")]
        [SerializeField] int battleFullCount = 15;

        [Tooltip("Tốc độ âm lượng loop đuổi theo giá trị mới (càng lớn càng nhanh)")]
        [SerializeField] float loopSmoothing = 4f;

        [Header("Nhạc nền")]
        [Tooltip("Số kẻ địch đang đánh trong vùng nghe để nhạc chuyển hẳn sang nhạc trận")]
        [SerializeField] int battleMusicEngagedCount = 8;

        [Tooltip("Số kẻ địch đang tiến vào trong vùng nghe để nhạc chuyển hẳn sang nhạc trận")]
        [SerializeField] int battleMusicMovingCount = 60;

        [Tooltip("Tốc độ chuyển giữa nhạc yên và nhạc trận (nhỏ = chuyển chậm, êm)")]
        [SerializeField] float musicSmoothing = 0.5f;

        readonly List<AudioEvent> events = new List<AudioEvent>(1024);
        readonly Dictionary<long, Bucket> buckets = new Dictionary<long, Bucket>(256);
        readonly List<Bucket> ordered = new List<Bucket>(256);
        readonly Dictionary<SoundId, float> tokens = new Dictionary<SoundId, float>();
        readonly Dictionary<SoundId, float> lastPlayed = new Dictionary<SoundId, float>();
        readonly Vector3[] corners = new Vector3[4];

        IAudioBackend backend;
        Transform listener;
        Health headquartersHealth;
        float lastHeadquartersHealth;
        float marchVolume;
        float battleVolume;
        float musicTension;
        bool inBattleMusic;
        float battleMusicHoldTimer;
        Vector4 view;
        float4 densityRect;
        Vector3 groundPivot;
        float zoomVolumeFactor;
        bool shotSubscribed;
        static int lastUiClickFrame = -1;
        static int lastUiHoverFrame = -1;

        public static AudioDirector Instance { get; private set; }
        public static SoundLibrary DefaultLibrary;

        // Tự tạo AudioDirector nếu scene hiện tại chưa có sẵn.
        public static void EnsureInstance(SoundLibrary fallbackLibrary = null)
        {
            if (fallbackLibrary != null) DefaultLibrary = fallbackLibrary;
            if (Instance != null) return;
#if UNITY_EDITOR
            if (DefaultLibrary == null)
                DefaultLibrary = UnityEditor.AssetDatabase.LoadAssetAtPath<SoundLibrary>("Assets/ScriptableObjects/Audio/SoundLibrary.asset");
#endif
            if (DefaultLibrary == null) return;
            var go = new GameObject("AudioDirector");
            go.AddComponent<AudioDirector>();
        }

        // Nguồn mật độ thay cho AudioDensitySystem (kẻ địch ECS): nhận vùng nghe (minX, minZ, maxX, maxZ), trả về số đang đi (x) và đang đánh (y).
        public System.Func<float4, int2> DensityProvider { get; set; }

        // Vị trí có nằm trong vùng đang nghe được không; bên phát sự kiện dùng để khỏi gửi những tiếng chắc chắn bị bỏ.
        public bool IsAudible(Vector3 p)
        {
            if (zoomVolumeFactor <= 0.001f) return false;
            if (p.x < view.x || p.z < view.y || p.x > view.z || p.z > view.w) return false;
            float dx = p.x - groundPivot.x;
            float dz = p.z - groundPivot.z;
            return dx * dx + dz * dz <= audibleGroundRadius * audibleGroundRadius;
        }

        // Thống kê của frame gần nhất, để kiểm tra bộ lọc hoạt động đúng.
        public int LastReceived { get; private set; }
        public int LastCulled { get; private set; }
        public int LastMerged { get; private set; }
        public int LastThrottled { get; private set; }
        public int LastStarted { get; private set; }
        public int2 LastDensity { get; private set; }

        // Cộng dồn từ lúc bắt đầu chơi (gọi ResetTotals để đếm lại).
        public long TotalReceived { get; private set; }
        public long TotalCulled { get; private set; }
        public long TotalMerged { get; private set; }
        public long TotalThrottled { get; private set; }
        public long TotalStarted { get; private set; }

        public void ResetTotals()
        {
            TotalReceived = TotalCulled = TotalMerged = TotalThrottled = TotalStarted = 0;
        }

        // Gọi từ main thread (UI, công trình...). Job ECS thì ghi qua AudioEventSystem.Writer.
        public static void Post(SoundId id, Vector3 position, float parameter = 0f)
        {
            if (Instance == null) EnsureInstance();
            if (id == SoundId.UiClick)
            {
                int frame = Time.frameCount;
                if (parameter > 0f)
                {
                    if (lastUiHoverFrame == frame) return;
                    lastUiHoverFrame = frame;
                }
                else
                {
                    if (lastUiClickFrame == frame) return;
                    lastUiClickFrame = frame;
                }
            }
            EventSystem?.Post(id, position, parameter);
        }

        static AudioEventSystem EventSystem
        {
            get
            {
                var world = World.DefaultGameObjectInjectionWorld;
                return world != null && world.IsCreated ? world.GetExistingSystemManaged<AudioEventSystem>() : null;
            }
        }

        void Awake()
        {
            Instance = this;
            if (library == null) library = DefaultLibrary;
#if UNITY_EDITOR
            if (library == null)
                library = UnityEditor.AssetDatabase.LoadAssetAtPath<SoundLibrary>("Assets/ScriptableObjects/Audio/SoundLibrary.asset");
#endif
            if (library != null) DefaultLibrary = library;
            if (cameraController == null) cameraController = FindFirstObjectByType<CameraController>();
            listener = new GameObject("AudioListenerPivot").transform;
            listener.SetParent(transform, false);

            backend = new UnityAudioBackend();
            backend.Initialize(library);
        }

        // Tạm dừng tiếng hiệu ứng (ví dụ khi mở menu cài đặt); nhạc và giao diện vẫn chạy.
        public void SetGameplayPaused(bool paused)
        {
            backend.SetChannelPaused(VolumeChannel.Sfx, paused);
        }

        void ApplyVolumes()
        {
            foreach (VolumeChannel channel in System.Enum.GetValues(typeof(VolumeChannel)))
                backend.SetChannelVolume(channel, AudioVolumeSettings.Get(channel));
        }

        void Start()
        {
            AudioVolumeSettings.Changed += ApplyVolumes;
            ApplyVolumes();
            TrySubscribeShots();

            var headquarters = FindFirstObjectByType<Headquarters>();
            if (headquarters != null)
            {
                headquartersHealth = headquarters.GetComponent<Health>();
                lastHeadquartersHealth = headquartersHealth.Current;
                headquartersHealth.Changed += OnHeadquartersHealthChanged;
            }
        }

        void TrySubscribeShots()
        {
            if (shotSubscribed || World.DefaultGameObjectInjectionWorld == null) return;
            EnemyWorld.ShotFired += OnShotFired;
            shotSubscribed = true;
        }

        void OnDestroy()
        {
            AudioVolumeSettings.Changed -= ApplyVolumes;
            if (shotSubscribed) EnemyWorld.ShotFired -= OnShotFired;
            if (headquartersHealth != null) headquartersHealth.Changed -= OnHeadquartersHealthChanged;
            backend?.Shutdown();
            if (Instance == this) Instance = null;
        }

        void OnShotFired(Vector3 from, Vector3 to)
        {
            Post(SoundId.TowerShot, from);
        }

        void OnHeadquartersHealthChanged(float current, float max)
        {
            if (current < lastHeadquartersHealth) Post(SoundId.AlertHq, headquartersHealth.transform.position);
            lastHeadquartersHealth = current;
        }

        void LateUpdate()
        {
            TrySubscribeShots();
            UpdateViewAndListener();

            events.Clear();
            EventSystem?.Drain(events);
            LastReceived = events.Count;

            BuildBuckets();
            PlayBuckets();
            UpdateLoops();

            TotalReceived += LastReceived;
            TotalCulled += LastCulled;
            TotalMerged += LastMerged;
            TotalThrottled += LastThrottled;
            TotalStarted += LastStarted;
        }

        void UpdateViewAndListener()
        {
            if (cameraController == null)
            {
                var mainCam = Camera.main;
                if (mainCam != null) listener.position = mainCam.transform.position;
                backend.UpdateListener(listener);
                return;
            }

            groundPivot = cameraController.GroundPivot();
            float ortho = cameraController.OrthographicSize;
            float minOrtho = cameraController.ZoomRange.x;
            float zoomClarity = Mathf.Clamp01((maxAudibleOrthoSize - ortho) / Mathf.Max(0.1f, maxAudibleOrthoSize - minOrtho));
            zoomVolumeFactor = ortho >= maxAudibleOrthoSize ? 0f : Mathf.SmoothStep(0.15f, 1f, zoomClarity);

            // Điều khiển bộ lọc EQ trên bus Combat và Ambience trong FMOD: zoom xa nghe trầm đục, zoom sát mở sáng rõ.
            backend.SetGlobalParameter("CameraZoom", zoomClarity);

            if (cameraController.GetGroundFrustumCorners(corners))
            {
                float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
                foreach (var c in corners)
                {
                    minX = Mathf.Min(minX, c.x);
                    minZ = Mathf.Min(minZ, c.z);
                    maxX = Mathf.Max(maxX, c.x);
                    maxZ = Mathf.Max(maxZ, c.z);
                }
                view = new Vector4(minX - viewMargin, minZ - viewMargin, maxX + viewMargin, maxZ + viewMargin);

                // Chỉ đếm mật độ quái trong bán kính nghe quanh điểm camera đang zoom vào.
                float r = zoomVolumeFactor > 0f ? audibleGroundRadius : 0f;
                densityRect = new float4(
                    groundPivot.x - r,
                    groundPivot.z - r,
                    groundPivot.x + r,
                    groundPivot.z + r);
                var density = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<AudioDensitySystem>();
                if (density != null) density.ViewRect = densityRect;
            }

            // Listener đặt ở điểm camera nhìn xuống mặt đất, xoay theo hướng camera để trái/phải khớp màn hình.
            listener.position = groundPivot;
            var forward = Vector3.ProjectOnPlane(cameraController.transform.forward, Vector3.up);
            if (forward.sqrMagnitude > 1e-4f) listener.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            backend.UpdateListener(listener);
        }

        void BuildBuckets()
        {
            buckets.Clear();
            int culled = 0;
            float maxRadiusSq = audibleGroundRadius * audibleGroundRadius;
            foreach (var e in events)
            {
                var rule = library.Get(e.Id);
                if (rule == null || rule.kind == SoundKind.Loop) continue;

                var p = (Vector3)e.Position;
                if (rule.cullOutsideView)
                {
                    // Khi chưa zoom gần hoặc nguồn phát nằm ngoài bán kính quanh điểm nhìn camera thì bỏ qua.
                    if (zoomVolumeFactor <= 0.001f || p.x < view.x || p.z < view.y || p.x > view.z || p.z > view.w)
                    {
                        culled++;
                        continue;
                    }

                    float dx = p.x - groundPivot.x;
                    float dz = p.z - groundPivot.z;
                    if (dx * dx + dz * dz > maxRadiusSq)
                    {
                        culled++;
                        continue;
                    }
                }

                long key = (long)e.Id << 48;
                if (rule.mergeCellSize > 0f)
                {
                    long cx = Mathf.FloorToInt(p.x / rule.mergeCellSize) + 32768;
                    long cz = Mathf.FloorToInt(p.z / rule.mergeCellSize) + 32768;
                    key |= (cx & 0xFFFFFF) << 24 | (cz & 0xFFFFFF);
                }
                else
                {
                    key |= buckets.Count;
                }

                buckets.TryGetValue(key, out var bucket);
                bucket.Id = e.Id;
                bucket.Sum += p;
                bucket.ParamSum += e.Parameter;
                bucket.Count++;
                buckets[key] = bucket;
            }
            LastCulled = culled;
        }

        void PlayBuckets()
        {
            ordered.Clear();
            ordered.AddRange(buckets.Values);
            // Chỗ đông sự kiện được ưu tiên phát trước khi hết ngân sách.
            ordered.Sort((a, b) => b.Count.CompareTo(a.Count));

            int merged = 0, throttled = 0, started = 0;
            float now = Time.unscaledTime;
            float deltaTime = Time.unscaledDeltaTime;
            foreach (var rule in library.rules)
            {
                tokens.TryGetValue(rule.id, out float t);
                // Cho phép dồn tối đa 1/4 giây ngân sách, tránh nổ một tràng sau lúc yên lặng.
                tokens[rule.id] = Mathf.Min(t + rule.maxStartsPerSecond * deltaTime, Mathf.Max(1f, rule.maxStartsPerSecond * 0.25f));
            }

            float innerRadius = audibleGroundRadius * 0.45f;
            float falloffSpan = Mathf.Max(0.1f, audibleGroundRadius - innerRadius);

            foreach (var bucket in ordered)
            {
                var rule = library.Get(bucket.Id);
                merged += bucket.Count - 1;

                if (rule.cooldown > 0f && lastPlayed.TryGetValue(rule.id, out float last) && now - last < rule.cooldown)
                {
                    throttled++;
                    continue;
                }
                if (tokens[rule.id] < 1f)
                {
                    throttled++;
                    continue;
                }

                Vector3 pos = bucket.Sum / bucket.Count;
                float avgParam = bucket.ParamSum / bucket.Count;
                float volume = Mathf.Min(rule.maxVolume, rule.volume * (1f + rule.mergeVolumeBoost * Mathf.Log(bucket.Count, 2f)));
                if (rule.cullOutsideView)
                {
                    float dist = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(groundPivot.x, groundPivot.z));
                    float distFactor = dist <= innerRadius
                        ? 1f
                        : Mathf.SmoothStep(1f, 0f, (dist - innerRadius) / falloffSpan);
                    volume *= zoomVolumeFactor * distFactor;
                    if (volume <= 0.01f) continue;
                }

                tokens[rule.id] -= 1f;
                lastPlayed[rule.id] = now;
                backend.PlayOneShot(rule, pos, volume, avgParam);
                started++;
            }

            LastMerged = merged;
            LastThrottled = throttled;
            LastStarted = started;
        }

        void UpdateLoops()
        {
            var density = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<AudioDensitySystem>();
            var counts = DensityProvider != null ? DensityProvider(densityRect)
                : density != null ? density.GetCounts() : int2.zero;
            LastDensity = counts;

            float k = 1f - Mathf.Exp(-loopSmoothing * Time.unscaledDeltaTime);
            float marchRatio = Mathf.Clamp01(counts.x / (float)marchFullCount);
            float battleRatio = Mathf.Clamp01(counts.y / (float)battleFullCount);
            float targetMarch = marchRatio * zoomVolumeFactor;
            float targetBattle = battleRatio * zoomVolumeFactor;
            marchVolume = Mathf.Lerp(marchVolume, targetMarch, k);
            battleVolume = Mathf.Lerp(battleVolume, targetBattle, k);

            // Truyền cường độ giao tranh vào FMOD để tự động biến đổi pitch và EQ của Ambience/Battle và Music/Battle.
            float combatIntensity = Mathf.Clamp01(Mathf.Max(battleRatio, marchRatio * 0.6f));
            backend.SetGlobalParameter("CombatIntensity", combatIntensity);

            var march = library.Get(SoundId.CrowdMarch);
            var battle = library.Get(SoundId.Battle);
            if (march != null) backend.SetLoopVolume(march, marchVolume * march.volume);
            if (battle != null) backend.SetLoopVolume(battle, battleVolume * battle.volume);

            // Chuyển trạng thái nhạc rõ ràng (yên <-> trận) kèm thời gian giữ để tránh đổi qua lại liên tục.
            bool combatActive = zoomVolumeFactor > 0f && (counts.y >= battleMusicEngagedCount || counts.x >= battleMusicMovingCount);
            if (combatActive)
            {
                inBattleMusic = true;
                battleMusicHoldTimer = 4f;
            }
            else if (inBattleMusic)
            {
                if (counts.y <= 1 && counts.x < battleMusicMovingCount / 2)
                {
                    battleMusicHoldTimer -= Time.unscaledDeltaTime;
                    if (battleMusicHoldTimer <= 0f) inBattleMusic = false;
                }
                else
                {
                    battleMusicHoldTimer = 4f;
                }
            }

            // Nhạc cũ nhỏ dần về 0 hoàn toàn (0..0.5) trước khi bài mới tăng dần lên (0.5..1) để 2 bài không bao giờ đè lên nhau.
            float tensionTarget = inBattleMusic ? 1f : 0f;
            musicTension = Mathf.MoveTowards(musicTension, tensionTarget, Mathf.Max(0.4f, musicSmoothing) * Time.unscaledDeltaTime);
            var calmMusic = library.Get(SoundId.MusicCalm);
            var battleMusic = library.Get(SoundId.MusicBattle);
            float calmFactor = Mathf.Clamp01(1f - musicTension * 2f);
            float battleFactor = Mathf.Clamp01((musicTension - 0.5f) * 2f);
            if (calmMusic != null) backend.SetLoopVolume(calmMusic, calmFactor * calmMusic.volume);
            if (battleMusic != null) backend.SetLoopVolume(battleMusic, battleFactor * battleMusic.volume);
        }
    }
}
