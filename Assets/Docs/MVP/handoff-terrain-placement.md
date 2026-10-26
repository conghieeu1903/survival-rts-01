# GIAO VIỆC: XOAY CÔNG TRÌNH, ĐỊA HÌNH CHUNK, ĐẶT NHÀ, SINH CÂY
## Tài liệu bàn giao cho AI thực hiện (người review: AI đã viết tài liệu này)

Dự án: Unity 6000.6.6f1, `D:\Unity3D\Projects\survival-rts-01`, game RTS trung cổ (URP, Entities, Input System, TextMeshPro, FishNet 4.6.22). Người dùng nói tiếng Việt, trả lời và comment bằng tiếng Việt.

---

## 0. Quy tắc bắt buộc (đọc trước khi làm)

Đọc `CLAUDE.md` và toàn bộ `.agents/rules/` (`rule.md`, `multiplayer.md`, `ui-kit.md`, `prefab.md`, `model-3d.md`, `test-tools.md`). Tóm tắt những điều dễ vi phạm:

- **Cấm dùng git** (không commit, không checkout, không reset, không stash).
- Comment: chỉ comment ngắn, kiểu `//`, tiếng Việt, **trên đầu hàm/class** và cạnh biến phức tạp. Không comment dài, xoá comment thừa. Không comment từng dòng.
- **Không đặt tên "test"** cho code thật, không viết code test lẫn trong code game. Công cụ thử nhanh đặt trong menu Editor `Debug` (`Assets/Scripts/Editor/DebugWindow.cs`), **không** đưa nút test/cheat vào giao diện game.
- Viết đúng cấu trúc thư mục (`Assets/Scripts/Runtime/...`, `SO/`, `Editor/`), sai thì sửa sau chứ không đặt tạm.
- Mô phỏng lockstep (mọi thứ ảnh hưởng trạng thái trận): **chỉ dùng số nguyên cố định**; cấm `float`, `Time`, `Random`, `Physics`, `NavMesh`, duyệt thứ tự không cố định; job song song chỉ ghi ô của chính nó; hành động người chơi chỉ qua `LockstepNetwork.SendCommand` → `ApplyTurn`; hình ảnh không ghi ngược vào mô phỏng; thêm trường mô phỏng phải đưa vào `BattleChecksumJob`. (Phần này **chưa** áp dụng cho chế độ offline hiện tại nhưng mọi hàm mới nên viết sẵn bằng số nguyên để chuyển vào lockstep được.)
- Không đụng vào scene/asset đang mở nếu chưa chắc; sau khi sửa scene phải lưu. Xoá thư mục `Assets/Screenshots/` nếu có chụp màn hình.
- Không tự tạo/commit file ngoài phạm vi. Không in khoá API hay bí mật.

Công cụ Unity qua MCP: mọi lệnh cần `port: 7894` (sau `unity_select_instance`). Biên dịch: `AssetDatabase.Refresh()` + `CompilationPipeline.RequestScriptCompilation()`, đợi xong rồi `unity_get_compilation_errors`. Khi Editor đang chạy Play Mode thì phải dừng Play Mode mới biên dịch được.

---

## 1. Bối cảnh thiết kế (đã chốt với người dùng)

1. Địa hình về sau: **sinh ngẫu nhiên từ seed** và **người chơi sửa được** (nâng/hạ, vật liệu, cỏ).
2. Bản đồ: **1.000 × 1.000 m trước, thiết kế để lên 10.000 × 10.000 m**. Chia **chunk 50 × 50 m** ngay từ đầu.
3. Nguồn sự thật độ cao là `heightGrid` **số nguyên** (đơn vị 1/256 m, kiểu `short`), nằm ở lớp dữ liệu riêng; **Unity Terrain chỉ để hiển thị**.
4. Công trình: xoay tự do từng 1°, sàn phẳng, móng đá nối xuống đất ở phía thấp.
5. Vật thể trên bản đồ có thuộc tính `placementRule`: `Block` (cây lớn, đá: chặn đặt) hoặc `Clear` (cây nhỏ, bụi, cỏ: đặt nhà đè lên thì biến mất). **Làm cả hai loại để kiểm thử MVP.**
6. PvP cũng có xây dựng (làm sau); chơi đơn sẽ chạy chung đường lockstep (làm sau). Việc hiện tại chạy ở chế độ **offline, scene `prototype`**, nhưng viết hàm theo hướng số nguyên.
7. Tìm phòng online sẽ dùng EOS (không thuộc phạm vi tài liệu này).

