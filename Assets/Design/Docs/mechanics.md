# CƠ CHẾ GAME (MECHANICS) — MVP
## After the Fall — Survival RTS

**Nguồn:** `prototype.md` (MVP 0.2)
**Mục đích:** liệt kê các cơ chế cần có để vòng lặp *Thu thập → Xây dựng → Tuyển quân → Phòng thủ → Phát triển* chạy được, kèm quy tắc và thông số khởi điểm để cân bằng.

> Mọi con số trong tài liệu là **giá trị khởi điểm đề xuất**, đặt trong ScriptableObject để chỉnh khi playtest.

---

## 1. Bản đồ cơ chế

| # | Cơ chế | Giai đoạn | Phụ thuộc |
|---|---|---|---|
| 1 | Camera | 1 | — |
| 2 | Chọn đơn vị / công trình | 1 | Camera |
| 3 | Ra lệnh (di chuyển, tấn công, thu thập) | 1 | Chọn |
| 4 | Tìm đường & di chuyển | 1 | NavMesh |
| 5 | Tài nguyên & điểm thu thập | 2 | — |
| 6 | Thu thập (Worker) | 2 | Ra lệnh, Tài nguyên |
| 7 | Kho tài nguyên chung | 2 | Thu thập |
| 8 | Đặt công trình | 2 | Kho, NavMesh |
| 9 | Máu & sát thương | 3 | — |
| 10 | Chiến đấu (tấn công, tầm đánh, mục tiêu) | 3 | Ra lệnh, Máu |
| 11 | Tháp canh tự động | 3 | Chiến đấu |
| 12 | AI kẻ địch | 3 | Chiến đấu, NavMesh |
| 13 | Tuyển quân | 4 | Barracks, Kho |
| 14 | Hệ thống đợt tấn công (Wave) | 4 | AI kẻ địch |
| 15 | Thắng / thua | 4 | Wave, HQ |
| 16 | HUD & phản hồi | 4 | Tất cả |
| 17 | Quản lý ván chơi | 4 | Tất cả |

Thứ tự trong bảng cũng là thứ tự nên làm.

---

## 2. Điều khiển & lệnh

### 2.1. Camera
- Isometric, góc nghiêng cố định (50°), orthographic.
- Di chuyển: **WASD**; **giữ chuột giữa và kéo** để trượt bản đồ; chạm mép màn hình để cuộn là tùy chọn.
- Xoay: **giữ chuột phải và kéo ngang** để xoay quanh điểm đang nhìn trên mặt đất (chỉ xoay quanh trục đứng, không đổi góc nghiêng). Nhả chuột phải sau khi kéo không bị tính là click hủy.
- Zoom: con lăn chuột, giới hạn min/max.
- Giới hạn camera trong biên bản đồ.

### 2.2. Chọn
- **Chuột trái** chọn một đối tượng; **kéo chuột** vẽ khung chọn nhiều đơn vị.
- **Shift + chuột trái** thêm/bớt khỏi nhóm chọn.
- Chỉ chọn được đơn vị của người chơi. Công trình chỉ chọn từng cái một.
- Khung chọn nhiều bỏ qua công trình nếu có ít nhất một đơn vị trong khung.
- Đối tượng đang chọn có vòng sáng dưới chân (selection ring).
- Bấm vào đất trống thì bỏ chọn.

### 2.3. Ra lệnh (chuột phải, phụ thuộc đối tượng bị trỏ)

| Trỏ vào | Worker | Rifleman / Heavy Guard |
|---|---|---|
| Mặt đất | Di chuyển | Di chuyển |
| Điểm tài nguyên | **Thu thập** | Di chuyển đến |
| Kẻ địch | Chạy đến (không đánh) | **Tấn công** |
| Công trình phe mình | Di chuyển đến | Di chuyển đến |

- Một lệnh mới thay thế lệnh cũ (không xếp hàng lệnh trong MVP).
- Nhóm nhiều đơn vị: lệnh di chuyển đến cùng một điểm thì rải thành đội hình lưới để không chồng lên nhau.
- Trạng thái đơn vị: `Idle → Moving → Gathering/Attacking`. Xem mục 6.

### 2.4. Tìm đường
- Dùng **NavMesh** (AI Navigation).
- Đơn vị tránh nhau bằng NavMeshAgent avoidance.
- Công trình là vật cản (NavMeshObstacle hoặc bake lại), kẻ địch cũng dùng cùng NavMesh.
- Nếu điểm đích không tới được, đi đến vị trí gần nhất hợp lệ.

---

## 3. Kinh tế

### 3.1. Tài nguyên

