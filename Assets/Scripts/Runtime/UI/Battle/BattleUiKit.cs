using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SurvivalRts
{
    // Nút wireframe: đổi màu chữ theo trạng thái vì Button chỉ đổi được ảnh nền.
    public class WireButton : Button
    {
        public TMP_Text Label;
        public Color TextColor;
        public Color DisabledTextColor;

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (Label != null) Label.color = state == SelectionState.Disabled ? DisabledTextColor : TextColor;
        }
    }

    // Bộ dựng widget uGUI theo phong cách wireframe xám sáng (theo theme của dự án Godot gd-01):
    // nền xám nhạt, panel trắng ngà viền 2px bo góc, nút chính màu tối chữ sáng, chữ TextMeshPro.
    public static class BattleUiKit
    {
        // Bảng màu wireframe
        public static readonly Color InkColor = new Color(0.149f, 0.149f, 0.149f, 1f);
        public static readonly Color TextColor = InkColor;
        public static readonly Color DimTextColor = new Color(0.424f, 0.424f, 0.424f, 1f);
        public static readonly Color PanelColor = new Color(0.98f, 0.98f, 0.98f, 1f);
        public static readonly Color RowColor = new Color(0.855f, 0.855f, 0.855f, 1f);
        public static readonly Color ButtonColor = RowColor;
        public static readonly Color BorderColor = new Color(0.502f, 0.502f, 0.502f, 1f);
        public static readonly Color ScreenColor = new Color(0.91f, 0.91f, 0.91f, 1f);
        public static readonly Color WorldColor = new Color(0.776f, 0.776f, 0.776f, 1f);
        public static readonly Color BackdropColor = new Color(0.149f, 0.149f, 0.149f, 0.45f);
        public static readonly Color SeparatorColor = new Color(0.753f, 0.753f, 0.753f, 1f);
        public static readonly Color PrimaryColor = new Color(0.314f, 0.314f, 0.314f, 1f);
        public static readonly Color PrimaryTextColor = new Color(0.98f, 0.98f, 0.98f, 1f);

        const int PanelRadius = 6;
        const int ControlRadius = 4;
        const int BorderWidth = 2;

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        // Font TMP có đủ tiếng Việt, gán bởi bên dựng giao diện
        public static TMP_FontAsset Font;

        // Sprite hình chữ nhật bo góc có viền, dùng kiểu Sliced nên kéo giãn không méo góc.
        public static Sprite RoundedSprite(Color fill, Color border, int radius, int borderWidth)
        {
            string key = $"{fill}{border}{radius}{borderWidth}";
            if (sprites.TryGetValue(key, out var cached) && cached != null) return cached;

            int size = (radius + borderWidth) * 2 + 2;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            float half = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // khoảng cách có dấu tới cạnh hình chữ nhật bo góc (âm là bên trong)
                    float px = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float py = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;
                    float d = outside + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                    float alpha = Mathf.Clamp01(0.5f - d);
                    float inner = Mathf.Clamp01(0.5f - (d + borderWidth));
                    var color = Color.Lerp(border, fill, inner);
                    color.a *= alpha;
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            float edge = radius + borderWidth;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(edge, edge, edge, edge));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            sprites[key] = sprite;
            return sprite;
        }

        // Sprite vòng cung tròn dùng cho biểu tượng xoay ở màn loading chuyển cảnh.
        public static Sprite SpinnerSprite(Color color, int size = 56, int thickness = 4)
        {
            string key = $"spinner:{color}{size}{thickness}";
            if (sprites.TryGetValue(key, out var cached) && cached != null) return cached;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            float half = size * 0.5f;
            float halfThick = thickness * 0.5f;
            float rMid = half - halfThick - 1f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float dRing = Mathf.Abs(dist - rMid) - halfThick;
                    float ringAlpha = Mathf.Clamp01(0.5f - dRing);
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (angle < 0f) angle += 360f;
                    float arcAlpha = angle <= 280f ? Mathf.Lerp(0.15f, 1f, angle / 280f) : 0f;
                    var c = color;
                    c.a *= ringAlpha * arcAlpha;
                    pixels[y * size + x] = c;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            sprites[key] = sprite;
            return sprite;
        }

        static void ApplyStyle(Image image, Color fill, Color border, int radius)
        {
            image.sprite = RoundedSprite(fill, border, radius, BorderWidth);
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }

        // Tạo một đối tượng UI có RectTransform, mặc định giãn kín khung cha.
        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        // Đặt neo và kích thước cho khung (pivot trùng điểm neo).
        public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        // Neo giãn theo chiều ngang ở mép trên hoặc dưới.
        public static void StretchX(RectTransform rt, float y, float height, bool top)
        {
            float anchorY = top ? 1f : 0f;
            rt.anchorMin = new Vector2(0f, anchorY);
            rt.anchorMax = new Vector2(1f, anchorY);
            rt.pivot = new Vector2(0.5f, anchorY);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        // Nền phẳng phủ kín khung (nền màn hình, lớp mờ phía sau hộp thoại), không viền.
        public static Image Background(Transform parent, string name, Color color)
        {
            var rt = Node(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        // Panel có viền 2px bo góc; fill là màu nền panel.
        public static Image Panel(Transform parent, string name, Color fill)
        {
            var rt = Node(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            ApplyStyle(image, fill, BorderColor, PanelRadius);
            return image;
        }

        // Ô giữ chỗ xám (logo, ảnh nền) có chữ mờ ở giữa.
        public static Image Placeholder(Transform parent, string text, float height, float fontSize = 22f)
        {
            var rt = Node(parent, "Placeholder");
            var image = rt.gameObject.AddComponent<Image>();
            ApplyStyle(image, RowColor, BorderColor, ControlRadius);
            Size(image, -1f, height);
            var label = Label(rt, text, fontSize, TextAlignmentOptions.Center, DimTextColor);
            label.name = "Text";
            return image;
        }

        public static VerticalLayoutGroup Vertical(RectTransform rt, float spacing, int padding, TextAnchor align = TextAnchor.UpperLeft)
        {
            var group = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.padding = new RectOffset(padding, padding, padding, padding);
            group.childAlignment = align;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return group;
        }

        public static HorizontalLayoutGroup Horizontal(RectTransform rt, float spacing, int padding, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var group = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.padding = new RectOffset(padding, padding, padding, padding);
            group.childAlignment = align;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            return group;
        }

        // Ép kích thước ưa thích của phần tử trong layout group; -1 là không đặt.
        public static LayoutElement Size(Component target, float width = -1f, float height = -1f, float flexWidth = -1f)
        {
            var element = target.GetComponent<LayoutElement>();
            if (element == null) element = target.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f) element.preferredWidth = width;
            if (height >= 0f) element.preferredHeight = height;
            if (flexWidth >= 0f) element.flexibleWidth = flexWidth;
            element.flexibleHeight = 0f;
            return element;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size = 20f,
            TextAlignmentOptions align = TextAlignmentOptions.Left, Color? color = null)
        {
            var rt = Node(parent, "Label");
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color ?? TextColor;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        // Chỉ gán lại chữ khi đổi để tránh dựng lại mesh mỗi frame.
        public static void SetText(TMP_Text label, string text)
        {
            if (label.text != text) label.text = text;
        }

        // Tiêu đề có đường kẻ 2px bên dưới, như tiêu đề hộp thoại của theme Godot.
        public static RectTransform Heading(Transform parent, string text, float size = 32f)
        {
            var box = Node(parent, "Heading");
            Vertical(box, 8f, 0);
            Size(Label(box, text, size), -1f, size * 1.4f);
            var line = Background(box, "Line", SeparatorColor);
            Size(line, -1f, 2f);
            return box;
        }

        public static Image Separator(Transform parent)
        {
            var line = Background(parent, "Separator", SeparatorColor);
            Size(line, -1f, 2f);
            return line;
        }

        // Nút bấm wireframe: nền xám, hover đậm viền, nhấn tối hơn; primary là nút chính nền tối chữ sáng.
        public static WireButton PushButton(Transform parent, string text, UnityAction onClick, float width = -1f, float height = 48f,
            float fontSize = 20f, bool primary = false)
        {
            var image = Panel(parent, "Button", ButtonColor);
            var button = image.gameObject.AddComponent<WireButton>();
            UiButtonFeedback.Attach(button);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;

            Color normal = primary ? PrimaryColor : ButtonColor;
            Color hover = primary ? new Color(0.24f, 0.24f, 0.24f, 1f) : new Color(0.776f, 0.776f, 0.776f, 1f);
            Color pressed = primary ? new Color(0.149f, 0.149f, 0.149f, 1f) : new Color(0.659f, 0.659f, 0.659f, 1f);
            Color disabled = primary ? new Color(0.62f, 0.62f, 0.62f, 1f) : new Color(0.89f, 0.89f, 0.89f, 1f);
            Color edge = primary ? InkColor : BorderColor;
            Color hoverEdge = primary ? InkColor : new Color(0.314f, 0.314f, 0.314f, 1f);
            Color pressedEdge = InkColor;
            Color disabledEdge = new Color(0.753f, 0.753f, 0.753f, 1f);

            image.sprite = RoundedSprite(normal, edge, ControlRadius, BorderWidth);
            button.spriteState = new SpriteState
            {
                highlightedSprite = RoundedSprite(hover, hoverEdge, ControlRadius, BorderWidth),
                selectedSprite = RoundedSprite(hover, hoverEdge, ControlRadius, BorderWidth),
                pressedSprite = RoundedSprite(pressed, pressedEdge, ControlRadius, BorderWidth),
                disabledSprite = RoundedSprite(disabled, disabledEdge, ControlRadius, BorderWidth)
            };
            if (onClick != null) button.onClick.AddListener(onClick);
            Size(button, width, height);

            var label = Label(image.transform, text, fontSize, TextAlignmentOptions.Center, primary ? PrimaryTextColor : InkColor);
            label.name = "Text";
            label.margin = new Vector4(8f, 2f, 8f, 2f);
            button.Label = label;
            button.TextColor = primary ? PrimaryTextColor : InkColor;
            button.DisabledTextColor = primary ? new Color(0.9f, 0.9f, 0.9f, 1f) : new Color(DimTextColor.r, DimTextColor.g, DimTextColor.b, 0.6f);
            return button;
        }

        public static TMP_Text ButtonLabel(Button button) => button.GetComponentInChildren<TMP_Text>();

        // Ô nhập chữ TMP dựng bằng code, nền trắng ngà viền xám.
        public static TMP_InputField Input(Transform parent, string initial, string hint, float height = 44f)
        {
            var image = Panel(parent, "Input", PanelColor);
            Size(image, -1f, height);

            var area = Node(image.transform, "Text Area");
            area.offsetMin = new Vector2(12f, 4f);
            area.offsetMax = new Vector2(-12f, -4f);
            area.gameObject.AddComponent<RectMask2D>();

            var text = Label(area, "", 20f, TextAlignmentOptions.Left);
            text.name = "Text";
            var placeholder = Label(area, hint, 20f, TextAlignmentOptions.Left, DimTextColor);
            placeholder.name = "Placeholder";
            placeholder.fontStyle = FontStyles.Italic;

            var field = image.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = image;
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.text = initial;
            field.caretColor = InkColor;
            field.selectionColor = new Color(0.502f, 0.502f, 0.502f, 0.4f);
            return field;
        }

        // Thanh tiến trình: track xám có viền, phần fill xám đậm kéo giãn theo anchorMax.x (0..1).
        public static RectTransform ProgressBar(Transform parent, float height, out RectTransform fill)
        {
            var track = Panel(parent, "ProgressBar", RowColor);
            track.raycastTarget = false;
            Size(track, -1f, height);

            var fillImage = Node(track.transform, "Fill");
            var image = fillImage.gameObject.AddComponent<Image>();
            image.sprite = RoundedSprite(new Color(0.408f, 0.408f, 0.408f, 1f), new Color(0.408f, 0.408f, 0.408f, 1f), 2, 0);
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            fillImage.anchorMin = Vector2.zero;
            fillImage.anchorMax = new Vector2(0f, 1f);
            fillImage.offsetMin = new Vector2(4f, 4f);
            fillImage.offsetMax = new Vector2(-4f, -4f);
            fill = fillImage;
            return track.rectTransform;
        }

        // Màn hình toàn khung có thẻ panel ở giữa: tiêu đề bên trái, ghi chú mờ bên phải, đường kẻ dưới tiêu đề.
        // Trả về thẻ để bên gọi thêm nội dung, cuối cùng gọi Footer.
        public static RectTransform ScreenCard(Transform parent, string name, string title, string note, float marginX, float marginY,
            Color? backdrop = null)
        {
            var screen = Background(parent, name, backdrop ?? ScreenColor);
            var card = Panel(screen.transform, "Card", PanelColor);
            card.rectTransform.offsetMin = new Vector2(marginX, marginY);
            card.rectTransform.offsetMax = new Vector2(-marginX, -marginY);
            Vertical(card.rectTransform, 16f, 24);

            var header = Node(card.transform, "Header");
            Size(header, -1f, 48f);
            Horizontal(header, 12f, 0, TextAnchor.LowerLeft).childForceExpandHeight = false;
            Size(Label(header, title, 32f), -1f, 44f, 1f);
            Size(Label(header, note, 14f, TextAlignmentOptions.Right, DimTextColor), 320f, 24f);
            Separator(card.transform);
            return card.rectTransform;
        }

        // Hàng nút cuối thẻ, có đường kẻ phía trên.
        public static RectTransform Footer(Transform card)
        {
            Separator(card);
            var row = Node(card, "Footer");
            Size(row, -1f, 52f);
            Horizontal(row, 12f, 0);
            return row;
        }

        // Vùng cuộn dọc chiếm hết chỗ còn lại của thẻ; trả về content để thêm dòng vào.
        public static RectTransform ScrollList(Transform parent, float spacing)
        {
            var root = Node(parent, "Scroll");
            var element = root.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 1f;
            element.flexibleWidth = 1f;
            element.minHeight = 0f;

            var viewport = Node(root, "Viewport");
            viewport.gameObject.AddComponent<RectMask2D>();
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);

            var content = Node(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            Vertical(content, spacing, 0);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        // Dòng cài đặt: tên bên trái, điều khiển thêm vào dòng, đường kẻ mảnh phía dưới.
        public static RectTransform SettingRow(Transform parent, string title)
        {
            var row = Node(parent, "Setting");
            Size(row, -1f, 56f);
            Horizontal(row, 16f, 4);
            Size(Label(row, title, 20f), 360f, -1f, 1f);

            var line = Background(row, "Line", SeparatorColor);
            var lineRect = line.rectTransform;
            lineRect.anchorMin = Vector2.zero;
            lineRect.anchorMax = new Vector2(1f, 0f);
            lineRect.pivot = new Vector2(0.5f, 0f);
            lineRect.sizeDelta = new Vector2(0f, 2f);
            lineRect.anchoredPosition = Vector2.zero;
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return row;
        }

        // Tiêu đề nhóm cài đặt cỡ 24.
        public static void GroupTitle(Transform parent, string text)
        {
            Size(Label(parent, text, 24f), -1f, 40f);
        }

        // Thanh trượt wireframe: rãnh xám có viền, phần đã chọn xám đậm, núm tròn góc.
        public static Slider SliderControl(Transform parent, float min, float max, bool whole, float value, UnityAction<float> onChange)
        {
            var root = Node(parent, "Slider");
            Size(root, 320f, 32f);

            var track = Node(root, "Track");
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.sizeDelta = new Vector2(0f, 14f);
            var trackImage = track.gameObject.AddComponent<Image>();
            ApplyStyle(trackImage, RowColor, BorderColor, 4);

            var fillArea = Node(root, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(7f, -4f);
            fillArea.offsetMax = new Vector2(-17f, 4f);
            var fill = Node(fillArea, "Fill");
            fill.sizeDelta = new Vector2(10f, 0f);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = RoundedSprite(new Color(0.408f, 0.408f, 0.408f, 1f), new Color(0.408f, 0.408f, 0.408f, 1f), 2, 0);
            fillImage.type = Image.Type.Sliced;

            var slideArea = Node(root, "Handle Slide Area");
            slideArea.offsetMin = new Vector2(10f, 0f);
            slideArea.offsetMax = new Vector2(-10f, 0f);
            var handle = Node(slideArea, "Handle");
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.sizeDelta = new Vector2(20f, 0f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            ApplyStyle(handleImage, PrimaryColor, InkColor, ControlRadius);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = whole;
            slider.SetValueWithoutNotify(value);
            if (onChange != null) slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        // Dòng thanh trượt kèm nhãn giá trị bên phải; format biến giá trị thành chữ.
        public static Slider SliderRow(Transform parent, string title, float min, float max, bool whole, float value,
            System.Func<float, string> format, UnityAction<float> onChange)
        {
            var row = SettingRow(parent, title);
            var valueLabel = Label(row, format(value), 20f, TextAlignmentOptions.Right, DimTextColor);
            var slider = SliderControl(row, min, max, whole, value, null);
            slider.onValueChanged.AddListener(v =>
            {
                SetText(valueLabel, format(v));
                onChange?.Invoke(v);
            });
            valueLabel.transform.SetAsLastSibling();
            Size(valueLabel, 90f, -1f);
            return slider;
        }

        // Dòng bật/tắt: nút hiện Bật (chữ đậm) hoặc Tắt (chữ mờ), bấm để đổi.
        public static Button ToggleRow(Transform parent, string title, bool value, UnityAction<bool> onChange)
        {
            var row = SettingRow(parent, title);
            var spacer = Node(row, "Spacer");
            Size(spacer, -1f, -1f, 1f);
            var button = PushButton(row, "", null, 120f, 40f, 18f);
            void Show(bool on)
            {
                button.Label.text = on ? "Bật" : "Tắt";
                button.Label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                button.TextColor = on ? InkColor : DimTextColor;
                button.Label.color = button.TextColor;
            }
            Show(value);
            button.onClick.AddListener(() =>
            {
                value = !value;
                Show(value);
                onChange?.Invoke(value);
            });
            return button;
        }

        // Dòng chọn một trong nhiều giá trị: nút trước, nhãn giá trị, nút sau (xoay vòng).
        public static void ChoiceRow(Transform parent, string title, string[] choices, int index, UnityAction<int> onChange)
        {
            var row = SettingRow(parent, title);
            Size(Node(row, "Spacer"), -1f, -1f, 1f);
            var label = Label(row, choices[index], 20f, TextAlignmentOptions.Center);
            Size(label, 220f, -1f);
            void Step(int delta)
            {
                index = (index + delta + choices.Length) % choices.Length;
                SetText(label, choices[index]);
                onChange?.Invoke(index);
            }
            var previous = PushButton(row, "<", () => Step(-1), 44f, 40f, 20f);
            var next = PushButton(row, ">", () => Step(1), 44f, 40f, 20f);
            previous.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
            next.transform.SetAsLastSibling();
        }

        // Hộp thoại xác nhận có lớp mờ phía sau, tiêu đề gạch chân, hai nút bên phải (nút chính là xác nhận).
        public static GameObject ConfirmDialog(Transform parent, string title, string message, string cancelText, string confirmText,
            UnityAction onConfirm, UnityAction onCancel)
        {
            var backdrop = Background(parent, "DialogBackdrop", BackdropColor);
            var panel = Panel(backdrop.transform, "Dialog", PanelColor);
            Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(560f, 0f), Vector2.zero);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Vertical(panel.rectTransform, 20f, 20);

            Heading(panel.transform, title);
            var body = Label(panel.transform, message, 20f);
            Size(body, -1f, 56f);

            var row = Node(panel.transform, "Actions");
            Size(row, -1f, 48f);
            Horizontal(row, 12f, 0, TextAnchor.MiddleRight);
            PushButton(row, cancelText, onCancel, 140f, 48f);
            PushButton(row, confirmText, onConfirm, 140f, 48f, 20f, true);
            return backdrop.gameObject;
        }
    }
}