Tài liệu thiết kế chi tiết: `Assets/Docs/MVP/terrain.md`, `building-placement.md`, `tech.md` (mục 4–6), `online-eos.md`.

---

## 2. Hiện trạng code liên quan

| File | Vai trò |
|---|---|
| `Assets/Scripts/Runtime/Building/BuildGrid.cs` | Lưới 50×50 ô (1 m), `occupancy`, `terrain` (`CellState`), `heights` (`float`, lấy bằng raycast). Có `CollectCells`, `Evaluate` mới. |
| `Assets/Scripts/Runtime/Building/GridOccupant.cs` | Mỗi vật chiếm ô: `footprint`, `baseFootprint`, `angle`, `Cells`, `WorldHalfExtents`. |
| `Assets/Scripts/Runtime/Building/BuildingPlacer.cs` | Bóng mờ, đặt, tháo dỡ. Đã chuyển sang góc `angle`. |
| `Assets/Scripts/Runtime/Building/ConstructionSite.cs`, `DemolishSite.cs` | Công trường, tháo dỡ. |
| `Assets/Scripts/Runtime/Building/ResourceNode.cs` | Cây, đá, quặng, bụi quả (`GridOccupant` tĩnh). |
| `Assets/Scripts/SO/BuildingData.cs` | Dữ liệu công trình (`footprint`, `height`, `category`, `harvest*`...). |
| `Assets/Scripts/Runtime/Enemies/ECS/Systems/EnemySimulationSystem.cs` | Flow field của quái, đọc `BuildGrid.Terrain/Occupancy/Heights`, `BuildGrid.Version`. |
| `Assets/Scripts/Runtime/Combat/PlayerTarget.cs` | Mục tiêu của quái, dùng `HalfExtents`. |
| `Assets/Scripts/Runtime/UI/MinimapController.cs`, `BuildMenuController.cs` | Dùng `footprint`. |
| Scene `Assets/Scenes/prototype.unity` | Mặt đất hiện là **mesh thường** (không có Terrain), bản đồ thử 50×50 m, có NavMesh bake sẵn. |

Mặt đất hiện tại nằm ở layer `groundMask` của `BuildGrid`. Các ScriptableObject công trình ở `Assets/ScriptableObjects/Buildings/`.

---

## 3. Danh sách việc, theo đúng thứ tự người dùng yêu cầu

### Việc 1 — Xoay công trình từng độ (đã viết code, cần kiểm chứng) 🔶

**Đã làm (cần review và biên dịch):**
- `BuildGrid.CollectCells(center, baseFootprint, angle, result)`: ô nào có tâm nằm trong hình chữ nhật xoay (toạ độ 1/256 ô, sin/cos bảng nguyên ×65536, viền 0,25 ô) được chiếm; ô ngoài lưới ghi `-1`; thứ tự duyệt z rồi x.
- `BuildGrid.Evaluate(center, baseFootprint, angle, cells, out baseY)`; `SetOccupancy(IReadOnlyList<int>, value)`; `CellCenter(int)`.
- `GridOccupant`: `baseFootprint`, `angle`, `Cells`, `BaseFootprint`, `WorldHalfExtents(cellSize)`.
- `BuildingPlacer`: biến `angle` (độ nguyên) thay `turns`; `Turns` suy ra; R sang mốc 90° kế tiếp; **Q/E xoay từng độ**, giữ 0,3 s thì xoay liên tục 45°/s, Shift 180°/s; công trình `requireAdjacentResource` (Xưởng đá) chỉ xoay theo 90°; lưu góc theo từng loại (`savedAngles`).
- `PlayerTarget.HalfExtents`, `ConstructionSite.GetWorkSlotPosition`, đích flow field HQ trong `EnemySimulationSystem` đã đọc theo `Cells` / hộp bao xoay.
- `SettingsPanel.OfflineControls` thêm dòng "Xoay mịn từng độ: Q / E (Shift: nhanh)".