| Tài nguyên | Nguồn | Dùng cho |
|---|---|---|
| Gỗ | Cây | Xây công trình |
| Đá | Mỏ đá | Xây công trình kiên cố |
| Thức ăn | Bụi quả / ruộng có sẵn | Tuyển quân |

- Mỗi điểm thu thập có **trữ lượng** hữu hạn (ví dụ cây 100 gỗ, mỏ đá 200, bụi quả 150) và biến mất khi cạn.
- Kho tài nguyên là **một kho chung toàn cục**, không có vận chuyển.
- Tài nguyên không âm; mọi giao dịch phải kiểm tra đủ trước khi trừ.

### 3.2. Thu thập
Worker thu thập theo chu kỳ:

1. Di chuyển đến điểm thu thập (đến trong tầm tương tác).
2. Mỗi **1 giây** lấy ra **X** tài nguyên, cộng thẳng vào kho chung (không có bước mang về).
3. Lặp lại đến khi điểm cạn hoặc bị ra lệnh khác.
4. Khi điểm cạn, tự tìm điểm cùng loại gần nhất trong **bán kính 15m**; nếu không có thì về `Idle`.

Thông số: Worker thu `2/giây`; **Lumber Camp** tăng hiệu suất gỗ `+50%` cho Worker đang thu trong **bán kính 12m** quanh nó.

### 3.3. Đặt công trình

**Lưới xây dựng**
- **1 ô = 1m**. Mỗi công trình có `footprint` riêng (số ô rộng x sâu) khai báo trong `BuildingData`, xem bảng 3.4.
- Mảng `occupied[x, y]` toàn bản đồ là nguồn sự thật duy nhất để kiểm tra chỗ trống (không dùng physics).
- Snap theo footprint: footprint **chẵn** snap vào **góc ô** (`round(p)`), footprint **lẻ** snap vào **tâm ô** (`floor(p) + 0.5`).
- Bóng mờ nhảy theo từng ô, chỉ phần hiển thị được làm mượt.

**Xoay**
- Phím **R** xoay công trình **90°** mỗi lần bấm. Footprint đổi chiều theo (2x3 thành 3x2).
- Hướng xoay được giữ lại cho lần đặt kế tiếp cùng loại công trình trong cùng ván.
- Công trình có footprint vuông (2x2, 3x3) vẫn xoay được để đổi hướng cửa/mặt trước.

**Luồng đặt**
1. Chọn công trình từ menu xây dựng. Hiện **bóng mờ** bám theo chuột, snap vào lưới.
2. Mỗi frame kiểm tra **mọi ô** trong footprint. **Chỉ cần một ô bị block là không cho đặt** (không tự dịch sang vị trí khác). Bóng **đỏ**, ô hỏng tô đỏ đậm; hợp lệ thì **xanh**.
3. Chuột trái xác nhận: kiểm tra lại một lần nữa, trừ tài nguyên, công trình xuất hiện ngay (không có giai đoạn xây, không cần Worker). Sau khi đặt xong, **thoát chế độ đặt** (không đặt liên tiếp).
4. Chuột phải hoặc Esc hủy, không mất tài nguyên.

**Ô bị coi là block khi:**

| Lý do | Cách kiểm tra |
|---|---|
| Đã có công trình | `occupied[x, y]` |
| Có cây, đá hoặc điểm tài nguyên | Đánh dấu ô vào `occupied` lúc spawn |
| Ngoài NavMesh, nước, vách | `NavMesh.SamplePosition` tại tâm ô |
| Dốc hoặc chênh độ cao quá lớn | So độ cao 4 góc ô, lệch hơn 0.5m thì block |
| Có kẻ địch trong khung | Kiểm tra riêng, không cho đặt |

**Đơn vị phe mình đứng trong khung:** vẫn cho đặt, sau đó đẩy đơn vị ra ô trống gần nhất bằng `agent.Warp`.

**Không được chặn kín đường:** trước khi cho đặt, kiểm tra `NavMesh.CalculatePath` từ mỗi điểm spawn kẻ địch đến HQ vẫn tới được. Nếu bị chặn kín thì không cho đặt và báo “Không thể chặn kín đường”.

**Kiểm tra tài nguyên:** không đủ thì bóng **đỏ** và hiện lý do trên HUD, nhưng ô lưới vẫn được hiển thị bình thường để người chơi biết vị trí có hợp lệ hay không.

### 3.4. Công trình

| Công trình | Footprint (ô 1m) | Giá | Máu | Chức năng |
|---|---|---|---|---|
| Headquarters | 4x4 | Có sẵn đầu ván | 1000 | Thua nếu bị phá. Là điểm bắt đầu của Worker |
| Lumber Camp | 2x2 | 50 gỗ | 300 | Tăng hiệu suất thu gỗ quanh nó |
| Barracks | 3x3 | 100 gỗ + 50 đá | 500 | Tuyển Rifleman, Heavy Guard |
| Watchtower | 2x2 | 50 gỗ + 80 đá | 400 | Tự bắn kẻ địch trong tầm |

