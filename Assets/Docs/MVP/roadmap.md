# LỘ TRÌNH PHÁT TRIỂN — MVP
## Medieval Kingdom

Ký hiệu: ✅ đã xong · 🔶 một phần (ghi rõ ở ghi chú) · ⬜ chưa làm.
Các giai đoạn bám phần "Phạm vi MVP" của `Assets/Docs/mô tả game.md`, đã điều chỉnh theo hiện trạng project. Giai đoạn 1–5 là MVP, giai đoạn 6 trở đi là sau MVP.

---

## Giai đoạn 0 — Nền tảng

| # | Việc | Trạng thái | Ghi chú |
|---|---|---|---|
| 0.1 | Scene và cấu trúc code | ✅ | Scene `init`, `menu`, `prototype`, `battle`; `Scripts/Runtime`, `SO`, `Editor` |
| 0.2 | ScriptableObject cấu hình | ✅ | `BattleData`, `BuildingData`, `SoldierData`, `TowerData`, `EnemyData`, `GpuAnimationData`, `SoundLibrary` |
| 0.3 | Luồng màn hình | ✅ | `GameFlow`: loading → menu → offline hoặc trận; chuyển cảnh có màn loading |
| 0.4 | Bộ giao diện | ✅ | `BattleUiKit` (uGUI + TMP, wireframe xám); thiết kế trên Figma, dữ liệu xuất ở `docs/figma/` |
| 0.5 | `GameManager` + `GameState` | ⬜ | Trạng thái ván dùng chung chưa có |

---

## Giai đoạn 1 — Prototype xây dựng (offline)

| # | Việc | Trạng thái | Ghi chú |
|---|---|---|---|
| 1.1 | `BuildGrid`, `BuildingPlacer` | ✅ | Lưới 1 m, snap theo footprint, xoay 90°, xanh/đỏ, huỷ |
| 1.2 | Kiểm tra ô | ✅ | Ô bị chiếm, ngoài NavMesh, dốc, chênh độ cao |
| 1.3 | Công trường và thợ xây | ✅ | 3 giai đoạn khung, thợ xây đến xây |
| 1.4 | Tháo dỡ | ✅ | Có thời gian, hoàn chi phí |
| 1.5 | Đẩy đơn vị ra khỏi khung khi đặt | ⬜ | |
| 1.6 | Lưu và tải công trình | ⬜ | Menu Bản lưu mới là giao diện rỗng (không có hệ thống save) |

---

## Giai đoạn 2 — Kinh tế và dân (offline)

| # | Việc | Trạng thái | Ghi chú |
|---|---|---|---|
| 2.1 | Kho tài nguyên chung | ✅ | `PopulationManager`: gỗ, đá, sắt, lúa, vàng |
| 2.2 | Nhà khai thác và thu thập | ✅ | Trại gỗ, Xưởng đá, Mỏ sắt, Nông trại; gán thợ +/−, `HarvesterUnit` |
| 2.3 | Dân và Nhà dân | ✅ | Dân tự do, giới hạn dân số |
| 2.4 | Trữ lượng điểm tài nguyên | ⬜ | `ResourceNode` chưa có trữ lượng và cạn |
| 2.5 | Menu xây dựng | ✅ | Tab Kinh tế / Quân sự / Phòng thủ, thẻ giá, xám khi thiếu |

---

## Giai đoạn 3 — Quân đội và chiến đấu quy mô lớn

