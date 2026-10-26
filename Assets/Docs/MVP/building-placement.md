# ĐẶT CÔNG TRÌNH VÀ VẬT THỂ TRÊN BẢN ĐỒ — MVP
## Medieval Kingdom

**Liên quan:** `terrain.md` (địa hình, `heightGrid`), `tech.md` (lockstep), `mechanics.md` mục 4.
Ký hiệu: ✅ đã có · 🔶 một phần · ⬜ chưa làm.

---

## 1. Hiện trạng
Đặt trên lưới 1 m, xoay mỗi lần 90° (phím R), kiểm tra mọi ô, một ô hỏng là đỏ, công trường có thợ xây, tháo dỡ hoàn chi phí. Độ cao lấy bằng raycast `float`, chênh quá 0,5 m thì chặn. (✅ offline)

## 2. Mục tiêu MVP
- Xoay **tự do từng 1°** thay vì chỉ các mốc 90°.
- Nhà **thích nghi địa hình**: sàn phẳng, móng đá nối xuống đất ở phía thấp.
- Có trong **cả offline và PvP** để kiểm thử.
- Quy tắc đặt phụ thuộc **thuộc tính của từng loại vật thể** (cây, đá, công trình).

## 3. Thuộc tính của vật thể trên bản đồ

Mỗi đối tượng (cây, bụi, đá, công trình, tài nguyên) khai báo thuộc tính trong dữ liệu (ScriptableObject), không viết cứng trong code đặt nhà:

| Thuộc tính | Ý nghĩa | Ví dụ |
|---|---|---|
| `placementRule` | `Block` (chặn đặt) hoặc `Clear` (đặt thì biến mất) | Cây lớn: `Block`; cây nhỏ, bụi, cỏ: `Clear` |
| `footprint` | Số ô chiếm | Cây lớn 1×1, đá 2×2 |
| `clearable` | Có thể dọn để lấy chỗ (chặt cây, phá đá) | Cây: có; núi đá: không |
| `clearReward` | Tài nguyên nhận khi dọn | Cây lớn cho gỗ |

Quy tắc: nhà đè lên vật thể `Clear` thì vật thể đó biến mất (ẩn, có thể khôi phục khi tháo nhà); đè lên `Block` thì đỏ và không cho đặt cho đến khi chặt/phá. MVP làm **cả hai loại** để kiểm thử.

## 4. Xoay 1°

- Góc lưu bằng **số nguyên độ** (0–359). `sin`/`cos` tra bảng số nguyên, không dùng `float` trong mô phỏng.
- Phím R giữ bước 90°; thêm xoay mịn bằng Q/E hoặc con lăn, giữ Shift cho bước lớn.
- Lệnh `PlaceBuilding(loại, x, z, góc)` đi qua `LockstepNetwork.SendCommand`. Công trình chỉ lưu `x, z, góc, loại`; độ cao đọc từ `heightGrid` khi dựng mesh.

## 5. Chiếm ô khi xoay
- Ô có **tâm nằm trong hình chữ nhật đã xoay** (kiểm tra bằng tích có hướng, số nguyên) cộng một viền nhỏ thì bị chiếm. Viền giữ cho quân không lách qua khe hở.
- Ô chéo bị răng cưa; chấp nhận được ở độ phân giải 1 m, quái và quân đi men theo mép răng cưa.
- Phép kiểm tra viết bằng số nguyên để chuyển nguyên vào lockstep, dùng chung cho offline.

## 6. Thích nghi địa hình
- Lấy mẫu các điểm cách nhau khoảng 0,5 m trong hình chữ nhật, nội suy song tuyến từ `heightGrid`.
- Sàn nằm ngang ở độ cao trung bình các góc.
- Phần đất thấp hơn sàn: dựng **móng đá** từ sàn xuống mặt đất. Phần đất cao hơn sàn: san phẳng một ít hoặc chặn nếu đào quá sâu.
- Ngưỡng chênh độ cao (cao nhất − thấp nhất) riêng từng loại nhà, đặt trong `BuildingData`; sơ bộ 1,0 m. Nhà to chịu ít hơn.
- Địa hình đổi sau khi xây: không xoá nhà, dựng lại móng; vượt ngưỡng thì nhà bị hư hại.

## 7. Bảng chặn khi đặt

| Tình huống | Hành vi |
|---|---|
| Đè vật thể `Clear` (cỏ, bụi, cây nhỏ) | Tự biến mất, không chặn |
| Đè vật thể `Block` (cây lớn, đá) | Chặn, báo lý do |
| Đè công trình khác | Chặn |
| Địa hình nghiêng trong ngưỡng | Tự thích nghi (sàn phẳng + móng) |
| Chênh độ cao quá ngưỡng, quá dốc | Chặn, "địa hình quá dốc" |
| Ngoài biên bản đồ, trong nước | Chặn |
| Có địch hoặc quân trong khung | Chặn (hoặc đẩy quân ra) |
| Không đủ tài nguyên | Chặn, bóng đỏ, hiện lý do |

Chiều xanh/đỏ của bóng mờ lấy từ cùng một hàm kiểm tra dùng trong mô phỏng, để máy nào cũng cho cùng kết quả. Máy nhận lệnh phải **kiểm tra lại** trước khi áp dụng và bỏ lệnh nếu không hợp lệ.

## 8. Trạng thái code (xoay 1°)

Đã hoàn thành, biên dịch 0 lỗi và kiểm chứng trong Unity (`DebugWindow`):
- `BuildGrid.CollectCells(center, baseFootprint, angle, result)`: chiếm ô bằng số nguyên (toạ độ 1/256 ô, sin/cos tra bảng nhân 65536, viền 0,25 ô). Góc bội của 90° cho ra đúng tập ô như hình chữ nhật cũ.
- `BuildGrid.Evaluate(center, baseFootprint, angle, cells, out baseY)`.
- `GridOccupant`: thêm `baseFootprint`, `angle`, `Cells`, `WorldHalfExtents`; `SetOccupancy` nhận danh sách ô.
- `BuildingPlacer`: góc `angle` (độ nguyên) thay `turns`; R sang mốc 90° kế tiếp, **Q/E xoay từng độ** (giữ để xoay liên tục, Shift nhanh hơn); công trình phải quay mặt về mỏ (Xưởng đá) vẫn chỉ xoay theo 90°.
- `PlayerTarget`, `ConstructionSite`, `EnemySimulationSystem` (đích flow field) đã đọc theo `Cells` / hộp bao xoay.
- Bảng `CosTable` 360 độ là hằng số nguyên nhân 65536 trong `BuildGrid`, không gọi `Math.Sin/Cos` lúc chạy.

## 9. Thứ tự làm
1. `heightGrid` số nguyên; hàm kiểm tra đặt đọc độ cao qua nó (`terrain.md`).
2. Thuộc tính `placementRule` cho cây/đá/công trình; cây hiện có chuyển sang dùng thuộc tính.
3. Xoay 1° và chiếm ô bằng số nguyên (offline).
4. Móng đá và sàn phẳng.
5. Lệnh `PlaceBuilding` trong lockstep (cùng kế hoạch ở `tech.md` mục 5).
6. Tường vẽ bằng spline, vòm cổng tự đục (sau MVP).
