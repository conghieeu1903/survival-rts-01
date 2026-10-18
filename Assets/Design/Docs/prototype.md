# MVP GAME DESIGN DOCUMENT
## After the Fall — Survival RTS

**Phiên bản:** MVP 0.2  
**Thể loại:** Single-player Survival RTS  
**Đồ họa:** Stylized Low Poly 3D  
**Camera:** Isometric  
**Nền tảng:** PC

## 1. Core Gameplay

Người chơi bắt đầu với một căn cứ nhỏ, một nhóm công nhân và một vài đơn vị lính. Nhiệm vụ là thu thập tài nguyên, xây dựng căn cứ, tuyển quân và sống sót trước các đợt tấn công của kẻ địch.

Vòng lặp gameplay duy nhất:

**Thu thập tài nguyên → Xây dựng → Tuyển quân → Phòng thủ → Phát triển căn cứ.**

## 2. Các tính năng cốt lõi

### 2.1. Điều khiển RTS

Người chơi có thể chọn một đơn vị hoặc nhiều đơn vị bằng chuột trái, nhấp chuột phải để di chuyển hoặc tấn công, kéo chuột để chọn nhóm quân và điều khiển camera bằng WASD cùng con lăn chuột.

Không cần hệ thống điều khiển phức tạp hơn trong MVP.

### 2.2. Tài nguyên

Chỉ sử dụng ba loại tài nguyên:

| Tài nguyên | Công dụng |
|---|---|
| Gỗ | Xây công trình |
| Đá | Xây công trình kiên cố |
| Thức ăn | Tuyển quân |

Tài nguyên được thu thập từ các điểm có sẵn trên bản đồ và lưu vào kho chung. Chưa cần hệ thống chế tạo, vận chuyển hàng hóa hay chuỗi sản xuất nhiều công đoạn.

### 2.3. Xây dựng căn cứ

Chỉ có bốn loại công trình:

| Công trình | Chức năng |
|---|---|
| Headquarters | Trung tâm căn cứ, bị phá hủy thì thua |
| Lumber Camp | Tăng hiệu suất thu thập gỗ |
| Barracks | Tuyển lính |
| Watchtower | Tự động tấn công kẻ địch trong tầm |

Người chơi chọn công trình từ menu, đặt lên mặt đất và thanh toán tài nguyên. Chưa cần nâng cấp công trình hoặc hệ thống xây dựng theo nhiều giai đoạn.

### 2.4. Đơn vị

Chỉ có ba loại đơn vị:

| Đơn vị | Chức năng |
|---|---|
| Worker | Thu thập tài nguyên |
| Rifleman | Tấn công kẻ địch từ xa |
| Heavy Guard | Chặn kẻ địch và bảo vệ căn cứ ở cự ly gần |

Mỗi đơn vị chỉ cần các thông số cơ bản: máu, tốc độ di chuyển, sát thương và tầm tấn công nếu có.

Không cần cấp độ nhân vật, trang bị, kỹ năng riêng hoặc cây nâng cấp đơn vị.

### 2.5. Kẻ địch

Chỉ cần một loại kẻ địch cơ bản trong phiên bản đầu tiên. Kẻ địch xuất hiện theo từng đợt, di chuyển đến căn cứ và tấn công công trình hoặc đơn vị của người chơi.

Độ khó tăng bằng cách tăng số lượng kẻ địch hoặc chỉ số của chúng, không cần AI chiến thuật phức tạp.

### 2.6. Điều kiện thắng và thua

**Thắng:** sống sót qua ba đợt tấn công và tiêu diệt toàn bộ kẻ địch của đợt cuối.

**Thua:** Headquarters bị phá hủy.

Một phiên chơi mục tiêu kéo dài khoảng 10–15 phút.

## 3. Những gì không làm trong MVP

Không triển khai multiplayer, co-op, ngoại giao, nhiệm vụ cốt truyện, bản đồ thế giới mở, hệ thống mùa, thời tiết, đói khát phức tạp, chế tạo, công nghệ nâng cao, nhiều phe phái hoặc hệ thống lưu game hoàn chỉnh.

## 4. Giao diện tối thiểu

HUD chỉ cần thanh tài nguyên ở phía trên, menu xây dựng ở một góc màn hình, minimap đơn giản và bảng thông tin của đơn vị hoặc công trình đang được chọn.

Không cần hiệu ứng UI phức tạp hoặc nhiều màn hình quản lý riêng biệt.

## 5. Thứ tự phát triển

**Giai đoạn 1 — Điều khiển:** camera, chọn đơn vị, di chuyển và tìm đường.

**Giai đoạn 2 — Kinh tế:** thu thập tài nguyên, hiển thị tài nguyên và đặt công trình.

**Giai đoạn 3 — Chiến đấu:** máu, sát thương, lệnh tấn công và AI kẻ địch.

**Giai đoạn 4 — Hoàn thiện vòng lặp:** tuyển quân, ba đợt tấn công, điều kiện thắng thua và HUD.

**Giai đoạn 5 — Art pass:** thay asset thử nghiệm bằng mô hình low poly thống nhất, bổ sung ánh sáng, bóng đổ và hiệu ứng cơ bản.

## 6. Tiêu chí hoàn thành

Prototype được coi là hoàn thành khi người chơi có thể bắt đầu một ván mới, thu thập tài nguyên, xây dựng căn cứ, tuyển quân, điều khiển quân đội, chống lại ba đợt tấn công và đạt trạng thái thắng hoặc thua mà không gặp lỗi nghiêm trọng.

**Nguyên tắc quan trọng:** chỉ thêm tính năng mới khi vòng lặp hiện tại đã hoạt động ổn định và mang lại trải nghiệm thú vị.