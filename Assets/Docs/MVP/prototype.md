# MVP GAME DESIGN DOCUMENT
## Medieval Kingdom — Build, Defend, Conquer

**Phiên bản:** MVP 1.0
**Nguồn ý tưởng:** `Assets/Docs/mô tả game.md`
**Thể loại:** Medieval City Builder + Survival RTS + Multiplayer PvE/PvP
**Đồ họa:** Stylized low poly 3D, camera isometric
**Nền tảng:** PC (ưu tiên Steam)
**Engine:** Unity 6 (6000.6), C#, URP, Entities, FishNet

Ký hiệu trạng thái: ✅ đã có trong project · 🔶 có một phần · ⬜ chưa làm.

---

## 1. Mục tiêu của MVP

MVP không phải game hoàn chỉnh. Nó phải **chứng minh ba rủi ro lớn nhất chạy được cùng nhau** trước khi mở rộng nội dung:

| Rủi ro | Câu hỏi MVP phải trả lời | Trạng thái |
|---|---|---|
| Quân đội đông | Mô phỏng và vẽ hàng chục nghìn lính ở tốc độ khung hình ổn định? | ✅ 4 đội × 4.000 lính (16.000) |
| Multiplayer | Nhiều người chơi cùng một trận mà không lệch trạng thái? | ✅ lockstep + checksum, LAN; online qua địa chỉ |
| Xây dựng + kinh tế | Công trình và tài nguyên ảnh hưởng thật tới trận đánh? | 🔶 chạy ở chế độ offline, chưa vào lockstep |

Rủi ro thứ tư của bản định hướng gốc, **xây dựng tự do kiểu Tiny Glade** (tường vẽ bằng spline, mesh procedural), **không nằm trong MVP**. MVP dùng đặt công trình theo lưới 1 m. Hệ thống tường tự do là giai đoạn sau MVP (xem `roadmap.md`, giai đoạn 6).

## 2. Vòng lặp chơi

**Thu thập tài nguyên → Xây dựng → Huấn luyện quân → Phòng thủ / Tấn công → Phát triển.**

Hai chế độ chạy trên cùng nền tảng:

| Chế độ | Scene | Nội dung |
|---|---|---|
| Offline (PvE) | `prototype` | Một người chơi: kinh tế dân + thợ, đặt công trình, tháp, quân phe mình chống đợt quái ECS |
| LAN / Online (PvP) | `battle` | Tối đa 4 đội, mỗi đội 4.000 lính, lockstep; đội AI bù chỗ trống; huấn luyện tiểu đội bằng vàng |

Luồng màn hình: `init` (loading) → `menu` → `prototype` (offline) hoặc `battle` (LAN/Online). Menu chính có Chơi offline, Chơi LAN, Chơi online (danh sách máy chủ), Bản lưu, Cài đặt, Credits, Thoát. Chi tiết giao diện nằm ở `docs/figma/`.

## 3. Tính năng cốt lõi của MVP

### 3.1. Điều khiển RTS ✅
Chọn bằng chuột trái, kéo khung chọn nhiều, chuột phải ra lệnh, camera WASD + con lăn. Trong trận lockstep, đơn vị nhận lệnh là **tiểu đội** (100 lính), không phải từng lính.

### 3.2. Tài nguyên
| Chế độ | Tài nguyên |
|---|---|
| Offline | Gỗ, đá, sắt, lúa, vàng (kho chung, thợ thu thập) |
| Trận lockstep | Vàng duy nhất: vốn đầu 400, +10 mỗi giây, trả cho huấn luyện tiểu đội |

Thống nhất kinh tế của hai chế độ là việc lớn của giai đoạn 4 (`roadmap.md`).

### 3.3. Xây dựng căn cứ 🔶
Đặt công trình trên lưới 1 m, xoay 90°, kiểm tra ô, công trường có thợ xây, tháo dỡ hoàn chi phí. Công trình hiện có: Headquarters, Nhà dân, Thợ xây, Trại gỗ, Xưởng đá, Mỏ sắt, Nông trại, Trại lính, Trường bắn cung, Tháp canh.

