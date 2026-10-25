---
name: ui-toolkit
description: How UI Toolkit is built in this project (sg02-2026, Unity 6000.3) - folder layout, the shared PanelSettings (1920x1080, match height), theme and text styles, the custom GameText and SkewButton elements, screen UXML/USS conventions, controller scripts that query elements by name and wire events, safe area handling (SafeAreaContainer for UI Toolkit, Crystal.SafeArea for uGUI), mixing UI Toolkit with uGUI canvases, and UI-to-world positions for VFX. Use whenever the task touches .uxml, .uss, .tss, UIDocument, PanelSettings, UI Builder, VisualElement, GameText, SkewButton, a screen under Assets/UI/Screens, a notch / cutout / safe area problem, or a *Controller.cs script that drives a UIDocument (MainMenuController, LevelHUDController, ShopController, SettingsController, LoadingScreen, HUDController, PopupController).
---

# UI Toolkit in sg02-2026

All game screens are UI Toolkit (`UIDocument` + UXML + USS). uGUI is only used for a few scene-level
bars, the Level background canvas (`Assets/Prefabs/UI/LevelBackground.prefab`) and the uGUI `SafeArea` helper. Read the reference that matches the task:

| Task | Read |
|---|---|
| Using or extending `GameText` / `SkewButton`, text styles, theme variables | [references/components.md](references/components.md) |
| Code recipes: screen controller, generated lists, uGUI alignment, UI → world position, tweens | [references/recipes.md](references/recipes.md) |

## Folder layout

```
Assets/UI/
  Common/        PanelSettings.asset, DefaultTheme.tss, TextStyles.uss
  Components/    UIComponents.asmdef (auto-referenced): GameText/, SkewButton/, SafeAreaContainer/,
                 StrokeText/, CurrencyPill/, ShopItemRow/, RewardAdCard/, SettingsRow/ ... (cs + uss)
  Screens/<Screen>/<Screen>.uxml + <Screen>.uss
Assets/Scripts/UI/   one MonoBehaviour controller per screen ([RequireComponent(typeof(UIDocument))])
```

Current screens: `Menu/MainMenu` (instances `LevelSelect`, `Shop`, `Settings` as templates), `LevelSelect/LevelSelect`,
`Shop/Shop`, `Settings/Settings`, `Level/LevelHUD` (instances `Settings`), `Loading/LoadingScreen`.

## Scaling contract (important)

- Every `UIDocument` uses `Assets/UI/Common/PanelSettings.asset`: **Scale With Screen Size, reference
  1920x1080, Match Width Or Height = 1 (height)**. uGUI Canvas Scalers in Level scenes use the same values.
- So **panel height is always 1080 units**, panel width varies: 1440 (4:3 tablet), 1920 (16:9),
  2376 (2.2:1 phone). Design USS in these units.
- Lay out horizontally with flex, `%`, or both-side anchors (`left` + `right`). Never assume the panel is
  1920 wide. Vertical px values are safe because height is fixed.
- Do not change PanelSettings back to a portrait reference or match 0.5: that shrank panel height to 971 on
  wide phones and broke LevelSelect (title overlapped, cards pushed down).

## Safe area (bắt buộc cho mọi screen)

UI Toolkit panels ignore notches, punch holes and the home bar. Every screen must keep its interactive
content inside `<SafeAreaContainer>` (`Assets/UI/Components/SafeAreaContainer`):

```xml
<ui:VisualElement name="shopRoot" class="shop">
    <ui:VisualElement name="shopBg" picking-mode="Ignore" class="shop__bg" />          <!-- full-bleed -->
    <ui:VisualElement name="shopOverlay" picking-mode="Ignore" class="shop__overlay" />
    <SafeAreaContainer name="shopSafeArea">                                              <!-- content -->
        <ui:VisualElement name="backButton" class="shop__back-btn">...</ui:VisualElement>
        ...
    </SafeAreaContainer>
</ui:VisualElement>
```

- `SafeAreaContainer` is absolute and fills the screen, then sets `left/top/right/bottom` from
  `Screen.safeArea` (converted with `RuntimePanelUtils.ScreenToPanel`). It re-checks every 250 ms, so
  rotation and resize are handled. Attributes `conform-x` / `conform-y` (default `true`).
- **Outside** the container (full-bleed): background images, overlays, decorative screen frames, big
  decorative characters. **Inside**: buttons, text, bars, panels, anything the player must see or tap.
- Children keep their absolute design coordinates; they are now relative to the safe rect. Don't add the
  inset by hand in USS.
- Several containers in one screen are fine to keep z-order (Loading has a top and a bottom one).
- Templates (`Shop`, `Settings`, `LevelSelect`) wrap their own content, so they are safe both standalone
  and when instanced inside `MainMenu` / `LevelHUD`.
- Elements positioned from world/screen space (e.g. LevelHUD reel headers) stay **outside** the
  container, because `ScreenToPanel` already returns full-panel coordinates. If such an element must line
  up with safe content, add the container offset: `designY + safeContainer.resolvedStyle.top`.
