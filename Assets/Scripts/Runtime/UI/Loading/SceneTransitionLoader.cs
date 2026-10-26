using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Màn hình loading chuyển cảnh dùng chung cho mọi lần đổi scene sau màn init (theo thiết kế 11a–11f trên Figma):
    // khung ảnh nền phía trên, thanh thông tin trắng ở đáy có vòng xoay + phần trăm, thanh tiến trình 8px sát mép dưới.
    public class SceneTransitionLoader : MonoBehaviour
    {
        const int SortingOrder = 500;
        const float MinimumSeconds = 0.45f;
        const float ReadyTimeoutSeconds = 14f;
        const float SpinSpeed = 270f;

        static SceneTransitionLoader instance;

        GameObject canvasObject;
        RectTransform spinnerRect;
        RectTransform fillRect;
        TextMeshProUGUI titleLabel;
        TextMeshProUGUI subtitleLabel;
        TextMeshProUGUI statusLabel;
        TextMeshProUGUI percentLabel;
        Coroutine activeRoutine;
        float displayedProgress;

        public static bool IsBusy => instance != null && instance.canvasObject != null && instance.canvasObject.activeSelf;

        static SceneTransitionLoader EnsureInstance()
        {
            if (instance != null) return instance;
            var go = new GameObject("SceneTransitionLoader");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SceneTransitionLoader>();
            instance.BuildUi();
            return instance;
        }

        // Chuyển sang scene khác kèm màn hình loading chuyển cảnh; waitUntilReady dùng khi cần chờ mạng kết nối xong mới ẩn màn loading.
        public static void LoadScene(string sceneName, Action onSceneLoaded = null, Func<bool> waitUntilReady = null)
        {
            var loader = EnsureInstance();
            if (loader.activeRoutine != null) loader.StopCoroutine(loader.activeRoutine);
            loader.activeRoutine = loader.StartCoroutine(loader.RunSceneLoad(sceneName, onSceneLoaded, waitUntilReady));
        }

        // Hiện nhanh màn loading chuyển cảnh trong cùng một scene (ví dụ từ phòng chờ vào trận đại chiến 11c).
        public static void ShowOverlay(float duration = 0.5f)
        {
            var loader = EnsureInstance();
            if (loader.activeRoutine != null) return;
            loader.activeRoutine = loader.StartCoroutine(loader.RunOverlay(duration));
        }

        void Update()
        {
            if (canvasObject == null || !canvasObject.activeSelf) return;
            if (spinnerRect != null) spinnerRect.Rotate(0f, 0f, -SpinSpeed * Time.unscaledDeltaTime);
        }

        void BuildUi()
        {
            if (BattleUiKit.Font == null) BattleUiKit.Font = TMP_Settings.defaultFontAsset;

            canvasObject = new GameObject("TransitionCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var root = BattleUiKit.Background(canvasObject.transform, "TransitionLoading", BattleUiKit.WorldColor).rectTransform;

            var backdropArea = BattleUiKit.Node(root, "BackdropArea");
            backdropArea.offsetMin = new Vector2(0f, 200f);
            backdropArea.offsetMax = Vector2.zero;
            BattleUiKit.Label(backdropArea, "ẢNH NỀN LOADING (dùng chung)", 28f, TextAlignmentOptions.Center, BattleUiKit.DimTextColor);

            var bottomBar = BattleUiKit.Panel(root, "BottomBar", BattleUiKit.PanelColor).rectTransform;
            bottomBar.anchorMin = new Vector2(0f, 0f);
            bottomBar.anchorMax = new Vector2(1f, 0f);
            bottomBar.pivot = new Vector2(0.5f, 0f);
            bottomBar.anchoredPosition = new Vector2(0f, 8f);
            bottomBar.sizeDelta = new Vector2(0f, 192f);

            var barLayout = bottomBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            barLayout.padding = new RectOffset(80, 80, 0, 0);
            barLayout.spacing = 40f;
            barLayout.childAlignment = TextAnchor.MiddleLeft;
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = false;
            barLayout.childForceExpandHeight = false;

            var info = BattleUiKit.Node(bottomBar, "Info");
            BattleUiKit.Size(info, 900f, 68f);
            BattleUiKit.Vertical(info, 8f, 0, TextAnchor.UpperLeft);
            titleLabel = BattleUiKit.Label(info, "Đang chuyển cảnh…", 32f, TextAlignmentOptions.Left, BattleUiKit.TextColor);
            BattleUiKit.Size(titleLabel, -1f, 37f);
            subtitleLabel = BattleUiKit.Label(info, "Vui lòng chờ trong giây lát", 20f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor);
            BattleUiKit.Size(subtitleLabel, -1f, 23f);

            var spacer = BattleUiKit.Node(bottomBar, "Spacer");
            BattleUiKit.Size(spacer, -1f, 20f, 1f);

            spinnerRect = BattleUiKit.Node(bottomBar, "Spinner");
            BattleUiKit.Size(spinnerRect, 56f, 56f);
            var spinnerImage = spinnerRect.gameObject.AddComponent<Image>();
            spinnerImage.sprite = BattleUiKit.SpinnerSprite(BattleUiKit.PrimaryColor, 56, 4);
            spinnerImage.raycastTarget = false;

            var status = BattleUiKit.Node(bottomBar, "Status");
            BattleUiKit.Size(status, 200f, 48f);
            BattleUiKit.Vertical(status, 4f, 0, TextAnchor.UpperLeft);
            statusLabel = BattleUiKit.Label(status, "Đang tải cảnh…", 20f, TextAlignmentOptions.Left, BattleUiKit.TextColor);
            BattleUiKit.Size(statusLabel, -1f, 23f);
            percentLabel = BattleUiKit.Label(status, "0%", 18f, TextAlignmentOptions.Left, BattleUiKit.DimTextColor);
            BattleUiKit.Size(percentLabel, -1f, 21f);

            var progressTrack = BattleUiKit.Background(root, "Progress", BattleUiKit.RowColor).rectTransform;
            progressTrack.anchorMin = new Vector2(0f, 0f);
            progressTrack.anchorMax = new Vector2(1f, 0f);
            progressTrack.pivot = new Vector2(0.5f, 0f);
            progressTrack.anchoredPosition = Vector2.zero;
            progressTrack.sizeDelta = new Vector2(0f, 8f);

            fillRect = BattleUiKit.Background(progressTrack, "Fill", new Color(0.408f, 0.408f, 0.408f, 1f)).rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            canvasObject.SetActive(false);
        }

        void SetProgress(float value)
        {
            displayedProgress = Mathf.Clamp01(value);
            if (fillRect != null) fillRect.anchorMax = new Vector2(displayedProgress, 1f);
            if (percentLabel != null) BattleUiKit.SetText(percentLabel, Mathf.RoundToInt(displayedProgress * 100f) + "%");
        }

        IEnumerator RunSceneLoad(string sceneName, Action onSceneLoaded, Func<bool> waitUntilReady)
        {
            if (BattleUiKit.Font != null)
            {
                if (titleLabel != null) titleLabel.font = BattleUiKit.Font;
                if (subtitleLabel != null) subtitleLabel.font = BattleUiKit.Font;
                if (statusLabel != null) statusLabel.font = BattleUiKit.Font;
                if (percentLabel != null) percentLabel.font = BattleUiKit.Font;
            }

            BattleUiKit.SetText(titleLabel, "Đang chuyển cảnh…");
            BattleUiKit.SetText(subtitleLabel, "Vui lòng chờ trong giây lát");
            BattleUiKit.SetText(statusLabel, "Đang tải cảnh…");
            SetProgress(0f);
            canvasObject.SetActive(true);

            float start = Time.unscaledTime;
            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                canvasObject.SetActive(false);
                activeRoutine = null;
                yield break;
            }

            op.allowSceneActivation = false;
            while (op.progress < 0.9f)
            {
                float target = (op.progress / 0.9f) * 0.8f;
                SetProgress(Mathf.MoveTowards(displayedProgress, target, Time.unscaledDeltaTime * 3f));
                yield return null;
            }

            while (Time.unscaledTime - start < MinimumSeconds * 0.7f || displayedProgress < 0.75f)
            {
                SetProgress(Mathf.MoveTowards(displayedProgress, 0.85f, Time.unscaledDeltaTime * 3.5f));
                yield return null;
            }

            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                SetProgress(Mathf.MoveTowards(displayedProgress, 0.9f, Time.unscaledDeltaTime * 3.5f));
                yield return null;
            }

            yield return null;
            onSceneLoaded?.Invoke();

            if (waitUntilReady != null)
            {
                float waitStart = Time.unscaledTime;
                while (!waitUntilReady() && Time.unscaledTime - waitStart < ReadyTimeoutSeconds)
                {
                    SetProgress(Mathf.MoveTowards(displayedProgress, 0.96f, Time.unscaledDeltaTime * 0.5f));
                    yield return null;
                }
            }

            while (Time.unscaledTime - start < MinimumSeconds || displayedProgress < 0.995f)
            {
                SetProgress(Mathf.MoveTowards(displayedProgress, 1f, Time.unscaledDeltaTime * 5f));
                yield return null;
            }

            SetProgress(1f);
            yield return null;
            canvasObject.SetActive(false);
            activeRoutine = null;
        }

        IEnumerator RunOverlay(float duration)
        {
            BattleUiKit.SetText(titleLabel, "Đang chuyển cảnh…");
            BattleUiKit.SetText(subtitleLabel, "Vui lòng chờ trong giây lát");
            BattleUiKit.SetText(statusLabel, "Đang tải cảnh…");
            SetProgress(0f);
            canvasObject.SetActive(true);

            float elapsed = 0f;
            float total = Mathf.Max(0.2f, duration);
            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;
                SetProgress(Mathf.Clamp01(elapsed / total));
                yield return null;
            }

            SetProgress(1f);
            yield return null;
            canvasObject.SetActive(false);
            activeRoutine = null;
        }
    }
}
