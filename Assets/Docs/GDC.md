
# GAME DESIGN CONCEPT — MEDIEVAL KINGDOM

Tài liệu định hướng ý tưởng, gameplay, multiplayer và công nghệ phát triển

## 1. Tổng quan dự án

Tên dự án tạm thời: Medieval Kingdom: Build, Defend, Conquer.

Thể loại: Medieval City Builder + Freeform Castle Builder + Survival RTS + Multiplayer PvE/PvP.

Nền tảng mục tiêu: PC, ưu tiên phát hành trên Steam.

Game engine: Unity, sử dụng C#.

Ý tưởng cốt lõi: Xây dựng một game chiến thuật trung cổ kết hợp khả năng xây dựng tự do và đồ họa đẹp mắt như Tiny Glade với hệ thống quản lý vương quốc, quân đội đông đảo và chiến đấu quy mô lớn như Diplomacy Is Not an Option. Game bổ sung multiplayer để người chơi có thể hợp tác chống AI hoặc trực tiếp chiến đấu với những người chơi khác.

Người chơi không chỉ xây dựng một ngôi làng hay lâu đài để ngắm nhìn. Họ phải phát triển một vương quốc hoàn chỉnh, quản lý dân cư, sản xuất tài nguyên, xây dựng hệ thống phòng thủ và tổ chức quân đội. Những công trình do người chơi tạo ra sẽ ảnh hưởng trực tiếp đến cách họ bảo vệ lãnh thổ hoặc tấn công đối thủ.

Mục tiêu là tạo ra một trải nghiệm vừa thỏa mãn nhu cầu sáng tạo, vừa có chiều sâu chiến thuật và khả năng chơi lại lâu dài.

## 2. Cảm hứng và đối thủ cạnh tranh

### 2.1. Cảm hứng chính

Tiny Glade — Xây dựng tự do và thẩm mỹ

Tiny Glade là nguồn cảm hứng cho hệ thống xây dựng, phong cách đồ họa và trải nghiệm sáng tạo. Game hướng đến khả năng tạo ra những bức tường, cổng vòm, tháp canh và công trình trung cổ hài hòa với môi trường. Các chi tiết kiến trúc cần kết nối tự nhiên, hạn chế cảm giác lắp ghép từ những khối prefab cứng nhắc.

