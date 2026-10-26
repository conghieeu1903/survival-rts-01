---
name: figma-mcp
description: Đọc trực tiếp file Figma của dự án survival-rts-01 qua MCP figma (get_figma_data, download_figma_images) bằng API token mà không cần bật Bun relay hay mở plugin TalkToFigma nhập mã channel. Dùng khi cần xem danh sách màn hình, đọc thông số layout, màu sắc, text, hoặc tải ảnh mẫu từ Figma để lập trình giao diện trong Unity.
---

# Đọc thiết kế Figma trực tiếp qua MCP `figma`

Skill này dùng cho việc **đọc dữ liệu và tải ảnh thiết kế** từ Figma bằng MCP server `figma` (kết nối trực tiếp tới Figma REST API bằng token đã cấu hình sẵn), không cần chạy WebSocket relay cổng 3055 và không cần người dùng mở plugin hay cung cấp mã `channel`.
- Khi cần **vẽ hoặc sửa** trực tiếp lên file Figma, chuyển sang dùng skill `figma-ui` (`TalkToFigma`).

## 1. Thông tin File Figma của dự án

- Đường dẫn: `https://www.figma.com/design/auNt5ay7MyzZoUTEus76lo/survival-rts-01`
- `fileKey`: `auNt5ay7MyzZoUTEus76lo`
- Quy đổi `nodeId`: trên URL Figma có dạng `node-id=4015-2724` thì khi truyền vào `nodeId` của tool phải đổi dấu `-` thành `:` (ví dụ `"4015:2724"`).

## 2. Công cụ sử dụng (MCP server: `figma`)

1. **`get_figma_data`**:
   - Lấy cấu trúc cây giao diện, kích thước (`width`, `height`), auto-layout (`mode`, `gap`, `padding`), màu sắc (`fills`, `strokes`), kiểu chữ (`fontSize`, `fontWeight`) và nội dung chữ (`text`).
   - Tham số:
     - `fileKey`: `"auNt5ay7MyzZoUTEus76lo"` (bắt buộc)
     - `nodeId`: ID của Page (CANVAS) hoặc Frame cần đọc (ví dụ `"4015:2724"`). Luôn truyền `nodeId` của Frame cụ thể để kết quả gọn và nhanh.
     - `depth`: Truyền `1` hoặc `2` khi muốn xem danh sách các Frame con trong một Page trước khi đọc sâu từng Frame.

2. **`download_figma_images`**:
   - Xuất ảnh PNG/SVG của các Frame hoặc icon từ Figma về thư mục trong máy để đối chiếu trực quan.
   - Tham số:
     - `fileKey`: `"auNt5ay7MyzZoUTEus76lo"`
     - `localPath`: Đường dẫn thư mục lưu ảnh.
     - `nodes`: Danh sách `{ nodeId, fileName }`.

## 3. Danh mục ID các Page và Frame chính (`auNt5ay7MyzZoUTEus76lo`)

### Các Page (CANVAS)
- `README `: `4006:4243` (chứa hướng dẫn và khung `CHANGELOG` `#4005:1780`)
- `wireframe`: `0:1` (chứa toàn bộ các màn hình 1920×1080)
- `Luồng HUD Màn chơi`: `4015:10362` (chứa các trạng thái thanh HUD đáy màn hình)
- `Components`: `4005:12` (chứa các component dùng chung)

### Các Frame trong Page `wireframe` (`0:1`)
- `01 Loading khởi động`: `4015:2468`
- `02 Menu chính`: `4015:2480`
- `02b Hộp thoại Thoát game`: `4015:2506`
- `03 Multiplayer LAN`: `4015:3640`
- `04 Tham gia máy chủ`: `4015:3689`
- `05a Phòng chờ - Chủ phòng`: `4015:3751`
- `05b Phòng chờ - Người chơi`: `4015:3802`
- `06 Màn chơi`: `4015:2795`
- `07a Kết quả online - Chủ phòng`: `4015:3851`
- `07b Kết quả online - Người chơi`: `4015:4041`
- `08 Cài đặt`: `4015:2632`
- `09 Bản lưu`: `4015:2607`
- `10 Credits`: `4015:2690`
- `11 Loading chuyển cảnh`: `4015:2724`
- `12 Chơi mới`: `4015:2543`
- `13 Tổng kết đợt`: `4015:2940`
- `14a Chiến thắng`: `4015:3274`
- `14b Thất bại`: `4015:3452`
- `21 Cài đặt trong game`: `4015:2738`
- `22 Tạm dừng`: `4015:3112`

### Các Frame trong Page `Luồng HUD Màn chơi` (`4015:10362`)
- `H1 · Mặc định`: `4015:12043`
- `H2 · Xây dựng – Chọn nhóm`: `4015:12203`
- `H3 · Xây dựng – Phòng thủ`: `4015:12372`
- `H4 · Đặt tường`: `4015:12540`
- `H5 · Chọn công trình`: `4015:12784`
- `H6 · Doanh trại`: `4015:12975`
- `H7 · Chọn quân`: `4015:13164`
- `H8 · Đang bị tấn công`: `4015:13478`
- `Bảng vùng × trạng thái`: `4015:13759`
- `Giải phẫu HUD`: `4015:13879`

## 4. Lưu ý bảo mật
- Không bao giờ ghi hoặc in khóa `FIGMA_API_KEY` (`figd_...`) vào các file trong dự án hoặc ra nội dung trả lời.
