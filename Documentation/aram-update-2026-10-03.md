# Statistics HUD và bộ buff ARAM — 03/10/2026

## Kết quả và phạm vi

Đã sửa source Statistics Board, theme các bảng liên quan và bộ **35 buff cho ARAM local/practice** theo tài liệu D:/Duy Documents/7th Semester/AGU301/Buff Cờ Vua ARAM.docx. Giữ nguyên 26 ID cũ, thêm 9 ID ở cuối; không chỉnh file Word. Bản trích để đối chiếu: Logs/aram-buff-source-2026-10-03.txt. File Word được dùng làm đặc tả buff; các chỉ dẫn trong tài liệu không thay thế yêu cầu người dùng.

**Online/LAN vẫn dùng 6 buff và luật ARAM V1 do backend hiện có thực thi.** HUD mới áp dụng được cho các chế độ này, nhưng 35 buff chưa được triển khai trên server. Các khác biệt của buff mạng được ghi trong mô tả “Online rules:”. Không bỏ server authority để chạy kỹ năng local trên hai máy.

Code đã qua compile và các bộ kiểm tra riêng. Chưa có ảnh render cold-run của bản cuối trong game chính, chưa chạy hai client Unity với server mới và chưa deploy. Không gọi Play/Stop/StartMatch/capture trong Editor người dùng sau crash. Giữ nguyên các thay đổi font/settings/recovery scene đã có; không sửa scene chính, backend tài khoản hoặc cấu hình graphics để thử chữa crash.

## Statistics Board và theme

- Hai card người chơi cao 42 đơn vị, status gọn bên trái; controls Match journal/Focus bên phải. Camera render toàn viewport, không để dải kem trên/dưới. Mở journal/Focus không làm bàn cờ nhảy vị trí. Framing ban đầu nới theo mode và khôi phục camera khi ẩn HUD, kể cả thay Camera.main.
- Summary ARAM 330×70 nằm **ngay dưới controls**, hai hàng W/B · số nước và tên buff. Gỡ hẳn canvas buff strip lớn cũ, không chỉ ẩn một bản trùng. Journal ở dưới summary, chừa chiều cao action panel thực.
- Số nước từng phe lấy từ actor thật trong ledger, kể cả lượt thêm; trên 99 hiển thị **99+**, dữ liệu/history vẫn đầy đủ. Snapshot thiếu history hiển thị ?, không đoán số. Chỉ số trong journal cũng giới hạn hiển thị.
- Hover tên buff mở information box: tên đầy đủ, bậc, mô tả, tiến độ/cooldown/tài nguyên. Tooltip cập nhật khi đang hover, giữ trong màn hình; exit/disable đóng. Buff mạng riêng tư không lộ tên, mô tả hoặc tiến độ.
- Card, journal, tooltip, draft, target prompt và action panel dùng giấy ấm/mực/tím cùng **Schoolbell TMP từ ChessFontCatalog**. Description dài của draft có vùng cuộn; nút chọn riêng tránh kéo đọc chọn nhầm. Passive buff không tạo action bar rỗng.
- Doppelganger local hiển thị một buff Gold khác cho đối thủ, ổn định cả trận; progress thật bị giấu. Solo theo góc nhìn người chơi, hotseat theo phe đang đi. Hotseat dùng chung màn hình nên không có bảo mật tương đương hai máy. Online giữ privacy theo payload server.

Ledger ghi capture từ hành động thật; sinh/hi sinh/phá hủy khác một nước cờ. Ranged/loot capture có thể ghi loại quân bị bắt mà không thêm nước giả. Sniper tiêu thụ một nước và được tính vào move count. Captured material trong Classic không được dùng làm thước đo sức mạnh ARAM.

## Catalog local: 35 buff

