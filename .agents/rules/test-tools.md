---
trigger: always_on
---

quy tắc về công cụ test và tính năng giao diện so với game gốc (SLS2):

- không thêm nút test/cheat (như Teleport dịch chuyển nhanh, cộng tiền, tua giờ...) trực tiếp vào UI trong game nếu game gốc (SLS2) không có. Ví dụ Bản đồ trong game (`Map.uxml`, `MapView.cs`, `PhoneMapApp.cs`) chỉ dùng để xem vị trí và bật/tắt chỉ dẫn 3D giống `MapPanel` của SLS2.

- mọi tính năng phục vụ việc test nhanh trong quá trình làm game (dịch chuyển nhanh tới các địa điểm, chỉnh tiền, tua thời gian, hồi đầy chỉ số sinh tồn, tháo/lắp thử linh kiện PC, test thanh cảnh báo...) bắt buộc gom vào menu `Debug` trên thanh menu chính của Unity Editor (`Assets/Scripts/Editor/DebugWindow.cs`, phím tắt `Ctrl + Shift + T`). Khi thêm địa điểm hoặc hệ thống mới cần test thì cập nhật vào `DebugWindow.cs`.
