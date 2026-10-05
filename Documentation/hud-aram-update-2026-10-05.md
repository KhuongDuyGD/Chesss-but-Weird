# HUD / ARAM: sửa và kiểm chứng ngày 05/10/2026

## Thay đổi

- Sửa xung đột visibility: trước đây `EnsureActionView` bật canvas vào đầu lượt, trong khi `AramAbilityView.Update` tắt canvas nếu không có actions. View mới giữ panel buff xuyên suốt trận, kể cả buff thụ động; `SetVisible` chỉ cập nhật trạng thái mong muốn. Pause, promotion, setup và kết thúc vẫn có trạng thái ẩn rõ ràng.
- Panel quản lý buff chuyển về góc trái dưới, rộng 300 đơn vị mặc định / 360 khi Expand. Tóm tắt tên buff và tiến độ/duration; action labels một dòng. Expand mở mô tả, chi tiết cooldown/giá và vùng cuộn. Nút được tái sử dụng; click vẫn kiểm tra ID/context/điều kiện mới nhất. Statistics revision không tự kéo phần đọc buff về đầu.
- Toast ở góc trái dưới; prompt chọn mục tiêu setup ở mép trái. Journal bên phải không còn phải giảm chiều cao theo panel buff bên trái. Statistics bổ sung `ResponsiveSafeArea`.
- Solo giữ thông tin buff của người chơi khi bot đang đi, và không cho người chơi dùng action của bot. Network chỉ trình bày buff của local team; không mở extended actions mà server V1 chưa hỗ trợ.
- Absolute Sniper: bỏ nút kích hoạt riêng. Khi Bishop đã tích charge, thao tác chọn Bishop rồi chọn quân địch trên đường chéo thông sẽ tự bắn, Bishop giữ nguyên ô, tiêu một lượt/charge. Không có hộp xác nhận. Kiểm tra an toàn vua dùng phép chiếu loại bỏ mục tiêu, kể cả Queen explosion, thay vì giả định Bishop di chuyển. Di chuyển vào ô trống không tiêu charge; expiry 4 combined moves được giữ.
- One Man Army: vào FPS yêu cầu khóa/ẩn chuột ngay, di chuột để ngắm, click trái bắn ở tâm camera. ALT trái/phải mở hoặc khóa chuột; khi mở chuột, ngừng xoay/bắn để dùng UI. Hint nằm bên trái; crosshair bám tâm viewport toàn màn hình, không bị lệch do safe area. Pause/mất focus tạm mở chuột; thoát hoặc disable runtime khôi phục trạng thái trước FPS.

## Auto-scaling

Canvas dùng `ScaleWithScreenSize`, reference 1280×720, `ScreenMatchMode.Expand`: fit theo chiều hạn chế mà không kéo méo giao diện. Panel buff giới hạn chiều rộng/chiều cao theo safe frame và dùng ScrollRect khi nhiều nội dung. Khi resize, safe area và bố cục được cập nhật; journal vẫn giữ vị trí đọc và nút Jump to latest. Ở độ phân giải thấp, chữ cũng thu nhỏ theo tỷ lệ; chưa có bằng chứng raster về độ dễ đọc trên màn hình vật lý.

## Kết quả kiểm tra

- Compile source bằng Unity Roslyn: 141 Editor / 141 Player / 18 Editor tools, PASS; không launch game chính.
- HUD headless: **9.755 assertions PASS**, report `Logs/hud-headless/20261005-153234/audit-result.txt`, source hashes ở cùng thư mục.
- ARAM headless: **318 assertions PASS**, report `Logs/aram-headless/20261005-153234/audit-result.txt`, production runtime/bridge và Core safety methods được trích từ source, có source hashes.
- Pure C# statistics: **15 checks PASS**; Domain: **105 checks PASS**.
- `git diff --check`: PASS.

HUD matrix: 640×480, 800×600, 1168×541, 1280×720, 1366×768, 1920×1080, 1024×768, 2560×1440, 2560×1080, 3440×1440, 3840×2160, 1080×1920. Mỗi kích thước kiểm tra Classic/ARAM, compact/expanded và 0–10 actions; bounds/text, scroll, journal không chồng panel bên trái, safe frame với insets bất đối xứng và crosshair đúng tâm. Regression giữ passive panel qua 100 turn contexts, pause/resume, stale clicks, tooltip/privacy, snapshot validation/deduplication/capture sync, draft và scroll đọc lịch sử.

ARAM regression gồm charge expiry, mục tiêu chắn/ô trống, lượt/pause, Bishop bị ghim vẫn bắn mà không rời ô, Queen explosion làm lộ vua bị từ chối, capture ledger một lượt; ALT trái/phải qua Input System, lock preference, cursor suspend/resume và restore khi exit/disable.

## Giới hạn bằng chứng

Các fixture Unity chạy trong project mới dưới Logs, `-batchmode -nographics`, dùng NullGfxDevice; HUD mô phỏng kích thước canvas sau Expand scaler bằng WorldSpace canvas/TMP/RectTransform thực. Kết quả không chứng minh pixel render, kích thước chữ trên monitor vật lý, OS capture chuột hay trận hai client với backend. Không chạy Play/Stop/capture trong Editor game của người dùng; Unity MCP hiện bị revoke quyền kết nối. Không deploy hoặc thay contract server. Cần cold Play Mode để tạo lại HUD runtime mới, tránh dùng object cũ qua hot reload.
