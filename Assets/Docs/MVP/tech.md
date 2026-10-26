# CÔNG NGHỆ VÀ KIẾN TRÚC — MVP
## Medieval Kingdom

**Nguồn:** `Assets/Docs/mô tả game.md` mục 8–9, đối chiếu với code hiện tại.
**Quy tắc chi tiết cho code:** `.agents/rules/` (`multiplayer.md`, `ui-kit.md`, `rule.md`).

---

## 1. Công nghệ dùng trong MVP

| Công nghệ | Vai trò | Trạng thái |
|---|---|---|
| Unity 6 (6000.6) + C# | Engine, logic | ✅ |
| URP | Rendering cho PC | ✅ |
| Entities + Entities Graphics | Vẽ hàng chục nghìn lính; mô phỏng quái offline | ✅ |
| Jobs + Burst | Mô phỏng trận lockstep và quái | ✅ |
| FishNet 4.6.22 (Tugboat) | Truyền lệnh giữa các máy, chỉ Broadcast | ✅ |
| Animation GPU (bone texture, `GpuSkinnedLit`) | Hoạt ảnh cho đám đông | ✅ |
| uGUI + TextMeshPro | Giao diện, dựng bằng code qua `BattleUiKit` | ✅ |
| Input System | Điều khiển | ✅ |
| Unity Audio + AudioMixer | Âm thanh, qua `AudioDirector` | ✅ |
| Steamworks.NET | Nền tảng Steam, dự kiến cho tìm phòng | 🔶 đã tích hợp `SteamManager`, chưa dùng lobby |
| NavMesh (AI Navigation) | Tìm đường cho dân, đặt công trình | ✅ |
| Flow field | Tìm đường cho quái đông | ✅ |

**Chưa dùng:** Unity Terrain (đã quyết định dùng, xem `terrain.md`), Epic Online Services (đã chọn cho tìm phòng, xem `online-eos.md`). **Sau MVP:** Unity Splines và procedural mesh (xây dựng tự do), Shader Graph stylized.

## 2. Khác biệt so với bản định hướng gốc

| Bản gốc | Thực tế MVP | Lý do |
|---|---|---|
| Server-authoritative | **Lockstep xác định**: mọi máy tự mô phỏng, host chỉ relay lệnh | 16.000 lính không thể đồng bộ từng trạng thái; chỉ gửi lệnh |
| Đồng bộ entity ECS bằng FishNet | Không dùng `NetworkObject`, RPC, SyncVar | Mô phỏng nằm ngoài ECS, ECS chỉ để vẽ |
| EOS cho lobby | Hiện: địa chỉ trực tiếp (LAN broadcast, danh sách máy chủ đã lưu). Đã chọn EOS cho tìm phòng, chưa làm | Tách sau `IRoomProvider`, chi tiết ở `online-eos.md` |
| UI Toolkit (bản tech cũ) | uGUI + TextMeshPro | Chỉnh trực tiếp trong Editor, có sẵn `BattleUiKit` |
| Unity Entities mô phỏng quân | NativeArray + Burst số nguyên cố định | Cần kết quả giống hệt nhau trên mọi máy |

## 3. Kiến trúc tổng thể

```
Input / UI  ──►  LockstepNetwork.SendCommand  ──►  (client) → host relay → (mọi máy)
                                                                 │
                                                                 ▼
                                                  ApplyTurn → BattleSimulation (Burst, số nguyên)
                                                                 │ (chỉ đọc)
                                                                 ▼
                                      BattleUnitPresentation / BattleAudio / UI  (trình diễn)
```

Module và vị trí:

| Module | Thư mục | Ghi chú |
|---|---|---|
| Mô phỏng trận | `Runtime/Battle/` | `BattleSimulation`, `BattleJobs`, `BattleFixed`, `BattleDirector`, `BattleBot`, `BattleInput` |
| Mạng | `Runtime/Network/` | `LockstepNetwork`, `NetMessages`, `ILockstepSimulation`, `LanDiscovery` |
| Hiển thị quân | `Runtime/Battle/BattleUnitPresentation.cs`, `Runtime/Units/GpuMeshAnimator.cs` | Entities Graphics, nội suy hai lượt |
| Xây dựng (offline) | `Runtime/Building/` | `BuildGrid`, `BuildingPlacer`, `ConstructionSite`, `DemolishSite` |
| Kinh tế / dân (offline) | `Runtime/Units/`, `Runtime/Building/` | `PopulationManager`, `HarvesterUnit`, `ResourceCamp` |
| Quái, tháp (offline) | `Runtime/Enemies/`, `Runtime/Combat/` | ECS, flow field |
| Âm thanh | `Runtime/Audio/` | `AudioDirector` + `BattleAudio` |
| Giao diện | `Runtime/UI/` | `Battle/` (trận, menu, phòng chờ), `Menu/` (panel), `Loading/` |
| Luồng game | `Runtime/Core/` | `GameFlow`, `DisplaySettings`, `CameraController` |
| Dữ liệu | `Scripts/SO/`, `ScriptableObjects/` | |
| Công cụ Editor | `Scripts/Editor/` | Sinh prefab UI, sinh nhân vật, nướng animation, xuất Figma, menu `Debug` |

Các module giao tiếp qua giao diện rõ ràng: hệ thống xây dựng chỉ tạo **lệnh có cấu trúc**, lớp mạng xác thực và phát tán, mô phỏng áp dụng ở `ApplyTurn`.