Footprint ở trên là giá trị khởi điểm, chỉnh trong `BuildingData`.

Tuyển Worker từ **Headquarters** (xem 3.5).

### 3.5. Tuyển quân
- Chọn công trình, bấm nút đơn vị trong bảng thông tin để vào **hàng đợi** (tối đa 5).
- Mỗi đơn vị có thời gian huấn luyện; trừ tài nguyên **ngay khi vào hàng đợi**, hủy thì hoàn lại.
- Đơn vị xuất hiện cạnh công trình, tự đi đến **rally point** (mặc định ngay trước công trình).
- **Giới hạn dân số** cố định (ví dụ 30) để giữ cân bằng; hiện trên HUD.

| Đơn vị | Giá | Thời gian | Từ |
|---|---|---|---|
| Worker | 30 thức ăn | 6s | Headquarters |
| Rifleman | 50 thức ăn | 10s | Barracks |
| Heavy Guard | 80 thức ăn | 14s | Barracks |

---

## 4. Chiến đấu

### 4.1. Máu & sát thương
- Mỗi đối tượng chiến đấu được có `Health` (hiện tại / tối đa).
- Sát thương = `damage` của kẻ tấn công (chưa có giáp, chí mạng, loại sát thương). Máu về 0 thì chết.
- Công trình cũng có máu; HQ về 0 thì thua.
- Không có hồi máu tự động trong MVP.

### 4.2. Chỉ số đơn vị

| Đơn vị | Máu | Tốc độ | Sát thương | Tầm đánh | Tốc độ đánh |
|---|---|---|---|---|---|
| Worker | 40 | 3.5 | — | — | — |
| Rifleman | 60 | 3.5 | 12 | 10m | 1 phát/1.0s |
| Heavy Guard | 160 | 2.5 | 15 | 2m (cận chiến) | 1 đòn/1.2s |
| Kẻ địch cơ bản | 80 | 3.0 | 10 | 2m (cận chiến) | 1 đòn/1.0s |
| Watchtower | 400 | — | 14 | 12m | 1 phát/1.2s |

Vai trò: Heavy Guard chặn phía trước, Rifleman bắn từ phía sau, đó là toàn bộ “chiến thuật” của MVP.

### 4.3. Tấn công
- Lệnh tấn công: đơn vị đuổi theo mục tiêu đến khi vào tầm, dừng lại, quay mặt, đánh theo tốc độ đánh.
- Mục tiêu chết hoặc mất: về `Idle` rồi tự tìm mục tiêu mới (xem 4.4).
- Đạn của Rifleman và tháp canh: tia bắn tức thời (hitscan) kèm hiệu ứng tia đạn là đủ cho MVP; không cần đạn vật lý.

### 4.4. Tự động phản công
- Đơn vị quân `Idle` tự đánh kẻ địch vào **tầm phát hiện** (bằng tầm đánh + 3m), ưu tiên mục tiêu gần nhất.
- Sau khi diệt xong mục tiêu, nếu bị kéo đi quá **15m** khỏi vị trí gốc thì quay về.
- Worker bị tấn công thì không phản kháng; không có cơ chế bỏ chạy trong MVP.

### 4.5. Tháp canh
- Tự chọn mục tiêu gần nhất trong tầm, bắn liên tục, không cần người điều khiển.
- Kẻ địch ưu tiên mục tiêu theo quy tắc ở mục 5, tháp canh không phải mục tiêu ưu tiên.

---

## 5. AI kẻ địch

Máy trạng thái đơn giản: `Spawn → Advance → Engage → (Dead)`.

- **Advance:** đi đến vị trí HQ bằng NavMesh.
- **Engage:** trong lúc đi, nếu có **đơn vị hoặc công trình** của người chơi trong **tầm phát hiện 6m**, chuyển sang tấn công mục tiêu gần nhất. Khi mục tiêu chết thì quay lại `Advance`.
- Ưu tiên: đơn vị > công trình > HQ, chỉ khi đều trong tầm; ngoài ra chọn gần nhất.
- Kẻ địch không rút lui, không đi vòng, không phối hợp.

Hướng xuất hiện: 1–2 **điểm spawn cố định** ở rìa bản đồ, có đánh dấu trên minimap để người chơi biết hướng phòng thủ.

---

## 6. Hệ thống đợt tấn công (Wave)

