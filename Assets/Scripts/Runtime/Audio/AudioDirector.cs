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
            public int Count;
        }

        [Header("Tham chiếu")]
        [Tooltip("Quy tắc phát cho từng loại âm thanh")]
        [SerializeField] SoundLibrary library;

        [Tooltip("Camera RTS, dùng để lấy khung nhìn và điểm nhìn xuống mặt đất (đặt listener)")]
        [SerializeField] CameraController cameraController;

        [Header("Lọc theo camera")]
        [Tooltip("Lề (m) cộng thêm quanh khung nhìn; sự kiện ngoài vùng này bị bỏ")]
        [SerializeField] float viewMargin = 6f;

        [Header("Loop đám đông")]
        [Tooltip("Số kẻ địch đang đi trong khung nhìn để loop bước chân đạt âm lượng tối đa")]
        [SerializeField] int marchFullCount = 150;

        [Tooltip("Số kẻ địch đang đánh trong khung nhìn để loop giao tranh đạt âm lượng tối đa")]
        [SerializeField] int battleFullCount = 60;

        [Tooltip("Tốc độ âm lượng loop đuổi theo giá trị mới (càng lớn càng nhanh)")]
        [SerializeField] float loopSmoothing = 3f;

        [Header("Nhạc nền")]
        [Tooltip("Số kẻ địch đang đánh trong khung nhìn để nhạc chuyển hẳn sang nhạc trận")]
        [SerializeField] int battleMusicEngagedCount = 15;

        [Tooltip("Số kẻ địch đang tiến vào trong khung nhìn để nhạc chuyển hẳn sang nhạc trận")]
        [SerializeField] int battleMusicMovingCount = 200;

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

        public static AudioDirector Instance { get; private set; }

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
        public static void Post(SoundId id, Vector3 position)
        {
            EventSystem?.Post(id, position);
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
            if (cameraController == null) cameraController = FindFirstObjectByType<CameraController>();
            listener = new GameObject("AudioListenerPivot").transform;
            listener.SetParent(transform, false);

            backend = new FmodAudioBackend();
            backend.Initialize(library);
        }

        // Tạm dừng tiếng hiệu ứng (ví dụ khi mở menu cài đặt); nhạc và giao diện vẫn chạy.
        public void SetGameplayPaused(bool paused)
        {
            backend.SetBusPaused(AudioVolumeSettings.BusPath(VolumeChannel.Sfx), paused);
        }

        void ApplyVolumes()
        {
            foreach (VolumeChannel channel in System.Enum.GetValues(typeof(VolumeChannel)))
                backend.SetBusVolume(AudioVolumeSettings.BusPath(channel), AudioVolumeSettings.Get(channel));
        }

        void Start()
        {
            AudioVolumeSettings.Changed += ApplyVolumes;
            ApplyVolumes();
            EnemyWorld.ShotFired += OnShotFired;

            var headquarters = FindFirstObjectByType<Headquarters>();
            if (headquarters != null)
            {
                headquartersHealth = headquarters.GetComponent<Health>();
                lastHeadquartersHealth = headquartersHealth.Current;
                headquartersHealth.Changed += OnHeadquartersHealthChanged;
            }
        }

        void OnDestroy()
        {
            AudioVolumeSettings.Changed -= ApplyVolumes;
            EnemyWorld.ShotFired -= OnShotFired;
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
            if (cameraController == null) return;

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

                var density = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<AudioDensitySystem>();
                if (density != null) density.ViewRect = new float4(view.x, view.y, view.z, view.w);
            }

            // Listener đặt ở điểm camera nhìn xuống mặt đất, xoay theo hướng camera để trái/phải khớp màn hình.
            listener.position = cameraController.GroundPivot();
            var forward = Vector3.ProjectOnPlane(cameraController.transform.forward, Vector3.up);
            if (forward.sqrMagnitude > 1e-4f) listener.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            backend.UpdateListener(listener);
        }

        void BuildBuckets()
        {
            buckets.Clear();
            int culled = 0;
            foreach (var e in events)
            {
                var rule = library.Get(e.Id);
                if (rule == null || rule.kind == SoundKind.Loop) continue;

                var p = (Vector3)e.Position;
                if (rule.cullOutsideView && (p.x < view.x || p.z < view.y || p.x > view.z || p.z > view.w))
                {
                    culled++;
                    continue;
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

                tokens[rule.id] -= 1f;
                lastPlayed[rule.id] = now;
                float volume = Mathf.Min(rule.maxVolume, rule.volume * (1f + rule.mergeVolumeBoost * Mathf.Log(bucket.Count, 2f)));
                backend.PlayOneShot(rule, bucket.Sum / bucket.Count, volume);
                started++;
            }

            LastMerged = merged;
            LastThrottled = throttled;
            LastStarted = started;
        }

        void UpdateLoops()
        {
            var density = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<AudioDensitySystem>();
            var counts = density != null ? density.GetCounts() : int2.zero;
            LastDensity = counts;

            float k = 1f - Mathf.Exp(-loopSmoothing * Time.unscaledDeltaTime);
            marchVolume = Mathf.Lerp(marchVolume, Mathf.Clamp01(counts.x / (float)marchFullCount), k);
            battleVolume = Mathf.Lerp(battleVolume, Mathf.Clamp01(counts.y / (float)battleFullCount), k);

            var march = library.Get(SoundId.CrowdMarch);
            var battle = library.Get(SoundId.Battle);
            if (march != null) backend.SetLoopVolume(march, marchVolume * march.volume);
            if (battle != null) backend.SetLoopVolume(battle, battleVolume * battle.volume);

            // Chuyển trạng thái nhạc rõ ràng (yên <-> trận) kèm thời gian giữ để tránh đổi qua lại liên tục.
            bool combatActive = counts.y >= battleMusicEngagedCount || counts.x >= battleMusicMovingCount;
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
