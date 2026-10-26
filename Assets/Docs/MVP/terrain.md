# ĐỊA HÌNH — MVP
## Medieval Kingdom

**Nguồn:** hội thoại thiết kế, tham khảo nguyên lý của Tiny Glade (chỉ lấy ý tưởng, tự cài đặt).
**Liên quan:** `building-placement.md` (đặt nhà), `tech.md` (lockstep), `roadmap.md`.
Ký hiệu: ✅ đã có · 🔶 một phần · ⬜ chưa làm.

---

## 1. Mục tiêu

- Địa hình tự sinh ngẫu nhiên từ seed.
- Người chơi chỉnh được địa hình (nâng, hạ, san phẳng, nước), cả vật liệu, cỏ, cây.
- Bản đồ lớn: **cỡ 1.000 × 1.000 m trước, thiết kế để lên 10.000 × 10.000 m sau** (xem mục 3).
- Chạy đúng trong lockstep: mọi máy ra cùng một địa hình.

Hiện tại: mặt đất trong `prototype` đã chuyển sang Unity Terrain được dẫn động từ `HeightField` chia chunk 50 × 50 m (bản đồ 1.000 × 1.000 m, cấp phát lười), `BuildGrid` đọc độ cao và độ dốc bằng số nguyên từ `HeightField`. ✅

## 2. Nguồn sự thật và hiển thị tách riêng

| Phần | Vai trò | Kiểu dữ liệu |
|---|---|---|
| `heightGrid` | **Nguồn sự thật**, nằm trong trạng thái mô phỏng, vào checksum | Số nguyên, đơn vị 1/256 m |
| Unity Terrain | **Chỉ hiển thị**: độ cao, Terrain Layers, cỏ, cây | Đọc từ `heightGrid`, không ghi ngược |
| Mặt nạ sơn | Đường, nước, hoa, cỏ (hiển thị) | Texture 2D |

Lý do: lockstep yêu cầu mọi máy tính ra từng con số giống nhau. `TerrainData` dùng `float` và API của Unity nên không đủ tin cậy làm nguồn sự thật.

**Unity Terrain có làm được tự sinh và người chơi tuỳ chỉnh không?** Có. Tự sinh: ghi `SetHeights` từ `heightGrid`. Người chơi sửa: lệnh sửa `heightGrid`, rồi cập nhật đúng vùng bị đổi trên Terrain. Terrain không làm được hang động và vách nhô (overhang); nếu cần thì sau này thay phần hiển thị bằng mesh sinh theo chunk, không phải đổi mô phỏng.

Chuyển mặt đất hiện tại sang Terrain: đổi mesh đang dùng, bake lại NavMesh (tạm thời, xem mục 3.3), kiểm tra lại prefab đặt trên mặt đất cũ.

## 3. Quy mô bản đồ

Mức yêu cầu: 1.000 × 1.000 m, có thể lên 10.000 × 10.000 m. *(Cần xác nhận con số 10.000: bản ghi ban đầu viết "1000x10000".)*

### 3.1. Bộ nhớ `heightGrid`
Dùng `short` (1/256 m cho khoảng ±128 m độ cao, đủ cho địa hình game) thay `int`.

| Kích thước | Ô ở 1 m | Dung lượng `short` |
|---|---|---|
| 400 × 400 | 160.000 | 0,3 MB |
| 1.000 × 1.000 | 1.000.000 | 2 MB |
| 10.000 × 10.000 | 100.000.000 | 200 MB |

**Chia chunk ngay từ đầu, mỗi chunk 50 × 50 m (2.500 ô, 5 KB bằng `short`).** Bản 1.000 m có 20 × 20 = 400 chunk, bản 10.000 m có 200 × 200 = 40.000 chunk. Chunk được nạp theo vùng và chỉ lưu khi khác mặc định (địa hình sinh từ seed nên chunk chưa đụng tới có thể sinh lại khi cần). `BuildGrid`, flow field và vật thể trên bản đồ cũng dùng chung lưới chunk 50 m này.

### 3.2. Unity Terrain
Một Terrain tối đa heightmap 4097; 1.000 m ở độ phân giải 1025 ≈ 1 m/mẫu. 10.000 m cần ghép nhiều Terrain (lưới các ô), chỉ bật những ô gần camera.

### 3.3. Hệ quả cho các hệ thống khác
- **Flow field:** hiện tính lại toàn bản đồ khi `BuildGrid.Version` đổi, đủ nhanh ở 50 × 50 và 400 × 400. Ở 1.000 × 1.000 phải chia chunk và chỉ tính lại chunk bị đổi; ở 10.000 × 10.000 cần flow field phân cấp (chunk + cổng nối). ⬜
- **`BuildGrid`:** hiện là mảng toàn bản đồ, cũng phải chia chunk.
- **NavMesh:** không bake trước cho bản đồ ngẫu nhiên hay bản đồ lớn. Dân và quân chuyển dần sang flow field số nguyên.
- **Mô phỏng lính:** 16.000 lính trên 1.000 m thưa hơn bản 400 m hiện tại; cần kiểm tra lại tìm mục tiêu (spatial grid) và camera xa.
- **Đưa cây, đá, tài nguyên lên:** dữ liệu cây là bảng đối tượng, không phải mảng toàn bản đồ.