| # | Việc | Trạng thái | Ghi chú |
|---|---|---|---|
| 3.1 | Mô phỏng quân lockstep | ✅ | NativeArray + job Burst, số nguyên cố định (1 m = 256), 16.000 lính |
| 3.2 | Hiển thị quân | ✅ | Entities Graphics + animation GPU, nội suy giữa hai lượt |
| 3.3 | Tiểu đội, đội hình, lệnh di chuyển | ✅ | Tiểu đội 100 lính, `MoveSquad` |
| 3.4 | Hai loại lính | ✅ | Lính kiếm (cận chiến), Lính cung (tia mũi tên) |
| 3.5 | AI đội bot | ✅ | `BattleBot` |
| 3.6 | Quái ECS offline | ✅ | Flow field, né nhau, đã đo 10.000–100.000 con |
| 3.7 | Tháp canh | ✅ | Tháp ECS tự chọn mục tiêu, có tia đạn |
| 3.8 | Lính kiếm phe mình, tự phản công | ✅ | Offline |
| 3.9 | Âm thanh chiến trường | ✅ | `AudioDirector`: lọc theo camera, gộp theo ô, giới hạn số tiếng, loop đám đông, Lowpass theo zoom |
| 3.10 | Kỵ binh và công thành | ⬜ | Sau MVP |
| 3.11 | Phá hủy công trình bởi quân | ⬜ | Cần công trình vào trận lockstep (giai đoạn 4) |
| 3.12 | Đo hiệu năng 100 / 500 / 1.000 / 16.000 lính | 🔶 | Đã chạy 16.000 trong trận; chưa có bảng số đo ghi lại |

---

## Giai đoạn 4 — Multiplayer và PvP

| # | Việc | Trạng thái | Ghi chú |
|---|---|---|---|
| 4.1 | Lockstep qua FishNet | ✅ | Tugboat, chỉ Broadcast, host relay, 10 lượt/giây |
| 4.2 | Checksum và phát hiện desync | ✅ | Mỗi 10 lượt |
| 4.3 | Tạm dừng khi máy chậm | ✅ | `AckMessage` / `PauseMessage` |
| 4.4 | LAN | ✅ | `LanDiscovery`, phòng chờ, bắt đầu trận |
| 4.5 | Online qua địa chỉ | 🔶 | Màn "Tham gia máy chủ" lưu tối đa 8 máy chủ; chưa có dịch vụ tìm phòng |
| 4.6 | Tìm phòng qua EOS | ⬜ | Đã chọn EOS; tách sau `IRoomProvider`, cần transport EOS cho FishNet (xem `online-eos.md`) |
| 4.7 | Huấn luyện quân bằng vàng trong trận | ✅ | Lệnh lockstep, 200 vàng / 15 giây / tiểu đội |
| 4.8 | Kinh tế, xây dựng, dân số vào lockstep | ⬜ | Việc lớn nhất của MVP; xem `tech.md` mục 5 |
| 4.12 | Chơi đơn chạy chung lockstep | ⬜ | Host nội bộ + AI, loopback; một bản mô phỏng cho cả đơn và mạng (`online-eos.md`) |
| 4.9 | Thắng / thua PvP và màn Kết quả | ✅ | `WinnerTeam`, Chơi lại / Về phòng chờ / Thoát |
| 4.10 | Kiểm thử nhiều máy | 🔶 | Có tham số `-host`, `-join <ip>`, `-bot`; chưa có kịch bản kiểm thử so checksum cố định |
| 4.11 | Xử lý host thoát, vào lại | 🔶 | Mất kết nối thì về menu kèm thông báo; chưa có vào lại giữa trận |

---

## Giai đoạn 5 — Hoàn thiện vòng lặp và giao diện

| # | Việc | Trạng thái | Ghi chú |
|---|---|---|---|
| 5.1 | Menu chính, Cài đặt, Bản lưu, Credits | ✅ | 3 tab Cài đặt (Đồ họa, Âm thanh, Điều khiển) |
| 5.2 | Menu Esc offline | ✅ | Cùng panel Cài đặt, thêm Tiếp tục và Về menu chính |
| 5.3 | Giao diện "Màn chơi" thống nhất | ⬜ | Theo thiết kế Figma mới (nền tối, dùng chung offline và online); HUD hiện tại là bản cũ |
| 5.4 | `WaveManager` và `WaveConfig` | ⬜ | Offline mới có `EnemySpawner` spawn thử theo vùng tròn |
| 5.5 | Thắng offline | ⬜ | Đã có sự kiện `Headquarters.Destroyed` cho thua, chưa có màn Thắng/Thua offline |
| 5.6 | Thông báo trong game | ⬜ | "Không đủ tài nguyên", "Đợt sắp đến", "HQ bị tấn công" |
| 5.7 | Bảng thông tin đối tượng | 🔶 | Thanh máu, trạng thái xây, bảng thợ; chưa có tên và nút hành động chung |
| 5.8 | Cân bằng | ⬜ | Chỉnh sau khi có đủ vòng lặp |

