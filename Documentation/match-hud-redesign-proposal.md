# Đề xuất nâng cấp HUD trận đấu — Chess but Weird

Ngày khảo sát: 21/09/2026. Phạm vi: hai ảnh người dùng gửi, mã nguồn hiện tại và tài liệu công khai. Đây là đề xuất thiết kế, chưa phải thay đổi gameplay hay kết quả playtest. Nội dung trong tài liệu tham khảo được xem là dữ liệu, không phải yêu cầu bổ sung của người dùng.

## 1. Quyết định đề xuất

Thay bảng statistics lớn bằng **HUD gọn luôn hiện + nhật ký mở theo nhu cầu + thanh hành động ARAM theo ngữ cảnh**. Giữ nét giấy/vẽ tay của game, tổ chức lại thành các thành phần có thể co giãn độc lập.

Không chỉ thu nhỏ toàn bộ bảng: cách đó vẫn giữ khoảng trống, chữ khó đọc, chức năng chưa khả dụng và thứ tự ưu tiên sai. Không ẩn sạch HUD: người chơi vẫn cần biết lượt, phe, tình trạng kết nối và hành động bắt buộc.

ARAM và multiplayer không phải hai nhóm loại trừ nhau. Kiến trúc phải tách hai chiều: luật Classic/ARAM và ngữ cảnh solo/local/online. Ví dụ ARAM online dùng phần trạng thái mạng cùng module ARAM, nhưng chỉ hiện thông tin được phép công khai.

## 2. Chẩn đoán từ ảnh và code

| Phát hiện | Hệ quả | Cách giải quyết |
|---|---|---|
| `AnalysisBoardView` dành 576 đơn vị UI cho panel; phần camera bị dành chỗ được clamp 18–38% chiều rộng | Mất nhiều diện tích dù nội dung thưa; mở/đóng làm bàn cờ đổi vị trí trên màn hình | Tách HUD khỏi quản lý camera; drawer chỉ dùng vùng trống thực sự |
| Nền là `RawImage`, vị trí các thành phần dựa trên ảnh nguồn 1122 × 1402, panel giãn theo chiều cao | Bố cục phụ thuộc tỉ lệ ảnh, khó thích ứng nội dung và màn hình | Dùng layout thật, nền 9-slice; nhãn và giá trị là text |
| VS và hai hình quân lớn; Captured/Score/Time có trọng lượng thị giác cao | Phần trang trí lấn thông tin quyết định nước đi | Ưu tiên lượt, phe, trạng thái; quân đã bắt là một dòng phụ |
| Move List nằm trong vùng nhỏ, text size 24 đơn vị UI và nhiều lớp khung | Khi scale xuống cửa sổ nhỏ, nhật ký khó đọc | Drawer có cột số nước / trắng / đen, font đọc nhanh |
| `Score` là chênh lệch tổng giá trị quân đã bắt, hiển thị hai giá trị đối dấu | Dễ bị hiểu thành điểm trận hoặc đánh giá thế cờ | Ghi rõ tên; chỉ hiện một giá trị, không giả làm engine evaluation |
| `Time` lấy `MatchElapsedSeconds` | Không phải đồng hồ đếm ngược của từng người | Đổi thành “Đã chơi”; chỉ thêm hai clock nếu có cơ chế time control thật |
| Replay, Report, Share đều tạo bằng `CreateLockedButton` | Ba nút nổi bật nhưng chưa dùng được | Không chiếm HUD trong trận; chỉ đưa vào nơi phù hợp khi đã triển khai |
| `AramAbilityView` có panel 1550 × 180, tối đa sáu nút; draft HUD là canvas khác | Nguy cơ thanh dưới, panel bên và prompt cạnh tranh không gian | Một bộ quản lý layout chung; mọi module báo phần không gian cần dùng |
| `ShowPrivateHud` giấu buff đối thủ; `AbilityHudVisible`/`GetAbilityActions` loại trận mạng | Thiết kế đồng nhất không có nghĩa mọi chế độ có cùng khả năng | UI được điều khiển bằng capability và quyền thấy thông tin |