**Việc cần làm:**
1. Biên dịch, sửa mọi lỗi (code chưa từng được biên dịch). Dùng `unity_get_compilation_errors` và `unity_console_log`.
2. Review lại logic theo quy ước Unity: `Quaternion.Euler(0, yaw, 0)` biến trục local +X thành `(cos, 0, -sin)` thế giới, +Z thành `(sin, 0, cos)`. Phép đổi sang toạ độ local trong `CollectCells` là `u = dx*cos - dz*sin`, `v = dx*sin + dz*cos`. Kiểm tra bóng mờ và các ô xanh/đỏ khớp nhau ở góc 0°, 37°, 90°, 135°.
3. Thử trong Play Mode scene `prototype` (cần `Application.runInBackground = true` nếu Editor mất focus): đặt Nhà dân, Trại lính ở các góc; xoay bằng R, Q, E, giữ Q/E, giữ Shift; xác nhận:
   - ở 0°/90°/180°/270° tập ô **y hệt** hình chữ nhật cũ (công trình cũ không đổi);
   - ở góc chéo các ô răng cưa nằm sát hình nhà, không lọt khe hở lớn;
   - Xưởng đá vẫn tự quay về mỏ và Q/E không làm nó lệch;
   - nhà đặt xong có `GridOccupant.angle` đúng, tháo dỡ hiện đúng các ô, thợ xây đứng quanh nhà xoay chéo ở ngoài thân nhà;
   - quái đi vòng/phá nhà xoay chéo bình thường (flow field đọc `Occupancy`);
   - Esc, chuột phải, tháo dỡ (X) không hỏng.
4. Chạy so sánh: cho 5 loại footprint (1×1, 2×2, 3×3, 2×3, 1×4) và 4 góc bội 90°, so tập ô `CollectCells` với công thức cũ (hình chữ nhật từ `MinCell`); ghi kết quả. Công cụ so sánh đặt trong menu Editor `Debug`, không để trong game.
5. Thay `Math.Sin/Cos` khi tạo bảng bằng bảng hằng số sinh sẵn (để không phụ thuộc nền tảng khi vào lockstep). Có thể giữ bản sinh bằng `Math` nếu thêm kiểm tra bằng bảng hằng ở menu `Debug`.
6. Cập nhật `docs/figma/screens/21c_offline_menu_esc_dieu_khien.json` nếu cần (dòng phím mới), hoặc ghi chú trong `docs/figma/README.md` rằng bản Figma chưa có dòng Q/E.

**Tiêu chí xong:** 0 lỗi biên dịch, thử xoay được ở mọi góc, công trình cũ đặt y như trước, không lỗi console.

---

### Việc 2 — `heightGrid` chia chunk 50 m và Terrain ⬜

**Mục tiêu:** có lớp dữ liệu độ cao số nguyên theo chunk làm nguồn sự thật; mặt đất hiển thị bằng Unity Terrain; `BuildGrid` đọc độ cao qua lớp này thay vì raycast `float`.