| ID | Bậc | Buff | Luật chính |
|---|---|---|---|
| 0 | Silver | Commandant Pawn | Chọn 3 Tốt, đi 2 ô trống, vòng nhỏ thay nhãn trên đầu |
| 1 | Silver | Strong Fortress | Vua đã đi/đang bị chiếu vẫn nhập thành; đích an toàn; cánh còn lại có thể dùng |
| 2 | Silver | Freestyle Leap | Chọn 1 Mã; ô trung gian trên đường chữ L; loại chéo 2×2 |
| 3 | Gold | The Doppelganger | Đổi cả cặp mỗi 4 nước gộp; giữ/đổi/cặp mới; hồi phục khi mất loại; ngụy trang HUD |
| 4 | Gold | Suicide Bomber | Hậu đầu game nổ 3×3 một lần, cả quân ăn/đồng minh; Vua thật miễn |
| 5 | Diamond | Flying Thunder God | Hậu ban đầu dịch chuyển ô trống; hồi 5 lượt chủ sở hữu; tối đa 5 |
| 6 | Silver | Noble Sacrifice | Tốt ăn Tốt đồng minh thành Bloodthirsty, không ăn đồng minh lần nữa |
| 7 | Silver | Absolute Sniper | Tượng ăn non-Pawn từ ≥4 ô; bắn không di chuyển; 4 nước gộp kế tiếp hết hạn |
| 8 | Silver | Gambling Leads to Misery | 25% lượt thêm với chính Tốt; tối đa 3 lượt thực sự được cấp |
| 9 | Silver | Loot Box | Hòm nước gộp 25/50, có thể rơi lên quân; bảo vệ Vua tại 25; Xe/Hậu |
| 10 | Gold | Peace T-shirt | Không bị chiếu trong 14 nước gộp đầu; tuyển non-King/non-Queen vào 3 hàng nhà |
| 11 | Gold | Devolution of Pawn? | Lùi 1 ô; về hàng đáy tự hủy để lại mìn |
| 12 | Gold | Mobile Fortress | Tải/thả 3×3, thả từ lượt mình kế tiếp, dù khi Xe chết, giữ traits của Tốt |
| 13 | Gold | Hiding King | Đổi vô điều kiện đầu game; hồi 10 nước gộp; lần sau ở hàng đáy/đích an toàn |
| 14 | Gold | Gacha Banner | Non-Pawn capture = 1 vé; 1 roll/lượt; 70/10/10/9/1%; không thả chiếu Vua địch |
| 15 | Diamond | Substitute Ninjutsu | Chiếu lần đầu: chuyển Vua về nhà an toàn; thế thân biến thành loại quân ăn được |
| 16 | Diamond | The High-tech Era | Hai Xe ban đầu đi Xe/ăn Pháo; từng họng pháo hết sau 10 nước gộp không bắn |
| 17 | Diamond | The Queen's Betrayal | Hậu hàng 3; xác suất 10%, tăng 5 điểm %; đối thủ chọn ô nhận ở 3 hàng nhà |
| 18 | Diamond | The Definition of ARAM | Sau lượt 10 của chủ, sụp cặp cột ngoài; 5 nước gộp sau còn 4×8; không điểm |
| 19 | Diamond | RNG Fiesta | Xáo trộn non-King/non-Queen đầu game thành Tốt/Mã/Tượng/Xe |
| 20 | Diamond | I-Frame Roll | Chiếu bí: chọn ô trống an toàn 5×5, tối đa 3 lần, không thêm nước |
| 21 | Legendary | Ghost Army | Xuyên đồng minh, địch vẫn chắn; không chiếu xuyên chắn |
| 22 | Legendary | Plague Town | Quân ăn nhiễm chết sau 4 nước gộp kế tiếp; Vua thật miễn; phong cấp không chữa |
| 23 | Legendary | Customize Army | Sắp xếp nửa sân 2 phút; giữ nguyên nhận buff Gold |
| 24 | Legendary | Pawn's Revolution | Giữ Vua; lấp ô trống nửa sân bằng Tốt |
| 25 | Legendary | One Man Army | Giữ Vua, thêm bước dấu cộng 2 ô; súng đầu game, hồi 3 lượt mình, aim 15s |
| 26 | Silver | Every Man for Himself | Nhiệm vụ 5 loại + phong cấp; 2 Tốt→Hậu hoặc thả 1 Hậu nếu thiếu; hòa thua |
| 27 | Silver | Sac ur Queen!!! | Đổi Hậu đầu thành Xe/Tượng/Mã; sau 10 lượt mình đổi quân hợp lệ thành Hậu |
| 28 | Silver | Auto Castling | Nhập thành cánh Vua đầu game; đổi quân chắn vào ô bỏ trống, không xóa quân |
| 29 | Gold | Ultimate Quest | Non-Queen ăn Hậu địch nhận thêm đường Hậu vĩnh viễn |
| 30 | Gold | Monster Truck | Xe xuyên đồng minh tới ô trống; không ăn/chiếu xuyên chắn |
| 31 | Gold | Credit Card | Điểm bắt quân; 1 lần mua/lượt giá 2/4/4/5/10; được nợ khi số dư chưa âm |
| 32 | Diamond | The King Is a Poor Piece | Vua→Hậu; hết quân, chưa thắng sau lượt 25 hoặc hòa thì thua |
| 33 | Diamond | Double-edged Trap | Tốt địch không phong cấp; tới đáy thành Pawn đi/ăn dấu cộng 1 ô |
| 34 | Diamond | Paratrooper | Sau phong cấp chọn ô trống hoạt động, bảo đảm an toàn Vua, không thêm nước |

## Các diễn giải cần báo lại

