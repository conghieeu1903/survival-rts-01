# Kế hoạch chuyển kinh tế + xây dựng + dân số + trại lính sang lockstep

Phạm vi: đưa phần kinh tế của game gốc (scene `prototype`) vào mô phỏng lockstep deterministic cạnh `BattleSimulation`. Tài liệu này chỉ phân tích và lập kế hoạch, chưa sửa code.

## 0. Nền tảng lockstep hiện có (cần bám theo)

- `LockstepNetwork` (`Network/LockstepNetwork.cs`): 10 lượt/giây (`TurnInterval = 0.1f`), host gom `LockstepCommand` rồi phát `TurnMessage`; mỗi client gọi `ILockstepSimulation.ApplyTurn(turn, commands)`; cứ 10 lượt gửi `ChecksumMessage`. Host ép `command.Team` theo kết nối (`OnServerCommand`).
- `LockstepCommand` (`Network/NetMessages.cs`) chỉ có `Team, Type, X, Z, Param`. Quá hẹp cho lệnh xây (cần loại công trình, hướng xoay, id đối tượng): phải thêm trường (mục c).
- `BattleSimulation` (`Battle/BattleSimulation.cs`): `NativeArray<int>` kiểu SoA, `StepsPerTurn = 2`, job Burst `FloatMode.Deterministic`, `BattleFixed.One = 256`, `Execute(command)` chạy tuần tự trước `Step()`. Hiện chỉ có `BattleCommand.MoveSquad/MoveArmy/PlayerLeft`.
- `LockstepNetwork.Simulation` chỉ nhận MỘT `ILockstepSimulation` (`net.Simulation = this` trong `BattleSimulation.Register`). Mô phỏng kinh tế phải chạy trong cùng vòng `ApplyTurn` (do `BattleSimulation` gọi một `EconomySimulation` con, hoặc một lớp bao ngoài).
- Khoảng trống cần vá trước: `BattleChecksumJob` chỉ băm PosX/PosZ/Health/Target/Timer, chưa băm `squadDestX/Z`, `stepIndex`. `BuildStats()` đổi float từ `BattleData` bằng `Mathf.RoundToInt` lúc khởi tạo (chấp nhận được vì cùng asset, nhưng dữ liệu kinh tế nên đi cùng một đường đổi duy nhất).
- Chưa có vào giữa trận/khôi phục (`StartMatch` chỉ ở đầu trận), nên chưa cần serialize trạng thái; ghi là câu hỏi mở.

## (a) Kiểm kê hệ thống game gốc

Tất cả trong `Assets/Scripts/Runtime/`.