### 3.4. Quân đội ✅
| Loại | Vai trò | Số tiểu đội / đội | Máu | Sát thương | Tầm |
|---|---|---|---|---|---|
| Lính kiếm | Cận chiến, hàng đầu | 28 | 140 | 22 | 1,1 m |
| Cung thủ | Bắn xa, hàng sau | 12 | 70 | 12 | 14 m |

Mỗi tiểu đội 100 lính; mỗi đội 40 tiểu đội = 4.000 lính. Mỗi loại có thêm tiểu đội dự trữ để huấn luyện.

### 3.5. Kẻ địch
- Offline: quái ECS (Grunt) spawn theo vùng tròn, đi theo flow field về Headquarters, đánh mục tiêu trong tầm. Đã đo 10.000–100.000 con.
- PvP: kẻ địch là đội khác (người hoặc AI `BattleBot`).

### 3.6. Điều kiện thắng / thua
- PvP: đội cuối cùng còn lính thì thắng (`WinnerTeam`), màn Kết quả cho Chơi lại / Về phòng chờ / Thoát.
- Offline: thua khi Headquarters bị phá (sự kiện đã có); **thắng và đợt tấn công (`WaveManager`) chưa có** ⬜.

### 3.7. Multiplayer ✅ (LAN) / 🔶 (Online)
Host làm relay, mô phỏng lockstep 10 lượt/giây, mọi lệnh đi qua `LockstepNetwork.SendCommand`. Phát hiện lệch bằng checksum mỗi 10 lượt, tạm dừng khi có máy chậm. Online hiện kết nối trực tiếp tới địa chỉ lưu trong "Tham gia máy chủ"; **tìm phòng qua dịch vụ (Steam/EOS/Unity Lobby) chưa làm** ⬜.

## 4. Không làm trong MVP

Tường/cổng vẽ tự do bằng spline và mesh procedural; kỵ binh và máy công thành; nâng cấp công trình; dân cư có nhu cầu (đói, nhà ở chi tiết); co-op chung một vương quốc; ranked/matchmaking; sửa chữa công trình sau trận; lưu game giữa trận (menu Bản lưu chỉ là giao diện rỗng); thời tiết, mùa; nhiệm vụ cốt truyện; sương mù chiến tranh.

## 5. Giao diện tối thiểu

uGUI + TextMeshPro dựng bằng code qua `BattleUiKit`, phong cách wireframe xám (xem `.agents/rules/ui-kit.md`). Màn chơi dùng một giao diện chung cho offline và online (thiết kế "Màn chơi" trong Figma): thanh tài nguyên, menu xây dựng, minimap, bảng thông tin, trạng thái đội và tiểu đội. Menu Esc trong game dùng cùng panel Cài đặt như menu chính.

## 6. Tiêu chí hoàn thành MVP

MVP đạt khi **cả hai** điều kiện thoả:

1. **Offline:** bắt đầu ván mới, thu thập, xây căn cứ, huấn luyện quân, chống đủ đợt tấn công và thấy màn Thắng hoặc Thua, không lỗi nghiêm trọng.
2. **PvP:** 2–4 máy vào cùng phòng, chơi trọn một trận từ đầu đến khi có đội thắng, huấn luyện thêm quân bằng vàng, **không desync** (checksum khớp suốt trận), thoát về phòng chờ được.

Ngưỡng hiệu năng: 60 FPS ở 16.000 lính trên máy PC tầm trung, cấu hình mục tiêu ghi trong `tech.md`.

**Nguyên tắc:** chỉ thêm tính năng mới khi vòng lặp hiện tại đã ổn định và thú vị. Mọi con số là giá trị khởi điểm, nằm trong ScriptableObject để chỉnh khi playtest.