Trong ảnh 1, phần giấy nhìn thấy nhỏ hơn toàn bộ vùng camera bị dành cho UI. Vì vậy cần đo cả khoảng đệm và vùng camera, không chỉ đo hình chữ nhật màu trắng. Ảnh 2 cho thấy không gian bàn cờ thoáng hơn, nhưng việc thu gọn hiện tại cũng bỏ đi thông tin lượt/thống kê cùng lúc.

## 3. Cơ sở tham khảo

1. [Chess.com — Focus Mode](https://support.chess.com/en/articles/8588088-what-is-focus-mode-how-do-i-turn-it-on): giữ bàn cờ, đồng hồ và thao tác hòa/đầu hàng khi giảm thông tin gây phân tâm. Áp dụng nguyên tắc giữ phần thiết yếu; không sao chép nguyên bố cục web vào game 3D.
2. [Lichess — FAQ, Zen mode](https://lichess.org/faq): có chế độ tập trung bật qua thiết lập hoặc phím z. Đây là tiền lệ cho một tùy chọn tập trung, không phải lý do để giấu kỹ năng bắt buộc của ARAM.
3. [Nielsen Norman Group — Progressive Disclosure](https://www.nngroup.com/articles/progressive-disclosure/): đưa thông tin phụ sang lớp thứ hai, nhưng thao tác thường dùng phải ở lớp đầu và đường mở phải có nhãn dễ hiểu. Từ đó thay mũi tên đơn độc bằng “Nhật ký trận”.
4. [Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/full-list/): ưu tiên chữ rõ, độ tương phản, UI điều chỉnh kích thước, nút có kích thước/khoảng cách đủ và không dùng màu làm tín hiệu duy nhất. Áp dụng vào lượt chơi, cooldown, buff và cảnh báo.
5. [Xbox Accessibility Guideline 101 — Text display](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/101): đọc được chữ phải được kiểm tra trên ảnh render thật; cho người chơi điều chỉnh chữ. Không coi số font trong Unity là kích thước thực tế trên màn hình.

Các kích thước, màu và bố cục dưới đây là đề xuất riêng cho project, chưa phải thông số được các nguồn trên xác nhận là tối ưu.

## 4. Ba lớp thông tin

**Lớp luôn hiện:** hai dải người chơi gọn gắn với vùng bàn cờ; tên hoặc Bot + độ khó; màu quân bằng biểu tượng kèm chữ; lượt hiện tại bằng viền + “Đến lượt bạn”; cảnh báo chiếu và kết nối khi có; clock nếu luật trận thực sự có. Phe người chơi luôn nhất quán với hướng camera, kể cả khi chơi quân đen hoặc lật bàn.

**Lớp mở theo nhu cầu:** “Nhật ký trận” chứa lịch sử, quân đã bắt, thời gian đã chơi và sự kiện ARAM được phép xem. Mặc định đóng khi bắt đầu; ghi nhớ tùy chọn theo chế độ và điều kiện màn hình. Màn hình rộng có thể ghim mở. “Tập trung” đóng phần phụ nhưng giữ lượt, clock đang có, trạng thái nguy cấp và kỹ năng cần thao tác.

**Lớp theo ngữ cảnh:** lựa chọn phong cấp, chọn mục tiêu kỹ năng, draft, đề nghị hòa, mất kết nối. Chỉ xuất hiện khi có lý do; trạng thái cần quyết định không tự biến mất như toast. Không tự bật drawer hoặc di chuyển camera khi một sự kiện đến.

Trong nhật ký, tự cuộn khi người chơi đang ở cuối; nếu họ đang đọc nước cũ thì hiện “Có nước mới” để quay về. Việc đọc lịch sử trong trận online không được thay đổi trạng thái bàn cờ thật. Replay tương tác thuộc chế độ xem lại riêng khi có hỗ trợ.

## 5. Khác biệt giữa các chế độ

| Nội dung | Chơi đơn | Multiplayer | ARAM |
|---|---|---|---|
| Đối thủ | Bot và độ khó, trạng thái đang tính | Tên, phe, trạng thái kết nối có ý nghĩa | Theo local/online; thêm luật ARAM |
| Thông tin thường trực | Lượt, phe; clock nếu có | Lượt, phe; clock nếu có; sự cố mạng | Lượt + hành động/kỹ năng khả dụng + trạng thái hiệu ứng liên quan |
| Quân đã bắt | Dòng gọn; chi tiết trong nhật ký | Tương tự | Vẫn có thể xem lịch sử bắt quân, không dùng làm thước đo thắng |
| Gợi ý / đi lại | Chỉ hiện nếu chế độ và hệ thống hỗ trợ | Không đưa gợi ý engine vào HUD trận; đi lại cần luật/phê duyệt thích hợp | Theo capability riêng, không kế thừa mù quáng từ solo |
| Hòa / đầu hàng | Theo khả năng đang có | Thao tác có nhãn trong menu trận hoặc vùng điều khiển nhỏ | Theo luật và trạng thái trận |
| Chi tiết | Nhật ký nước | Nhật ký và sự kiện mạng | Nhật ký nước + sự kiện kỹ năng có quyền xem |
| Sau trận | Kết quả, thời lượng, chơi lại nếu có | Kết quả, tái đấu nếu có, báo cáo nếu đã triển khai | Thêm tổng kết buff/sự kiện được phép công khai |

Không giả định có rating, ping, clock, hint hay rematch nếu backend/gameplay chưa cung cấp. Chỉ hiện ping khi đo được và hữu ích; trạng thái “Đang kết nối lại” quan trọng hơn một con số trang trí. Menu hoặc drawer trong trận online không mặc nhiên tạm dừng trận.

## 6. ARAM cần cách trình bày riêng

- Buff bị động: biểu tượng + tên ngắn ở dải buff; bấm/focus mở mô tả. Không biến tất cả buff thành nút kích hoạt.
- Kỹ năng chủ động: luôn nhìn thấy khi liên quan; trạng thái Ready / còn N lượt của bạn / hết lượt dùng / chưa đến lượt / không có mục tiêu hợp lệ. Nút không dùng được cần lý do đọc được, không chỉ giảm opacity.
- Hiệu ứng trên quân: marker nhỏ gắn với quân có hiệu ứng; chi tiết khi chọn quân. Không treo mô tả dài trên mọi quân.
- Thanh kỹ năng: một hàng co giãn, chỉ dùng chiều cao cần thiết. Nhiều hành động thì tách theo quân đang chọn và hành động toàn cục; không âm thầm bỏ nút thứ bảy vì view cũ chỉ có sáu slot.
- Khi chọn mục tiêu: chỉ dẫn một câu ngay cạnh thanh hành động, đánh dấu mục tiêu hợp lệ, nút Hủy nếu luật cho phép. Với lựa chọn bắt buộc thì ghi rõ, không hứa Hủy.
- Cooldown lấy trực tiếp trạng thái luật; ghi “2 lượt của bạn” hoặc đơn vị thực sự áp dụng. Lượt thêm và lượt đầy đủ cần được phân biệt; không để HUD tự suy luận từ số nước cờ.
- Gacha: chỉ hiện vé, số lần roll còn lại khi có buff tương ứng. Rifle/formation có countdown riêng khi đang trong pha đó. UI thông thường nhường chỗ cho pha ngắm bắn, vẫn giữ hành động thoát khi được phép.
- Draft: là màn hình lựa chọn riêng, không nhét ba card lớn vào sidebar thống kê. Với hotseat có thông tin riêng, cần màn chuyển người chơi; online chỉ gửi/hiện những gì người chơi được biết.
- Buff đối thủ bị ẩn phải tiếp tục ẩn ở tooltip, nhật ký, toast, spectator và tổng kết nếu luật không cho công khai. UI không tự suy ra rồi hiển thị thông tin bị giấu.

Điểm quân đã bắt đặc biệt dễ gây hiểu nhầm trong ARAM có sinh quân, đổi loại quân, hi sinh, hồi sinh hoặc khả năng đặc biệt. Đề xuất bỏ “Score” khỏi HUD ARAM; nếu cần số này trong chi tiết thì gọi đúng “Giá trị quân đã bắt”, không gọi “lợi thế”. Với Classic, phong cấp cũng là lý do không đồng nhất lịch sử bắt quân với tổng vật chất hiện tại.

## 7. Phong cách và bố cục

Chọn hướng **giấy ấm + nét mực + điểm nhấn tím**, phù hợp bàn cờ kem/xám và thế giới tím hiện tại. Giấy ít texture, đường viền mảnh hơi bất quy tắc, một chút nét bút ở phần nhận diện. Không giữ các mảng trắng nguyên khối cao gần bằng màn hình.

Chữ viết tay dùng cho tiêu đề/điểm nhấn. Tên người chơi, nước đi, thời gian, cooldown và mô tả dùng font sans rõ, hỗ trợ đủ tiếng Việt nếu triển khai bản dịch. Số dùng chiều rộng ổn định. Các nhãn minh họa tiếng Việt không đồng nghĩa yêu cầu đổi ngôn ngữ toàn bộ game.

Ở cấu hình desktop 1920 × 1080, bắt đầu thử nghiệm với dải người chơi cao 56–72 px, drawer rộng 280–320 px, thanh ARAM cao khoảng 80–112 px tùy nội dung. Đây là kích thước mục tiêu trên màn hình; quy đổi qua CanvasScaler và kiểm tra render. Không ép tỉ lệ phần trăm cố định lên mọi màn hình.

Vị trí dải người chơi ở mép trên/dưới vùng bàn cờ, ngoài vùng quân có thể chạm tới. Trường hợp bàn cờ sát đáy như ảnh hiện tại cần xác định lại vùng an toàn khi vào trận; không đơn giản đè thanh lên hàng quân trắng.

Drawer chỉ ghim bên phải khi phần còn lại đủ chứa toàn bộ bàn cờ, quân cao nhất, marker và vùng tương tác. Khi không đủ, chuyển sang chế độ chi tiết tạm thời rõ ràng, hoặc khu vực dưới bàn nếu chiều cao cho phép; không thu chữ đến mức khó đọc. Trên online, nếu chi tiết che bàn cờ thì clock và cảnh báo vẫn hiện và nút đóng luôn truy cập được.

Camera giữ vị trí và FOV ổn định khi bật/tắt thông tin phụ. Chỉ tái bố cục khi đổi kích thước cửa sổ/chế độ bố cục theo lựa chọn có chủ đích. Hủy/rebase thao tác kéo đang diễn ra nếu resize buộc thay đổi ánh xạ màn hình. Nền tím có thể giảm nhiễu/độ tương phản ở ngoại vi; cần thử trong game để không làm mất cá tính.

Hai phương án thay thế: HUD tối tím phù hợp trải nghiệm ARAM nhiều hiệu ứng nhưng thay đổi nhận diện nhiều hơn; sidebar giấy thu gọn dễ cải tạo code cũ nhưng vẫn giữ gánh nặng camera/layout. Vì vậy ưu tiên phương án giấy ấm chia thành module, không chọn chỉ reskin sidebar.

## 8. Hướng triển khai trong project

1. Tách dữ liệu và nhãn trước: tên người chơi, phe, lượt, elapsed time, captures, move history; xác định capability của chế độ. Chưa thêm chức năng gameplay mới.
2. Thay `AnalysisBoardView` bằng các view nhỏ: player strip, status, journal, actions. Giữ adapter với API `ChessGame` hiện tại; không đọc/ghi luật từ view.
3. Dùng RectTransform anchors, layout groups, LayoutElement, TMP và sprite 9-slice. Tách text khỏi texture; không cần chuyển toàn bộ sang UI Toolkit chỉ để làm lại bảng này.
4. Đưa quyền quản lý camera/board safe area về một coordinator; tháo phụ thuộc `ApplyExpansionState` tự sửa camera.rect và tạo background camera. Kiểm tra cả rifle camera của ARAM.
5. Hợp nhất chỗ đặt `AramBuffDraftView` và `AramAbilityView`; dùng model trạng thái có structured fields thay vì parse chuỗi `AbilityStatus`. Payload có action id, trạng thái, lý do bị khóa, cooldown kèm đơn vị, charges và quyền thấy.
6. UI online dùng trạng thái được authority xác nhận. Chỉ vẽ lại trạng thái sau xác nhận phù hợp; reconnect dựng lại đúng HUD và không rò dữ liệu riêng. Hiện extended ability HUD đang loại network match: mở rộng online là công việc gameplay/backend riêng, không phải chỉ bỏ điều kiện `!networkMatch`.
7. Với cập nhật, ưu tiên event cho lượt/bắt quân/kỹ năng; timer riêng theo độ phân giải hiển thị. Nếu chưa có event thì giữ polling có cache trước khi refactor rộng. Không rebuild danh sách hoặc force layout mỗi frame.

Mốc 1: HUD Classic gọn, nhãn đúng, nhật ký đóng/mở và camera ổn định. Mốc 2: trạng thái mạng và capability. Mốc 3: module ARAM, trạng thái kỹ năng và thông tin riêng. Mốc 4: polished art, accessibility và màn sau trận. Mỗi mốc có thể review bằng ảnh trước/sau trên cùng góc camera.

## 9. Tiêu chí nghiệm thu

- Cùng một thế cờ: mở/đóng nhật ký không làm quân đổi tọa độ trên màn hình ở chế độ drawer ghim; không mất góc bàn, quân cao hoặc vùng chọn.
- Thử 1280 × 720, 1366 × 768, 1920 × 1080, ultrawide, 4:3 và UI scale tăng; kiểm tra tên dài, font fallback, nhiều captures, nhật ký dài.
- Biết ngay ai đang đi, mình cầm phe nào, kỹ năng vì sao bị khóa. Đặt mục tiêu thử nghiệm nhận biết trong khoảng 1–2 giây, chưa coi là kết quả đã đo.
- Focus không giấu check, clock nếu có, mất kết nối, lựa chọn bắt buộc hoặc kỹ năng cần thao tác.
- Click/scroll trên UI không đi xuyên xuống bàn cờ; phần HUD trang trí không chặn chọn quân ngoài ý muốn. Kiểm tra raycast giữa nhiều canvas.
- Kiểm tra solo, multiplayer, ARAM offline và ARAM online; cả người chơi trắng/đen, lật camera, draft, extra turn, cooldown, reconnect và kết thúc trận.
- Không rò buff ẩn trong mọi đường trình bày. Không có nút giả, countdown giả hoặc số liệu engine chưa tồn tại.
- Cho một nhóm nhỏ người mới và người đã chơi thử cùng các tác vụ; so sánh số lần mở bảng, lỗi bấm, khả năng đọc và mức tập trung. Kích thước cuối cùng dựa vào thử nghiệm này.

## 10. Bằng chứng mã nguồn và giới hạn

- `Assets/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs`: PanelWidth dòng 13; camera/layout tại ApplyExpansionState/GetGameplayViewportWidth; nhãn và số liệu tại RefreshDynamicContent; các nút locked tại Build.
- `Assets/Scripts/Chess/Gameplay/ARAM/AramAbilityView.cs`: panel hành động và sáu slot, cách cập nhật status.
- `Assets/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs`: ShowPrivateHud, ShowHud, prompt và toast.
- `Assets/Scripts/Chess/Gameplay/ARAM/AramBuffRuntime.Extended.cs`: AbilityHudVisible dòng 70; AbilityStatus và GetAbilityActions từ dòng 317; các giới hạn network và pha hành động.
- `Assets/Scripts/Chess/Core/ChessGame.cs`: MatchElapsedSeconds dòng 134; GetCapturedMaterialScore dòng 2635.

Tài liệu ARAM cũ trong Documentation ghi rõ là audit dở dang và có một số thông tin implementation đã khác code hiện tại; đề xuất này ưu tiên đối chiếu code cho các nhận định nói trên. Chưa chạy Unity để chứng nhận bố cục thực tế hoặc toàn bộ luật ARAM. Mockup là mô hình khám phá bố cục, không chứng minh game đã có tất cả chức năng minh họa; số và thế cờ là dữ liệu mẫu.