| Class (file) | Trách nhiệm | Điểm không deterministic / trạng thái nằm trong GameObject |
|---|---|---|
| `BuildGrid` (`Building/BuildGrid.cs`) | Lưới 50x50, ô 1 m, căn quanh transform; `occupancy`, `terrain` (`CellState`), `heights` (float); `Evaluate()` kiểm ô trống/dốc/chênh cao; `Version` | `EnsureTerrain()` dùng `Physics.Raycast`, `Vector3.Angle`, `NavMesh.SamplePosition`; `heights` float; bake lười ở lần dùng đầu |
| `GridOccupant` (`Building/GridOccupant.cs`) | Vật cản/công trình đăng ký footprint vào lưới ở `Start()`/`Register()`, hủy ở `OnDestroy` | Chiếm ô gắn vòng đời GameObject; `MinCell` dùng float `transform.position`; `DemolishTime` mặc định 2.5f |
| `BuildingPlacer` (`Building/BuildingPlacer.cs`) | UI đặt/xoay/tháo dỡ, ghost; `Place()` trừ tài nguyên, `Instantiate`, gắn `GridOccupant`, `Health`, `PlayerTarget`, `ConstructionSite`, `NavMeshObstacle` | Raycast chuột, `Physics.OverlapBox` (`EnemyInside`), `EnemyWorld.AnyInRect`, NavMesh carving; sửa trạng thái trực tiếp từ input (trái quy tắc lockstep) |
| `BuildingData` (`SO/BuildingData.cs`) | footprint, `maxHealth` float, chi phí 5 loại, `buildTime`/`demolishTime` giây float, `populationProvided`, `maxWorkers`, `harvest*`, `tower`, `soldier` | Float giây/mét; `prefab` GameObject |
| `ConstructionSite` (`Building/ConstructionSite.cs`) | Tiến độ `elapsed` float, 3 giai đoạn hiển thị; khi xong `AddComponent` Tower/HouseBuilding/Barracks/ResourceCamp/BuilderHut | `AddWork(Time.deltaTime)` do `BuilderUnit` gọi; `Mathf.Cos/Sin` ở `GetWorkSlotPosition`; chức năng sinh bằng `AddComponent` |
| `DemolishSite` (`Building/DemolishSite.cs`) | Hoàn 100% chi phí ngay khi xác nhận, tắt component chức năng, đếm `elapsed += Time.deltaTime`, rung/lún | `Time.deltaTime`, `Mathf.Sin`; gọi thẳng `PopulationManager.RefundResources` |
| `HouseBuilding` | Cộng `populationProvided`, sinh dân quanh nhà | `AddPopulation` + sinh GameObject; `OnDestroy` trừ dân |
| `BuilderHut` + `IWorkerBuilding` (`Building/`) | Nhà thợ xây, +/- thợ từ dân rảnh | `PopulationManager.TryAssignBuilder` tạo GameObject |
| `ResourceCamp` (`Building/ResourceCamp.cs`) | Trại gỗ/xưởng đá/mỏ sắt/nông trại: tìm `ResourceNode` trong `harvestRadius` (hoặc 4 m nếu `requireAdjacentResource`), gán `HarvesterUnit`, tính điểm đứng/giao hàng | Duyệt `ResourceNode.All` (List theo thứ tự `OnEnable`), `sqrMagnitude` float, `Quaternion.Euler` |
| `ResourceNode` (`Building/ResourceNode.cs`) | Đánh dấu cây/đá/bụi quả/mỏ sắt; KHÔNG có sản lượng (vô hạn) | `FindObjectsByType<GridOccupant>(FindObjectsSortMode.None)` rồi đoán loại theo tên GameObject (`StartsWith("Tree")`...): thứ tự không cố định |
| `Barracks` (`Building/Barracks.cs`) | Hàng đợi `queued`, `trainTimer`; `TryAddWorker` trừ tài nguyên + `TryReserveWorker`; sinh `SoldierUnit` ở cửa, slot canh gác; hủy thì hoàn; `OnSoldierDied` giảm dân | `Time.deltaTime`, `Time.unscaledDeltaTime`, `Instantiate`; lính chết giảm `TotalPopulation` |
| `PopulationManager` (`Units/PopulationManager.cs`) | Kho `Gold/Food/Wood/Stone/Iron` (int), `TotalPopulation`, `AssignedWorkers`, `FreePopulation`; `CanAfford/TrySpend/Refund/RefundResources`; `TryAssignBuilder/Harvester`; spawn visual; khởi điểm `initialWood=10, initialStone=4, initialFood=6, initialFreePopulation=4, initialBuilders=1` | Singleton `Instance`, sự kiện `Changed` cho UI; trộn logic với sinh GameObject. Dữ liệu là int nên dễ chuyển nhất |
| `BuilderUnit` (`Units/`) | Tự tìm `ConstructionSite` gần nhất (`ActiveSites`), đi tới và `AddWork(Time.deltaTime)` | float, `Time.deltaTime`, `FindNearestSite` theo `sqrMagnitude`, `Slerp`; đi thẳng, không NavMesh |
| `HarvesterUnit` (`Units/`) | Đi tới điểm tài nguyên, khai thác `HarvestInterval`, về trại, `AddResource(type, HarvestAmount)` | float, `Time.deltaTime`; thu nhập phụ thuộc thời gian đi lại |
| `VillagerUnit` (`Units/`) | Dân rảnh đi dạo | `Random.Range`, `Random.insideUnitCircle` (chỉ thẩm mỹ) |
| `SoldierUnit` + `SoldierData` (`Units/`, `SO/`) | Lính canh gác: nhận mục tiêu từ `SoldierCombatSystem`, đuổi, chém; `CommandMove`; chi phí đào tạo `foodCost=3`, `ironCost`, `goldCost`, `trainTime` | `Entity` ECS, `Time.deltaTime`, float; `OnDestroy` -> `Barracks.OnSoldierDied` |
| `Health`, `PlayerTarget`, `Headquarters` (`Combat/`, `Building/`) | Máu float; `PlayerTarget` cấp id tăng dần cho ECS kẻ địch; chết thì `Destroy`; Nhà chính chết = thua | float; id cấp theo `Awake` (thứ tự không cố định) |
| `Tower`, `TowerData`, `TowerJobs.cs` | Tháp tạo entity ECS (`EnemyWorld.CreateTower`), `TowerFireJob` bắn zombie | `float DeltaTime`, cooldown float, ECS đơn người |
| `EnemySimulationSystem`, `EnemyJobs`, `FlowFieldJob`, `SoldierCombatSystem` (`Enemies/ECS/Systems/`), `EnemySpawner`, `EnemySpawnArea`, `EnemyWorld` | Zombie ECS: spawn ngẫu nhiên, flow field Dijkstra trên `BuildGrid` (đọc `Version`), tách nhau, đánh `PlayerTarget` | `Unity.Mathematics.Random` (gọi theo thứ tự frame), `SystemAPI.Time.DeltaTime`, float2/float3, `Dictionary<int,float>` gộp sát thương, `BuildGrid.Heights` float |
| UI: `BuildMenuController`, `HealthBarController`, `UnitCommandController`, `MinimapController` (`UI/`) | Menu xây, bảng chọn công trình (+/- thợ, tháo dỡ, đào tạo), lệnh lính, bản đồ nhỏ | Đọc `PopulationManager.Instance`, `BuildGrid.Instance`; nút gọi thẳng `TryAddWorker/ConfirmDemolish/CommandMove` |

