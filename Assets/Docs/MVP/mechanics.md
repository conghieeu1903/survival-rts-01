# CƠ CHẾ GAME (MECHANICS) — MVP
## Medieval Kingdom

**Nguồn:** `prototype.md` (MVP 1.0)
**Mục đích:** liệt kê cơ chế và quy tắc của vòng lặp *Thu thập → Xây dựng → Huấn luyện → Chiến đấu*, kèm thông số khởi điểm.

> Mọi con số là giá trị khởi điểm, nằm trong ScriptableObject (`BattleData`, `BuildingData`, `SoldierData`, `TowerData`, `EnemyData`) để chỉnh khi playtest.
> Ký hiệu: ✅ đã có · 🔶 có một phần · ⬜ chưa làm.

---

## 1. Bản đồ cơ chế

| # | Cơ chế | Offline | Trận lockstep |
|---|---|---|---|
| 1 | Camera | ✅ | ✅ |
| 2 | Chọn và ra lệnh | ✅ | ✅ (theo tiểu đội) |
| 3 | Đặt công trình trên lưới | ✅ | ⬜ |
| 4 | Thu thập tài nguyên, dân số | ✅ | ⬜ |
| 5 | Huấn luyện quân | ✅ (dân → lính, Trại lính) | ✅ (vàng, theo tiểu đội) |
| 6 | Chiến đấu | ✅ | ✅ |
| 7 | AI đối phương | ✅ quái ECS | ✅ `BattleBot` |
| 8 | Đợt tấn công (Wave) | ⬜ | không dùng |
| 9 | Thắng / thua | 🔶 chỉ có thua | ✅ |
| 10 | Mạng, đồng bộ | không dùng | ✅ |
| 11 | Âm thanh | ✅ | ✅ |
| 12 | Lưu game | ⬜ | không dùng |

---

## 2. Điều khiển

### 2.1. Camera
- Isometric, góc nghiêng cố định, WASD di chuyển, con lăn zoom, kéo chuột giữa để trượt, chuột phải kéo để xoay quanh trục đứng, giới hạn trong biên bản đồ.
- Bấm minimap để nhảy camera; bản đồ lớn mở bằng **M**.

### 2.2. Chọn và ra lệnh
- Offline: chuột trái chọn đơn vị hoặc công trình (`BuildingSelector`), hiện thanh máu và vòng tầm. Lệnh là một lệnh thay một lệnh, không xếp hàng.
- Trận lockstep: đơn vị nhận lệnh là **tiểu đội 100 lính**. Lệnh `MoveSquad` đưa tiểu đội về đích, đội hình tự dàn theo khoảng cách `formationSpacing` (1,2 m). Lính tự đánh địch trong tầm phát hiện rồi tiếp tục đi.
- Mọi lệnh của người chơi phải đi qua `LockstepNetwork.SendCommand`, không sửa trạng thái mô phỏng trực tiếp từ UI hay input.

### 2.3. Phím tắt (offline)
W A S D camera · cuộn chuột zoom · chuột trái đặt · **R** xoay · chuột phải huỷ · **X** tháo dỡ · **M** bản đồ lớn · **Esc** menu.

---

## 3. Kinh tế

### 3.1. Offline: tài nguyên và dân
| Tài nguyên | Nguồn | Dùng cho |
|---|---|---|
| Gỗ | Cây (Trại gỗ) | Xây công trình |
| Đá | Mỏ đá (Xưởng đá) | Công trình kiên cố |
| Sắt | Quặng (Mỏ sắt) | Công trình, quân |
| Lúa | Nông trại, bụi quả | Nuôi dân, huấn luyện |
| Vàng | Kho | Huấn luyện, xây |

- Kho chung `PopulationManager`; mọi giao dịch kiểm tra đủ trước khi trừ, không âm.
- Dân tự do sinh nhờ **Nhà dân** (giới hạn dân số theo số nhà). Gán thợ vào nhà khai thác bằng nút +/−; `HarvesterUnit` tự đi thu và mang về kho.
- Điểm tài nguyên (`ResourceNode`) 🔶: đã có cây, đá, quặng, bụi quả; **trữ lượng hữu hạn và cạn thì biến mất chưa làm** ⬜.