- uGUI canvases use `Crystal.SafeArea` (`Assets/Scripts/Utilities/SafeArea.cs`) on a stretched child
  RectTransform; put content (e.g. the Level reel frame) under it and leave backgrounds outside. Both read
  `Screen.safeArea`, so uGUI and UI Toolkit stay aligned.
- Test in the **Device Simulator** with a notch / punch-hole device in both landscape orientations
  (left and right) before calling a screen done.

## Conventions

1. **UXML is layout only.** No callbacks in UXML or UI Builder. Controllers find elements by `name` and
   wire events in `OnEnable`, unsubscribe in `OnDisable`. The `name` attribute is the contract between UXML
   and C#: renaming it silently breaks wiring (`Q<>()` returns null).
2. **Use project components**, not raw `Label`/`Button`: `GameText` with `text-style`, `SkewButton` with
   `clicked`. Use `Clickable` manipulator for clickable non-button elements (level cards).
3. **`picking-mode="Ignore"`** on every decorative element, text and layout wrapper, so only real buttons
   receive clicks.
4. **Styles**: one USS per screen, BEM-like class names (`level-hud__coins`, `level-card--locked`).
   Disabled look via `:disabled` (`opacity: 0.5`), press/hover via `:hover`/`:active` with
   `transition-duration`. Reference images with `url("/Assets/Arts/Sprites/...png")`.
5. **Screen switching** inside one UIDocument: toggle `style.display` between `Flex` and `None`
   (see `MainMenuController.ShowLevelSelect/ShowMainMenu`). Templates via `<ui:Template>` + `<ui:Instance>`.
6. **Prefer this event style order**: `Q<>()` + `clicked` (default) → generate repeated items from data
   with a template (lists that depend on ScriptableObjects) → custom `[UxmlElement]` only for reusable
   widgets with their own behaviour.
7. **Comments** (agents/rules/rule.md): one short Vietnamese `//` line above each class and method, short
   Vietnamese trailing `//` beside fields. No long or multi-line comments. Identifiers, UXML names, USS
   classes and visible UI strings stay English.
8. **No git commands** (agents/rules/rule.md).

## Gotchas

- **Unity Auto Refresh is off.** After editing `.cs/.uxml/.uss` from outside Unity, run
  `AssetDatabase.Refresh()` (Unity MCP `unity_execute_code`) and check `unity_get_compilation_errors`
  before asking the user to test.
- **Safe area**: `Crystal.SafeArea` only moves uGUI RectTransforms. For UI Toolkit always wrap content in
  `SafeAreaContainer` (section above). A new screen without it will be covered by the notch on phones.
- **uGUI and UI Toolkit draw independently.** To place UI Toolkit content inside a uGUI bar (e.g. the
  scene `top-bar`), measure the RectTransform on screen and convert with `RuntimePanelUtils.ScreenToPanel`
  (recipe in references/recipes.md). Don't hand-tune px offsets.
- **`TextStyles.uss` font URL** points to `Assets/Art/...` (old folder name); it still resolves through the
  GUID. Keep the GUID when editing; fix the path only together with the GUID.
- `UQueryExtensions.Q` is an extension method: in `unity_execute_code` snippets call
  `UnityEngine.UIElements.UQueryExtensions.Q(root, "name")` because `using` lines are not available.
- Game view capture (`unity_graphics_game_capture`) renders the camera only, **without UI Toolkit**. Use
  `unity_screenshot_game` with a path in the scratchpad to see the UI. When the editor is unfocused, play
  mode barely advances: pause and call `EditorApplication.Step()` to advance frames.

- **Flexbox & `ScrollView` button squishing bug**: In UI Toolkit (Yoga), `ScrollView` with only `flex-grow: 1` keeps `flex-basis: auto` (its content height). When switching to a tab with tall content (e.g., `controlsPage` in `Settings`), `ScrollView` expands and crushes sibling headers, tab bars, and footers. Always set `flex-shrink: 0` on headers, tab containers, footers, and buttons (`.wf-button`, `.wf-tab`), and always set `flex-grow: 1; flex-shrink: 1; flex-basis: 0; min-height: 0;` on `ScrollView` (plus `min-height: 0` on the parent panel).
- **Active tab border/padding shift**: If an active tab class (`.wf-tab--active`) increases `border-bottom-width` (e.g., `2px` -> `4px`), reduce `padding-bottom` by the same amount (`8px` -> `6px`) so button height and text position never shift when switching tabs.
- **Image aspect ratio**: Always set `-unity-background-scale-mode: scale-to-fit` (or `scale-and-crop`) on image previews/containers so images are not stretched when maximized.
- **Unity 6 `Button` localization**: `((INotifyValueChanged<string>)textEl).SetValueWithoutNotify(translated)` updates `Label` but does not update `Button.text` in Unity 6. Always follow up with `if (textEl.text != translated) textEl.text = translated;`.
