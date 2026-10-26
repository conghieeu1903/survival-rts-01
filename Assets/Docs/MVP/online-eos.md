# CHƠI ĐƠN VÀ MẠNG, TÌM PHÒNG BẰNG EOS — MVP
## Medieval Kingdom

**Liên quan:** `tech.md` (lockstep), `mechanics.md` mục 7, `roadmap.md` giai đoạn 4.
Ký hiệu: ✅ đã có · 🔶 một phần · ⬜ chưa làm.

---

## 1. Chơi đơn trong game có chế độ mạng

Cách làm phổ biến (RTS như StarCraft, Age of Empires): **chơi đơn chạy cùng đường mô phỏng với chơi mạng**, chỉ khác là không có người chơi từ xa.

- Chơi đơn = một "host" tại máy mình, các đội còn lại là AI. Lệnh vẫn đi qua `LockstepNetwork.SendCommand`, nhưng đi vòng nội bộ (loopback), không qua mạng thật.
- Lợi ích: **chỉ có một bản mô phỏng**. Nhà xoay, địa hình, kinh tế, huấn luyện chỉ viết một lần, và chơi đơn kiểm thử luôn phần mạng.
- Nhược điểm: phải viết kinh tế và xây dựng bằng số nguyên cố định ngay từ đầu (xem `tech.md` mục 5).

Hiện tại: offline (`prototype`) dùng code riêng bằng `float`, PvP (`battle`) dùng lockstep. Quyết định: **chuyển offline sang chạy trên lockstep** theo mô hình trên. Trong thời gian chuyển, offline cũ vẫn giữ để thử nghiệm.

| Chế độ | Mô hình |
|---|---|
| Chơi đơn | Host nội bộ + AI, loopback |
| LAN | Host + client qua `LanDiscovery`, Tugboat |
| Online | Host + client, tìm phòng qua EOS |

## 2. Tìm phòng online bằng EOS (Epic Online Services)

**Quyết định:** dùng EOS cho tìm phòng, lời mời, session. (Đã có `SteamManager` cho Steam; có thể thêm Steam sau, không loại trừ.)

EOS **không tự đồng bộ gameplay và không thay FishNet**. Nó chỉ lo:
- đăng nhập (Epic Account, hoặc đăng nhập thiết bị cho thử nghiệm),
- **Lobbies / Sessions**: tạo phòng, danh sách phòng, tìm kiếm theo thuộc tính, tham gia,
- lời mời bạn bè,
- **relay/NAT traversal** (P2P) để host không cần mở cổng.

Vì mình dùng lockstep, host chỉ là relay lệnh. Đường đi của dữ liệu:

```
Menu "Tham gia máy chủ"  ──►  IRoomProvider  ──►  EOS Lobbies   (danh sách, tạo, tìm, vào)
                                     │
                                     ▼
                              địa chỉ/peer của host
                                     │
                                     ▼
                  FishNet transport  ──►  LockstepNetwork (Broadcast, như hiện tại)
```

### 2.1. Việc cần làm
| # | Việc | Trạng thái |
|---|---|---|
| 1 | Giao diện `IRoomProvider`: `CreateRoom`, `ListRooms`, `JoinRoom`, `LeaveRoom` (ẩn cài đặt sau giao diện) | ⬜ |
| 2 | Bản `DirectAddressRoomProvider` (địa chỉ nhập tay / LAN, đang có sẵn) bọc lại theo giao diện | 🔶 |
| 3 | Bản `EosRoomProvider` | ⬜ |
| 4 | Đăng ký ứng dụng trên Epic Developer Portal (ProductId, SandboxId, DeploymentId, ClientId/Secret) | ⬜ |
| 5 | Cài EOS SDK (gói C# hoặc plugin cộng đồng) | ⬜ |
| 6 | **Transport EOS cho FishNet** (P2P): chọn gói có sẵn hoặc tự bọc | ⬜ |
| 7 | Đăng nhập, lưu thông tin | ⬜ |
| 8 | Giao diện danh sách phòng thật trong màn "Tham gia máy chủ" (lọc, làm mới, độ trễ) | ⬜ |
| 9 | Kiểm thử qua NAT, hai máy khác mạng | ⬜ |

### 2.2. Rủi ro
- **Transport EOS cho FishNet** là phần khó nhất. Cần chọn gói đã hỗ trợ FishNet 4.x hoặc tự viết. Prototype riêng trước khi xây giao diện.
- Dự án đã vá khoảng 14 chỗ trong `Assets/FishNet/Runtime` để compile trên Unity 6000.6; nâng cấp FishNet sẽ mất vá.
- Cần Epic Account cho người chơi, hoặc dùng đăng nhập thiết bị khi thử nghiệm.
- Nếu P2P qua EOS không đủ ổn định, phương án dự phòng: dedicated server hoặc relay riêng.

### 2.3. Thuộc tính phòng (lobby attributes)
Tên phòng, host, số người / tối đa, cỡ bản đồ, seed (nếu công khai), phiên bản game (để chặn vào phòng khác phiên bản, tránh desync), ping ước lượng.

## 3. Lưu ý lockstep khi vào phòng
- Mọi máy phải cùng phiên bản mô phỏng và dữ liệu cấu hình (`BattleData`...). Gửi hash phiên bản khi vào phòng; lệch thì từ chối.
- Vào lại giữa trận chưa hỗ trợ ⬜ (cần gửi snapshot hoặc phát lại chuỗi lệnh).