Nhận xét: trạng thái kinh tế (kho, dân, thợ, hàng đợi, tiến độ xây) rải trong các MonoBehaviour, không có một nguồn sự thật. Cần dựng mô phỏng mới rồi biến các class trên thành lớp hiển thị.

## (b) Mô hình dữ liệu số nguyên đề xuất

Nguyên tắc: lớp mới `EconomySimulation` cạnh `BattleSimulation`; tất cả `NativeArray<int/short/byte>` kiểu SoA, dung lượng cố định cấp lúc `StartMatch` (không cấp phát khi chạy), mỗi đội một đoạn chỉ số (như `team * unitsPerTeam`). Id đối tượng = chỉ số mảng, không dùng tên/GameObject/InstanceID. Slot trống cấp lại theo "slot trống thấp nhất" (quét tuần tự) để mọi máy chọn cùng slot.

Đơn vị thời gian: kinh tế tick 1 lần mỗi lượt (100 ms); thời gian trong SO (giây) đổi `ceil(giây*10)` một lần ở bước dựng stats, giống `BattleSimulation.BuildStats()`.

### Lưới xây dựng chung (`EconGrid`, thay `BuildGrid`)
- Kích thước: `BuildGrid` hiện 50x50 ô 1 m, quá nhỏ cho bản đồ `mapHalfSize = 200` (400x400 m). Đề xuất ô 1 m, lưới toàn bản đồ 400x400 = 160.000 ô, chung cho mọi đội (chống đặt chồng giữa các đội). Lưới 4 m của lính (`BattleGrid`) độc lập, không trộn.
- Mảng: `byte terrain` (đã bake, bất biến: Free/OffGround/Steep/NoNavMesh), `short heightFixed` (1/256 m, bake, dùng hiển thị và kiểm chênh cao), `int occupant` (id công trình/node, -1 = trống), `byte occKind` (None/Static/Building, tương ứng `CellOccupancy`).
- Tọa độ ô từ vị trí cố định bằng phép chia nguyên như `BattleGrid.Cell`.
- Bake địa hình deterministic: làm ở EDITOR, không bake lúc chạy. Công cụ editor (đặt trong menu `Debug`/`Assets/Scripts/Editor/`) chạy lại logic `BuildGrid.EnsureTerrain()` (Raycast + góc dốc + `NavMesh.SamplePosition`) rồi ghi ra asset `EconMapData` (`byte terrain`, `short height` = round(y*256), hash nội dung). Runtime chỉ đọc asset này nên không còn Physics/NavMesh/float. Hash asset đưa vào checksum lượt 0 để bắt lệch phiên bản giữa các máy.
- Tài nguyên tĩnh (cây, đá, mỏ sắt, bụi quả) bake cùng: danh sách (loại, ô, sản lượng) xếp thứ tự cố định (z rồi x), thay cho `ResourceNode.EnsureSceneNodesRegistered()` (đoán theo tên, `FindObjectsSortMode.None`). Vật cản tĩnh khác (`GridOccupant.isBuilding == false`) bake thành `occKind = Static`.
- Mở: bản đồ PvP là scene `battle` hay bake lại từ `prototype`.