**Thiết kế cần theo:**
- Chunk 50 × 50 ô (1 m/ô). `short` đơn vị 1/256 m. Kích thước bản đồ cấu hình được (mặc định 1.000 × 1.000 m).
- Lớp dữ liệu độc lập MonoBehaviour, ví dụ `Assets/Scripts/Runtime/Terrain/HeightField.cs` (+ `TerrainChunk.cs`): `short` mảng theo chunk, chỉ nạp/tạo chunk khi cần; API:
  - `int HeightAt(int cellX, int cellZ)` (đơn vị 1/256 m);
  - `int SampleBilinear(int xSub, int zSub)` với toạ độ 1/256 m (nội suy song tuyến bằng số nguyên);
  - `bool TryGetChunk(...)`, sự kiện/phiên bản khi chunk đổi (`Version` theo chunk);
  - ghi độ cao theo vùng (phục vụ sinh địa hình và sửa địa hình sau này).
- `BuildGrid` cần chia chunk: `occupancy`, `terrain`, `heights` hiện là mảng toàn bản đồ 50×50. Chuyển sang chia chunk 50 × 50 theo cùng lưới, **giữ nguyên API công khai mà code khác đang dùng** (`Evaluate`, `CollectCells`, `GetOccupancy`, `CellCenter`, `Occupancy`, `Terrain`, `Heights`, `Version`) hoặc cập nhật tất cả chỗ dùng. Phải sửa `EnemySimulationSystem.RebuildFlowField` nếu đổi dữ liệu (đang đọc `buildGrid.Terrain/Occupancy` theo chỉ số `x + y*Size.x`).
- Hạn chế bắt buộc: flow field của quái hiện tính cả bản đồ 50×50. Khi bản đồ 1.000 m thì không tính cả bản đồ một lần được. Việc này **tách riêng** (xem "Chưa thuộc phạm vi" dưới đây); trong việc này chỉ cần không làm hỏng chạy trên bản đồ thử.
- Unity Terrain: tạo Terrain (hoặc lưới Terrain, mỗi ô tương ứng một hay nhiều chunk) từ `HeightField`: `SetHeights` theo vùng đổi; Terrain Layers cho cỏ/đất/đá (vật liệu cơ bản trước, tinh chỉnh sau). **Thay mesh mặt đất đang dùng ở scene `prototype` bằng Terrain**, giữ layer `groundMask` và collider để chuột/raycast vẫn trúng; bake lại NavMesh (tạm thời cho dân/thợ vẫn dùng NavMesh) và kiểm tra các prefab đang đặt trên mặt đất cũ (cây, đá, HQ, khu vật cản thử) vẫn đứng đúng độ cao.
- Lấy mẫu `heightGrid` ban đầu từ Terrain/mặt đất hiện có **một lần khi nạp màn** (tạm thời, trước khi có sinh từ seed), rồi `BuildGrid` dùng nó.
- Công cụ Editor trong menu `Debug`: tạo Terrain từ mặt đất hiện tại, in thống kê chunk, kiểm tra cùng một trạng thái sinh hai lần ra cùng hash.

**Tiêu chí xong:** scene `prototype` chạy với mặt đất là Terrain; đặt công trình, tháo dỡ, thu thập, quái đi và đánh như trước; `BuildGrid` đọc độ cao qua `HeightField`; bản đồ cấu hình được kích thước, mặc định 1.000 × 1.000 m không crash (có thể chưa chơi mượt do flow field).

**Rủi ro cần để ý:** `Physics.Raycast` lên Terrain khác mesh cũ; NavMesh trên Terrain; ECS render mặt đất không liên quan; `BuildGrid.Awake` hiện cấp phát mảng `Persistent`.

---

### Việc 3 — Sửa lại phần đặt công trình (thích nghi địa hình) ⬜

**Mục tiêu:** đặt nhà trên địa hình gồ ghề: sàn phẳng, móng đá, ngưỡng theo loại nhà.