Website: [https://tinyglade.com/](https://tinyglade.com/?utm_source=chatgpt.com)

Steam: [https://store.steampowered.com/app/2198150/Tiny_Glade/](https://store.steampowered.com/app/2198150/Tiny_Glade/?utm_source=chatgpt.com)

Diplomacy Is Not an Option — Quản lý và chiến đấu quy mô lớn

Game là nguồn cảm hứng cho hệ thống kinh tế, quản lý dân cư, xây dựng công sự, phát triển quân đội và phòng thủ trước những đợt tấn công với số lượng kẻ địch rất lớn.

Website: [https://door407.com/](https://door407.com/?utm_source=chatgpt.com)

Steam: [https://store.steampowered.com/app/1272320/Diplomacy_is_Not_an_Option/](https://store.steampowered.com/app/1272320/Diplomacy_is_Not_an_Option/?utm_source=chatgpt.com)

### 2.2. Đối thủ cạnh tranh cần nghiên cứu

Bergfried: Đối thủ cần phân tích kỹ vì có sự giao thoa đáng kể với ý tưởng xây lâu đài và chiến tranh vây hãm. Cần nghiên cứu cách game xử lý việc xây dựng, địa hình, quản lý và chiến đấu để xác định hướng khác biệt cho dự án.

Steam: [https://store.steampowered.com/search/?term=Bergfried](https://store.steampowered.com/search/?term=Bergfried\&utm_source=chatgpt.com)

Cataclismo: Tham khảo cách xây dựng pháo đài từ nhiều bộ phận, bố trí phòng tuyến và chống lại các đợt tấn công của quái vật.

Steam: [https://store.steampowered.com/app/1422440/Cataclismo/](https://store.steampowered.com/app/1422440/Cataclismo/?utm_source=chatgpt.com)

Becastled: Tham khảo sự kết hợp giữa xây thành, quản lý tài nguyên, phát triển quân đội và phòng thủ.

Steam: [https://store.steampowered.com/app/1330460/Becastled/](https://store.steampowered.com/app/1330460/Becastled/?utm_source=chatgpt.com)

Kingdoms and Castles: Tham khảo cách phát triển làng mạc thành vương quốc, tổ chức dân cư và tạo ra một thế giới trung cổ có sức sống.

Steam: [https://store.steampowered.com/app/569480/Kingdoms_and_Castles/](https://store.steampowered.com/app/569480/Kingdoms_and_Castles/?utm_source=chatgpt.com)

Going Medieval: Tham khảo hệ thống xây dựng không gian ba chiều, nhiều tầng, quản lý người dân và xây dựng khu định cư có tính phòng thủ.

Steam: [https://store.steampowered.com/app/1029780/Going_Medieval/](https://store.steampowered.com/app/1029780/Going_Medieval/?utm_source=chatgpt.com)

### 2.3. Định vị khác biệt

Dự án không chỉ là game xây lâu đài hoặc game RTS sinh tồn. Điểm khác biệt hướng đến là sự kết hợp giữa tự do sáng tạo kiến trúc, thế giới trung cổ có tính thẩm mỹ cao, mô phỏng quân đội quy mô lớn và multiplayer PvE/PvP.

Người chơi vừa có thể tạo ra một vương quốc đẹp theo trí tưởng tượng, vừa phải chứng minh rằng thiết kế của mình có thể đứng vững trước chiến tranh.

## 3. Phong cách đồ họa

Game hướng đến phong cách stylized medieval diorama, lấy cảm hứng từ vẻ đẹp của Tiny Glade nhưng được điều chỉnh để phù hợp với một game chiến thuật có quy mô lớn.

Các đặc điểm chính gồm kiến trúc trung cổ có hình khối rõ ràng, tường đá và mái nhà có chi tiết, cây cối phong phú, địa hình tự nhiên, màu sắc hài hòa, ánh sáng đẹp và hiệu ứng môi trường sống động.

Đồ họa cần ưu tiên khả năng đọc chiến trường. Người chơi phải dễ dàng nhận biết tường thành, cổng, tháp canh, quân đội và hướng tiến công ngay cả khi trên màn hình có rất nhiều đơn vị.

Cần phân biệt hai mục tiêu: đạt chất lượng hình ảnh cao ở cấp độ công trình và giữ hiệu năng ổn định khi toàn bộ bản đồ có nhiều nhà cửa, cây cối, công trình và hàng nghìn quân lính.

## 4. Hệ thống xây dựng tự do

Đây là một trong những hệ thống cốt lõi và cũng là rủi ro kỹ thuật lớn nhất của dự án.

Người chơi có thể xây dựng nhà ở, đường đi, tường thành, cổng, tháp canh, lâu đài và các công trình kinh tế trên địa hình. Hệ thống xây dựng cần cho phép tạo hình linh hoạt thay vì chỉ đặt prefab theo ô vuông.

Các tính năng dự kiến gồm:

- Vẽ tường bằng cách đặt và chỉnh sửa các điểm trên đường đi.
- Tạo tường thẳng, tường cong, góc nối và các đoạn thành có độ cao khác nhau.
- Tự động tạo hình học cho tường, trụ, cổng và các điểm giao nhau.
- Đặt tháp canh, nhà ở và công trình trên địa hình.
- Kiểm tra tính hợp lệ của vị trí xây dựng.
- Tính chi phí tài nguyên và thời gian xây dựng.
- Cho phép sửa chữa, nâng cấp và phá hủy công trình.
- Lưu và khôi phục chính xác dữ liệu xây dựng.

Trong multiplayer, hệ thống cần đồng bộ các lệnh xây dựng và trạng thái công trình giữa những người chơi. Không nên truyền liên tục toàn bộ mesh đã sinh qua mạng; thay vào đó, cần đồng bộ dữ liệu xây dựng có cấu trúc để mỗi máy có thể tái tạo hình học theo cùng quy tắc.

## 5. Hệ thống quản lý vương quốc

Người chơi bắt đầu với một vùng đất nhỏ và từng bước phát triển thành một vương quốc có dân cư, kinh tế và quân đội.

Các tài nguyên ban đầu dự kiến gồm gỗ, đá, thực phẩm và vàng. Người chơi xây dựng nhà ở, xưởng sản xuất, công trình khai thác, kho chứa và các công trình quân sự.

Dân cư đóng vai trò lao động và nguồn lực cho nền kinh tế. Khi vương quốc phát triển, người chơi cần cân đối giữa mở rộng sản xuất, cung cấp thực phẩm, xây dựng phòng tuyến và tuyển quân.

Trong chế độ multiplayer co-op, các thành viên có thể cùng quản lý một vương quốc, chia sẻ tài nguyên và đảm nhận những nhiệm vụ khác nhau. Trong PvP, kinh tế trở thành một yếu tố cạnh tranh trực tiếp vì hai bên phải cân đối phát triển căn cứ với việc xây dựng quân đội.

## 6. Hệ thống quân đội và chiến đấu quy mô lớn

Game hướng đến những trận chiến có số lượng quân đông, với mục tiêu thử nghiệm ban đầu từ hàng trăm đến khoảng 1.000 đơn vị trên chiến trường, sau đó mở rộng nếu hiệu năng cho phép.

Các loại đơn vị cơ bản có thể gồm bộ binh, cung thủ, kỵ binh và đơn vị công thành. Quân đội cần có khả năng di chuyển theo nhóm, tìm đường, giao chiến, tấn công công trình và phản ứng với địa hình.

Quân địch có thể tổ chức các đợt tấn công nhằm vào thành trì, tìm cách tiếp cận cổng, phá tường hoặc xuyên qua các vị trí phòng thủ yếu. Người chơi phải bố trí quân, tận dụng địa hình và xây dựng nhiều lớp phòng tuyến.

Công trình không chỉ là vật trang trí. Tường thành có thể cản bước quân địch, cổng thành tạo ra điểm nghẽn, còn tháp canh tạo vị trí phòng thủ có lợi. Nếu một khu vực bị phá hủy, người chơi phải điều chỉnh chiến thuật hoặc sửa chữa sau trận chiến.

Về kỹ thuật, hệ thống cần tách biệt số lượng quân được hiển thị với số lượng quân đang thực sự được mô phỏng. Một đơn vị có thể cần AI, tìm đường, va chạm, hoạt ảnh và chiến đấu; vì vậy chỉ tối ưu render là chưa đủ.

## 7. Chế độ chơi

### 7.1. Single-player PvE

Người chơi xây dựng một vương quốc, quản lý kinh tế và phòng thủ trước các đợt quân địch do AI điều khiển.

Đây là chế độ phù hợp để kiểm chứng hệ thống xây dựng, kinh tế, chiến đấu và lưu tiến trình trước khi tích hợp multiplayer.

### 7.2. Multiplayer Co-op PvE

Nhiều người chơi cùng tham gia một vương quốc, xây dựng chung, chia sẻ tài nguyên và phối hợp phòng thủ trước AI.

Mục tiêu ban đầu là hỗ trợ 2–4 người chơi. Có thể phân chia nhiệm vụ giữa phát triển kinh tế, xây dựng thành trì và chỉ huy quân đội, nhưng vẫn cho phép tất cả thành viên tham gia vào các hoạt động cốt lõi.

### 7.3. Multiplayer PvP

Người chơi hoặc các đội chơi đối đầu với nhau, phát triển căn cứ, khai thác tài nguyên, xây dựng phòng tuyến và tổ chức tấn công.

Các hình thức PvP dự kiến gồm:

- 1v1: hai người chơi xây dựng và chiến đấu trên cùng một bản đồ.
- 2v2: hai đội phối hợp phát triển kinh tế và tấn công đối phương.
- Competitive Siege: chế độ tập trung vào xây dựng thành trì và phá vỡ phòng tuyến của đối thủ.

Các chế độ này là mục tiêu thiết kế, chưa phải tính năng đã được triển khai.

Để bảo đảm tính công bằng, PvP cần giới hạn thời gian chuẩn bị, tài nguyên ban đầu, khu vực xây dựng và các hành vi có thể lợi dụng hệ thống hình học. Cần tránh để một thành trì có thể trở nên bất khả xâm phạm chỉ nhờ một kiểu xây dựng đơn giản.

## 8. Công nghệ phát triển

Dự án sử dụng Unity và C# làm nền tảng chính. Các công nghệ bổ trợ được chia thành những nhóm độc lập để dễ phát triển, kiểm thử và thay thế.

| Công nghệ                            | Vai trò                                            |
| -------------------------------------- | --------------------------------------------------- |
| Unity + C#                             | Engine và logic gameplay                           |
| URP                                    | Rendering pipeline cho PC                           |
| Shader Graph                           | Xây dựng shader stylized                          |
| Unity Splines                          | Biểu diễn đường cong xây dựng                |
| Procedural Mesh                        | Sinh tường, cổng và hình học công trình     |
| Unity Entities (ECS)                   | Mô phỏng dữ liệu quân đội quy mô lớn       |
| Unity Jobs + Burst                     | Tối ưu các tác vụ mô phỏng phù hợp         |
| GPU Instancing, LOD, Culling           | Tối ưu hiển thị nhiều đối tượng            |
| FishNet                                | Đồng bộ multiplayer                              |
| Epic Online Services (EOS)             | Lobby, lời mời, session và các dịch vụ online |
| AI Navigation hoặc pathfinding riêng | Tìm đường cho dân cư và quân đội          |
| Unity Profiler                         | Đo CPU, GPU và bộ nhớ                           |
| Hệ thống save riêng                 | Lưu công trình, tài nguyên và tiến trình    |

### 8.1. Unity và URP

URP được đề xuất để xây dựng phong cách đồ họa stylized và kiểm soát hiệu năng. Shader Graph có thể hỗ trợ tạo vật liệu cho đá, gỗ, cỏ và mái nhà.

Không nên phụ thuộc hoàn toàn vào shader để tạo ra chất lượng giống Tiny Glade. Hình học công trình, tỷ lệ kiến trúc, chi tiết môi trường, ánh sáng và cách các vật thể kết hợp với nhau cũng rất quan trọng.

Tài liệu: [https://docs.unity3d.com/Manual/urp/urp-introduction.html](https://docs.unity3d.com/Manual/urp/urp-introduction.html?utm_source=chatgpt.com)

### 8.2. Procedural Building

Hệ thống xây dựng nên được phát triển riêng bằng C#, kết hợp Unity Splines và thuật toán tạo mesh. Dữ liệu xây dựng cần được lưu dưới dạng các điểm, thông số hình học, loại công trình và trạng thái.

Unity Splines: [https://docs.unity3d.com/Packages/com.unity.splines@latest](https://docs.unity3d.com/Packages/com.unity.splines@latest?utm_source=chatgpt.com)

### 8.3. ECS, Jobs và Burst

ECS phù hợp để quản lý số lượng lớn thực thể với dữ liệu và hệ thống cập nhật có cấu trúc. Jobs và Burst có thể giúp tối ưu các tác vụ mô phỏng song song.

Tuy nhiên, ECS không tự động giải quyết mọi bài toán. Tìm đường, tránh va chạm, chiến đấu, hoạt ảnh và hiển thị quân đội vẫn cần được thiết kế riêng. Những hệ thống như UI, công trình và các đối tượng ít quan trọng về hiệu năng có thể tiếp tục dùng GameObject.

Entities: [https://docs.unity3d.com/Packages/com.unity.entities@latest](https://docs.unity3d.com/Packages/com.unity.entities@latest?utm_source=chatgpt.com)

Burst: [https://docs.unity3d.com/Packages/com.unity.burst@latest](https://docs.unity3d.com/Packages/com.unity.burst@latest?utm_source=chatgpt.com)

### 8.4. FishNet

FishNet được sử dụng để đồng bộ gameplay giữa các máy, bao gồm yêu cầu xây dựng, trạng thái tài nguyên, đơn vị chiến đấu và trạng thái trận đấu.

Kiến trúc cần có cơ chế server-authoritative, trong đó host hoặc server xác thực các hành động quan trọng thay vì tin tưởng hoàn toàn vào client.

FishNet GitHub: [https://github.com/FirstGearGames/FishNet](https://github.com/FirstGearGames/FishNet?utm_source=chatgpt.com)

FishNet Documentation: [https://fish-networking.gitbook.io/docs/](https://fish-networking.gitbook.io/docs/?utm_source=chatgpt.com)

### 8.5. Epic Online Services

EOS được sử dụng cho các chức năng online như lobby, session, lời mời và kết nối người chơi theo phương thức triển khai đã chọn.

EOS không tự động thay thế FishNet và cũng không tự đồng bộ gameplay. Dự án cần một transport tương thích để chuyển dữ liệu mạng của FishNet qua cơ chế kết nối đã chọn. Khả năng tương thích, xác thực, NAT traversal và quy trình kết nối cần được kiểm thử sớm.

EOS SDK: [https://onlineservices.epicgames.com/en-US/sdk](https://onlineservices.epicgames.com/en-US/sdk?utm_source=chatgpt.com)

EOS Lobbies and Sessions: [https://dev.epicgames.com/docs/epic-online-services/multiplayer/lobbies-and-sessions](https://dev.epicgames.com/docs/epic-online-services/multiplayer/lobbies-and-sessions?utm_source=chatgpt.com)

### 8.6. AI Navigation và pathfinding

NavMesh có thể được dùng cho dân cư và các đối tượng thông thường. Đối với những trận chiến có hàng nghìn quân, nên thử nghiệm flow field hoặc pathfinding theo nhóm để giảm chi phí tìm đường.

Unity AI Navigation: [https://docs.unity3d.com/Packages/com.unity.ai.navigation@latest](https://docs.unity3d.com/Packages/com.unity.ai.navigation@latest?utm_source=chatgpt.com)

## 9. Kiến trúc tổng thể

Dự án nên được chia thành các module chính.

Building System: quản lý dữ liệu công trình, hình học procedural, đặt công trình, phá hủy và sửa chữa.

Kingdom Simulation: quản lý dân cư, tài nguyên, sản xuất, tiêu thụ và tiến trình phát triển.

Army Simulation: quản lý di chuyển, tìm đường, chiến đấu, sát thương và trạng thái đơn vị.

Rendering: hiển thị công trình, môi trường, quân đội, hoạt ảnh và hiệu ứng.

Networking: quản lý kết nối, đồng bộ trạng thái, RPC, quyền sở hữu và xác thực hành động.

Online Services: quản lý lobby, lời mời, session và quy trình tham gia trận đấu thông qua EOS.

Save System: lưu và khôi phục dữ liệu thế giới, công trình, tài nguyên và tiến trình.

Các module cần giao tiếp qua những giao diện rõ ràng. Ví dụ, hệ thống xây dựng không nên phụ thuộc trực tiếp vào cách FishNet truyền dữ liệu; nó chỉ cần cung cấp một lệnh xây dựng có cấu trúc để lớp mạng xác thực và phát tán.

Với ECS, cần thiết kế lớp chuyển đổi dữ liệu riêng khi đồng bộ thực thể quân đội. Không nên giả định các entity ECS có thể tự động đồng bộ bằng cơ chế NetworkObject thông thường của FishNet.

## 10. Lưu tiến trình và quản lý phiên chơi

Game cần có hệ thống lưu thế giới, bao gồm vị trí và hình dạng công trình, tài nguyên, dân cư, quân đội và tiến trình trận đấu.

Trong single-player, người chơi có thể lưu và tải trực tiếp. Trong multiplayer, cần xác định host hoặc server là nguồn dữ liệu chính thức, đồng thời xử lý tình huống host thoát, người chơi mất kết nối và người chơi tham gia lại.

Nếu muốn giữ tiến trình giữa nhiều phiên chơi co-op, cần thiết kế thêm cơ chế lưu trữ và khôi phục thế giới. EOS không tự cung cấp toàn bộ hệ thống save game cho gameplay của dự án.

## 11. Phạm vi MVP

Không nên bắt đầu bằng một game hoàn chỉnh với hàng nghìn quân, nhiều bản đồ và tất cả chế độ multiplayer. MVP nên chứng minh rằng những hệ thống cốt lõi có thể hoạt động cùng nhau.

Giai đoạn 1 — Prototype xây dựng: tạo địa hình nhỏ, vẽ tường, đặt cổng và tháp, xử lý các điểm nối, lưu và tải dữ liệu công trình.

Giai đoạn 2 — Prototype đồ họa: hoàn thiện một khu vực nhỏ với nhà cửa, tường thành, cây cối, ánh sáng và vật liệu đạt chất lượng hình ảnh mục tiêu.

Giai đoạn 3 — Prototype chiến đấu: mô phỏng 100, 500 rồi 1.000 quân, đo hiệu năng CPU, GPU và bộ nhớ; thử nghiệm di chuyển theo nhóm và phá hủy công trình.

Giai đoạn 4 — Prototype multiplayer: kết nối hai người chơi, đồng bộ xây dựng và tài nguyên, sau đó cho cả hai chống lại một đợt quân địch. Kiểm tra độ trễ và tính nhất quán trạng thái.

Giai đoạn 5 — Prototype PvP: triển khai trận đấu 1v1 với bản đồ, tài nguyên và điều kiện chiến thắng đơn giản. Kiểm thử việc xây dựng, tuyển quân, tấn công và xác thực hành động.

Giai đoạn 6 — Vertical Slice: tích hợp đồ họa, xây dựng, kinh tế, quân đội, co-op và PvP thành một bản chơi thử có thể hoàn thành một trận đấu từ đầu đến cuối.

## 12. Rủi ro kỹ thuật chính

Rủi ro đầu tiên là hệ thống xây dựng tự do. Mesh phải được tạo và cập nhật chính xác khi người chơi thay đổi hình dạng công trình; các góc nối, cổng và tháp không được xuất hiện lỗi hình học.

Rủi ro thứ hai là mô phỏng quân đội đông. Việc render nhiều quân không đồng nghĩa với việc có thể mô phỏng AI, tìm đường và chiến đấu cho tất cả quân ở tốc độ khung hình ổn định.

Rủi ro thứ ba là multiplayer. Khi nhiều người chơi cùng xây dựng và chiến đấu, dữ liệu phải được xác thực và đồng bộ. Sự kết hợp giữa FishNet, EOS và ECS cần một prototype riêng để kiểm tra trước khi xây dựng gameplay phức tạp.

Rủi ro thứ tư là cân bằng PvP. Hệ thống xây dựng tự do có thể tạo ra những thiết kế quá mạnh, vì vậy cần các giới hạn và quy tắc để bảo đảm chiến thuật vẫn có thể phản công.

## 13. Định nghĩa sản phẩm cuối cùng

Medieval Kingdom là một game chiến thuật trung cổ kết hợp xây dựng tự do, quản lý vương quốc, mô phỏng quân đội quy mô lớn và multiplayer PvE/PvP.

Người chơi có thể tạo ra những thành trì và vương quốc đẹp mắt, phát triển kinh tế, tuyển quân, chống lại các đợt tấn công của AI hoặc cạnh tranh với người chơi khác. Multiplayer cho phép họ cùng nhau xây dựng và bảo vệ một vương quốc, hoặc đối đầu trong những trận chiến mà kiến trúc và chiến thuật có ảnh hưởng trực tiếp đến kết quả.

Công nghệ mục tiêu: Unity + C# + URP + Procedural Building + ECS/Jobs/Burst + FishNet + Epic Online Services.

Ưu tiên phát triển: chứng minh hệ thống xây dựng tự do, chất lượng đồ họa, hiệu năng mô phỏng quân đội và khả năng đồng bộ multiplayer trước khi mở rộng nội dung.

Mục tiêu của dự án là tạo ra một trải nghiệm trong đó người chơi không chỉ xây dựng một vương quốc đẹp, mà còn có thể cùng bạn bè bảo vệ nó hoặc dùng chính thành trì do mình tạo ra để đánh bại đối thủ.