### Công trình (dung lượng cố định mỗi đội, ví dụ 512)
`bActive(byte)`, `bTeam(byte)`, `bType(byte, chỉ số BuildingData)`, `bCellX/bCellZ(short, ô góc min)`, `bTurns(byte, xoay 0..3)`, `bState(byte: Constructing/Active/Demolishing)`, `bHealth(int)`, `bProgress(int, tick)`, `bWorkers(byte)`, `bQueued(byte)`, `bTrainLeft(int, tick)`.
Dữ liệu tĩnh theo loại: `NativeArray<EconBuildingStats>` dựng từ `BuildingData` lúc khởi tạo: footprint, `maxHealth`, chi phí 5 loại, `buildTicks`, `demolishTicks`, `populationProvided`, `maxWorkers`, `harvestResource`, `harvestRadiusCells` (int, so sánh bình phương ô), `harvestTicks`, `harvestAmount`, `soldierType`, `towerType`.

### Kho tài nguyên và dân số
`stock[team * 5 + res]` int (Wood, Stone, Food, Iron, Gold); `popTotal[team]`, `popAssigned[team]`; `FreePopulation = popTotal - popAssigned`. Chỉ cập nhật trong `ApplyTurn` theo thứ tự lệnh rồi theo chỉ số công trình. Giá trị khởi đầu lấy từ `PopulationManager.initial*`.

### Dân và công việc (bỏ chuyển động thật)
Thay đổi lớn nhất: không mô phỏng từng dân đi bộ; dân/thợ là "slot công việc" gắn vào công trình, không có vị trí.
- Thợ xây: nằm trong `BuilderHut` (`bWorkers`). Mỗi tick, danh sách công trường `Constructing` của đội theo chỉ số tăng, chia thợ rảnh theo round-robin cố định (thay luật "gần nhất theo float" của `BuilderUnit.FindNearestSite`). Mỗi thợ đóng góp 1 đơn vị tiến độ/tick.
- Thợ khai thác: mỗi trại có `bWorkers`; mỗi worker chạy chu kỳ `harvestTicks + travelTicks`, hết chu kỳ cộng `harvestAmount` vào `stock`. `travelTicks` tính bằng số nguyên từ khoảng cách ô tới node (Chebyshev/Manhattan nhân hằng, chia tốc độ int), không đi thật. Node chọn theo luật cố định (sắp theo chỉ số ô, `slotIndex % count` như `TryGetHarvestPoint`).
- Dân rảnh: chỉ là số đếm; đi dạo thuần hiển thị.
- Cập nhật mỗi tick bằng job tuần tự theo chỉ số công trình (không song song ghi vào `stock`).

### Xây dựng và tháo dỡ
- Xây: `bProgress` tăng theo số thợ tại công trường; `>= buildTicks` thì `Active`, kích hoạt chức năng (nhà cộng `popTotal`, trại bật huấn luyện, trại khai thác nhận worker).
- Tháo dỡ: hoàn 100% tài nguyên lúc xác nhận (như `DemolishSite.Setup`), `bState = Demolishing`, đếm ngược `demolishTicks`, xong thì giải phóng ô, trả thợ về dân rảnh, trừ `popTotal` nếu là nhà.