**Yêu cầu:**
- Độ cao lấy mẫu từ `HeightField` (nội suy song tuyến), không còn raycast trong đường đặt.
- Lấy mẫu các điểm cách nhau khoảng 0,5 m trong hình chữ nhật xoay; tính cao nhất, thấp nhất, trung bình.
- **Sàn** nằm ngang ở độ cao trung bình các góc (hoặc quy tắc rõ ràng, ghi trong tài liệu); `baseY` của bóng mờ và nhà đặt xong dùng độ cao sàn.
- **Móng đá**: sinh mesh (procedural, chỉ hiển thị) từ sàn xuống mặt đất ở phía thấp; vật liệu đá đơn giản. Không ảnh hưởng ô lưới.
- **Ngưỡng chênh độ cao** (cao nhất − thấp nhất) và độ dốc thành trường trong `BuildingData` (`maxHeightDelta`, `maxSlopeAngle` riêng cho từng loại; mặc định sơ bộ 1,0 m; nhà to chịu ít hơn). `BuildGrid.maxHeightDelta` toàn cục hiện là 0,5 m: chuyển thành giá trị mặc định khi `BuildingData` không đặt.
- Bảng chặn khi đặt (xem `building-placement.md` mục 7): đè công trình khác → chặn; chênh quá ngưỡng/dốc → chặn, báo "địa hình quá dốc"; ngoài biên/trong nước → chặn; có địch/quân trong khung → chặn; không đủ tài nguyên → chặn; đè vật thể `Clear` → tự biến mất; đè `Block` → chặn. Hiện lý do chặn trên giao diện (qua hệ thống thông báo sẵn có hoặc dòng chữ gần bóng mờ, **không** thêm nút test).
- Hàm kiểm tra viết bằng số nguyên, dùng chung cho bóng mờ và khi đặt xong (kiểm tra lại trước khi tạo công trình).
- Khi `HeightField` đổi sau khi nhà đã xây: dựng lại móng (chưa cần xử lý hư hại).

**Tiêu chí xong:** đặt Nhà dân, Trại lính trên dốc nhẹ thì sàn phẳng, móng chạm đất, không lơ lửng; dốc quá ngưỡng thì đỏ kèm lý do; công trình cũ trên đất phẳng không đổi hành vi.

---

### Việc 4 — Thuộc tính đặt và tự động sinh cây ⬜

**4a. Thuộc tính `placementRule` cho vật thể**
- Thêm dữ liệu cấu hình (ScriptableObject, ví dụ `ObjectPlacementData` hoặc trường trong dữ liệu tài nguyên hiện có): `placementRule` (`Block`/`Clear`), `footprint`, `clearable` (chặt/phá để lấy chỗ), `clearReward`.
- `GridOccupant` (cây, đá, bụi) mang tham chiếu tới cấu hình này; đặt công trình đọc nó:
  - ô bị chiếm bởi vật `Clear` **không** làm bóng đỏ; khi đặt xong, các vật `Clear` trong hình chiếu bị ẩn/xoá (lưu để khôi phục khi tháo nhà nếu thực hiện được);
  - ô bị chiếm bởi vật `Block` thì chặn.
- Cây hiện có trong scene chuyển sang dùng thuộc tính này (phân loại: ít nhất một loại cây lớn `Block`, một loại cây/bụi nhỏ `Clear` để kiểm thử cả hai).
- `BuildGrid` cần phân biệt "bị chiếm bởi vật Clear" và "bị chiếm bởi vật Block/công trình" (hiện `CellOccupancy` chỉ có `None/Static/Building`): thêm giá trị hoặc lớp riêng, **cập nhật mọi chỗ đọc** `CellOccupancy` (`EnemySimulationSystem`, `UnitCommandController`, `VillagerUnit`, `HarvesterUnit`, `SoldierUnit`...). Vật `Clear` vẫn nên coi là vật cản đi lại cho quái/dân cho tới khi bị dọn.

