# UI Toolkit recipes (from working code in this project)

Comments in examples follow the project rule: one short Vietnamese line above classes/methods, short
Vietnamese trailing comments beside fields.

## 1. Screen controller

Pattern used by `MainMenuController`, `LevelHUDController`, `LoadingScreen`.

```csharp
using UnityEngine;
using UnityEngine.UIElements;

// Điều khiển màn ví dụ: lấy phần tử theo tên và gắn sự kiện
[RequireComponent(typeof(UIDocument))]
public class ExampleScreenController : MonoBehaviour
{
    [SerializeField] SlotMachine slotMachine; // Đối tượng gameplay mà màn này điều khiển

    GameText titleLabel; // Nhãn tiêu đề
    SkewButton confirmButton; // Nút xác nhận

    // Lấy phần tử và đăng ký sự kiện khi bật
    void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        titleLabel = root.Q<GameText>("titleLabel");
        confirmButton = root.Q<SkewButton>("confirmButton");
        confirmButton.clicked += OnConfirm;
        slotMachine.SpinFinished += OnSpinFinished;
    }

    // Hủy đăng ký sự kiện khi tắt
    void OnDisable()
    {
        confirmButton.clicked -= OnConfirm;
        slotMachine.SpinFinished -= OnSpinFinished;
    }

    // Xử lý khi bấm xác nhận
    void OnConfirm() => slotMachine.SpinLocal();

    // Cập nhật giao diện khi có kết quả
    void OnSpinFinished(SpinOutcome outcome) => titleLabel.text = outcome.ToString();
}
```

Rules: query in `OnEnable` (UIDocument rebuilds its tree when re-enabled), unsubscribe everything in
`OnDisable`, kill tweens there too. Methods subscribed with `+=` must be named methods (not lambdas) so
they can be removed.

## 2. Clickable non-button element and data-driven items

Current LevelSelect wiring (`MainMenuController.OnEnable`) loops `levelCard1..N` by name:

```csharp
card.EnableInClassList(LockedClass, !unlocked);
if (unlocked) card.AddManipulator(new Clickable(() => LevelLoader.Load(data)));
```

When item count depends on data (LevelData list, shop packs), prefer one item template instead of
hand-numbered elements:

```csharp
[SerializeField] VisualTreeAsset cardTemplate; // Template UXML của một thẻ

// Sinh thẻ theo danh sách dữ liệu và gắn click ngay lúc tạo
void BuildCards(VisualElement container, LevelData[] levels)
{
    container.Clear();
    foreach (LevelData data in levels)
    {
        TemplateContainer item = cardTemplate.Instantiate();
        VisualElement card = item.Q<VisualElement>(className: "level-card");
        card.Q<GameText>("levelNumber").text = data.Level.ToString();
        card.AddManipulator(new Clickable(() => LevelLoader.Load(data)));
        container.Add(item);
    }
}
```

Add the `TemplateContainer` itself, not the inner element: the template's `<Style>` sheets are attached
to the container, so moving the child out loses its styles.

Use `ListView` (`makeItem`/`bindItem`) only for long scrolling lists.

## 3. Align UI Toolkit content inside a uGUI RectTransform

uGUI RectTransforms follow `Crystal.SafeArea` and the Canvas Scaler. Measure them on screen, convert to
panel space, then set absolute styles. (The old `LevelHUDController.FitCoinBoxToTopBar` used this; the
current HUD no longer needs it. `LevelBootstrap.ViewportArea` uses the canvas-local variant to fit the
camera into `LevelBackground/SafeArea/ReelFrame/ReelArea`.) For notch insets use `SafeAreaContainer`
instead of this recipe.

```csharp
// Vùng chữ nhật trên màn hình (pixel, gốc dưới trái) của một RectTransform uGUI
Rect ScreenRect(RectTransform rect)
{
    Canvas canvas = rect.GetComponentInParent<Canvas>();
    Camera cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    rect.GetWorldCorners(corners);
    Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
    Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
    return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
}
```

`RuntimePanelUtils.ScreenToPanel(panel, p)` expects a **top-left origin**, so flip Y:
`new Vector2(bar.xMin, Screen.height - bar.yMax)` for the top-left corner. Re-run only when the screen
rect changes (cache the last `Rect`, check in `LateUpdate`).

## 4. Element position for world-space effects

Coins fly from reels to the HUD coin icon (`LevelHUDController.CoinIconScreenPosition` →
`SlotVfx.CoinTarget`):

```csharp
// Vị trí tâm phần tử theo pixel màn hình (gốc dưới trái)
Vector2 ScreenPosition(VisualElement element)
{
    Rect panel = element.panel.visualTree.worldBound;
    Vector2 center = element.worldBound.center;
    return new Vector2(center.x / panel.width * Screen.width, (1f - center.y / panel.height) * Screen.height);
}
```

Then `camera.ScreenToWorldPoint(new Vector3(x, y, depth))`. Pass a `Func<Vector2>` rather than a cached
value so it follows layout changes. World particles meant to show over 3D objects use the
`Slot/ParticleOverlay` shader (ZTest Always).

## 5. Animating values and elements

- Hover/press feedback: USS `transition-duration` + `:hover`/`:active` `scale`/`translate`.
- Counters and one-off motion: DOTween (`using DG.Tweening;`, in `Assets/Plugins/Demigiant`):

```csharp
countTween?.Kill();
countTween = DOTween.To(() => shownCoins, SetCoins, target, countSeconds).SetEase(Ease.OutCubic);
```

- Scale an element from code with `element.style.scale = new Scale(new Vector3(s, s, 1f));`.
- Keep the `Tween` in a field, `Kill()` it before restarting and in `OnDisable`.

## 6. Show/hide and states

- Toggle sub-screens: `element.style.display = DisplayStyle.None / Flex`.
- State classes: `element.EnableInClassList("level-card--locked", locked)`; style children with
  `.level-card--locked .level-card__number { opacity: 0.5; }`.
- Enable pairs of buttons together (`SetSpinning` in LevelHUDController): SPIN disabled while spinning,
  STOP enabled only while spinning.

## 7. Verify in the editor (Unity MCP)

1. `unity_execute_code`: `AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)`.
2. `unity_get_compilation_errors` until `isCompiling: false` and no entries.
3. Open the scene (`EditorSceneManager.OpenScene`, check `isDirty` first), `unity_play_mode play`.
4. Pause + `EditorApplication.Step()` to advance frames when the editor is unfocused.
5. `unity_screenshot_game` with a scratchpad path, then step 1-2 frames and read the PNG.
6. Inspect layout numerically: `UQueryExtensions.Q(root, "name").worldBound`.
7. Stop play, reopen the scene the user had open; discard play-mode dirtiness instead of saving it.