### 3.2. Trận lockstep: vàng
| Thông số | Giá trị |
|---|---|
| Vàng đầu trận | 400 |
| Thu nhập | +10 vàng/giây mỗi đội (số nguyên) |
| Giá huấn luyện | 200 vàng / tiểu đội |
| Thời gian huấn luyện | 15 giây / tiểu đội |
| Tiểu đội dự trữ | 10 mỗi loại lính, mỗi đội |

Huấn luyện là lệnh lockstep, trừ vàng khi `ApplyTurn` xử lý, nên mọi máy trừ giống nhau.

### 3.3. Hướng tới thống nhất kinh tế ⬜
Mục tiêu giai đoạn 4: đưa kinh tế, xây dựng, dân số vào lockstep (xem `tech.md` mục 5). Cho tới đó hai chế độ giữ hai mô hình kinh tế riêng.

---

## 4. Xây dựng (offline)

### 4.1. Lưới
- 1 ô = 1 m. Mỗi công trình có `footprint` trong `BuildingData`. Mảng chiếm ô `BuildGrid` là nguồn sự thật, không dùng physics.
- Footprint chẵn snap vào góc ô, lẻ snap vào tâm ô.

### 4.2. Luồng đặt
1. Chọn công trình từ menu xây dựng (tab Kinh tế / Quân sự / Phòng thủ), hiện bóng mờ bám chuột.
2. Mỗi frame kiểm tra mọi ô. **Chỉ một ô hỏng là đỏ**, không tự dịch vị trí.
3. Chuột trái xác nhận: trừ tài nguyên, tạo **công trường**; thợ xây tới xây qua 3 giai đoạn khung theo `BuildingData`.
4. Chuột phải hoặc Esc huỷ, không mất tài nguyên. Xong một công trình thì thoát chế độ đặt.

### 4.3. Ô bị coi là block
Đã có công trình · có cây/đá/điểm tài nguyên · ngoài NavMesh, nước, vách · dốc hoặc chênh độ cao quá 0,5 m · có kẻ địch trong khung.

Đơn vị phe mình đứng trong khung: vẫn cho đặt, sau đó đẩy ra ô trống gần nhất 🔶 (đã kiểm tra, **chưa đẩy đơn vị**).

### 4.4. Chặn đường
Chi phí flow field: ô trống 1, ô có công trình phe mình ≈ 20 (không chặn hẳn), cây/đá/nước chặn hẳn. Người chơi **được phép chặn kín**: quái đi tới công trình chắn và tấn công nó. Vì vậy quy tắc cấm chặn kín đường không dùng.

### 4.5. Tháo dỡ
Phím **X**, có thời gian tháo (`DemolishSite`), hoàn lại chi phí.