### Huấn luyện (Barracks)
`bQueued`, `bTrainLeft`. Tương đương `Barracks.TryAddWorker`: kiểm `workerCount < maxWorkers`, `FreePopulation > 0`, đủ tài nguyên, trừ ngay, `popAssigned++`. Mỗi tick giảm `bTrainLeft`; về 0 thì sinh lính (mảng lính của `BattleSimulation`, mục e), `bQueued--`, đặt lại `trainTicks` nếu còn hàng. Hủy: hoàn tài nguyên, `popAssigned--`. Phá trại: hoàn các lượt đang chờ, giải ngũ lính (như `Barracks.OnDestroy`).

### Checksum
Mọi mảng trên (trừ dữ liệu bake bất biến, băm một lần ở lượt 0) vào `EconChecksumJob` kiểu FNV như `BattleChecksumJob`, trộn vào kết quả `ComputeChecksum()`.

## (c) Lệnh lockstep cần thêm và validate

Mở rộng `LockstepCommand` thêm hai trường int (ví dụ `A`, `B`); thêm bảng `EconCommand` cạnh `BattleCommand`, dải số riêng (200+). Đổi struct này làm đổi định dạng gói FishNet, mọi người chơi phải cùng phiên bản.

| Lệnh | Tham số | Validate trên mô phỏng (mọi máy chạy y hệt, lệnh sai bị bỏ qua im lặng) |
|---|---|---|
| `Build` | Param=chỉ số `BuildingData`, X/Z=ô góc, A=xoay | Đội hợp lệ; loại hợp lệ; footprint sau xoay trong lưới; mọi ô `terrain == Free` và `occupant == -1` (thay `BuildGrid.Evaluate`); chênh cao `<= maxHeightDelta` (số nguyên đã bake); `requireAdjacentResource`; đủ tài nguyên (thay `CanAfford`); luật lãnh thổ/gần căn cứ (mới, nếu PvP); không có lính trên ô (thay `EnemyInside`, kiểm bằng lưới lính 4 m hoặc bỏ). Đạt thì trừ tài nguyên, cấp id thấp nhất, chiếm ô, `Constructing` |
| `Demolish` | Param=id công trình | Thuộc đúng đội; không phải Nhà chính; chưa `Demolishing` |
| `AssignWorker` / `UnassignWorker` | Param=id công trình | Công trình của đội, `Active`, có `maxWorkers`; `workers < max`; `FreePopulation > 0` |
| `QueueTrain` / `CancelTrain` / `Dismiss` | Param=id trại | Như `Barracks.CanAddWorker/TryRemoveWorker` (đủ tài nguyên, dân rảnh, `maxSoldiers`) |
| `SetSoldierPost` / `ReturnToBarracks` | Param=nhóm lính, X/Z | Thay `SoldierUnit.CommandMove`; ô đích không bị chiếm như `UnitCommandController.TryGetGroundPoint` |

Quy tắc chung:
- Hai đội tranh một ô cùng lượt: lệnh đứng trước trong mảng do host phát thắng (deterministic).
- UI chỉ gọi `LockstepNetwork.SendCommand`. Ghost và ô xanh/đỏ ở UI đọc lưới của mô phỏng (chỉ để hiển thị); kết quả thật có sau khi lệnh quay về (trễ khoảng 1-3 lượt), nên UI cần trạng thái "đang chờ".
- Người rời trận: đã có `BattleCommand.PlayerLeft = 100` do host chèn; kinh tế cần luật tương ứng (bot tiếp quản hoặc đóng băng).
- `BattleBot` chỉ ra `MoveArmy`; cần bot kinh tế (xây theo kịch bản cố định) để kiểm thử nhiều máy tự động.

## (d) Hiển thị: đọc từ mô phỏng, không ghi ngược

