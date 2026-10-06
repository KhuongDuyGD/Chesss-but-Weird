> Historical note (updated 2026-10-06): this document describes work before the .NET online migration. Spring/Node endpoints, classes and validation results below refer to the retired backend. Use [OnlineDotNetIntegration.md](OnlineDotNetIntegration.md) for the current implementation.

# Statistics Board — audit hiện trạng

Ngày: 21/09/2026. Chỉ khảo sát tính năng Statistics Board và đường dữ liệu liên quan; không sửa code, asset hoặc scene. Kết luận dựa trên mã nguồn đang có. Không đánh đồng với kiểm thử tích hợp đã chạy.

## Kết luận

Statistics Board có UI và dữ liệu thật cho lượt hiện tại, quân đã bắt, điểm vật chất đã bắt, thời lượng và lịch sử nước đi. Nó chưa có backend statistics độc lập; view đọc trực tiếp ChessGame. Logic local cơ bản đã tồn tại, nhưng lịch sử/captures/time chưa được đồng bộ đầy đủ theo vòng đời trận mạng, và model lịch sử chưa phù hợp với ARAM có lượt thêm hoặc hành động đặc biệt.

Vì vậy nên giữ API/gameplay đang hoạt động, bổ sung lớp dữ liệu statistics nhất quán trước hoặc cùng lúc thay view. Chỉ đổi hình thức sẽ giữ nguyên các lỗi dữ liệu dưới đây.

## Luồng hiện tại

```text
Local/Bot: thực hiện nước đi → ChessGame ghi List<string> và List<PieceType>
Online: thực hiện nước local → ghi tạm → phản hồi server → ApplyFenState
        → dựng lại captures từ quân còn lại → thay/thêm chuỗi notation
UI: ChessTurnSelectionUI tạo/quản lý AnalysisBoardView
    → polling mỗi 0,1 giây → đọc ChessGame → cập nhật các ô text
```

Tên class `ChessLanController` hiện chứa đường Spring REST/WebSocket; không nên suy ra đây chỉ là LAN. OnlineServer/README.md xác nhận Node relay trong repo là legacy, không phải backend Spring đang được client chọn. Không tìm thấy mã nguồn server Spring trong workspace được khảo sát; hành vi server triển khai thực tế chưa được xác minh.

## Thành phần đã có

| Thành phần | Hiện trạng |
|---|---|
| UI | Canvas runtime riêng, sortingOrder 18; nền RawImage + UnityEngine.UI.Text, ScrollRect |
| Asset | Catalog ở Resources/GameplayUI/AnalysisBoardAssets.asset, có tham chiếu nền, move list, ba nút và shader |
| Thu/mở | Có; mặc định mở nếu aspect >= 1,25; lựa chọn giữ trong instance, không có lưu preference bền vững trong view |
| Camera | Khi mở, view sửa camera.rect, giữ FOV đã chụp, tạo camera nền; vùng dành chỗ clamp 18–38% |
| Lượt | Đọc CurrentTurn; chỉ báo trắng/đen |
| Captures local | Ghi loại quân khi bắt quân qua đường nước đi thông thường, gồm đường en passant |
| Score | Chênh lệch tổng giá trị captures: hậu 9, xe 5, tượng/mã 3, tốt 1; hai phía hiển thị đối dấu |
| Time | Time.unscaledTime trừ thời điểm bắt đầu và thời gian SetPauseLocked; chốt khi kết thúc |
| History | List<string>, format local kiểu e2-e4, Ng1-f3, dấu x, nhập thành, hậu tố phong cấp; không phải SAN đầy đủ |
| Kết thúc | Bảng được hiện trên màn kết quả; thời lượng ngừng tăng |
| Replay / Report / Share | Chỉ hover/tooltip Coming soon; OnPointerClick không thực thi |
| Thống kê nâng cao | View không có accuracy, engine evaluation, win probability hoặc phân tích nước sai |

Tên người chơi đã có tại ChessTurnSelectionUI.SetMatchPlayers nhưng Statistics Board không dùng chúng. Không có nhánh trình bày riêng Classic/ARAM trong AnalysisBoardView.

## Phát hiện cần sửa — từ đọc code

### 1. History có thể không cập nhật khi sửa một nước có sẵn — ưu tiên cao