**4b. Tự động sinh cây**
- Hiện tại cây đặt tay trong scene; thay bằng hệ thống sinh theo thủ tục từ **seed**, chạy trên `HeightField`/Terrain:
  - rải theo độ cao, độ dốc, mật độ (bỏ qua nước, dốc, khu xuất phát và ô công trình);
  - dùng noise/hàm băm **số nguyên** và thứ tự duyệt cố định; cấm `UnityEngine.Random`, `Mathf.PerlinNoise`. Cùng seed → cùng kết quả, kiểm tra bằng hash;
  - rải theo chunk 50 m: chunk nào cần mới sinh;
  - tạo/huỷ đối tượng theo chunk gần camera (pooling hoặc instancing; số lượng lớn cần cẩn thận về hiệu năng, ghi lại thông số);
  - tạo `GridOccupant` + `ResourceNode` đúng loại, đánh dấu ô bị chiếm.
- Bản đồ kiểm thử 50×50 hiện tại phải sinh cây bằng hệ thống mới và cho kết quả chơi được như bản đặt tay.
- Công cụ Editor trong menu `Debug`: sinh lại với seed khác, in hash, đếm số cây theo loại.

**Tiêu chí xong:** cây sinh tự động từ seed (cùng seed cùng kết quả); cây lớn chặn đặt (bóng đỏ, báo "vướng cây, hãy chặt trước"); cây nhỏ/bụi bị nhà đè thì biến mất; thu thập gỗ ở cây sinh tự động vẫn hoạt động.

---

## 4. Chưa thuộc phạm vi (để việc sau)

Sinh địa hình từ seed; sửa địa hình bằng nét qua lệnh lockstep; lớp sơn đường/nước/hoa; vách đá và bậc thang tự sinh; chuyển kinh tế/xây dựng vào lockstep; chơi đơn chạy chung lockstep; EOS; chunk hoá flow field cho bản đồ 1.000 m trở lên (**sẽ là blocker để chơi bản đồ lớn thật sự**); tường vẽ bằng spline. Không làm các việc này trừ khi người dùng yêu cầu.

---

## 5. Quy trình làm việc và báo cáo

1. Làm lần lượt Việc 1 → 2 → 3 → 4. **Không sang việc sau khi việc trước còn lỗi biên dịch.**
2. Sau mỗi việc: biên dịch (0 lỗi), chạy thử Play Mode scene `prototype`, kiểm tra console không có lỗi/cảnh báo mới, rồi mới tính là xong.
3. Cập nhật tài liệu cùng lúc với code: `terrain.md`, `building-placement.md`, `roadmap.md` (giai đoạn 4b) đổi ⬜/🔶/✅ đúng thực tế. Không ghi "đã làm" những gì chưa chạy thử.
4. Không đổi giao diện người dùng ngoài những gì cần thiết (thông báo lý do chặn; dòng phím Q/E); không thêm nút test/cheat.
5. Báo cáo cuối mỗi việc: danh sách file sửa/tạo/xoá, những gì đã chạy thử và kết quả, những gì **chưa** chạy thử, quyết định tự đặt ra khi tài liệu chưa nói rõ.
6. Không dọn dẹp ngoài phạm vi, không xoá asset nếu chưa kiểm tra không còn tham chiếu.

## 6. Checklist cho người review (AI viết tài liệu này sẽ kiểm tra lại)

- [ ] Biên dịch 0 lỗi, console sạch khi chạy `prototype`.
- [ ] Mọi comment mới ngắn, tiếng Việt, đúng vị trí; không có git, không có tên "test" trong code game.
- [ ] Hàm mô phỏng mới dùng số nguyên, không `Random`/`Mathf.PerlinNoise`/`Time` trong đường chiếm ô, kiểm tra đặt, sinh cây.
- [ ] Tập ô ở góc bội 90° trùng khít hình chữ nhật cũ.
- [ ] `CellOccupancy`/`CellState` được cập nhật ở **mọi** nơi đọc chúng.
- [ ] Công trình, thợ, quái, tháp, minimap, tháo dỡ vẫn chạy như trước.
- [ ] Tài liệu `terrain.md`, `building-placement.md`, `roadmap.md` khớp với trạng thái thật.
- [ ] Không còn `Assets/Screenshots/` hay file tạm.
