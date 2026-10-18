# THIẾT KẾ ECS CHO KẺ ĐỊCH
## After the Fall — Survival RTS

**Nguồn:** `tech.md` (nhiều quái → dùng ECS), `mechanics.md` mục 4–6 (chiến đấu, AI, đợt tấn công)
**Phạm vi:** chỉ kẻ địch. Công trình, đơn vị phe mình, UI, camera giữ nguyên MonoBehaviour (hybrid).
**Trạng thái:** đã triển khai E1–E5 (xem mục 0). Mục tiêu nâng lên **10.000** kẻ địch.

---

## 0. Triển khai hiện tại (khác thiết kế ban đầu ở đâu)

| Thiết kế ban đầu | Đã làm | Lý do |
|---|---|---|
| SubScene + Baker | Prefab entity tạo lúc chạy từ `EnemyData` (`EnemyEcsConfig` → `RenderMeshUtility`) | Ít asset, không cần SubScene, đổi `EnemyData` là xong |
| 10 system riêng | Một `EnemySimulationSystem` (SystemBase) điều phối chuỗi job Burst | Dữ liệu native (flow field, spatial grid) chỉ có một chủ, phụ thuộc job rõ ràng |
| `TargetRegistry` | `PlayerTarget` (MonoBehaviour) tự đăng ký | Gắn thẳng lên HQ và công trình đặt xuống |

Chuỗi job mỗi frame: `EnemySnapshotJob` → `EnemySortJob` (spatial grid theo ô) → `EnemySimulateJob` (tìm mục tiêu, flow field, né nhau, tấn công) → `EnemyDeathJob`. Flow field (`FlowFieldJob`) chỉ tính lại khi `BuildGrid.Version` đổi.

**Đo trong Editor (chỉ phần mô phỏng, chưa gồm vẽ):**

| Số kẻ địch | Trung bình mỗi frame | p95 | Frame spawn |
|---|---|---|---|
| 200 | 0,27 ms | — | — |
| 10.005 | 0,59 ms | 0,74 ms | 31 ms (spawn 10.000 con cùng lúc, một lần) |

**Animation (GPU, bone texture):**
- Không dùng Animator. Animation được nướng thành bone texture (mỗi hàng một frame, mỗi xương 3 pixel), shader `SurvivalRts/GpuSkinnedLit` tự skin ở vertex stage.
- `EnemyAnimationSystem` chọn clip theo trạng thái (chết → Death, `Engaged` → Attack, `Moving` → Walk, còn lại Idle) và ghi `EnemyAnimData` (`[MaterialProperty("_AnimData")]`) cho từng thực thể. Idle/Walk lệch pha theo thực thể. Quái chết nằm lại hết clip Death + 2 giây rồi mới bị hủy.
- Nhân vật thử "Grunt" (192 tam giác, 11 xương, 4 clip) do menu **SurvivalRts → Tạo kẻ địch thử (Grunt)** sinh ra hoàn toàn bằng code. Bộ nướng `GpuAnimationBaker` dùng lại được cho model có rig thật (Mixamo, Asset Store): truyền root, danh sách xương theo chỉ số trong mesh, bindpose và các `AnimationClip`. Mesh cần chỉ số xương ở UV1.x (mỗi đỉnh gắn một xương).

**Vấn đề đã thấy:** bản đồ thử 50x50 quá nhỏ cho 10.000 con. Chúng dồn thành một khối chồng lên nhau quanh HQ (né nhau bị giới hạn 16 hàng xóm và lực kéo về đích lớn hơn lực đẩy). Cần bản đồ lớn hơn và chỉnh lực né nhau (mục 11, câu hỏi 5).

---

## 1. Mục tiêu và ràng buộc

| Mục | Giá trị |
|---|---|
| Số kẻ địch cùng lúc | Thiết kế cho **2000**, mục tiêu tối thiểu 60 FPS ở 1000 (PC) |
| Hành vi | Spawn theo vùng tròn → tiến về HQ → đánh mục tiêu trong tầm → chết |
| Độ phức tạp AI | Máy trạng thái đơn giản, không phối hợp, không rút lui (đúng `mechanics.md` mục 5) |
| Đồ họa | Low poly, hiện tại là capsule thử |
| Cùng tồn tại với | Công trình và đơn vị phe mình là GameObject (có `Health` MonoBehaviour, NavMesh) |

