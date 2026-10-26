---
trigger: always_on
---

quy tắc UI Toolkit bắt buộc để tránh lỗi lặp lại:

- chống co ép nút và thanh tab khi chuyển tab có nội dung dài: mọi header, thanh tab, footer và nút bấm (Button, .wf-button, .wf-tab) bắt buộc có flex-shrink: 0. Mọi ScrollView hoặc vùng danh sách co giãn nằm trong flex-direction: column bắt buộc đặt đủ flex-grow: 1; flex-shrink: 1; flex-basis: 0; min-height: 0; (và khung cha có min-height: 0) để nội dung dài không ép lùn các nút xung quanh.

- chống nhảy chữ trên nút tab active: khi tăng border-bottom-width ở trạng thái active (ví dụ từ 2px lên 4px trên .wf-tab--active), bắt buộc giảm padding-bottom tương ứng (từ 8px xuống 6px) hoặc cố định height để chiều cao nút và vị trí chữ không bị lệch khi bấm chuyển tab.

- chống méo hình khi phóng to: mọi phần tử hiển thị ảnh (background-image) phải đặt -unity-background-scale-mode: scale-to-fit (hoặc scale-and-crop), không để mặc định stretch-to-fill.

- đa ngôn ngữ trên Unity 6 UI Toolkit: SetValueWithoutNotify trên TextElement không cập nhật thuộc tính text của Button, phải luôn kiểm tra và gán lại textEl.text = translated nếu khác. Với DropdownField, luôn kiểm tra evt.target == dropdown trong RegisterValueChangedCallback.


- UI prototype hiện tại luôn thiết kế đơn giản không màu mè, dạng prototype này là wireframe màu xám, xóa các dạng style khác,đang ở trạng thái prototype nên giao diện nên lun thiết kế kiểu dạng style wireframe xám không màu mục tiêu chính là layout phải chuẩn

- giao diện luồng game mới (loading, menu, multiplayer, HUD trận đại chiến) dựng bằng code với `BattleUiKit` (`Assets/Scripts/Runtime/UI/Battle/BattleUiKit.cs`) theo theme wireframe xám sáng lấy từ dự án Godot gd-01 (`D:\Godot\Projects\gd-01\Arts\UI\wireframe_theme.tres`): nền xám nhạt, panel trắng ngà viền 2px bo góc, nút chính nền tối chữ sáng, ô logo/hình nền là ô giữ chỗ xám. Màu và bo góc nằm ở một chỗ trong `BattleUiKit`, thêm màn mới thì dùng lại các hàm của nó (Panel, PushButton, Heading, Placeholder, ProgressBar, ConfirmDialog), không tự đặt màu riêng

- luôn dùng TextMeshPro (TextMeshProUGUI / TMP_Text) cho mọi thành phần chữ trên UI, cấm dùng UnityEngine.UI.Text thường để tránh chữ bị mờ nhòe; font TMP_FontAsset phải bật chế độ Dynamic (hoặc nạp đủ bảng mã tiếng Việt) để hiển thị rõ nét tiếng Việt có dấu.