### 6.1. Luồng
1. Ván bắt đầu có **thời gian chuẩn bị** (ví dụ 90s) với đếm ngược trên HUD.
2. Hết giờ, đợt 1 xuất hiện. Có cảnh báo trước 10s.
3. Giữa các đợt có **thời gian nghỉ** 60s.
4. Đợt cuối: tiêu diệt toàn bộ kẻ địch thì thắng.

### 6.2. Cân bằng ban đầu

| Đợt | Số kẻ địch | Hệ số máu | Hệ số sát thương |
|---|---|---|---|
| 1 | 5 | ×1.0 | ×1.0 |
| 2 | 10 | ×1.2 | ×1.0 |
| 3 | 16 | ×1.4 | ×1.2 |

Kẻ địch xuất hiện rải theo thời gian (ví dụ 1 con mỗi 1–2s) thay vì cùng lúc. Dữ liệu đợt nằm trong một `WaveConfig`, thêm đợt không cần sửa code.

---

## 7. Thắng / thua & quản lý ván

### 7.1. Điều kiện
- **Thắng:** vượt đợt 3 và không còn kẻ địch sống.
- **Thua:** HQ máu về 0.
- Tình huống đặc biệt: HQ bị phá cùng lúc với kẻ địch cuối chết thì tính là **thua**.

### 7.2. Luồng ván chơi
`Menu → Loading → Chuẩn bị → Chiến đấu (đợt 1–3) → Kết thúc (Thắng/Thua) → Chơi lại / Về menu`

- Màn hình kết thúc: kết quả, thời gian chơi, số kẻ địch diệt, số đơn vị mất.
- Chơi lại = nạp lại scene, không cần lưu game.
- Có thể **tạm dừng** (Esc): `Time.timeScale = 0`, vẫn mở được menu.
- Trạng thái ván: một `GameState` toàn cục (`Preparing`, `Playing`, `Won`, `Lost`, `Paused`) để các hệ thống khác đọc.

---

## 8. HUD & phản hồi

| Phần tử | Nội dung |
|---|---|
| Thanh trên | Gỗ, Đá, Thức ăn, dân số hiện tại / tối đa, số đợt, đếm ngược |
| Menu xây dựng | Nút cho 3 công trình có thể xây, hiện giá, tô xám nếu không đủ |
| Bảng thông tin | Tên, máu, các nút hành động (tuyển quân), hàng đợi huấn luyện |
| Minimap | Vị trí quân mình, kẻ địch, điểm spawn, khung nhìn camera |
| Thanh máu | Hiện trên đầu đối tượng khi bị thương hoặc đang được chọn |
| Thông báo | “Đợt 2 sắp đến”, “Không đủ gỗ”, “HQ đang bị tấn công” |

Phản hồi tối thiểu: vòng chọn, âm thanh click, tia đạn, hiệu ứng đơn vị chết, rung/viền đỏ khi HQ bị đánh.

---

## 9. Kiến trúc dữ liệu gợi ý

Dùng **ScriptableObject** cho cấu hình để cân bằng không cần sửa code:

| Asset | Nội dung |
|---|---|
| `UnitData` | máu, tốc độ, sát thương, tầm, tốc độ đánh, giá, thời gian huấn luyện, prefab |
| `BuildingData` | máu, giá, footprint (số ô), chức năng, prefab |
| `ResourceNodeData` | loại, trữ lượng |
| `WaveConfig` | danh sách đợt: số lượng, hệ số, khoảng cách spawn |

Thành phần chính (MonoBehaviour): `Health`, `Selectable`, `UnitController` (state machine), `Gatherer`, `Attacker`, `ResourceNode`, `Building`, `ProductionQueue`, `EnemyAI`, `WaveManager`, `ResourceManager`, `GameManager`.

Các hệ thống giao tiếp qua **event** (ví dụ `OnResourceChanged`, `OnUnitDied`, `OnWaveStarted`) để HUD và logic tách rời.

---

## 10. Ngoài phạm vi MVP (nhắc lại)

Chưa làm: multiplayer, ngoại giao, nhiệm vụ, thời tiết, đói khát, chế tạo, nâng cấp, nhiều phe, lưu game, xếp hàng lệnh, giáp/loại sát thương, kỹ năng, sương mù chiến tranh, giai đoạn xây công trình có Worker.

## 11. Câu hỏi cần chốt trước khi code

1. Worker có tự đánh trả hay hoàn toàn bất lực khi bị tấn công?
2. Có cần sương mù chiến tranh (fog of war) không, hay thấy toàn bản đồ?
3. Công trình xây tức thì (như tài liệu này giả định) hay có thanh tiến độ xây?
4. Thức ăn lấy từ bụi quả hữu hạn hay từ ruộng thu thập vô hạn? Bản này chọn hữu hạn.
5. Có giới hạn dân số cứng (giả định 30) không?