- Tạo `EconomyPresentation` đọc mảng của `EconomySimulation` sau mỗi `ApplyTurn`:
  - Công trình: tạo/hủy GameObject theo `bActive/bType/bState` (so khớp theo chỉ số; `Dictionary<int,GameObject>` chỉ ở phía hiển thị). `ConstructionSite`/`DemolishSite` thành thành phần thuần hiển thị đọc `bProgress` chọn giai đoạn 1-3 và rung/lún, nội suy giữa hai lượt như `BattleUnitPresentation`.
  - Dân/thợ: giữ `VillagerUnit/HarvesterUnit/BuilderUnit` làm "diễn viên", bỏ logic kinh tế; số diễn viên theo `bWorkers`, đi/về/vung búa theo chu kỳ mô phỏng. Sai lệch vị trí chấp nhận được vì không ảnh hưởng kết quả.
  - Kho, dân số: `PopulationManager` còn lại là bản sao hiển thị (`Wood/Stone/...`, `Changed`) cập nhật mỗi lượt; `BuildMenuController`, `HealthBarController` tiếp tục nghe `Changed`.
  - `BuildingPlacer`: giữ ghost/raycast/chuột, `Evaluate` đọc `EconGrid`; `Place()` đổi thành `SendCommand(Build...)`; bỏ `Instantiate`, `AddComponent`, `NavMeshObstacle`.
  - `HealthBarController`: nút +/-/tháo dỡ gửi lệnh; đọc `bWorkers/bQueued/bTrainLeft/bProgress`.
  - `MinimapController`: đọc `EconMapData.terrain` và `occupant` thay cho `BuildGrid.Terrain/Heights`.
  - Âm thanh: mô phỏng phát sự kiện (xong công trình, tháo dỡ, thợ gõ búa) như `ShotFired`/`MeleeHit` của `BattleSimulation`, nối qua `AudioDirector`/`BattleAudio`; không phát âm thanh từ `ConstructionSite.Complete()` nữa.
- Quy tắc cứng: lớp hiển thị không ghi vào mảng của `EconomySimulation`; đường duy nhất vào là `SendCommand`.
- Job hiển thị đọc `NativeArray` phải hoàn tất trước khi mô phỏng ghi (mẫu `CompletePresentation()` ở `BattleSimulation`).

## (e) Điểm không thể chuyển nguyên trạng và phương án

1. Zombie ECS (`EnemySimulationSystem`, `EnemyJobs`, `FlowFieldJob`): float, `Time`, `Random`, entity. Phương án: (A) bỏ zombie khỏi PvP; (B) về sau viết lại như "đội thứ 5" dùng chính `BattleSimulation`, spawn bằng bộ sinh số tự viết (xorshift/LCG) gieo từ `StartMatchMessage.Seed`, đến lượt cố định (đồng hồ lượt, không `Time`), đường đi bằng flow field int. Nên làm (A) trước.
2. Tháp (`Tower`, `TowerData`, `TowerFireJob`): thành công trình trong `EconomySimulation` có `towerType`; mỗi tick (cooldown tick), tìm địch gần nhất trong lưới lính 4 m của `BattleSimulation` theo `DistSq` số nguyên (bằng nhau lấy chỉ số nhỏ), ghi sát thương như `BattleDamageJob` (gộp tuần tự). Tia đạn tái dùng `ShotFired`. Cần `bHealth` int và sát thương lên công trình.
3. Đường đi: dân không cần đi thật (đã mô hình hóa bằng số). Lính chiến hiện đi thẳng và chỉ đẩy nhau (`BattleStepJob`), chưa né công trình. Nếu cần né: flow field int trên `EconGrid` (Dijkstra, chi phí 10/14 như `FlowFieldJob` nhưng toàn int, tie-break theo chỉ số ô), tính lại khi occupancy đổi (ý tưởng `BuildGrid.Version`); chỉ tính cho vài đích để rẻ. Ở giai đoạn đầu có thể coi công trình là chướng ngại mềm.
4. Nhặt tài nguyên (`ResourceNode`, `HarvesterUnit`): node bake ở editor, sản lượng theo chu kỳ tick; thêm `nodeLeft` (mặc định rất lớn, hiện node vô hạn) để sau này bật cạn kiệt.
5. Đặt công trình cạnh kẻ địch (`EnemyInside`, `Physics.OverlapBox`): đổi sang tra lưới lính của `BattleSimulation` hoặc bỏ ràng buộc; làm sau.
6. Sinh lính vật lý (`Barracks.SpawnSoldier`, `Instantiate`): `BattleSimulation` cấp mảng lính cố định lúc `StartMatch` (`unitsPerTeam`, tiểu đội 100). Chưa có "tăng lính giữa trận": cần trạng thái slot dự trữ (hiện `Health <= 0` nghĩa là chết) hoặc để lính đào tạo bổ sung vào tiểu đội có sẵn. Đây là thay đổi lớn nhất phía chiến đấu.
7. `Health`/`PlayerTarget`/`Headquarters`: `Health` float, id cấp theo `Awake`. Đổi sang `bHealth` int, id = chỉ số; điều kiện thua thêm "Nhà chính bị phá" ngoài `WinnerTeam` hiện tại (còn một đội có lính, trong `RefreshSquads`).
8. Thời gian: `Time.deltaTime`, `trainTimer`, `buildTime`, `demolishTime` đổi hết sang tick nguyên; giữ SO ở dạng giây và làm tròn một lần.
9. `ConstructionSite.Complete()` dùng `AddComponent` theo `BuildingData`: thay bằng bảng khả năng theo loại trong `EconBuildingStats`.
10. `Random` ở `VillagerUnit` và `Mathf.Sin/Cos` trong chuyển động: để ở lớp hiển thị, không đọc ngược.