`AnalysisBoardView.cs:208` chỉ rebuild text khi MoveHistory.Count đổi. Trong khi `ChessGame.cs:1702` sửa phần tử cuối khi người chơi chọn phong cấp; `ChessGame.cs:2645` cho phép thay phần tử cuối bằng notation server.

Nếu UI đã đọc chuỗi cũ trước khi sửa, Count không đổi nên chuỗi cũ tiếp tục hiển thị tới lần thêm/xóa bản ghi kế tiếp. Phong cấp là trường hợp rõ vì người chơi có thể để hộp chọn mở lâu hơn chu kỳ refresh 0,1 giây. Cần revision/event hoặc so sánh nội dung, không chỉ Count.

### 2. Server từ chối nước đi có thể để lại history sai — ưu tiên cao

`ChessGame.cs:720` thêm history khi thực hiện nước local. `ChessLanController.cs:1285` trở đi, HandleSocketErrorPayload rollback bàn cờ bằng ApplyFenState(lastConfirmedFen), nhưng không xóa/khôi phục history tương ứng. ApplyFenState không phục hồi moveHistory.

Hậu quả suy ra: board đã quay về trạng thái server, nhật ký vẫn có nước bị từ chối; các cột tiếp theo cũng có thể lệch. Cần phân biệt pending/confirmed và rollback cùng một transaction dữ liệu statistics.

### 3. Reconnect chưa khôi phục lịch sử và thời lượng — ưu tiên cao

`BackendDtos.cs:56` có BackendMatchMoveDto; BackendMatchDto có moves, startedAt, finishedAt. Nhưng LoadActiveMatchCoroutine (`ChessLanController.cs:735`) gọi BeginServerMatch với currentFen mà không nạp các trường trên vào statistics.

BeginServerMatch gọi BeginLanGame/BeginAramNetworkGame, đi qua khởi tạo mới: clear history, captures và reset matchStartedAt (`ChessGame.cs:447`). ApplyFenState chỉ khôi phục vị trí và một số trạng thái bàn cờ.

Hậu quả: khôi phục được bàn cờ nhưng history có thể trống và “Time” tính lại từ lúc client khôi phục. GAME_STATE chỉ có moveCount, không có danh sách history/thời gian trong DTO hiện tại; snapshot trong lúc đang chơi cũng không tự bù nước đã bỏ lỡ. Không có bằng chứng client đang đưa moves/startedAt vào board. Chưa xác nhận server thực tế trả đủ các field REST.

### 4. Captures từ FEN không phải lịch sử bắt quân — ưu tiên cao

`ChessGame.cs:2546` gọi RebuildCapturedPiecesFromBoard. Hàm tại dòng 2662 lấy số quân chuẩn ban đầu trừ số quân còn trên bàn theo từng loại. Vì vậy cùng một nhãn Captured có hai nghĩa: local ghi sự kiện bắt; snapshot online suy ra quân thiếu.

Ví dụ tốt phong cấp thành hậu: số tốt giảm dù không bị bắt, nhưng thuật toán có thể tính thêm một tốt đã mất. Nếu đã mất một hậu và sau đó có tốt lên hậu, số hậu trở lại một có thể che mất lần hậu đã bị bắt. ARAM sinh/đổi/xóa quân càng làm suy luận này không đáng tin.

Cần nguồn capture/event rõ ràng. Không thể tái dựng chính xác mọi lịch sử capture từ một FEN duy nhất.

### 5. Cache captures cũng chỉ dựa vào số lượng — ưu tiên vừa

`AnalysisBoardView.cs:196` chỉ refresh icon và score khi số phần tử captures đổi. Nếu snapshot thay đổi thành phần nhưng giữ tổng số (ví dụ một tốt thành một mã trong danh sách suy ra), UI có thể giữ icon và điểm cũ. Cần revision hoặc so sánh nội dung dữ liệu.

### 6. Chia cột history mặc định luân phiên, không đúng lượt thêm ARAM — ưu tiên cao

`AnalysisBoardView.cs:585` tính whiteIndex/blackIndex bằng chẵn/lẻ và firstTurn. Không có actor/team trong mỗi record vì history chỉ là chuỗi. `AramBuffRuntime.Extended.cs:170` có thể trả lại chính team đang đi để cấp lượt thêm.

Ví dụ thứ tự thực tế Trắng → Trắng → Đen sẽ được view phân vào Trắng → Đen → Trắng. Cần record có team, số lượt và loại sự kiện; không suy phe từ index.

