---
name: figma-ui
description: Cách làm việc với file Figma wireframe của dự án survival-rts-01 qua MCP TalkToFigma - khởi động relay WebSocket (bun, cổng 3055), join kênh plugin, đọc và vẽ frame, quy ước tên frame, ghi CHANGELOG trong Figma, xuất giao diện Unity ra JSON (docs/figma) và plugin import. Dùng khi nhắc tới Figma, wireframe, frame, mũi tên luồng màn, changelog Figma, mcp__TalkToFigma, hoặc muốn đối chiếu giao diện Unity với bản thiết kế.
---

# Figma wireframe của survival-rts-01

File Figma: `https://www.figma.com/design/auNt5ay7MyzZoUTEus76lo/survival-rts-01` (một page `wireframe`, id `0:1`).
Link có `node-id=4005-1780` thì ID node là `4005:1780` (đổi dấu `-` thành `:`).

## 1. Kết nối (làm theo thứ tự)

Chuỗi: Claude → MCP `TalkToFigma` → relay WebSocket (cổng 3055) → plugin **Talk to Figma** trong Figma.

1. **Relay phải chạy bằng Bun**, không chạy được bằng Node (`Bun.serve`). Chạy nền:
   `cd C:\Users\daclo\AppData\Roaming\npm\node_modules\claude-talk-to-figma-mcp` rồi `bun run dist/socket.js`.
   Thấy dòng "Auto-routing Figma WebSocket server running on port 3055" là xong. Kiểm tra cổng: `Get-NetTCPConnection -LocalPort 3055 -State Listen`.
2. Người dùng mở file Figma, chạy plugin Talk to Figma và **để cửa sổ plugin mở**. Plugin hiện tên kênh (đổi mỗi lần mở, ví dụ `fgvjie6p`).
3. `mcp__TalkToFigma__join_channel` với tên kênh đó, rồi `get_document_info` để xác nhận.

Lỗi thường gặp:
- "Not connected to Figma": relay chưa chạy.
- "No Figma plugin connected": plugin chưa mở hoặc đã đóng. Xem log relay: `[CLOSE] ... code=1001` nghĩa là cửa sổ plugin bị đóng. Xin người dùng mở lại plugin và đưa tên kênh mới.
- Không tự tắt tiến trình Unity/Figma của người dùng; nhờ họ đóng.
- Có hai gói TalkToFigma khác nhau (`claude-talk-to-figma-mcp` đang cài, `cursor-talk-to-figma-mcp` trong cấu hình mẫu), cùng cổng 3055. Chỉ chạy một relay.
- Không in `FIGMA_API_KEY` trong cấu hình MCP `figma` ra kết quả. MCP `figma` chính thức (`figma-developer-mcp`) hay hết thời gian chờ khi khởi động; không cần nó để dùng TalkToFigma.

## 2. Giới hạn của công cụ

- Không đọc được lịch sử chỉnh sửa hay giờ sửa của layer (plugin API không có). Muốn biết ai sửa gì lúc nào thì dựa vào khung **CHANGELOG** (mục 4) và *File → Show version history* do người dùng mở.
- Không chuyển page: `set_current_page` bị relay chặn, `get_pages` không có trên plugin hiện tại. Làm việc theo ID node, khi tạo layer truyền ID page làm `parentId`.
- Kết quả `get_node_info` bị cắt (`_childrenTruncated`): đọc tiếp bằng `get_node_info` / `get_nodes_info` theo ID con.

## 3. Cấu trúc file

Frame cấp cao nhất trong page `wireframe` đặt tên `NN Tên màn` (hậu tố a/b/c là biến thể):

| Nhóm | Frame | Khung |
|---|---|---|
| Vào game, màn phụ | 01 Loading, 02 Menu chính, 02b Hộp thoại Thoát, 08a/b/c Cài đặt (Đồ họa, Âm thanh, Điều khiển), 09a/b Bản lưu, 10 Credits | 1920×1080 |
| Multiplayer, trận | 03a/b Multiplayer LAN, 04 Tham gia máy chủ, 05a/b Phòng chờ, 06a/b HUD, 07a/b Kết quả | 1920×1080 |
| Offline (cũ, nền tối) | 20a/b/c Màn chơi, Đặt công trình, Bản đồ lớn; 21 Menu Esc | 1440×810 |
| Khác | `Luồng màn hình`, nhãn mục `§ 1` đến `§ 5`, component set `Màn Bản lưu`, `Màn Phòng chờ`, `Màn Kết quả trận`, khung `CHANGELOG` (`4005:1780`) | |