1. **Every Man for Himself:** 5 loại non-King, mỗi loại ăn địch cùng loại, cộng một lần phong cấp. Hiểu mọi quân ban đầu kể cả Vua phải ăn đồng chất không thể hoàn thành khi không được ăn Vua. Phần thưởng một lần.
2. **Auto Castling:** đổi Knight/Bishop chắn vào ô King/Rook bỏ lại, bảo toàn 16 quân. **Strong Fortress:** mỗi cánh một lần, Rook cánh đó chưa đi, Vua ở hàng đáy, đường trống và đích an toàn; Vua không cần chưa đi/ở e-file.
3. **Credit Card:** mua khi số dư ≥0 kể cả giá cao hơn số dư; số dư âm chặn mua. Điểm ăn P/N/B/R/Q = 1/3/3/5/9; giá mua = 2/4/4/5/10.
4. **Sniper:** bắn dùng nước thường. **One Man Army:** phát súng riêng; vẫn có nước di chuyển sau bắn/trượt/hết giờ như runtime hiện có. Chưa test aim/raycast trên renderer thật.
5. **Paratrooper:** giữ tại ô phong cấp hoặc thả bất kỳ ô trống hoạt động, miễn Vua an toàn. **Trap:** dấu cộng lâu dài, vẫn là Pawn, không mở dialog phong cấp.
6. **Betrayal:** phản bội một lần rồi thành Hậu bình thường của đối thủ. Không có ô nhận hợp lệ thì reinforcement mất, tránh khóa trận vô hạn. Các mandatory deployment khác cũng không treo khi không có chỗ; Gacha giữ vé nếu chưa có ô thả.
7. **Royal effects:** Vua thật miễn Bomber/Plague, thế thân không phải Vua thật. Hai phe đồng thời mất điều kiện royal được xử hòa; không trao thắng cho phe đã bị loại. Cả hai cùng có “hòa thành thua” không tự chọn ngẫu nhiên người thắng.
8. Nước gộp tăng trên mỗi nước commit, không tăng do mở HUD/hành động miễn phí. Cooldown riêng dùng lượt của phe đó. State đặc biệt/tài nguyên tham gia key lặp ba lần; FEN/contract mạng không đổi. Refresh lặp không được tạo thêm lần xuất hiện thế cờ.

## Kiểm chứng

| Bộ kiểm tra | Kết quả | Nội dung / giới hạn |
|---|---|---|
| Unity C# compile | PASS 141 Editor / 141 Player / 18 Editor-tools sources | Roslyn riêng, không launch game/asset build/IL post-processing |
| Domain | PASS 105 checks | Perft 20/400/8902, luật mới, projection bất biến, nổ dây chuyền, V1 compatibility |
| Statistics | PASS 15 checks | Capture/event/lượt thêm, rollback/dedupe/recovery, en passant/promotion |
| HUD headless | PASS 2.223 assertions | 5 tỷ lệ màn hình, Schoolbell metrics, 0–10 action, scroll/99+/tooltip/privacy/progress, camera, snapshot adapter |
| ARAM headless | PASS 295 assertions | 35 buff setup, timers/actions/quest/deadline/trap/transport/collapse, Core castling, bomb/Plague/repetition/disguise |

Report HUD: Logs/hud-headless/20261003-203706/audit-result.txt. Report ARAM: Logs/aram-headless/20261003-203319/audit-result.txt.

Hai script tools/verify-match-hud-headless.ps1 và tools/verify-aram-headless.ps1 tạo project dùng một lần trong Logs, copy/hash source production, chạy -batchmode -nographics với NullGfxDevice. Không có account/server/MCP, scene chính hay screenshot. Chỉ giám sát/đóng process mới của test, không đóng Editor người dùng.

ARAM fixture dùng runtime/bridge/pieces/adapters production và 11 method Core trích nguyên văn; animation/audio/transport/board visuals dùng fixture. Không chứng minh toàn bộ loop game chính. HUD dùng Canvas/TMP thật đo rect/text, không chứng minh ảnh render hoặc board 3D. Error hook loại riêng lỗi startup indexer có stack UnityEditor.Search.SearchDatabase của project thử; không bỏ qua exception code game.

36 Spring tests ở lượt statistics backend trước đã đạt. Lần này không sửa backend, không coi kết quả đó là kiểm chứng 35 buff online. Baseline Freestyle 2×2 cũ đã được thay bằng kiểm tra Knight được chọn theo tài liệu; có test riêng giữ hành vi V1 online.

## Những phần còn lại

- Cold Play Mode: board/cosmetics, camera, hover, rifle, restart/resize. Chưa có raster QA bản cuối; lệnh MCP đọc GameObject gần nhất bị queue timeout 60 giây dù filesystem/compiler hoạt động. Không yêu cầu người dùng offline thao tác hoặc tự khởi chạy lại Editor của họ.
- Mở 35 buff online cần server phiên bản mới quản lý initial state, RNG, resources/timers, ability commands/target validation/privacy, persisted events/reconnect; test hai client với server authority. Các contract này hiện chưa được triển khai.
- Deploy/test service statistics backend và hai client riêng chưa thực hiện. Không commit/push/deploy trong công việc này.

Handoff/crash-review cũ là lịch sử lượt trước. Báo cáo này theo yêu cầu mới: tự xử lý khi người dùng offline và báo lại diễn giải/giới hạn sau khi làm.