---

## Giai đoạn 4b — Địa hình và đặt nhà (MVP)

Chi tiết: `terrain.md`, `building-placement.md`.

| # | Việc | Trạng thái | Ghi chú |
|---|---|---|---|
| 4b.1 | `heightGrid` số nguyên theo chunk | ✅ | `HeightField` + `TerrainChunk` (50 × 50 m, đơn vị 1/256 m) |
| 4b.2 | Chuyển mặt đất sang Unity Terrain | ✅ | Đã chuyển trong `prototype.unity`, dẫn động từ `HeightField` |
| 4b.3 | Thuộc tính `placementRule` cho cây, đá, công trình | ✅ | `ObjectPlacementData` (`Block` / `Clear`), `CellOccupancy.Clearable` |
| 4b.4 | Nhà xoay 1° và chiếm ô bằng số nguyên | ✅ | Bảng hằng số nguyên `CosTable`, `CollectCells` kiểm chứng 20/20 trường hợp |
| 4b.5 | Móng đá, sàn phẳng thích nghi địa hình | ✅ | `BuildingFoundation`, `SampleFootprintHeights`, báo lý do chặn cụ thể |
| 4b.6 | Sinh địa hình từ seed | 🔶 | Đã xong sinh cây từ seed (`ProceduralVegetation`); phần sinh độ cao đồi núi từ seed làm tiếp |
| 4b.7 | Người chơi sửa địa hình qua lệnh lockstep | ⬜ | Nét địa hình có `height`, `falloff` |
| 4b.8 | Chunk hoá `BuildGrid` và flow field | ⬜ | Cần từ 1.000 m trở lên |

---

## Sau MVP

| Giai đoạn | Nội dung | Trạng thái |
|---|---|---|
| 6 — Xây dựng tự do | Tường vẽ bằng spline, cổng, tháp, mesh procedural, Unity Splines | ⬜ |
| 7 — Art pass | Mô hình low poly thống nhất, ánh sáng, vật liệu stylized (URP, Shader Graph) | ⬜ (đang dùng khối thử và nhân vật sinh bằng code) |
| 8 — Co-op PvE | 2–4 người chung một vương quốc, chia sẻ tài nguyên | ⬜ |
| 9 — Vertical Slice | Đồ họa, xây dựng, kinh tế, quân đội, co-op và PvP trong một trận đủ đầu đến cuối | ⬜ |

---

## Đã làm thêm (ngoài danh sách gốc)

| Việc | Trạng thái | Ghi chú |
|---|---|---|
| Hệ thống cài đặt hiển thị | ✅ | `DisplaySettings`: độ phân giải, toàn màn hình, VSync, giới hạn FPS, chất lượng |
| Công cụ xuất giao diện sang Figma | ✅ | `UiLayoutExporter`, `docs/figma/` |
| Sinh nhân vật và nướng animation | ✅ | Grunt, dân, lính; `GpuAnimationBaker` |
| Menu Editor Debug | 🔶 | Theo quy tắc: công cụ test nhanh đặt trong menu Editor, không đưa vào giao diện game |

---

## Thứ tự ưu tiên đề xuất

1. **Giai đoạn 4.8**: đưa kinh tế và xây dựng vào lockstep. Đây là rủi ro lớn nhất còn lại và quyết định thiết kế của cả hai chế độ.
2. **Giao diện "Màn chơi"** (5.3), làm sau khi 4.8 đã chốt dữ liệu cần hiển thị.
3. **`WaveManager`** và Thắng/Thua offline (5.4–5.5).
4. Tìm phòng online (4.6).
5. Hệ thống save (1.6).
