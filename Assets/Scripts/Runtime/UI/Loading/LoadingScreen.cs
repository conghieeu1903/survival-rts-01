using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Màn hình loading của scene init: nạp trước các tài nguyên nặng theo từng bước, chạy thanh tiến trình, rồi vào màn kế tiếp.
    public class LoadingScreen : MonoBehaviour
    {
        struct Step
        {
            public string Label;
            public float Weight;
            public Func<IEnumerator> Run;
        }

        static readonly string[] Tips =
        {
            "Mẹo: Lính cung đứng sau lính kiếm sẽ sống lâu hơn.",
            "Mẹo: Kéo chuột trái để chọn nhiều tiểu đội, chuột phải để ra lệnh di chuyển.",
            "Mẹo: Ctrl + số để gán nhóm điều khiển, bấm số để gọi nhóm.",
            "Mẹo: Huấn luyện thêm tiểu đội ngay khi đủ vàng để giữ thế trận.",
            "Mẹo: Đội không có người chơi sẽ do máy điều khiển."
        };

        const float TipSeconds = 3f;

        [Header("Màn kế tiếp")]
        [Tooltip("Tên scene mở sau khi nạp xong, phải có trong Build Settings")]
        [SerializeField] string nextScene = GameFlow.MenuScene;

        [Header("Nạp trước")]
        [Tooltip("Thư viện âm thanh: nạp sẵn dữ liệu các clip để lúc chơi không khựng")]
        [SerializeField] SoundLibrary soundLibrary;

        [Tooltip("Dữ liệu trận: giữ các mesh, vật liệu và animation lính trong bộ nhớ trước khi vào trận")]
        [SerializeField] BattleData battleData;

        [Header("Hiển thị")]
        [Tooltip("Font TMP có đủ tiếng Việt")]
        [SerializeField] TMP_FontAsset fontAsset;

        [Tooltip("Thời gian hiện loading tối thiểu (giây) để người chơi kịp thấy thanh chạy")]
        [SerializeField] float minimumSeconds = 1.5f;

        [Tooltip("Tốc độ thanh tiến trình đuổi theo tiến độ thật (càng lớn càng nhanh)")]
        [SerializeField] float barSmoothing = 6f;

        [Tooltip("Tên game hiện trong ô logo")]
        [SerializeField] string title = "SURVIVAL RTS";

        RectTransform fill;
        TextMeshProUGUI statusLabel;
        TextMeshProUGUI percentLabel;
        TextMeshProUGUI tipLabel;

        float totalWeight;
        float doneWeight;
        float currentWeight;
        float stepProgress;
        float displayed;
        float nextTipTime;
        int tipIndex;
        bool finished;

        AsyncOperation pendingScene;
        bool sceneBlocked;

        void Awake()
        {
            // Lockstep cần cả trận chạy tiếp khi cửa sổ mất focus
            Application.runInBackground = true;
            if (soundLibrary != null) AudioDirector.DefaultLibrary = soundLibrary;
            BattleUiKit.Font = fontAsset;
            tipIndex = UnityEngine.Random.Range(0, Tips.Length);
            BuildUi();
        }

        void Start()
        {
            StartCoroutine(Load());
        }

        void Update()
        {
            float target = finished ? 1f : Mathf.Clamp01((doneWeight + currentWeight * stepProgress) / Mathf.Max(0.001f, totalWeight));
            displayed = Mathf.MoveTowards(displayed, target, Time.unscaledDeltaTime * Mathf.Max(0.5f, barSmoothing * Mathf.Max(0.05f, target - displayed)));
            fill.gameObject.SetActive(displayed > 0.01f);
            fill.anchorMax = new Vector2(displayed, 1f);
            BattleUiKit.SetText(percentLabel, Mathf.RoundToInt(displayed * 100f) + "%");

            if (Time.unscaledTime >= nextTipTime)
            {
                nextTipTime = Time.unscaledTime + TipSeconds;
                BattleUiKit.SetText(tipLabel, Tips[tipIndex]);
                tipIndex = (tipIndex + 1) % Tips.Length;
            }
        }

        // Bố cục theo màn loading wireframe: ô logo và mẹo ở giữa, thanh tiến trình dọc đáy.
        void BuildUi()
        {
            var canvasGo = new GameObject("LoadingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var background = BattleUiKit.Background(canvasGo.transform, "Background", BattleUiKit.ScreenColor);
            background.raycastTarget = false;

            var center = BattleUiKit.Node(background.transform, "CenterGroup");
            BattleUiKit.Anchor(center, new Vector2(0.5f, 0.5f), new Vector2(900f, 360f), new Vector2(0f, 50f));
            var layout = BattleUiKit.Vertical(center, 48f, 0, TextAnchor.UpperCenter);
            layout.childForceExpandWidth = false;

            var logo = BattleUiKit.Placeholder(center, title, 200f, 44f);
            BattleUiKit.Size(logo, 480f, 200f);

            tipLabel = BattleUiKit.Label(center, "", 20f, TextAlignmentOptions.Center);
            tipLabel.name = "Tip";
            BattleUiKit.Size(tipLabel, 900f, 60f);

            var footer = BattleUiKit.Node(background.transform, "Footer");
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(-240f, 70f);
            footer.anchoredPosition = new Vector2(0f, 80f);
            BattleUiKit.Vertical(footer, 12f, 0);

            BattleUiKit.ProgressBar(footer, 24f, out fill);

            var row = BattleUiKit.Node(footer, "StatusRow");
            BattleUiKit.Size(row, -1f, 30f);
            BattleUiKit.Horizontal(row, 12f, 0);
            statusLabel = BattleUiKit.Label(row, "", 18f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor);
            statusLabel.name = "Status";
            BattleUiKit.Size(statusLabel, -1f, -1f, 1f);
            percentLabel = BattleUiKit.Label(row, "0%", 18f, TextAlignmentOptions.Right, BattleUiKit.DimTextColor);
            percentLabel.name = "Percent";
            BattleUiKit.Size(percentLabel, 120f, -1f);
        }

        IEnumerator Load()
        {
            var steps = new List<Step>
            {
                new Step { Label = "Khởi tạo hệ thống...", Weight = 1f, Run = WaitForWorld },
                new Step { Label = "Nạp thành phần mạng...", Weight = 1f, Run = LoadNetwork },
                new Step { Label = "Nạp âm thanh...", Weight = 3f, Run = LoadAudio },
                new Step { Label = "Nạp dữ liệu trận...", Weight = 1f, Run = LoadBattleData },
                new Step { Label = "Nạp màn chơi...", Weight = 4f, Run = LoadScene }
            };
            foreach (var step in steps) totalWeight += step.Weight;

            float start = Time.unscaledTime;
            foreach (var step in steps)
            {
                BattleUiKit.SetText(statusLabel, step.Label);
                currentWeight = step.Weight;
                stepProgress = 0f;
                yield return StartCoroutine(step.Run());
                doneWeight += step.Weight;
                stepProgress = 0f;
                currentWeight = 0f;
                if (sceneBlocked) yield break;
            }

            BattleUiKit.SetText(statusLabel, "Sẵn sàng");
            finished = true;
            while (Time.unscaledTime - start < minimumSeconds || displayed < 0.999f) yield return null;
            pendingScene.allowSceneActivation = true;
        }

        IEnumerator WaitForWorld()
        {
            while (World.DefaultGameObjectInjectionWorld == null || !World.DefaultGameObjectInjectionWorld.IsCreated) yield return null;
            yield return null;
            stepProgress = 1f;
        }

        IEnumerator LoadNetwork()
        {
            var request = Resources.LoadAsync<GameObject>("NetworkManager");
            while (!request.isDone)
            {
                stepProgress = request.progress;
                yield return null;
            }
            if (request.asset == null) Debug.LogWarning("Thiếu Resources/NetworkManager.prefab, không vào mạng được");
            stepProgress = 1f;
        }

        IEnumerator LoadAudio()
        {
            var clips = new List<AudioClip>();
            if (soundLibrary != null)
            {
                foreach (var rule in soundLibrary.rules)
                {
                    if (rule.clips == null) continue;
                    foreach (var clip in rule.clips)
                        if (clip != null && !clips.Contains(clip)) clips.Add(clip);
                }
            }

            foreach (var clip in clips) clip.LoadAudioData();
            while (clips.Count > 0)
            {
                int loaded = 0;
                foreach (var clip in clips)
                    if (clip.loadState != AudioDataLoadState.Loading) loaded++;
                stepProgress = loaded / (float)clips.Count;
                if (loaded == clips.Count) break;
                yield return null;
            }
            stepProgress = 1f;
        }

        IEnumerator LoadBattleData()
        {
            if (battleData == null) Debug.LogWarning("LoadingScreen chưa gán BattleData");
            stepProgress = 1f;
            yield return null;
        }

        IEnumerator LoadScene()
        {
            if (!Application.CanStreamedLevelBeLoaded(nextScene))
            {
                sceneBlocked = true;
                BattleUiKit.SetText(statusLabel, $"Không tìm thấy màn \"{nextScene}\" trong Build Settings");
                Debug.LogError($"Scene {nextScene} chưa có trong Build Settings");
                yield break;
            }

            pendingScene = SceneManager.LoadSceneAsync(nextScene);
            pendingScene.allowSceneActivation = false;
            while (pendingScene.progress < 0.9f)
            {
                stepProgress = pendingScene.progress / 0.9f;
                yield return null;
            }
            stepProgress = 1f;
        }
    }
}