## 4. Quy tắc mô phỏng lockstep (tóm tắt)

Chi tiết đầy đủ ở `.agents/rules/multiplayer.md`.

- Chỉ số nguyên cố định (1 m = 256 đơn vị). Cấm `float`, `Time`, `Random`, `Physics`, `NavMesh`, duyệt thứ tự không cố định trong vòng mô phỏng.
- Job song song chỉ ghi vào ô của chính nó; gộp (như trừ máu) làm tuần tự theo chỉ số.
- Hành động người chơi chỉ tác động mô phỏng qua `LockstepNetwork.SendCommand` → `ApplyTurn`.
- Hình ảnh, âm thanh, tia mũi tên không ghi ngược vào trạng thái mô phỏng.
- Thêm trường vào trạng thái thì phải đưa vào `BattleChecksumJob`.
- Sau khi sửa mô phỏng: chạy cùng chuỗi lệnh hai lần so checksum, và chạy nhiều máy so checksum từng mốc.

## 5. Kế hoạch đưa kinh tế và xây dựng vào lockstep ⬜

Đây là phần kỹ thuật lớn nhất còn lại của MVP.

**Vấn đề:** kinh tế, xây dựng, dân số, trại lính hiện là `MonoBehaviour` dùng `float`, `Time`, NavMesh trong scene `prototype`, không xác định được.

**Hướng:**
1. Biểu diễn lưới xây dựng và công trình bằng mảng số nguyên trong `BattleSimulation` (chiếm ô, máu, loại, tiến độ xây).
2. Thêm lệnh lockstep: `PlaceBuilding` (loại, ô, hướng xoay), `DemolishBuilding`, `TrainSquad` (đã có).
3. Kinh tế dạng số nguyên: tài nguyên mỗi đội, thu nhập theo công trình mỗi lượt; bỏ thợ đi bộ cá nhân, thay bằng thu nhập theo công trình hoặc theo nhóm.
4. Quân tấn công công trình: công trình là mục tiêu trong cùng lưới mục tiêu của mô phỏng.
5. Tìm đường quân dùng flow field số nguyên thay NavMesh.
6. Thêm mọi trường mới vào checksum, kiểm thử hai lần chạy và nhiều máy.

Chế độ chơi đơn cũng chạy trên đường lockstep này (xem `online-eos.md`), nên kinh tế và xây dựng chỉ viết một lần. Tài liệu di chuyển chi tiết sẽ nằm ở `docs/lockstep-economy-migration.md` khi bắt đầu làm.

## 6. Chủ đề kỹ thuật có tài liệu riêng

| Chủ đề | File | Tóm tắt |
|---|---|---|
| Địa hình | `terrain.md` | `heightGrid` số nguyên là nguồn sự thật, Unity Terrain chỉ hiển thị; sinh từ seed; sửa bằng nét qua lệnh lockstep; bản đồ 1.000 m trở lên cần chia chunk |
| Đặt công trình | `building-placement.md` | Xoay 1°, thích nghi địa hình, móng đá, thuộc tính `placementRule` (chặn hoặc tự biến mất) của cây, đá, công trình |
| Chơi đơn, mạng, tìm phòng | `online-eos.md` | Chơi đơn chạy chung đường lockstep (host nội bộ + AI); tìm phòng online bằng EOS qua `IRoomProvider` |
| Mô phỏng quân | `army-simulation.md` | Mô phỏng trận lockstep và quái ECS |

## 7. Hiệu năng

| Mục tiêu | Giá trị |
|---|---|
| Quân trong trận | 16.000 (4 × 4.000) |
| Khung hình | 60 FPS ở cấu hình PC tầm trung (ghi cấu hình khi đo) |
| Lượt mô phỏng | 10 lượt/giây, nội suy hiển thị giữa hai lượt |
| Đổ bóng lính | Tắt mặc định (`castShadows = false`) để giảm chi phí vẽ |
| Quái offline | 10.000 con, mô phỏng khoảng 0,6 ms/frame (đo trong Editor) |

Cần ghi bảng số đo thật ở 100 / 500 / 1.000 / 4.000 / 16.000 lính (CPU, GPU, bộ nhớ) bằng Unity Profiler.

## 8. Giao diện

- Dựng bằng code qua `BattleUiKit`: sprite bo góc 9-slice sinh bằng code, bảng màu wireframe xám, TextMeshPro với font tiếng Việt.
- Thiết kế gốc nằm trên Figma; dữ liệu đo trực tiếp từ Unity ở `docs/figma/` (`screens/*.json`, `flow.json`, `tokens.json`), có plugin dựng lại lên Figma.
- Kỹ năng thao tác Figma: `.agents/skills/figma-ui/SKILL.md`.
- Không đặt nút test/cheat trong giao diện game; công cụ nhanh nằm ở menu Editor `Debug`.

## 9. Ghi chú kỹ thuật cần nhớ

- Đã vá khoảng 14 chỗ trong `Assets/FishNet/Runtime` (SceneHandle, `GetInstanceID`) để compile trên Unity 6000.6; nâng cấp FishNet sẽ mất các bản vá.
- Tham số dòng lệnh của bản build: `-host` mở phòng, `-join <địa chỉ>` vào phòng, `-bot` để máy đó tự điều quân.
- Không xuất giao diện bằng `UiLayoutExporter` trong lúc đang chạy trận 16.000 lính (Unity có thể treo).
