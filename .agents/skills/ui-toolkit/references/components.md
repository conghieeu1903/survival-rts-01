# Project UI components

Both live in `Assets/UI/Components` (assembly `UIComponents`, auto-referenced by `Assembly-CSharp`) and are
declared with `[UxmlElement] public partial class ...`, so they appear in UI Builder's Library under
"Project" and are written in UXML without a namespace prefix: `<GameText .../>`, `<SkewButton .../>`.

## GameText (`GameText/GameText.cs`)

`Label` subclass that applies one shared text style instead of setting font, size and outline per element.

| Member | Notes |
|---|---|
| `text-style` (UXML) / `textStyle` (C#) | `GameTextStyle`: `None`, `ComicTitle`, `ComicButton`, `Heading`, `Subheading`, `Body` (default), `Caption` |
| `GameText(string text, GameTextStyle style)` | constructor for code-built UI |
| `GameText.GetUssClassName(style)` | `ComicButton` → `game-text--comic-button` |

Styles live in `Assets/UI/Common/TextStyles.uss` (imported by `DefaultTheme.tss`):

| Class | Size | Look |
|---|---|---|
| `game-text--comic-title` | 84px | white, 4px black outline, hard drop shadow |
| `game-text--comic-button` | 52px | white, 3px outline, drop shadow |
| `game-text--heading` | 62px | dark text |
| `game-text--subheading` | 46px | dark text |
| `game-text--body` | 42px | dark text |
| `game-text--caption` | 30px | muted grey |

Theme variables on `:root`: `--text-color-dark`, `--text-color-muted`, `--text-color-light`,
`--text-color-outline`. Font: Yokelvision SDF (TextMesh Pro font asset via `-unity-font-definition`).

To add a style: add the enum value, the `GetUssClassName` case and the USS class together. Override size
for one element with a screen class (`.level-card__number { font-size: 220px; }`) or inline
`style.fontSize` from code (LevelHUDController scales the coin label to its box).

## SkewButton (`SkewButton/SkewButton.cs` + `SkewButton.uss`)

Comic-style slanted button drawn with `Painter2D` (shape child) plus a `GameText` label.

| Member | Notes |
|---|---|
| `text` (UXML/C#) | label text |
| `text-style` | label `GameTextStyle`, default `ComicButton` |
| `event Action clicked` | raised by an internal `Clickable` manipulator |
| `SetEnabled(bool)` | standard; pair with a `:disabled` rule in the screen USS |

Every screen UXML that uses it must include the stylesheet:
`<ui:Style src="../../Components/SkewButton/SkewButton.uss" />`.

USS classes: `skew-button` (root, min 452x114, hover scale 1.04, active translate 3px 5px),
`skew-button__shape`, `skew-button__label`.

Shape colours are USS custom properties read in `CustomStyleResolvedEvent`, so restyle per screen by
targeting the shape child:

```css
.level-hud__stop > .skew-button__shape { --skew-fill-color: rgb(230, 57, 70); }
.level-hud__stop:hover > .skew-button__shape { --skew-fill-color: rgb(245, 90, 100); }
```

| Custom property | Default |
|---|---|
| `--skew-fill-color` | `rgb(253, 206, 3)` (hover `rgb(255, 222, 70)`) |
| `--skew-stroke-color` | black |
| `--skew-stroke-width` | 3 |
| `--skew-shadow-color` | black |
| `--skew-shadow-offset-x` / `-y` | 6 / 11 (active 3 / 6) |

Smaller variant example: `.level-select__back { min-width: 260px; min-height: 80px; padding: 12px 36px 22px 36px; }`.

## Writing a new component

- `[UxmlElement] public partial class Name : VisualElement` in `Assets/UI/Components/<Name>/`, with its own
  USS next to it and `public const string UssClassName = "kebab-name";` plus `__part` constants.
- Expose settings with `[UxmlAttribute]` properties (lowercase-first names map to kebab-case attributes).
- Custom drawing: `generateVisualContent += ...` and `MarkDirtyRepaint()`; custom USS values via
  `CustomStyleProperty<T>` + `RegisterCallback<CustomStyleResolvedEvent>`.
- Keep gameplay references out of components; controllers in `Assets/Scripts/UI` connect them to the game.