File dùng **Component instance** (`Button - Chơi LAN`, `Logo`, `Heading`) và font **Arial**. Khi thêm màn mới bám cách này: dùng lại component có sẵn, đặt tên layer `Button - <nhãn>`, `Text - <nội dung>`, không tạo frame thuần cho thứ đã có component.

Theme: nền màn `#E8E8E8`, nền menu/multiplayer `#C6C6C6`, panel `#FAFAFA`, hàng/nút `#DADADA`, viền `#808080` 2px, chữ `#262626`, chữ phụ `#6C6C6C`, nút chính `#505050`. Bo góc panel 6, nút 4. Đầy đủ ở `docs/figma/tokens.json`. Nguồn thật của số liệu là code Unity, không đoán.

## 4. CHANGELOG trong Figma (bắt buộc)

Khung `CHANGELOG` (`4005:1780`) là dòng thời gian chỉnh sửa. Quy ước ghi trên khung: **mỗi lần sửa file (AI hay người) thêm một dòng lên ĐẦU danh sách, mới nhất ở trên, không sửa hay xoá dòng cũ, giờ Việt Nam (UTC+7).**

Mỗi dòng là instance của component "Dòng changelog", đặt tên `Log - dd/mm/yyyy hh:mm`, nằm trong khung `Entries`. Các trường: `Time` (dd/mm/yyyy hh:mm), `Author` (ví dụ "Claude"), `TypeBadge/Type` (loại thay đổi), `Content` (nội dung), `Scope` (phạm vi, ví dụ "📌 README · Components").

Quy trình sau **mỗi** lần Claude sửa Figma:
1. Đọc dòng mới nhất (`get_node_info` trên khung `Entries`) để lấy kiểu `Type` và `Scope` đang dùng, và đọc mục 12 của frame README nếu có.
2. Nhân bản một dòng (`clone_node`), đưa lên đầu, đổi tên và nội dung các trường bằng `set_multiple_text_contents`.
3. Ghi đúng giờ hiện tại theo UTC+7, `Author` là "Claude", nội dung ngắn nêu rõ frame/ID đã đổi.

Chỉ đọc file thì không cần ghi.

## 5. Xuất giao diện Unity sang Figma

Dữ liệu chính xác nằm ở `docs/figma/` (đọc `docs/figma/README.md`):
- `screens/*.json`: cây layer đo từ Unity (x, y, w, h, màu, chữ, auto layout). `flow.json`: vị trí frame và mũi tên chuyển màn. `tokens.json`: design tokens.
- `plugin/`: plugin Figma đọc các JSON và vẽ. Chọn tất cả file JSON khi chạy.
- `Assets/Scripts/Editor/UiLayoutExporter.cs`: xuất lại JSON từ Unity trong **Play Mode** (`FixFrame`, `CreateFrame`, `Export`). Đừng xuất HUD lúc trận 16000 lính đang chạy, Unity treo; dựng màn bằng khung riêng với dữ liệu mẫu.
- `tools/build_missing_screens.py`: dựng JSON cho HUD, Kết quả và các màn offline mà không cần Unity.

## 6. Quy tắc làm việc

- Đọc trước khi ghi: `get_document_info` để biết frame hiện có, tránh vẽ trùng.
- Vẽ vào Figma là thay đổi bên ngoài mà người dùng thấy ngay; hỏi trước khi sửa hoặc xoá frame có sẵn, thêm frame mới thì báo lại ID.
- Mọi thay đổi trong Figma phải có dòng CHANGELOG (mục 4).
- Giao diện Unity là nguồn sự thật: khi Figma và Unity lệch, đối chiếu số liệu rồi báo chỗ lệch, không tự sửa phía Unity.
- Trả lời người dùng bằng tiếng Việt.