## (f) Giai đoạn làm

Quy ước kiểm thử desync (theo `.agents/rules/multiplayer.md`): chạy cùng chuỗi lệnh hai lần so checksum; Editor làm host cùng các bản build `-join <ip> -bot` so checksum từng mốc 10 lượt. Đề xuất thêm công cụ ghi/phát lại chuỗi `LockstepCommand` theo lượt (đặt trong menu Editor `Debug`, `Assets/Scripts/Editor/DebugWindow.cs`) để chạy không mạng; thêm kiểm bất biến sau mỗi tick (kho không âm, `popAssigned <= popTotal`, ô `occupant` khớp công trình `Active`); thử chạy dồn nhiều lượt trong một khung hình.

| GĐ | Nội dung | Độ lớn / rủi ro | Tự kiểm chứng |
|---|---|---|---|
| 0 | Băm thêm `squadDestX/Z`, `stepIndex`; mở rộng `LockstepCommand` + bảng `EconCommand`; công cụ ghi/phát lại lệnh; khung `EconomySimulation` rỗng gắn vào `ApplyTurn`/`ComputeChecksum` | Nhỏ / thấp (đổi gói FishNet) | Trận hiện tại vẫn khớp checksum hai lần |
| 1 | Bake địa hình + tài nguyên ra `EconMapData` (Editor); `EconGrid`; minimap/ghost đọc từ đây | Vừa / vừa (đúng logic raycast/dốc/NavMesh, chọn bản đồ và kích thước lưới) | Từng ô khớp `BuildGrid.Terrain` trong Editor; hash asset trùng mọi máy; checksum lượt 0 bằng nhau |
| 2 | Kho + dân số + lệnh `Build/Demolish` với công trình dựng tức thì; hiển thị từ mô phỏng; `BuildingPlacer.Place` gửi lệnh | Vừa / vừa (viết lại `Place`, `DemolishSite`; trễ phản hồi UI) | Hai người đặt cùng ô cùng lượt -> cùng người thắng trên mọi máy; replay hai lần; kho không âm |
| 3 | Tiến độ xây theo tick + thợ xây (`BuilderHut`, round-robin); nhà cộng dân khi xong | Vừa / thấp-vừa | Replay nhiều công trường, thợ ít hơn công trường; so tiến độ giữa các máy |
| 4 | Khai thác: trại gỗ/đá/sắt/nông trại, chu kỳ tick, node bake; `AssignWorker/UnassignWorker`; diễn viên hiển thị | Vừa / vừa (mất "hành trình thật", cân bằng lại số liệu) | Bot kinh tế 10 phút: `stock` khớp giữa hai lần chạy và giữa các máy; thu nhập tương đương game gốc |
| 5 | Trại lính: hàng đợi, `trainTime` tick, tiêu hao, hủy/hoàn, giải ngũ (lính chưa vào chiến đấu) | Vừa / thấp | Replay hàng đợi, hủy, phá trại giữa chừng; bất biến dân |
| 6 | Hợp nhất lính với `BattleSimulation` (slot dự trữ hoặc bổ sung tiểu đội); `SetSoldierPost`; thua khi mất Nhà chính | Lớn / cao (đụng `unitsPerTeam`, `BattleUnitPresentation`, checksum, hiệu năng 16.000 lính) | Trận 4 đội kinh tế đầy đủ, nhiều máy, so checksum 10+ phút; đo thời gian bước mô phỏng |
| 7 | Tháp và công trình bị tấn công (`bHealth` int); hoàn thiện điều kiện thua | Vừa-lớn / vừa | Replay có tấn công công trình; thử phá Nhà chính |
| 8 | (tùy chọn) Zombie/đội thứ 5 bằng mô phỏng số nguyên | Lớn / cao | Seed cố định cho kết quả giống nhau mọi máy; replay |
| 9 | Dọn dẹp: gỡ logic kinh tế khỏi MonoBehaviour cũ; chế độ đơn người (nếu giữ) chạy cùng `EconomySimulation` với mạng giả cục bộ | Vừa / thấp | Không còn `Time.deltaTime` trong logic kinh tế |