## 4. Địa hình lưu bằng nét (stroke), không lưu ảnh xám

Mỗi lần nâng/hạ đất là một nét có `index`, danh sách điểm, `height`, `falloff`. `heightGrid` là kết quả rasterize các nét theo thứ tự, bằng số nguyên.

- Lệnh lockstep: `NewTerrainStroke`, `EditTerrainStroke` (đổi `height`, `falloff`), `DeleteTerrainStroke`. Lệnh nhỏ, mọi máy dựng ra cùng `heightGrid`.
- Chỉnh lại từng ngọn đồi; xoá bằng cách hạ dần `height` về 0 (hiệu ứng chỉ là hiển thị).
- `falloff` nhỏ tạo vách đứng. Vùng dốc vượt ngưỡng đánh dấu vào mặt nạ đá; hiển thị ghép khối đá và bậc thang lên sườn (sau MVP).
- Nét "nước" hạ độ cao dưới mực nước và sinh bờ; đường đi cắt qua nước thì đắp bờ đất.
- Khi `heightGrid` đổi: tăng `BuildGrid.Version`, dựng lại móng nhà, đặt lại cây trong vùng đó, chỉ cập nhật vùng Terrain bị đổi (gộp nhiều lần sửa trong cùng một lượt).
- Hạn chế chi phí rasterize: mỗi nét có hộp bao, chỉ ghi vào chunk nó chạm.

## 5. Sinh địa hình ngẫu nhiên từ seed

- Host chỉ gửi seed (đã có trong `StartMatchMessage`); mọi máy sinh `heightGrid` giống hệt.
- Chỉ dùng noise số nguyên (value/gradient noise cố định, nhiều tầng) và hàm băm số nguyên. **Cấm** `Mathf.PerlinNoise`, `Random`, `float`.
- Từ cùng seed sinh tiếp: độ dốc, nước, cây, đá, quặng, bụi quả, vị trí 4 đội. San nền khu xuất phát, kiểm tra đủ phẳng, có đường tới nhau, công bằng; không đạt thì thử seed phụ xác định.
- Sinh theo chunk, song song bằng Burst; chunk nào cần mới sinh.
- Hash `heightGrid` vào checksum từ lượt đầu để báo lệch sớm.
- Kiểm thử: cùng seed sinh hai lần so hash. Công cụ nằm ở menu Editor `Debug`, không đưa vào giao diện game.

## 6. Lớp sơn lên địa hình (hiển thị)

Đường đi, nước, hoa, cỏ là **mặt nạ 2D** tô lên địa hình, không phải mesh và không ảnh hưởng ô lưới.

- Nét cọ lưu: chuỗi điểm, kích thước cọ, thêm/tẩy, blend `Max`; rasterize rồi làm mượt biên.
- Đường đi đổi màu đất, xoá cỏ, rải sỏi hai mép. Cỏ phủ mặc định theo màu đất; hoa và bụi chỉ sinh ở chỗ không vướng nhà hoặc nước.
- Ở vách đá dốc, lớp đá đè lên và không hiện đường mòn.
- Nếu một lớp ảnh hưởng gameplay (tốc độ đi trên đường, vùng cấm) thì phải có dữ liệu số nguyên riêng, không đọc từ mặt nạ hiển thị.

## 7. Thứ tự làm

| # | Việc | Trạng thái |
|---|---|---|
| 0 | Nhà xoay 1° và chiếm ô bằng số nguyên (`building-placement.md`) | ✅ |
| 1 | `heightGrid` số nguyên theo chunk 50 m; `BuildGrid` đọc độ cao qua nó | ✅ |
| 2 | Chuyển mặt đất sang Unity Terrain (mỗi chunk một ô Terrain hoặc gộp), lấy mẫu `heightGrid` | ✅ |
| 3 | Sửa lại phần đặt: sàn phẳng, móng đá, ngưỡng chênh độ cao theo loại nhà | ✅ |
| 4 | Sinh địa hình và tự động sinh cây từ seed (cây lớn chặn đặt, cây nhỏ tự biến mất) | 🔶 đã xong sinh cây từ seed (`ProceduralVegetation`) và `placementRule` |
| 5 | Sửa địa hình bằng nét qua lệnh lockstep | ⬜ |
| 6 | Vật liệu, cỏ, cây, nước, đường đi (hiển thị) | ⬜ |
| 7 | Chunk hoá `BuildGrid` và flow field cho bản đồ 1.000 m trở lên | ⬜ |
| 8 | Vách đá, bậc thang tự sinh; Terrain nhiều ô cho 10.000 m (sau MVP) | ⬜ |