Package đã có sẵn trong project: `entities`, `entities.graphics`, `physics`, `collections` (cùng bản 6.6.0), `burst` 2.0.0, `mathematics` 1.4.0.

---

## 2. Quyết định kiến trúc

### 2.1. Hybrid, ranh giới rõ ràng

| Thế giới | Gồm | Công nghệ |
|---|---|---|
| **ECS** | Kẻ địch (di chuyển, tìm mục tiêu, tấn công, máu, chết) | Entities + Burst + Jobs |
| **GameObject** | HQ, công trình, đơn vị phe mình, UI, camera, đặt công trình, `GameManager`, `WaveManager` | MonoBehaviour |
| **Cầu nối** | Mục tiêu (phe mình) và sát thương hai chiều | Mục 5 |

Lý do: số lượng nằm ở phe địch. Phe mình chỉ vài chục, dùng GameObject cho đơn giản và dùng lại code đã có (`Health`, `BuildGrid`, NavMesh, UI Toolkit).

### 2.2. Tìm đường: flow field thay vì NavMesh

`NavMeshAgent` không dùng được trong ECS, và tự gọi `NavMeshQuery` cho 2000 con rất tốn. Mọi kẻ địch đều đi về **cùng một đích (HQ)**, đúng bài toán của **flow field**:

- Tính **một lần** một trường vector trên lưới, mỗi ô chỉ về hướng ô kế tiếp gần HQ nhất.
- Mỗi kẻ địch chỉ đọc ô đang đứng để biết hướng đi: chi phí **O(1)** mỗi con, không phụ thuộc số lượng.
- Dùng lại lưới 1m của `BuildGrid` (50x50 = 2500 ô, tính lại chỉ vài trăm µs).

**Chi phí ô:**

| Ô | Chi phí |
|---|---|
| Đi được, trống | 1 |
| Có công trình phe mình | **cao (ví dụ 20) nhưng không chặn hẳn** |
| Cây, đá, tường tĩnh, nước, ngoài bản đồ | Chặn hẳn |

Công trình tính chi phí cao thay vì chặn hẳn có hệ quả quan trọng: nếu người chơi xây tường **chặn kín đường tới HQ**, flow field vẫn có đường đi xuyên qua công trình chặn. Kẻ địch đi tới đó, bị chặn lại và **tấn công công trình chắn đường**. Nhờ vậy:
- Không cần quy tắc "không cho chặn kín đường" ở `BuildingPlacer`, chỉ cần bỏ bước đó (xem câu hỏi mở 1).
- Không có tình huống quái kẹt vô hạn.

Flow field tính lại khi lưới thay đổi (đặt hoặc phá công trình), có cờ `dirty` và gom nhiều thay đổi trong cùng một frame.

### 2.3. Né nhau (separation)

Không dùng avoidance của NavMesh. Dùng **lực đẩy nhẹ giữa các kẻ địch gần nhau**, tra cứu bằng **spatial hash** theo ô lưới (`NativeParallelMultiHashMap<int, Entity>` hoặc mảng sắp xếp theo ô). Chỉ xét 9 ô quanh con đang xét. Đủ để quái không chồng lên nhau, không cần vật lý.

### 2.4. Vật lý: chưa dùng Unity Physics

Không cần va chạm vật lý cho MVP: di chuyển theo flow field, né nhau bằng spatial hash, tầm đánh bằng khoảng cách. Nếu sau này cần click chọn quái hoặc raycast thì mới thêm `physics`.

---

## 3. Dữ liệu (Components)

Tất cả là `IComponentData` nhỏ, không có kiểu managed.

| Component | Trường | Ghi chú |
|---|---|---|
| `EnemyTag` | — | Đánh dấu thực thể kẻ địch |
| `EnemyHealth` | `float Current, Max` | Máu |
| `EnemyStats` | `float MoveSpeed, Damage, AttackRange, AttackCooldown, DetectRange` | Đọc từ `EnemyData` (SO), bake vào |
| `EnemyState` | enum `Advance`, `Engage`, `Dead` | Trạng thái máy trạng thái |
| `EnemyTarget` | `int TargetId, float3 TargetPosition, bool HasTarget` | Mục tiêu đang đánh (phe mình) |
| `AttackTimer` | `float Remaining` | Đếm ngược giữa hai đòn |
| `LocalTransform` | (có sẵn) | Vị trí, hướng |

