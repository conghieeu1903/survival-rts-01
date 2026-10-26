---
trigger: always_on
---

dự án này là game RTS multiplayer tối đa 4 người chơi (4 đội), mỗi đội hàng nghìn lính (mặc định 4.000, tổng 16.000), mạng theo mô hình lockstep: mọi máy tự mô phỏng toàn bộ trận, chỉ gửi lệnh của người chơi qua mạng, không đồng bộ vị trí hay trạng thái từng lính

công nghệ:
- mạng: FishNet 4.6.22 với transport Tugboat, chỉ dùng Broadcast (không dùng NetworkObject, RPC, SyncVar). Host làm relay: gom lệnh theo lượt (10 lượt mỗi giây) rồi phát cho mọi máy. Mã nằm ở `Assets/Scripts/Runtime/Network/`
- mô phỏng: NativeArray + job Burst, số nguyên cố định (1 m = 256 đơn vị), không dùng ECS entity. Mã nằm ở `Assets/Scripts/Runtime/Battle/`
- hiển thị: Entities Graphics (ECS) với animation GPU (`GpuSkinnedLit`), chỉ đọc kết quả mô phỏng và nội suy giữa hai lượt
- âm thanh: `AudioDirector` có sẵn, trận đại chiến nối vào qua `BattleAudio`
- kiểm tra đồng bộ: mỗi 10 lượt mọi máy gửi checksum trạng thái lên host, lệch thì báo desync

quy tắc khi sửa mô phỏng (mọi thứ ảnh hưởng tới trạng thái trận):
- chỉ dùng số nguyên cố định; không dùng float, `Time`, `Random`, `Physics`, `NavMesh` hay thứ tự duyệt không cố định trong vòng mô phỏng
- kết quả không được phụ thuộc thứ tự luồng: job song song chỉ ghi vào ô của chính nó, việc gộp (như trừ máu) làm tuần tự theo chỉ số
- mọi hành động của người chơi phải đi qua `LockstepNetwork.SendCommand` rồi mới tác động lên mô phỏng ở `ApplyTurn`; không sửa trạng thái mô phỏng trực tiếp từ UI hay input
- hình ảnh, âm thanh, tia mũi tên chỉ là trình diễn, không được ghi ngược vào trạng thái mô phỏng
- thêm trường vào trạng thái mô phỏng thì phải đưa vào `BattleChecksumJob`
- sau khi sửa mô phỏng phải kiểm tra lại không desync: chạy cùng chuỗi lệnh hai lần so checksum, và chạy nhiều máy (Editor làm host cùng các bản build `-join <ip> -bot`) so checksum từng mốc

lưu ý:
- đã vá khoảng 14 chỗ trong `Assets/FishNet/Runtime` (SceneHandle, GetInstanceID) để compile trên Unity 6000.6; nâng cấp FishNet sẽ mất các bản vá này
- chưa có game PvP đầy đủ: kinh tế, xây dựng, dân số, trại lính vẫn là code đơn (MonoBehaviour) trong scene `prototype`, chưa vào lockstep
- tham số dòng lệnh của bản build: `-host` mở phòng, `-join <địa chỉ>` vào phòng, `-bot` để máy đó tự điều quân
