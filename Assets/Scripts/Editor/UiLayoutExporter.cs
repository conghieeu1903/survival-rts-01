using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalRts.Editor
{
    // Xuất cây giao diện uGUI đang chạy ra JSON (vị trí, kích thước, màu, chữ, auto layout) để dựng lại trên Figma.
    // Chạy trong Play Mode: đưa Canvas về World Space đúng khung tham chiếu rồi đo sau khi layout xong.
    public static class UiLayoutExporter
    {
        static readonly Dictionary<Sprite, (Color fill, Color border)> spriteColors = new Dictionary<Sprite, (Color, Color)>();

        // Tạo Canvas World Space đúng kích thước khung để dựng màn hình vào.
        public static Canvas CreateFrame(string name, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(width, height);
            rt.localScale = Vector3.one;
            return canvas;
        }

        // Đưa Canvas có sẵn trong scene về khung cố định: bỏ CanvasScaler, World Space, kích thước width x height.
        public static void FixFrame(Canvas canvas, float width, float height)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)canvas.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.localScale = Vector3.one;
            rt.position = Vector3.zero;
        }

        // Đo và ghi JSON một màn hình: mọi con trực tiếp của canvas là một node gốc.
        public static string Export(Canvas canvas, string screenName, string title, string outPath, string only = null)
        {
            var root = (RectTransform)canvas.transform;
            var states = new List<(GameObject go, bool active)>();
            Collect(root, states);
            // Bật hết để layout tính cả phần ẩn mặc định, đo xong khôi phục
            foreach (var s in states) s.go.SetActive(true);
            for (int i = 0; i < 3; i++)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            }

            var builder = new StringBuilder();
            builder.Append("{\n");
            builder.Append($"  \"screen\": {Str(screenName)},\n  \"title\": {Str(title)},\n");
            builder.Append($"  \"frame\": {{ \"width\": {Num(root.rect.width)}, \"height\": {Num(root.rect.height)} }},\n");
            builder.Append("  \"nodes\": [\n");
            var activeMap = new Dictionary<GameObject, bool>();
            foreach (var s in states) activeMap[s.go] = s.active;
            bool first = true;
            foreach (Transform child in root)
            {
                if (only != null && child.name != only) continue;
                if (!first) builder.Append(",\n");
                first = false;
                WriteNode(builder, (RectTransform)child, root, root, activeMap, 2);
            }
            builder.Append("\n  ]\n}\n");

            foreach (var s in states) s.go.SetActive(s.active);

            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, builder.ToString(), new UTF8Encoding(false));
            return outPath;
        }

        static void Collect(Transform t, List<(GameObject, bool)> list)
        {
            foreach (Transform child in t)
            {
                list.Add((child.gameObject, child.gameObject.activeSelf));
                Collect(child, list);
            }
        }

        static void WriteNode(StringBuilder b, RectTransform rt, RectTransform parent, RectTransform frame,
            Dictionary<GameObject, bool> activeMap, int depth)
        {
            string pad = new string(' ', depth * 2);
            var origin = ToFrame(rt, frame);
            var parentOrigin = ToFrame(parent, frame);
            var text = rt.GetComponent<TMP_Text>();
            var image = rt.GetComponent<Image>();
            var raw = rt.GetComponent<RawImage>();
            var button = rt.GetComponent<Button>();
            var layout = rt.GetComponent<HorizontalOrVerticalLayoutGroup>();
            var element = rt.GetComponent<LayoutElement>();
            var fitter = rt.GetComponent<ContentSizeFitter>();
            var outline = rt.GetComponent<Outline>();
            bool ignore = element != null && element.ignoreLayout;

            b.Append(pad).Append("{\n");
            b.Append($"{pad}  \"name\": {Str(DisplayName(rt, text, button))},\n");
            b.Append($"{pad}  \"type\": {Str(text != null ? "TEXT" : "FRAME")},\n");
            b.Append($"{pad}  \"x\": {Num(origin.x - parentOrigin.x)}, \"y\": {Num(origin.y - parentOrigin.y)}, \"w\": {Num(rt.rect.width)}, \"h\": {Num(rt.rect.height)},\n");
            b.Append($"{pad}  \"visible\": {(activeMap.TryGetValue(rt.gameObject, out bool v) ? v : true).ToString().ToLowerInvariant()}");

            if (text != null)
            {
                b.Append($",\n{pad}  \"text\": {{ \"characters\": {Str(text.text)}, \"fontSize\": {Num(text.fontSize)}, \"color\": {Str(Hex(text.color))}, ");
                b.Append($"\"opacity\": {Num(text.color.a)}, \"bold\": {((text.fontStyle & FontStyles.Bold) != 0).ToString().ToLowerInvariant()}, ");
                b.Append($"\"italic\": {((text.fontStyle & FontStyles.Italic) != 0).ToString().ToLowerInvariant()}, ");
                b.Append($"\"alignH\": {Str(HorizontalName(text.horizontalAlignment))}, \"alignV\": {Str(VerticalName(text.verticalAlignment))} }}");
            }
            else if (image != null && image.enabled)
            {
                WriteImage(b, pad, image, outline);
            }
            else if (raw != null)
            {
                b.Append($",\n{pad}  \"fill\": {{ \"color\": {Str(Hex(Color.gray))}, \"opacity\": 1 }}, \"note\": \"RawImage (bản đồ), vẽ ô xám\"");
                if (outline != null) b.Append($",\n{pad}  \"stroke\": {{ \"color\": {Str(Hex(outline.effectColor))}, \"width\": {Num(outline.effectDistance.x)} }}");
            }

            if (button != null)
                b.Append($",\n{pad}  \"button\": {{ \"interactable\": {button.interactable.ToString().ToLowerInvariant()} }}");

            if (layout != null)
            {
                b.Append($",\n{pad}  \"autoLayout\": {{ \"direction\": {Str(layout is HorizontalLayoutGroup ? "HORIZONTAL" : "VERTICAL")}, ");
                b.Append($"\"spacing\": {Num(layout.spacing)}, \"padding\": [{layout.padding.left}, {layout.padding.top}, {layout.padding.right}, {layout.padding.bottom}], ");
                b.Append($"\"align\": {Str(layout.childAlignment.ToString())}, \"expandWidth\": {layout.childForceExpandWidth.ToString().ToLowerInvariant()}, \"expandHeight\": {layout.childForceExpandHeight.ToString().ToLowerInvariant()} }}");
            }

            if (element != null)
            {
                b.Append($",\n{pad}  \"layoutChild\": {{ \"ignoreLayout\": {ignore.ToString().ToLowerInvariant()}, \"growX\": {Num(Mathf.Max(0f, element.flexibleWidth))}, \"growY\": {Num(Mathf.Max(0f, element.flexibleHeight))} }}");
            }
            if (fitter != null)
            {
                b.Append($",\n{pad}  \"hug\": {{ \"x\": {(fitter.horizontalFit != ContentSizeFitter.FitMode.Unconstrained).ToString().ToLowerInvariant()}, \"y\": {(fitter.verticalFit != ContentSizeFitter.FitMode.Unconstrained).ToString().ToLowerInvariant()} }}");
            }

            if (rt.childCount > 0 && text == null)
            {
                b.Append($",\n{pad}  \"children\": [\n");
                bool first = true;
                foreach (Transform child in rt)
                {
                    if (!first) b.Append(",\n");
                    first = false;
                    WriteNode(b, (RectTransform)child, rt, frame, activeMap, depth + 2);
                }
                b.Append($"\n{pad}  ]");
            }
            b.Append($"\n{pad}}}");
        }

        // Tên layer dễ đọc trên Figma: nút và nhãn kèm nội dung chữ.
        static string DisplayName(RectTransform rt, TMP_Text text, Button button)
        {
            if (text != null && rt.name != "Placeholder" && !string.IsNullOrEmpty(text.text))
                return "Text - " + Shorten(text.text);
            if (button != null)
            {
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null && !string.IsNullOrEmpty(label.text)) return "Button - " + Shorten(label.text);
            }
            return rt.name;
        }

        static string Shorten(string value)
        {
            value = value.Replace('\n', ' ');
            return value.Length > 28 ? value.Substring(0, 28) : value;
        }

        static void WriteImage(StringBuilder b, string pad, Image image, Outline outline)
        {
            Color fill = image.color;
            Color border = image.color;
            int radius = 0;
            int strokeWidth = 0;
            if (image.sprite != null)
            {
                var colors = SpriteColors(image.sprite);
                fill = colors.fill * image.color;
                border = colors.border * image.color;
                bool hasStroke = !Approximately(colors.fill, colors.border);
                strokeWidth = hasStroke ? 2 : 0;
                radius = Mathf.Max(0, Mathf.RoundToInt(image.sprite.border.x) - (hasStroke ? 2 : 0));
            }
            else if (outline != null)
            {
                border = outline.effectColor;
                strokeWidth = Mathf.RoundToInt(outline.effectDistance.x);
            }

            b.Append($",\n{pad}  \"fill\": {{ \"color\": {Str(Hex(fill))}, \"opacity\": {Num(fill.a)} }}");
            if (strokeWidth > 0)
                b.Append($",\n{pad}  \"stroke\": {{ \"color\": {Str(Hex(border))}, \"width\": {strokeWidth} }}");
            if (radius > 0) b.Append($",\n{pad}  \"cornerRadius\": {radius}");
        }

        // Đọc màu nền (giữa) và màu viền (giữa cạnh trái) của sprite bo góc; texture không đọc được nên blit ra RenderTexture.
        static (Color fill, Color border) SpriteColors(Sprite sprite)
        {
            if (spriteColors.TryGetValue(sprite, out var cached)) return cached;

            var source = sprite.texture;
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var read = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            var fill = read.GetPixel(source.width / 2, source.height / 2);
            var border = read.GetPixel(1, source.height / 2);
            Object.DestroyImmediate(read);
            spriteColors[sprite] = (fill, border);
            return (fill, border);
        }

        static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;
        }

        // Góc trên trái của RectTransform trong hệ khung (gốc ở góc trên trái, y hướng xuống).
        static Vector2 ToFrame(RectTransform rt, RectTransform frame)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var topLeft = frame.InverseTransformPoint(corners[1]);
            var rect = frame.rect;
            return new Vector2(topLeft.x - rect.xMin, rect.yMax - topLeft.y);
        }

        static string HorizontalName(HorizontalAlignmentOptions option)
        {
            switch (option)
            {
                case HorizontalAlignmentOptions.Center: return "CENTER";
                case HorizontalAlignmentOptions.Right: return "RIGHT";
                case HorizontalAlignmentOptions.Justified: return "JUSTIFIED";
                default: return "LEFT";
            }
        }

        static string VerticalName(VerticalAlignmentOptions option)
        {
            switch (option)
            {
                case VerticalAlignmentOptions.Middle: return "CENTER";
                case VerticalAlignmentOptions.Bottom: return "BOTTOM";
                default: return "TOP";
            }
        }

        static string Hex(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }

        static string Num(float value)
        {
            return (Mathf.Round(value * 100f) / 100f).ToString("0.##", CultureInfo.InvariantCulture);
        }

        static string Str(string value)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in value ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