**Singleton / dữ liệu dùng chung:**

| Dữ liệu | Kiểu | Ai ghi | Ai đọc |
|---|---|---|---|
| `FlowField` | `NativeArray<float2>` hướng + `NativeArray<int>` chi phí | `FlowFieldSystem` | `EnemyMovementSystem` |
| `TargetGrid` | Mảng mục tiêu phe mình, bucket theo ô | `TargetSyncSystem` | `EnemyTargetingSystem` |
| `EnemySpatialIndex` | Spatial hash kẻ địch | `EnemySpatialSystem` | Separation, và code phe mình (xem 5.3) |
| `DamageToPlayer` | `DynamicBuffer<DamageEvent>` | Hệ thống tấn công | `DamageApplySystem` |
| `SpawnRequest` | Entity tạm | `EnemySpawner` (managed) | `EnemySpawnSystem` |

**Cấu hình qua ScriptableObject** (theo quy ước dự án: code ở `Assets/Scripts/SO`, asset ở `Assets/ScriptableObjects`):

- `EnemyData`: máu, tốc độ, sát thương, tầm đánh, tốc độ đánh, tầm phát hiện, prefab.
- Một `Baker` đọc `EnemyData` và ghi vào component lúc bake, nên đổi số liệu không cần sửa code.

---

## 4. Hệ thống (Systems)

Chạy theo thứ tự này mỗi frame, `ISystem` + `[BurstCompile]` + `IJobEntity` song song khi được.

| # | System | Việc | Loại |
|---|---|---|---|
| 1 | `EnemySpawnSystem` | Đọc `SpawnRequest`, tạo thực thể trong vùng tròn, gán chỉ số | Burst, dùng `EntityCommandBuffer` |
| 2 | `FlowFieldSystem` | Khi lưới đổi, tính lại chi phí và hướng từ HQ | Burst, chạy khi `dirty` |
| 3 | `TargetSyncSystem` | Sao chép vị trí mục tiêu phe mình (công trình, đơn vị) từ GameObject vào `TargetGrid` | Managed → NativeArray, mỗi frame |
| 4 | `EnemySpatialSystem` | Dựng spatial hash kẻ địch | Burst |
| 5 | `EnemyTargetingSystem` | Tìm mục tiêu gần nhất trong `DetectRange` | Burst song song |
| 6 | `EnemyMovementSystem` | `Advance`: theo flow field; `Engage`: đứng lại trong tầm | Burst song song |
| 7 | `EnemySeparationSystem` | Cộng lực đẩy từ các con kề cận | Burst song song |
| 8 | `EnemyAttackSystem` | Hết cooldown và mục tiêu trong tầm thì ghi `DamageEvent` | Burst |
| 9 | `DamageApplySystem` | Đọc `DamageEvent`, gọi `Health.TakeDamage` của GameObject | **Managed, main thread** |
| 10 | `EnemyDeathSystem` | `EnemyHealth <= 0` thì hủy thực thể (qua ECB) | Burst |

Máy trạng thái:

```
Spawn → Advance ──(có mục tiêu trong DetectRange)──► Engage
          ▲                                             │
          └────────(mục tiêu chết hoặc ra khỏi tầm)─────┘
Engage / Advance ──(EnemyHealth <= 0)──► Dead → hủy
```

Ưu tiên mục tiêu (theo `mechanics.md` mục 5): đơn vị > công trình > HQ, chỉ khi trong tầm; còn lại chọn gần nhất.

---

## 5. Cầu nối ECS ↔ GameObject

Đây là phần khó nhất, và là lý do có những quy tắc chặt.

### 5.1. Mục tiêu phe mình (GameObject → ECS)

- `TargetRegistry` (MonoBehaviour, một instance) giữ danh sách `Health` của công trình và đơn vị phe mình, đăng ký trong `OnEnable`/`OnDisable`.
- Mỗi frame, `TargetSyncSystem` đọc danh sách đó, ghi vào `NativeArray<TargetInfo>` gồm `InstanceId`, vị trí, bán kính, loại (đơn vị/công trình), bucket theo ô lưới.
- Chỉ **copy dữ liệu một chiều**, không giữ tham chiếu GameObject trong job.

