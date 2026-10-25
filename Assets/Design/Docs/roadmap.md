# LỘ TRÌNH PHÁT TRIỂN — MVP
## After the Fall — Survival RTS

Ký hiệu: ✅ đã xong · trống = chưa làm · "Một phần" = đã có một phần, ghi rõ ở cột ghi chú.

---

## Giai đoạn 0 — Nền tảng

| # | Việc | Trạng thái | Ghi chú |
|---|---|---|---|
| 1 | Scene thử nghiệm | ✅ | Scene `prototype`: mặt đất, NavMesh, ánh sáng, khu vật cản thử |
| 2 | Cấu trúc code | ✅ | Thư mục `Scripts/Runtime`, `Scripts/SO`, `Scripts/Editor`; SO: `BuildingData`, `EnemyData`, `TowerData`, `SoldierData`, `GpuAnimationData`, `SoundLibrary` |
| 3 | `GameManager` + `GameState` |  | Chưa có |

---

## Giai đoạn 0.5 — Đặt và xoay công trình

| # | Việc | Trạng thái | Ghi chú |
|---|---|---|---|
| 0.5a | `BuildingData` + prefab thử | ✅ | Khối thử 1x1, 2x2, 3x3, 4x4, 2x3, 1x4 |
| 0.5b | `BuildGrid` | ✅ | Lưới 1m; chiếm ô, địa hình, độ cao lưu bằng `NativeArray` |
| 0.5c | `BuildingPlacer` | ✅ | Bóng mờ bám chuột, snap theo footprint chẵn/lẻ, xanh/đỏ, hủy |
| 0.5d | Xoay 90° | ✅ | Footprint đổi chiều theo hướng xoay |
| 0.5e | Kiểm tra ô | ✅ | Ô bị chiếm, ngoài NavMesh, dốc, chênh độ cao; một ô hỏng là đỏ |

---

## Giai đoạn 1 — Điều khiển

| # | Việc | Trạng thái | Ghi chú |
|---|---|---|---|
| 4 | Camera | ✅ | WASD, zoom con lăn, kéo chuột giữa, xoay chuột phải, giới hạn biên, bấm minimap để di chuyển |
| 5 | Chọn đối tượng | ✅ | Click chọn công trình/đơn vị (`BuildingSelector`), hiện thanh máu và vòng tầm |

---

## Giai đoạn 2 — Kinh tế

| # | Việc | Trạng thái | Ghi chú |
|---|---|---|---|
| 6 | Kho tài nguyên chung | ✅ | Trong `PopulationManager`: gỗ, đá, sắt, lúa, vàng; kiểm tra, trừ, hoàn |
| 7 | `ResourceNode` |  | Một phần: cây, đá, quặng sắt, bụi quả có `ResourceNode`; chưa có trữ lượng và cạn |
| 8 | Thu thập tài nguyên | ✅ | Gán thợ vào nhà khai thác bằng nút +/−; `HarvesterUnit` tự đi thu và mang về |
| 9 | HUD thanh tài nguyên | ✅ | Dân rỗi/tổng, gỗ, đá, sắt, lúa, vàng |
| 10 | Hoàn thiện đặt công trình |  | Một phần: đã kiểm tra và trừ tài nguyên; chưa đẩy đơn vị ra khỏi khung khi đặt |
| 11 | Menu xây dựng + Lumber Camp | ✅ | Tab Kinh tế/Quân sự/Phòng thủ, thẻ hiện giá, tô xám khi thiếu tài nguyên; Trại gỗ có bán kính khai thác |

---

## Giai đoạn 3 — Chiến đấu

| # | Việc | Trạng thái | Ghi chú |
|---|---|---|---|
| 12 | `Health` | ✅ | `Health` (GameObject) và `EnemyHealth` (ECS), sự kiện `Died` |
| 13 | Tấn công | ✅ | Quái ECS đuổi và đánh mục tiêu; lính kiếm đuổi và chém quái |
| 14 | Tự phản công | ✅ | Lính kiếm tự đánh quái trong tầm canh gác, quay về khi đuổi quá xa |
| 15 | Watchtower | ✅ | Tháp ECS tự chọn mục tiêu và bắn, có tia đạn |
| 16 | AI kẻ địch | ✅ | ECS: flow field về HQ, đánh mục tiêu trong tầm (ưu tiên đơn vị > công trình > HQ) |
| 17 | Thanh máu | ✅ | Hiện khi chọn công trình hoặc lính |

---

## Giai đoạn 4 — Hoàn thiện vòng lặp

| # | Việc | Trạng thái | Ghi chú |
|---|---|---|---|
| 18 | Trại lính + đào tạo lính | ✅ | Đào tạo dân tự do thành Lính kiếm, có thời gian đào tạo và hàng chờ, tối đa 6 lính mỗi trại; dân số tăng nhờ Nhà dân |
| 19 | Bảng thông tin |  | Một phần: thanh máu, trạng thái xây, bảng thợ +/− kèm tiến độ đào tạo; chưa có tên và nút hành động chung |
| 20 | `WaveManager` |  | Chưa có (mới có `EnemySpawner` spawn thử theo vùng tròn) |
| 21 | Thắng/thua |  | Chưa có màn hình (đã có sự kiện `Headquarters.Destroyed`) |
| 22 | Hoàn thiện UI |  | Một phần: đã có minimap, menu cài đặt và tạm dừng (Esc); có UXML màn hình loading nhưng chưa dùng; chưa có thông báo, menu chính |

---

## Giai đoạn 5 — Art pass & cân bằng

| # | Việc | Trạng thái | Ghi chú |
|---|---|---|---|
| 23 | Art pass |  | Đang dùng khối hộp thử và nhân vật sinh bằng code |
| 24 | Cân bằng |  | |

---

## Đã làm thêm (ngoài danh sách ban đầu)

| Việc | Trạng thái | Ghi chú |
|---|---|---|
| Kẻ địch ECS | ✅ | Entities + Burst: spawn theo vùng tròn, flow field, né nhau bằng spatial grid; đã đo 10.000–100.000 con |
| Animation GPU | ✅ | Bone texture cho quái, dân, lính; công cụ sinh nhân vật và nướng animation |
| Nhà chính (HQ) | ✅ | Có máu, là đích của quái, phát sự kiện khi bị phá |
| Hệ thống dân | ✅ | `PopulationManager`, Nhà dân, dân tự do, thợ xây (Builder's Hut) |
| Công trường xây dựng | ✅ | 3 giai đoạn khung, thợ xây đến xây, thời gian xây theo `BuildingData` |
| Tháo dỡ công trình | ✅ | Có thời gian tháo dỡ, hoàn lại chi phí |
| Nhà khai thác | ✅ | Trại gỗ, Xưởng đá, Mỏ sắt, Nông trại |
| Âm thanh | ✅ | Unity Audio + AudioMixer (Music/SFX/UI); hàng đợi sự kiện từ ECS, lọc theo camera, gộp theo ô, giới hạn số tiếng, ưu tiên giọng; loop đám đông; nhạc yên/trận; Lowpass theo zoom |
| Menu cài đặt âm lượng | ✅ | Tổng, Nhạc nền, Hiệu ứng, Giao diện; lưu bằng PlayerPrefs |
