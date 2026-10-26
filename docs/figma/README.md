# Xuất giao diện Unity sang Figma

Thư mục này chứa dữ liệu giao diện **đo trực tiếp từ Unity** (không phải ước lượng), cộng một plugin Figma dựng lại từng layer đúng toạ độ.

```
docs/figma/
  flow.json        luồng chuyển màn: vị trí frame trên canvas + danh sách mũi tên và nhãn
  tokens.json      màu, viền, bo góc, cỡ chữ, kích thước thành phần
  screens/*.json   mỗi file một màn: cây layer với x, y, w, h, màu, chữ, auto layout
  plugin/          plugin Figma đọc các JSON trên và vẽ
```

## Cách dùng

1. Figma Desktop → **Plugins → Development → Import plugin from manifest…** → chọn `plugin/manifest.json`.
2. Mở file Figma trống, chạy **Plugins → Development → Survival RTS - Import UI JSON**.
3. Chọn **tất cả** file: `flow.json` và mọi file trong `screens/` (Ctrl+A trong hộp chọn file).
4. Bấm **Vẽ lên Figma**. Mỗi màn thành một frame đặt theo lưới của `flow.json`, kèm mũi tên chuyển màn có nhãn nút bấm.

Tuỳ chọn:
- **Auto Layout** (thử nghiệm): bật Auto Layout cho các nhóm có layout group bên Unity. Mặc định tắt để layer nằm đúng toạ độ tuyệt đối.
- **Mũi tên**: tắt nếu chỉ cần các frame.

Font: plugin dùng **Inter** (có sẵn trong Figma). Game dùng Liberation Sans, độ rộng chữ lệch nhẹ; muốn khớp hơn thì cài Liberation Sans rồi đổi `FONT_REGULAR` / `FONT_BOLD` ở đầu `code.js`.

## Định dạng `screens/*.json`

```json
{
  "screen": "02_menu_chinh",
  "title": "Menu chính",
  "frame": { "width": 1920, "height": 1080 },
  "nodes": [ { ...node } ]
}
```

Mỗi node:

| Trường | Ý nghĩa |
|---|---|
| `name` | tên layer ("Button - Chơi LAN", "Text - Lượt 0"...) |
| `type` | `FRAME` hoặc `TEXT` |
| `x`, `y`, `w`, `h` | toạ độ góc trên trái **so với node cha**, kích thước (px, gốc trên trái, y hướng xuống) |
| `visible` | trạng thái hiện mặc định trong game (false: panel ẩn như hộp thoại, thông báo) |
| `fill` | `{ color, opacity }` |
| `stroke` | `{ color, width }` (viền nằm trong) |
| `cornerRadius` | bo góc px |
| `text` | `{ characters, fontSize, color, opacity, bold, italic, alignH, alignV }` |
| `button` | có mặt nếu là nút bấm; `interactable: false` là nút mờ |
| `autoLayout` | `{ direction, spacing, padding[trái, trên, phải, dưới], align, expandWidth, expandHeight }` |
| `layoutChild` | `{ ignoreLayout, growX, growY }` |
| `hug` | `{ x, y }` frame tự co theo nội dung |
| `children` | node con |

Mọi màn chỉ dùng hai kiểu layer (khung và chữ), nên dùng được cả khi không dùng plugin: dán JSON cho AI Figma kèm `tokens.json`.

## Màn không xuất từ Unity đang chạy

4 màn `06_hud_tran`, `07_ket_qua`, `20_offline_hud`, `21_offline_menu_esc` được dựng bằng `tools/build_missing_screens.py` (chạy `python build_missing_screens.py`). Script lấy số liệu từ `BattleHudScreen.cs`, `BattleResultScreen.cs` và `UiPrefabGenerator.cs` (nguồn sinh ra các prefab UI cũ), rồi tự tính vị trí theo luật của Unity LayoutGroup. Số liệu mẫu (số lính, vàng...) là giả định; kích thước và vị trí chính xác. Riêng độ rộng chữ ở giao diện cũ (thanh tài nguyên, tab) là ước lượng vì chữ co theo nội dung.

## Menu Esc offline (21a/21b/21c)

Menu Esc của màn chơi offline dùng cùng panel Cài đặt như menu chính (`SettingsPanel`), phủ lớp mờ lên game. Khác bản menu chính: nút **Tiếp tục** thay "Quay lại", thêm nút **Về menu chính**, tab Điều khiển liệt kê phím của màn offline (W A S D, cuộn chuột, R, X, M, Esc). JSON dựng bằng `tools/build_esc_menu.py` từ bản xuất của Unity (frame 1920×1080; nếu đặt vào frame offline 1440×810 thì scale 0.75).

Lưu ý: các bản xuất tab Âm thanh / Điều khiển lúc đầu còn sót nhóm của tab trước (Unity `Destroy` chậm một frame). `build_esc_menu.py` đã làm sạch `08b`, `08c`; khi xuất lại tab khác hãy chờ một frame sau khi bấm tab rồi mới xuất.

## Xuất lại khi giao diện đổi

Công cụ nằm ở `Assets/Scripts/Editor/UiLayoutExporter.cs`. Nó đưa Canvas về khung cố định rồi đo sau khi layout xong, nên phải chạy trong **Play Mode**:

- `UiLayoutExporter.FixFrame(canvas, 1920, 1080)` đưa Canvas có sẵn trong scene về khung chuẩn.
- `UiLayoutExporter.CreateFrame(tên, 1920, 1080)` tạo khung mới để dựng riêng một màn.
- `UiLayoutExporter.Export(canvas, id, tiêu đề, đường dẫn, tênNodeGốc)` ghi JSON.

Lưu ý: đừng xuất HUD khi trận 16000 lính đang chạy, Unity có thể treo; dựng màn bằng khung riêng và dữ liệu mẫu.