### 5.2. Sát thương lên phe mình (ECS → GameObject)

- Job tấn công **không** gọi `Health.TakeDamage` (không được phép gọi managed trong Burst). Nó ghi `DamageEvent { InstanceId, Amount }` vào buffer.
- `DamageApplySystem` (managed, chạy trên main thread) đọc buffer, tra `InstanceId` trong `TargetRegistry`, gọi `Health.TakeDamage`, rồi xóa buffer.
- HQ hết máu sẽ phát `Headquarters.Destroyed` như đã làm, `GameManager` đăng ký sự kiện này để thua ván.

### 5.3. Sát thương lên kẻ địch (GameObject → ECS)

Rifleman, Heavy Guard, Watchtower là GameObject và phải đánh được thực thể ECS:

- Cung cấp API tĩnh nhỏ `EnemyWorld`: `TryFindNearest(position, range, out Entity)` (tra `EnemySpatialIndex`) và `Damage(Entity, float)`.
- `Damage` ghi vào `EnemyHealth` qua `EntityManager` trên main thread. Tần suất thấp (vài phát mỗi giây mỗi tháp) nên chấp nhận được.
- Chết thì `EnemyDeathSystem` lo, code phe mình không cần biết.

### 5.4. Việc **không** làm ở giai đoạn này

- Click chọn kẻ địch và thanh máu của chúng (cần raycast vào ECS; để sau).
- Hoạt ảnh (dùng capsule, sau này có thể dùng GPU animation).

---

## 6. Hiển thị

- Dùng **Entities Graphics** với **SubScene**. Prefab kẻ địch được bake thành entity prefab (mesh + material hỗ trợ DOTS instancing, URP Lit dùng được).
- `EnemyCatalogAuthoring` trong SubScene giữ danh sách prefab, bake thành một buffer `EnemyPrefabEntry` để `EnemySpawnSystem` lấy.
- Mọi kẻ địch cùng mesh và material nên được gom batch, chi phí render rất thấp.
- Hướng quay: `EnemyMovementSystem` cập nhật `LocalTransform.Rotation` theo hướng di chuyển, làm mượt bằng `slerp`.

---

## 7. Spawn theo vùng (nối với cơ chế đã có)

`EnemySpawnArea` (tâm + bán kính) giữ nguyên, vẫn là MonoBehaviour trong scene để chỉnh và có gizmo.

1. `EnemySpawner` (managed) không còn `Instantiate` GameObject. Nó tạo một entity `SpawnRequest { EnemyType, Count, Center, Radius, Interval }`.
2. `EnemySpawnSystem` chọn điểm ngẫu nhiên đều trong hình tròn (`r = R * sqrt(u)`, góc ngẫu nhiên, `Unity.Mathematics.Random` có seed cố định để tái lập được).
3. Kiểm tra ô đích bằng flow field: ô bị chặn thì thử lại tối đa vài lần (thay cho `NavMesh.SamplePosition` hiện đang dùng).
4. Spawn rải theo `Interval` giữa các con, không cùng lúc.

---

## 8. Cấu trúc thư mục

```
Assets/Scripts/Runtime/Enemies/
  EnemySpawnArea.cs, EnemySpawner.cs          (đã có, sẽ sửa EnemySpawner)
  ECS/
    Components/    EnemyComponents.cs, FlowFieldData.cs, DamageEvent.cs
    Systems/       EnemySpawnSystem.cs, FlowFieldSystem.cs, ...
    Authoring/     EnemyAuthoring.cs, EnemyCatalogAuthoring.cs
    Bridge/        TargetRegistry.cs, TargetSyncSystem.cs, DamageApplySystem.cs, EnemyWorld.cs
Assets/Scripts/SO/EnemyData.cs
Assets/ScriptableObjects/Enemies/Enemy_Basic.asset
Assets/Scenes/  (SubScene chứa EnemyCatalog)
```

