# LỘ TRÌNH PHÁT TRIỂN — MVP
## After the Fall — Survival RTS

**Nguồn:** `prototype.md` (thứ tự giai đoạn), `mechanics.md` (chi tiết cơ chế)
**Mục đích:** thứ tự các chức năng cần làm, mỗi bước có tiêu chí test để biết khi nào được sang bước kế tiếp.

**Quy tắc:** mỗi bước phải chạy được và test được trước khi làm bước sau. Chỉ thêm tính năng mới khi vòng lặp hiện tại đã ổn định.

---

## Tổng quan

| Giai đoạn | Nội dung | Bước |
|---|---|---|
| 0 | Nền tảng | 1–4 |
| 0.5 | Đặt và xoay công trình (làm sớm) | 0.5a–0.5f |
| 1 | Điều khiển | 5–7 |
| 2 | Kinh tế | 8–13 |
| 3 | Chiến đấu | 14–20 |
| 4 | Hoàn thiện vòng lặp | 21–26 |
| 5 | Art pass & cân bằng | 27–28 |

---

## Giai đoạn 0 — Nền tảng

Làm một lần, các giai đoạn sau đều dựa vào.

| # | Việc | Ghi chú |
|---|---|---|
| 1 | Scene thử nghiệm | Mặt đất, bake NavMesh, ánh sáng cơ bản |
| 2 | Cấu trúc code | Thư mục `Scripts/`, ScriptableObject: `UnitData`, `BuildingData`, `ResourceNodeData`, `WaveConfig` |
| 3 | `GameManager` + `GameState` | `Preparing`, `Playing`, `Won`, `Lost`, `Paused` |
| 4 | Hệ thống event | `OnResourceChanged`, `OnUnitDied`, `OnWaveStarted`... để HUD và logic tách rời |

**Xong khi:** scene chạy được, có thể đổi `GameState` và các hệ thống khác nhận được event.

---

## Giai đoạn 0.5 — Ưu tiên: đặt và xoay công trình

Đây là cơ chế đặc trưng và rủi ro kỹ thuật cao nhất (lưới, NavMesh, kiểm tra ô), nên làm sớm để biết sớm có khả thi không. Bước 12 của giai đoạn 2 được **kéo lên đây**, chỉ làm phần đặt và xoay, các phần phụ thuộc kinh tế tạm giả lập.

| # | Việc | Ghi chú |
|---|---|---|
| 0.5a | Camera tối thiểu | Chỉ cần WASD và zoom để nhìn được chỗ đặt (bước 5 rút gọn) |
| 0.5b | `BuildingData` + prefab thử | 3 khối: 2x2, 3x3, 2x3 (để thử xoay chiều) |
| 0.5c | `BuildGrid` | Lưới 1m, mảng `occupied[x, y]`, vẽ lưới khi đang đặt |
| 0.5d | `BuildingPlacer` | Bóng mờ bám chuột, snap theo footprint chẵn/lẻ, xanh/đỏ, chuột trái đặt, chuột phải/Esc hủy |
| 0.5e | Xoay bằng R | Xoay 90°, footprint đổi chiều, giữ hướng cho lần đặt sau |
| 0.5f | Kiểm tra ô | Công trình đã có, ngoài NavMesh, dốc; chỉ một ô hỏng là đỏ |

Tạm bỏ qua ở giai đoạn này: kiểm tra và trừ tài nguyên (luôn coi là đủ), chống chặn kín đường (làm sau khi có điểm spawn và HQ), đẩy đơn vị ra khỏi khung (chưa có đơn vị).

**Xong khi:** chọn một công trình thử, bóng bám chuột và nhảy theo ô, nhấn R xoay đúng, ô bị block hiện đỏ và không đặt được, đặt xong thì ô đó bị chiếm.

---

## Giai đoạn 1 — Điều khiển

| # | Việc | Ghi chú |
|---|---|---|
| 5 | Camera | WASD, zoom bằng con lăn, giới hạn trong biên bản đồ |
| 6 | `Selectable` + chọn | Chọn một đơn vị, chọn khung, Shift thêm/bớt, vòng chọn dưới chân |
| 7 | Di chuyển + NavMesh | `NavMeshAgent`, lệnh chuột phải, đội hình rải khi chọn nhiều đơn vị |

**Xong khi:** chọn một nhóm quân, ra lệnh đi qua vật cản mà không đè lên nhau.

---

## Giai đoạn 2 — Kinh tế

| # | Việc | Ghi chú |
|---|---|---|
| 8 | `ResourceManager` | Kho chung, thêm/trừ, kiểm tra đủ trước khi trừ |
| 9 | `ResourceNode` | Cây, mỏ đá, bụi quả có trữ lượng, biến mất khi cạn |
| 10 | Worker thu thập | Chuột phải vào điểm tài nguyên, thu theo chu kỳ, tự tìm điểm gần khi cạn |
| 11 | HUD thanh tài nguyên | Gỗ, đá, thức ăn |
| 12 | Hoàn thiện đặt công trình | Phần đặt/xoay đã làm ở giai đoạn 0.5; thêm kiểm tra và trừ tài nguyên, chống chặn kín đường, đẩy đơn vị ra khỏi khung |
| 13 | Menu xây dựng + Lumber Camp | Nút công trình có giá, tô xám khi thiếu tài nguyên, Lumber Camp tăng thu gỗ |