### 4.6. Danh mục công trình hiện có
| Công trình | Chức năng |
|---|---|
| Headquarters | Trung tâm, đích của quái, phá là thua; tuyển dân |
| Nhà dân | Tăng giới hạn dân |
| Thợ xây (Builder's Hut) | Thợ xây công trường |
| Trại gỗ / Xưởng đá / Mỏ sắt / Nông trại | Nhà khai thác, gán thợ +/− |
| Trại lính | Huấn luyện Lính kiếm từ dân tự do (hàng chờ, tối đa 6 mỗi trại) |
| Trường bắn cung | Huấn luyện Cung thủ |
| Tháp canh | Tự chọn mục tiêu và bắn, có tia đạn |

Giá, máu, footprint từng công trình xem `BuildingData`.

---

## 5. Chiến đấu

### 5.1. Chỉ số loại lính (trận lockstep)
| Loại | Tiểu đội/đội | Máu | Sát thương | Nhịp đánh | Tầm đánh | Phát hiện | Tốc độ |
|---|---|---|---|---|---|---|---|
| Lính kiếm | 28 | 140 | 22 | 0,9 s | 1,1 m | 8 m | 3,2 m/s |
| Lính cung | 12 | 70 | 12 | 1,4 s | 14 m | 16 m | 3,0 m/s |

Bán kính thân 0,4 m, lính đẩy nhau để không chồng. Lính kiếm xếp hàng đầu, cung thủ hàng sau.

### 5.2. Quy tắc
- Sát thương = `damage` của kẻ đánh, chưa có giáp, chí mạng hay loại sát thương. Máu về 0 thì chết, xác nằm lại 3 giây rồi biến mất.
- Mỗi đòn có `strikeDelay`: sát thương được tính giữa đòn chứ không phải lúc vung.
- Cung thủ bắn trúng tức thời (hitscan trong mô phỏng); tia mũi tên chỉ là hiển thị.
- Gộp sát thương làm tuần tự theo chỉ số để kết quả không phụ thuộc thứ tự luồng.

### 5.3. Offline: quái và tháp
- Quái ECS: spawn theo vùng tròn, flow field về HQ, tầm phát hiện nhỏ, ưu tiên đơn vị > công trình > HQ, không rút lui, không phối hợp.
- Lính kiếm phe mình tự đánh quái trong tầm canh gác, quay về nếu bị kéo quá xa.
- Tháp canh: bắn mục tiêu gần nhất trong tầm.

---

## 6. Thắng / thua

| Chế độ | Thắng | Thua |
|---|---|---|
| PvP | Đội cuối cùng còn lính | Đội hết lính |
| Offline | Sống sót hết các đợt tấn công và diệt quái cuối ⬜ | Headquarters máu về 0 ✅ |

Màn Kết quả: kết quả, đội thắng, nút Chơi lại / Về phòng chờ / Thoát (PvP).

### 6.1. Đợt tấn công offline ⬜
Thiết kế: thời gian chuẩn bị 90 s, cảnh báo trước 10 s, nghỉ 60 s giữa các đợt, ba đợt, cấu hình trong một `WaveConfig` (ScriptableObject) để thêm đợt không sửa code.

| Đợt | Số quái | Hệ số máu | Hệ số sát thương |
|---|---|---|---|
| 1 | 5 | ×1,0 | ×1,0 |
| 2 | 10 | ×1,2 | ×1,0 |
| 3 | 16 | ×1,4 | ×1,2 |

Quái spawn rải theo thời gian (1 con mỗi 1–2 giây), không cùng lúc.

---

## 7. Mạng (PvP)

- Tối đa 4 đội. Host là relay, gom lệnh theo lượt (10 lượt/giây) rồi phát cho mọi máy.
- Máy chậm làm cả phòng tạm dừng (`PauseMessage`), host biết đội nào gây chậm.
- Mỗi 10 lượt mọi máy gửi checksum, lệch thì báo desync và kết thúc trận.
- Đội trống bù bằng AI (`BattleBot`, `AiMask`).
- Người chơi mất kết nối: quay về menu kèm thông báo.

---

## 8. HUD và phản hồi

| Phần tử | Nội dung |
|---|---|
| Thanh trên | Tài nguyên (offline) hoặc vàng, lượt, đội (PvP) |
| Menu xây dựng | Tab công trình, hiện giá, xám khi thiếu |
| Bảng thông tin | Tên, máu, hành động, hàng chờ huấn luyện, tiến độ |
| Minimap | Quân, kẻ địch, khung nhìn; bản đồ lớn |
| Thanh máu | Hiện khi chọn hoặc bị thương |
| Thông báo | Không đủ tài nguyên, đợt sắp tới, HQ bị tấn công ⬜ |

Phản hồi tối thiểu có: vòng chọn, âm thanh giao diện, tia đạn, hoạt ảnh chết, âm đám đông theo mật độ.

---

## 9. Dữ liệu cấu hình

| Asset | Nội dung |
|---|---|
| `BattleData` | Kích thước tiểu đội, bản đồ, màu đội, kinh tế vàng, các `BattleUnitType` |
| `BuildingData` | Máu, giá, footprint, chức năng, prefab |
| `SoldierData` / `TowerData` / `EnemyData` | Chỉ số lính, tháp, quái ở chế độ offline |
| `GpuAnimationData` | Animation GPU đã nướng |
| `SoundLibrary` | Âm thanh, mixer |
| `WaveConfig` ⬜ | Danh sách đợt |

Các hệ thống giao tiếp qua event để HUD tách khỏi logic.

---

## 10. Câu hỏi cần chốt

1. Offline giữ kinh tế dân + thợ, hay chuyển sang vàng đơn giản như PvP? (Chơi đơn sẽ chạy chung lockstep, xem `online-eos.md`.)
2. ~~Xây dựng trong PvP~~ Đã chốt: PvP có xây dựng, kiểm thử cả hai loại vật thể chặn và tự biến mất (`building-placement.md`).
3. ~~Online tìm phòng bằng dịch vụ nào?~~ Đã chọn EOS (`online-eos.md`).
4. Có cần sương mù chiến tranh không, hay thấy toàn bản đồ?
5. Giới hạn dân số cứng bao nhiêu ở chế độ offline?
