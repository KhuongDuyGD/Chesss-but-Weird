# Statistics Board: implementation và kiểm chứng

> Cập nhật sau yêu cầu mới ngày 03/10: xem [báo cáo HUD và ARAM](aram-update-2026-10-03.md). Summary ARAM đã chuyển dưới controls, dải buff cũ được gỡ, UI dùng Schoolbell, catalog local có 35 buff. Phần kiểm chứng/trạng thái bên dưới là lịch sử lượt trước; lỗi Freestyle baseline đã được thay bằng luật tài liệu và test V1 riêng. Contract statistics backend vẫn áp dụng.

## Phạm vi

Unity ở `D:/Game_Project/Chesss-but-Weird`; backend trận đấu Spring Boot ở `D:/Game_Project/ChessButWeirdLAN_BE/chess-lan-backend`. Backend tài khoản ASP.NET không cần thay đổi cho tính năng này. Chưa commit, push hoặc deploy backend live.

HUD dùng card giấy/mực nhỏ cho người chơi và lượt, nhật ký mở theo nhu cầu, Focus giữ trạng thái trận, font body rõ. Camera render toàn màn hình; không để viewport cắt gây dải kem trên/dưới. Camera không đổi khi bật/tắt nhật ký. Chiều cao nhật ký chừa vùng ARAM theo panel thực, với hai tab History/Captures. Các kỹ năng và lựa chọn bắt buộc của ARAM vẫn dùng luật/capability sẵn có.

## Vì sao bản cũ hoạt động online/LAN

`ChessGame` tự tích lũy history/captures/elapsed trong client. `ChessLanController` nhận FEN và notation từ backend rồi cập nhật bàn cờ và nhãn. Trong một phiên liên tục, cơ chế đó đủ để bảng thống kê hiện dữ liệu dù server không có module statistics riêng.

Điểm yếu xuất hiện khi reconnect, khôi phục trận, rollback nước dự đoán, phong cấp hoặc ARAM sinh/hi sinh/phá hủy/đổi loại quân. FEN là trạng thái hiện tại, không thể khôi phục lịch sử bắt quân từ số quân thiếu. Tên `ChessLanController` không chứng minh mọi đường LAN dùng cùng protocol; relay Node legacy trong `OnlineServer` chưa được kiểm thử đầu cuối trong thay đổi này.

## Quyền sở hữu dữ liệu mới

- Local/solo: ledger `MatchStatistics` ghi nước, phe thực, captured kind và public events từ hành động thật. Không ép trắng/đen luân phiên trong ARAM có lượt thêm.
- Multiplayer: dự đoán đánh dấu pending; server xác nhận thay thế ledger. Disconnect/send failure/reject rollback cả notation lẫn capture. Full snapshot dựng lại dữ liệu từ history persisted khi reconnect.
- ARAM: capture khác destruction/sacrifice/spawn/transform. Nhật ký không ghi private network buffs. Captured material chỉ hiển thị cho Classic và không gọi là điểm trận/đánh giá thế cờ.
- Elapsed là thời gian đã trôi qua, không phải hai đồng hồ time control. Backend dùng `startedAt` → `finishedAt`/hiện tại; chưa có pause accounting phía server. Snapshot elapsed được client nội suy rồi đóng băng khi kết thúc.

## Contract backend

REST match detail/active/history và WebSocket `GAME_START`, `MOVE_RESULT`, `GAME_STATE`, `GAME_OVER` thêm `statistics`:

```json
{
  "version": 1,
  "moveCount": 1,
  "elapsedSeconds": 12,
  "historyComplete": true,
  "capturesComplete": true,
  "entries": [
    {"id": "move-example", "moveNumber": 1, "actor": "WHITE", "text": "e4", "isMove": true, "captured": null}
  ]
}
```

IDs là opaque. `isMove` đúng tên key JSON; `actor`/`captured` parse enum không phân biệt hoa thường. Events dùng `isMove=false`, moveNumber của nước vừa áp dụng (0 cho setup), không có `captured`. Client kiểm tra version, count, ID trùng, sequence, actor/kind và completeness; không nhận snapshot cũ hơn confirmed move. Snapshot lặp không rebuild journal.

Server projector dùng persisted ordered moves và FEN trước/sau để xác định capture/en passant/promotion và public board effects. Nó kiểm tra actor, gap và consistency với current board. Server hiện bắt đầu từ đội hình tiêu chuẩn kể cả ARAM V1; custom initial position tương lai cần persisted initial state. Full history gửi mỗi lần hiện có chi phí O(n); nên đo và thay bằng delta/checkpoint nếu history lớn.

Backend cũ vẫn có fallback REST moves hoặc partial history/time unavailable; không dựng dữ liệu đầy đủ giả. Ngày giờ legacy không có múi giờ sẽ báo time unavailable, không giả định timezone server giống client. Online extended abilities không được mở bằng việc bỏ `!networkMatch`: backend ARAM V1 và offline Unity có capability khác nhau.

## Kiểm chứng

- Roslyn Unity Editor/Player/tools compilation: 137/137/18 sources PASS, không launch Editor game.
- Pure C# statistics: 14 checks PASS.
- Spring Maven: 36 tests, 0 failures/errors; capture/reconnect/terminal/ARAM integration và projector en passant/promotion/castling/effects/gaps/JSON key.
- Headless Unity fixture: 1.626 assertions PASS ở bản kiểm tra cuối (report `Logs/hud-headless/20261003-140048/audit-result.txt`). 5 tỷ lệ màn hình (1168×541, 1280×720, 1920×1080, 1024×768, 2560×1080), 0–10 actions, text metrics/bounds, panel separation, scroll/follow/latest, camera replacement/restoration và stale ability guards. Adapter statistics production kiểm tra snapshot malformed/deduplication/capture sync/recovery/timezone. WorldSpace canvas mô phỏng kích thước Expand scaler; không chứng minh pixel render hay bàn cờ 3D.
- Domain suite có baseline failure `Freestyle 2x2 leap` Program.cs:226, tái hiện từ HEAD riêng; không sửa luật ngoài scope để che lỗi.

## Những việc vẫn cần xác minh trước khi coi hoàn tất

1. Cold Play Mode qua menu thực: chữ/board/cosmetic/camera, journal/Focus, kết thúc/restart và resize. Sau crash, không tự chạy/capture trong Editor của người dùng. Xem [rà soát crash](statistics-crash-review-2026-10-03.md).
2. ARAM local: draft, mục tiêu bắt buộc, action/cooldown, buff strip và rifle; so sánh nhật ký với hành động thật.
3. Hai client Unity với backend đã cập nhật: confirm/reject/disconnect/reconnect/terminal; xác minh parity giữa các màn hình.
4. Triển khai backend trước client mới trong môi trường kiểm thử, rồi kiểm tra live service version. Không tự deploy production từ task UI.

Compile/tests đạt không đồng nghĩa đã hoàn tất các bước trên. Những thay đổi font Schoolbell/settings và recovery scene từ Editor/người dùng được giữ nguyên.