**Xong khi:** Worker thu gỗ, đủ tài nguyên thì đặt được Lumber Camp và tốc độ thu gỗ tăng.

---

## Giai đoạn 3 — Chiến đấu

| # | Việc | Ghi chú |
|---|---|---|
| 14 | `Health` | Máu, nhận sát thương, chết, event `OnUnitDied` |
| 15 | `Attacker` | Đuổi mục tiêu, vào tầm thì đánh, tốc độ đánh |
| 16 | Rifleman và Heavy Guard | Đặt sẵn trong scene để test |
| 17 | Tự phản công | Đơn vị `Idle` tự đánh kẻ địch trong tầm phát hiện, quay về nếu bị kéo xa |
| 18 | Watchtower | Tự chọn mục tiêu và bắn, dùng lại `Attacker` |
| 19 | `EnemyAI` | `Advance` về HQ, `Engage` khi có mục tiêu trong tầm |
| 20 | Thanh máu | Hiện trên đầu đối tượng khi bị thương hoặc đang chọn |

**Xong khi:** 5 kẻ địch tiến vào căn cứ, quân và tháp canh chống lại được, HQ bị đánh thì máu giảm.

---

## Giai đoạn 4 — Hoàn thiện vòng lặp

| # | Việc | Ghi chú |
|---|---|---|
| 21 | `ProductionQueue` | Hàng đợi tối đa 5, trừ tiền khi vào hàng, hoàn tiền khi hủy, rally point |
| 22 | Barracks + tuyển quân | Tuyển Worker từ HQ, lính từ Barracks, giới hạn dân số |
| 23 | Bảng thông tin | Tên, máu, nút hành động, hàng đợi của đối tượng đang chọn |
| 24 | `WaveManager` | Thời gian chuẩn bị, 3 đợt, spawn rải theo thời gian, cảnh báo trước đợt |
| 25 | Thắng/thua | Màn hình kết thúc, chơi lại |
| 26 | Hoàn thiện UI | Minimap, thông báo, tạm dừng, màn hình loading, menu chính |

**Xong khi:** chơi trọn một ván từ đầu đến kết quả thắng hoặc thua mà không lỗi nghiêm trọng.

---

## Giai đoạn 5 — Art pass & cân bằng

| # | Việc | Ghi chú |
|---|---|---|
| 27 | Art pass | Thay asset thử nghiệm bằng low poly thống nhất, ánh sáng, bóng đổ, hiệu ứng cơ bản |
| 28 | Cân bằng | Chỉnh máu, giá, thời gian, số lượng kẻ địch theo playtest (qua ScriptableObject) |

**Xong khi:** đạt tiêu chí hoàn thành trong `prototype.md`, một ván khoảng 10–15 phút.

---

## Vì sao theo thứ tự này

| Thứ tự | Lý do |
|---|---|
| Điều khiển trước kinh tế | Mọi thứ sau đều cần chọn và ra lệnh |
| Kinh tế trước chiến đấu | Có tài nguyên thì mới có lý do xây và tuyển quân |
| Chiến đấu trước wave | Phải đánh được kẻ địch thì mới thiết kế được đợt tấn công |
| Wave và thắng/thua ở cuối | Chúng ghép các hệ thống ở trên lại với nhau |
| Art pass sau cùng | Chỉ làm đẹp khi vòng lặp đã ổn |

## Mốc chơi thử đầu tiên

Hết **bước 7** là có bản chơi thử được: camera, chọn quân, di chuyển. Nên dừng tại đây để kiểm tra cảm giác điều khiển trước khi làm kinh tế.

## Rủi ro cần để ý

| Rủi ro | Cách giảm |
|---|---|
| NavMesh bị bake lại khi đặt công trình | Dùng `NavMeshObstacle` carving hoặc cập nhật NavMesh cục bộ, thử sớm ở bước 12 |
| Đơn vị chen nhau, kẹt ở cổng | Test với nhóm lớn ở bước 7, chỉnh avoidance |
| Cân bằng lệch | Mọi con số nằm trong ScriptableObject, không hard-code |
| Tính năng phình ra | Đối chiếu mục 3 của `prototype.md` và mục 10 của `mechanics.md` trước khi thêm |

## Câu hỏi còn mở

Các câu hỏi ở mục 11 của `mechanics.md` (Worker có đánh trả không, có sương mù chiến tranh không, công trình xây tức thì hay có thanh tiến độ, nguồn thức ăn, giới hạn dân số) cần chốt trước **giai đoạn 2** và **giai đoạn 3**.
