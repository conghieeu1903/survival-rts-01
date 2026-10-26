# MÔ PHỎNG QUÂN ĐỘI VÀ QUÁI
## Medieval Kingdom — thay cho `ecs-enemies.md`

**Phạm vi:** hai hệ thống mô phỏng số lượng lớn, dùng hai kỹ thuật khác nhau vì yêu cầu khác nhau.

| Hệ thống | Dùng ở | Kỹ thuật | Yêu cầu xác định |
|---|---|---|---|
| Mô phỏng trận (`Runtime/Battle/`) | Trận lockstep (PvP) | NativeArray + Burst, số nguyên cố định | **Bắt buộc**: mọi máy ra kết quả giống hệt |
| Quái ECS (`Runtime/Enemies/`) | Offline (PvE) | Entities + Burst, float, flow field | Không cần, chạy một máy |

---

## 1. Mô phỏng trận (lockstep)

### 1.1. Dữ liệu
- Mỗi đội 40 tiểu đội × 100 lính (Lính kiếm 28, Lính cung 12), cộng tiểu đội dự trữ để huấn luyện thêm.
- Trạng thái nằm trong `NativeArray` theo chỉ số lính và chỉ số tiểu đội (`squadAlive`, tổng toạ độ tiểu đội...), vị trí là số nguyên cố định (1 m = 256).
- Cấu hình loại lính từ `BattleData.unitTypes` (`BattleUnitType`: máu, sát thương, nhịp đánh, `strikeDelay`, tầm, tốc độ, bán kính).

### 1.2. Vòng lượt
1. Host gom lệnh trong 1/10 giây thành một `TurnMessage` và phát cho mọi máy (lượt rỗng vẫn gửi).
2. Mỗi máy chạy `ApplyTurn`: áp dụng lệnh theo thứ tự, rồi bước mô phỏng bằng chuỗi job Burst (tìm mục tiêu, di chuyển, né nhau, tấn công, gộp sát thương tuần tự theo chỉ số, xử lý chết).
3. Mỗi 10 lượt: `BattleChecksumJob` băm trạng thái, gửi `ChecksumMessage`; host so, lệch thì phát `DesyncMessage`.
4. Cuối lượt: kiểm tra `WinnerTeam` (đội cuối cùng còn lính).

### 1.3. Lệnh
| Lệnh | Tham số | Tác dụng |
|---|---|---|
| `MoveSquad` | đội, tiểu đội, đích (×100) | Đưa tiểu đội về đích, dàn đội hình theo `formationSpacing` |
| Huấn luyện tiểu đội | đội, loại lính | Trừ vàng, sau `trainSeconds` thêm tiểu đội dự trữ ra trận |

Bản đồ là hình vuông nửa cạnh `mapHalfSize` (200 m), bốn đội xuất phát ở bốn phía.

### 1.4. Hiển thị tách khỏi mô phỏng
- `BattleUnitPresentation` đọc kết quả mô phỏng, nội suy vị trí giữa hai lượt, ghi dữ liệu animation GPU cho Entities Graphics.
- Tia mũi tên, âm thanh (`BattleAudio`), thanh máu chỉ là trình diễn, không ghi ngược vào trạng thái.
- Xác nằm lại `corpseLinger` (3 giây) sau clip Death.

### 1.5. Việc cần làm
- Công trình làm mục tiêu và vật cản trong cùng mô phỏng (xem `tech.md` mục 5) ⬜.
- Bảng số đo hiệu năng ở các mốc 100 / 500 / 1.000 / 4.000 / 16.000 lính ⬜.
- Kịch bản kiểm thử desync cố định (chuỗi lệnh chạy hai lần, nhiều máy) ⬜.

---

## 2. Quái ECS (offline)

### 2.1. Kiến trúc
Prefab entity tạo lúc chạy từ `EnemyData` (`EnemyEcsConfig` → `RenderMeshUtility`). Một `EnemySimulationSystem` điều phối chuỗi job Burst mỗi frame:

`EnemySnapshotJob` → `EnemySortJob` (spatial grid theo ô) → `EnemySimulateJob` (tìm mục tiêu, flow field, né nhau, tấn công) → `EnemyDeathJob`.

Flow field (`FlowFieldJob`) chỉ tính lại khi `BuildGrid.Version` đổi.

### 2.2. Tìm đường
Mọi quái đi về cùng một đích (HQ) nên dùng flow field trên lưới 1 m của `BuildGrid`, chi phí đọc O(1) mỗi con.

| Ô | Chi phí |
|---|---|
| Trống | 1 |
| Có công trình phe mình | cao (≈ 20), không chặn hẳn |
| Cây, đá, nước, ngoài bản đồ | Chặn hẳn |

Người chơi chặn kín đường thì quái đi tới công trình chắn và tấn công nó, không bị kẹt vô hạn.

### 2.3. Cầu nối với GameObject
- Mục tiêu phe mình: `PlayerTarget` (MonoBehaviour) tự đăng ký, dữ liệu sao chép một chiều vào mảng native.
- Sát thương lên phe mình: job ghi sự kiện sát thương, hệ thống managed trên main thread gọi `Health.TakeDamage`.
- Sát thương lên quái: `EnemyWorld.TryFindNearest` và `EnemyWorld.Damage` cho lính, tháp (GameObject).
- Tháp canh (`TowerJobs`) và lính kiếm (`SoldierCombatSystem`) cũng chạy trong ECS.

### 2.4. Animation GPU
Không dùng Animator. Animation nướng thành bone texture, shader `SurvivalRts/GpuSkinnedLit` skin ở vertex stage. `EnemyAnimationSystem` chọn clip theo trạng thái (Death, Attack, Walk, Idle) và ghi `EnemyAnimData` cho từng thực thể; Idle/Walk lệch pha theo thực thể. Bộ nướng `GpuAnimationBaker` dùng lại được cho model có rig thật (mesh cần chỉ số xương ở UV1.x).

### 2.5. Số đo (Editor, chỉ phần mô phỏng, chưa gồm vẽ)
| Số quái | Trung bình/frame | p95 |
|---|---|---|
| 200 | 0,27 ms | — |
| 10.005 | 0,59 ms | 0,74 ms |

### 2.6. Vấn đề đã biết
Bản đồ thử 50×50 quá nhỏ cho 10.000 con (cần khoảng 5.000 m² chỗ đứng), quái dồn thành khối quanh HQ; cần bản đồ từ 200×200 và chỉnh lực né nhau.

### 2.7. Việc cần làm
- `WaveManager` + `WaveConfig` thay `EnemySpawner` spawn thử ⬜.
- Click chọn quái và thanh máu quái (cần raycast vào ECS) ⬜.
- Quyết định có giữ hệ thống quái riêng không, hay hợp nhất với mô phỏng trận khi kinh tế và xây dựng vào lockstep (câu hỏi mở).

---

## 3. Câu hỏi mở

1. Offline có chạy luôn trên mô phỏng lockstep (một máy, nhiều đội, đội địch là AI), để chỉ còn một hệ thống mô phỏng?
2. Số lượng quái mục tiêu mỗi đợt offline: vài chục (đúng thiết kế đợt) hay hàng nghìn?
3. Quái có bị chặn bởi quân phe mình không (hiện: không chặn, chỉ đánh khi vào tầm)?