Giai đoạn 2-5 không đụng chiến đấu; 6 là cột mốc rủi ro nhất.

## (g) Rủi ro chính và câu hỏi cho chủ dự án

Rủi ro:
- Độ trễ lệnh: xây/đào tạo chỉ hiệu lực sau vòng host (từ 1 lượt, 100 ms, tới vài trăm ms); ghost không phải kết quả cuối.
- Cảm giác khác game gốc: thợ không đi thật, thời gian đi/làm rút thành số; phải cân bằng lại.
- Hiệu năng: lưới 160.000 ô và tick kinh tế rẻ; rủi ro nằm ở sinh lính giữa trận trong mảng cố định 16.000 lính và flow field lính chiến.
- Bake địa hình lỗi thời nếu scene đổi mà quên bake: cần hash và cảnh báo.
- Nhiều file đang sửa chưa commit (`Assets/Scenes/prototype.unity`, `UiPrefabGenerator.cs`, các prefab UI): chốt trước khi làm để tránh xung đột.
- Phụ thuộc bản vá `Assets/FishNet/Runtime` (nâng cấp sẽ mất); đổi `LockstepCommand` làm lệch tương thích giữa các bản build.
- Không có vào giữa trận/khôi phục: rớt mạng không vào lại được; trận kinh tế dài làm điểm này nặng hơn.
- Mọi chỗ còn float khi dựng stats từ SO (`BuildStats`, tương tự `EconBuildingStats`) phải đi một đường đổi duy nhất.

Câu hỏi cần quyết định:
1. PvP chỉ kinh tế nhẹ (kho, vài công trình, huấn luyện) hay đầy đủ như game gốc (nhà, 4 loại khai thác, thợ xây, tháp, tháo dỡ)? Quyết định này đổi cỡ giai đoạn 3-7.
2. Zombie còn trong PvP không? Nếu có: đội thứ 5 trong `BattleSimulation` hay làm sau? Có giữ chế độ sinh tồn đơn người song song không?
3. Bản đồ PvP: dùng lại địa hình `prototype` hay bản đồ mới (scene `battle`)? Kích thước lưới xây dựng, ranh giới lãnh thổ từng đội?
4. Quân PvP: lính đào tạo ở trại thay thế hay cộng thêm vào 4.000 lính mặc định (tiểu đội 100)? Mỗi đội bắt đầu với bao nhiêu lính?
5. Dân/thợ có cần đi thật, bị tấn công, bị giết không, hay chỉ là chỉ số?
6. Tài nguyên có cạn không (hiện node vô hạn)? Điều kiện thắng: hết lính, phá Nhà chính, hay cả hai?
7. Người rời/rớt: bot tiếp quản (bot cần biết xây dựng kinh tế) hay đóng băng đội?
8. Chấp nhận lệnh xây trễ một vòng host, hay cần dự đoán cục bộ?
