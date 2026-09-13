# Loading và tải asset nền

> Kế hoạch cũ, đã được thay thế bằng yêu cầu Addressables mới của người dùng: chỉ tải cosmetic được chọn trước trận. Không dùng tài liệu này làm trạng thái triển khai hiện tại.

Ngày: 2026-09-11. Triển khai giao cho GPT-5.6 Luna, reasoning effort `max`; agent chính review và kiểm tra độc lập.

## Mục tiêu

Sau đăng nhập, đăng ký thành công, Play as Guest hoặc khôi phục phiên, người chơi thấy một bước chuẩn bị ngắn trước menu. Bước này phải gắn với công việc thật. Trong menu và trận đấu, tài nguyên tùy chọn được tải trước có kiểm soát, không hiện thông báo loading giữa trận.

## Hiện trạng đã kiểm tra

- `ChessGame.Start` chuẩn bị bàn trước xác thực; callback đầu tiên chờ hai frame rồi dựng menu. Đăng nhập lại từ `ChessTurnSelectionUI` dùng callback khác.
- `HandDrawnMenuView` đang thể hiện tiến độ theo animation chuyển màn hình, không phải tiến độ tải asset.
- `HandDrawnMenuAssets` đọc và giải mã nhiều PNG đồng bộ khi dựng menu.
- `PieceSkinPresenter` tải prefab đồng bộ; skin DC nằm ngoài Resources và dựa vào AssetDatabase trong Editor.
- `GameRuntimeSettings` đã đặt background loading priority Low, nhưng chưa có hàng đợi, cache và điều tiết riêng.
- Workspace có thay đổi kiến trúc từ trước: phải giữ nguyên. Backend Spring và luật cờ không thuộc phạm vi.

## Thiết kế và thứ tự triển khai

1. **Một cổng chuẩn bị phiên:** dùng cùng một luồng cho login, signup, guest, restore và login lại. Loading có trạng thái chuẩn bị, hoàn tất, lỗi/thử lại; khóa thao tác xuyên qua overlay. Coroutine sống ngoài `ChessGame.PrepareGame` vì hàm này gọi `StopAllCoroutines`.
2. **Giao diện:** nền giấy sáng và nét tối phù hợp menu hiện có, tiêu đề ngắn, thông điệp dễ hiểu, chuyển động nhẹ. Thanh tiến độ phản ánh số công việc hoàn tất; không chạy phần trăm theo thời gian hoặc chờ giả. Chỉ hoàn tất sau khi menu thực sự dùng được. Chuyển menu thông thường chỉ dùng animation.
3. **Asset nền tảng:** font/UI thiết yếu, tài nguyên bàn/quân mặc định và âm thanh cần ngay. Những tài nguyên đã được scene tham chiếu trực tiếp không được báo cáo sai là mới tải bất đồng bộ. Chuẩn bị artwork menu theo từng bước và tái sử dụng kết quả để tránh đọc/giải mã lặp lại.
4. **Tải nền:** một yêu cầu async đang chạy; ưu tiên asset người chơi chọn, rồi asset có khả năng dùng tiếp. Hàng đợi khử trùng lặp, cache có giới hạn theo catalog và vòng đời rõ ràng. Không dùng `Resources.LoadAll` hoặc tải mọi skin cùng lúc.
5. **Bảo vệ trận đấu:** kiểm tra input, animation/input lock và frame time trước khi phát yêu cầu mới. Ngưỡng frame cần tính FPS cap/vsync để 30 FPS hợp lệ không làm hàng đợi ngừng vĩnh viễn. Giảm ưu tiên và chừa khoảng nghỉ sau thao tác/frame chậm. Không khởi tạo hàng loạt prefab ngầm giữa lượt.
6. **Skin:** đóng gói prefab cho bản player bằng đường dẫn runtime thực; giữ GUID, không nhân bản mesh/texture. Khi skin chưa sẵn sàng ở đầu trận, chọn mặc định cho cả trận. Không đổi bộ skin do tải hoàn tất giữa trận hoặc lúc phong cấp. Nếu đã sẵn sàng, dùng cache; gameplay không quay lại tải đồng bộ.
7. **Vòng đời/lỗi:** yêu cầu trùng được gộp; callback cũ không được mở UI sau logout/hủy/destroy. Tài nguyên thiếu dùng fallback có kiểm soát, không lặp lỗi mỗi frame. Lỗi bắt buộc phải cho thoát hoặc thử lại; không treo loading. Tài nguyên không còn dùng được thả tham chiếu ở thời điểm an toàn; không gọi GC/unload lớn giữa trận.

## Điều kiện review và kiểm thử

- Login/guest/restore/login lại đều đi qua cổng chuẩn bị, không mở menu trước khi sẵn sàng; logout/hủy không để callback cũ hồi sinh UI.
- Tiến độ đơn điệu theo công việc; cache hit không tạo thời gian chờ giả; lỗi tài nguyên không gây treo.
- Hàng đợi chỉ chạy một yêu cầu, khử trùng lặp, ưu tiên đúng, dừng phát việc khi tương tác hoặc frame chậm và tiếp tục khi an toàn.
- Skin có resource path dùng được trong player, cache miss vẫn chơi được với mặc định; phong cấp và restart nhất quán.
- Không có tải skin đồng bộ trong đường gameplay. Menu không giải mã lại cùng artwork ở mỗi lần mở.
- Chạy `tools/verify-architecture.ps1` và Unity smoke phù hợp; giữ kết quả và phân biệt lỗi môi trường với lỗi runtime.
- Đo bản Development Build có đồ họa trên thiết bị mục tiêu: thời gian từ auth thành công tới menu, frame-time p95/p99/max, GC/frame và peak memory khi tải nền bật/tắt. Thử ở 30/60 FPS và cấu hình thấp. Batch/nographics không chứng minh độ mượt.

## Giới hạn phải giữ rõ

Unity vẫn có bước tích hợp asset/GPU trên main thread. Điều tiết chỉ kiểm soát lúc phát yêu cầu mới, không bảo đảm hủy hoặc ngắt một yêu cầu đang chạy. Phiên bản này không được tuyên bố tuyệt đối không giật khi chưa đo bản build. Nếu một asset đơn lẻ quá nặng, cần giảm texture/mesh hoặc chia gói trong bước tối ưu tiếp theo.

## Trạng thái xác minh

- Baseline trước thay đổi: 83 domain/application checks và ARAM server rule tests pass.
- Unity hiện mở dự án gốc; kiểm tra CLI dùng bản sao riêng. Hai lượt baseline với Library sao chép gặp native Mono crash trước khi hoàn tất smoke; đang thử lại bằng Library sạch. Không coi đây là kết quả pass/fail của mã loading.
- Triển khai và review: đang tiến hành.