**Về assembly:** hiện dự án dùng một assembly mặc định (không có `.asmdef`). Giữ nguyên để code ECS truy cập được `Health`, `Headquarters`, `BuildGrid` mà không phải tách assembly. Chỉ tách nếu thời gian compile trở thành vấn đề.

---

## 9. Thứ tự triển khai

Mỗi bước có tiêu chí để kiểm tra trước khi sang bước sau.

| Bước | Việc | Xong khi |
|---|---|---|
| E1 | SubScene, `EnemyData`, `EnemyAuthoring`, hiển thị một thực thể đứng yên | Thấy capsule do ECS vẽ trong Game view |
| E2 | `EnemySpawnSystem` + nối `EnemySpawner`/`EnemySpawnArea` | Spawn 100 con rải đều trong vòng tròn, 0 GameObject kẻ địch |
| E3 | `FlowFieldSystem` + `EnemyMovementSystem` | 100 con cùng đi về HQ, vòng qua cây, tường, ao |
| E4 | Spatial hash + separation | 500 con không chồng lên nhau, FPS ổn |
| E5 | Cầu nối mục tiêu + `EnemyTargetingSystem` + `EnemyAttackSystem` + `DamageApplySystem` | Quái đánh công trình trên đường đi và đánh HQ, HQ giảm máu, hết máu thì phát `Destroyed` |
| E6 | `EnemyWorld` + `EnemyDeathSystem` | Một khối thử bắn trúng và giết được quái |
| E7 | Đo hiệu năng và tối ưu | 2000 con, profiler cho thấy từng system trong ngân sách |

**Ngân sách khung hình (2000 con, mục tiêu):** toàn bộ system ECS dưới **3 ms** mỗi frame (flow field không tính vì chỉ chạy khi `dirty`).

---

## 10. Rủi ro

| Rủi ro | Cách giảm |
|---|---|
| Flow field bị tính lại liên tục khi đặt nhiều công trình | Cờ `dirty` gom trong một frame, tính ở job nền |
| `TargetSyncSystem` copy từng frame tốn khi có nhiều đơn vị | Chỉ vài chục mục tiêu nên rẻ; nếu cần thì chỉ cập nhật mục tiêu đã di chuyển |
| Quái tụm đông ở một điểm làm separation quá tải | Giới hạn số hàng xóm xét mỗi con, bucket theo ô |
| Quái kẹt ở góc do hướng ô giật | Làm mượt hướng, nội suy giữa 4 ô lân cận |
| `DamageEvent` trỏ vào GameObject đã bị hủy | `DamageApplySystem` bỏ qua `InstanceId` không còn trong `TargetRegistry` |
| Khó debug (không xem được trong Inspector như GameObject) | Dùng cửa sổ Entities Hierarchy/Inspector, thêm gizmo vẽ flow field khi bật debug |
| Trộn managed và Burst gây lỗi khó đoán | Giữ đúng ranh giới mục 5: job chỉ đọc/ghi dữ liệu thuần, mọi lời gọi tới GameObject nằm trong hệ thống managed |

---

## 11. Câu hỏi mở (cần chốt trước E3)

1. **Chặn kín đường:** chấp nhận cách "công trình có chi phí cao, quái phá công trình chặn" (mục 2.2) và **bỏ** quy tắc cấm chặn kín đường trong `BuildingPlacer`, hay vẫn giữ cấm?
2. **Kẻ địch có bị chặn bởi đơn vị phe mình không?** (đơn vị phe mình là vật cản mềm hay quái xuyên qua, chỉ đánh khi vào tầm.) Đề nghị: không chặn, chỉ đánh.
3. **Bay hay đi bộ:** chỉ một loại kẻ địch đi bộ, đúng `prototype.md`, nên flow field một lớp là đủ. Nếu sau này có loại bay thì cần thêm lớp field riêng.
4. **Tầm phát hiện:** `mechanics.md` ghi 6m. Giữ, hay tăng khi quái tụm đông cho dễ đọc?
5. **Kích thước bản đồ cho 10.000 con:** 10.000 thân bán kính 0,4m cần khoảng 5.000 m² chỗ đứng, gấp đôi diện tích bản đồ 50x50 hiện tại. Cần bản đồ cỡ 200x200 trở lên (flow field 40.000 ô vẫn tính được trong vài ms khi đổi).