### 7. Các sự kiện loại quân của ARAM chưa có nhật ký statistics đầy đủ — ưu tiên cao cho ARAM

Captures được thêm tại đường nước thông thường (`ChessGame.cs:723`). `DestroyAramExplosionVictims` (`ChessGame.cs:807`) và `AramRemove` trong ChessGame.Aram.cs xóa quân nhưng không thêm vào capture history. Spawn/replace/relocate cũng không tạo event thống kê chung.

Đây không có nghĩa mọi quân bị xóa đều phải được tính là đối thủ bắt; cần phân biệt captured, sacrificed, destroyed, transformed, spawned theo luật. Hiện UI chưa có model biểu diễn các nghĩa đó. Nên thống nhất semantics trước khi đưa chúng vào nhật ký mới.

### 8. Khi xem bàn cờ sau trận, bảng bị khóa tương tác — ưu tiên vừa

`ChessTurnSelectionUI.cs:392` SpectateFinishedGame vẫn SetVisible(true), nhưng SetInteractionEnabled(false). View thực hiện việc này bằng tắt GraphicRaycaster, nên không chỉ khóa nút đặc biệt mà còn khóa cuộn history và nút thu/mở. Cần tách tương tác xem thống kê khỏi tương tác gameplay.

## Giới hạn UI cần giữ trong kế hoạch thay thế

- Move list luôn kéo về cuối khi Count tăng, dù người dùng đang đọc nước cũ.
- Mỗi chuỗi bị cắt còn tối đa 13 ký tự bởi TrimMove; không có cách mở xem bản đầy đủ trong board.
- PadRight(14) căn cột bằng số ký tự, không đảm bảo thẳng hàng với font có độ rộng ký tự khác nhau.
- Tọa độ các thành phần dựa vào ảnh nguồn 1122 × 1402; layout phụ thuộc kích thước texture và CanvasScaler.
- Thu gọn ẩn cả panel, bao gồm turn indicator, time và captures của board.
- Camera lifecycle nằm ngay trong view nên thay UI phải kiểm tra restore camera, scene switch và camera khác của ARAM.
- UI không có counter giờ riêng cho từng người; Time hiện là thời lượng client có trừ pause, không phải server clock.

## Phạm vi nên làm khi thay UI

1. Giữ các API gameplay đã dùng; bổ sung model statistics có match identity, revision, history có team/action id và trạng thái pending/confirmed.
2. Hợp nhất reset/new match/reconnect/rollback để view luôn nhận một snapshot statistics nhất quán.
3. Dùng lịch sử/sự kiện đáng tin cho captures; nếu thiếu dữ liệu thì thể hiện thiếu, không suy diễn thành thống kê chính xác.
4. Xác minh contract server cho moves/time; tách phần cần server thay đổi khỏi phần client tự sửa được.
5. Tạo adapter Classic/ARAM cho cùng HUD; nhật ký ARAM phân biệt nước cờ với sự kiện, giữ quyền riêng tư.
6. Thay view, tách camera layout và giữ scroll/thu mở được khi xem sau trận.

Chưa cần mở rộng sang engine analysis, replay, report hay share trong đợt này nếu mục tiêu là thay Statistics Board ổn định.

## Kiểm chứng và giới hạn của audit

- Đã truy nguồn từ view → ChessGame → luồng mạng → DTO; kiểm tra catalog và builder của asset trong source.
- Đã tìm test liên quan: suite Domain/application có kiểm tra luật và NetworkMatchSession, nhưng không tìm thấy test trực tiếp cho AnalysisBoardView/BuildMoveRows/capture presentation trong các file khảo sát. Test session không chứng minh history của ChessGame được rollback đúng.
- Unity scene-info trả scene ChessClassic, loaded và không dirty ở thời điểm đọc.
- Thử gọi một lệnh kiểm tra chỉ đọc trong Unity bị từ chối với `Connection revoked`; lệnh không chạy. Không thay đổi quyền kết nối và không chuyển sang kênh khác để thực thi cùng lệnh.
- Chưa chạy playtest, chưa kiểm thử với hai client/server, chưa xác minh dữ liệu live của backend Spring. Các phát hiện lỗi ở trên là kết luận từ đường code, không phải tuyên bố đã tái hiện trong Play Mode.
- Không sửa code/gameplay/UI/asset hoặc scene. Chỉ thêm tài liệu audit này.
