# Kế hoạch kiến trúc Chess but Weird

Ngày khởi tạo: 2026-09-08. Cập nhật: 2026-09-10. Trạng thái: đang triển khai theo từng mốc; các mục dưới đây ghi rõ phần đã kiểm chứng và phần còn ghép với runtime cũ.

## 1. Mục tiêu và phạm vi

- Tách luật cờ và buff khỏi Unity để kiểm thử độc lập, dùng chung với server C#.
- Giữ hành vi hiện có trong quá trình chuyển đổi; sửa lỗi luật được phát hiện bằng thay đổi riêng có kiểm thử.
- Giảm phụ thuộc giữa gameplay, UI, networking, dữ liệu tài khoản và asset.
- Tối ưu CPU, bộ nhớ và thời gian tải dựa trên số đo trước/sau; refactor tự nó không đảm bảo tăng FPS.
- Giai đoạn đầu giữ backend Spring và giao thức hiện tại. Chuyển ASP.NET Core là mốc riêng sau khi lõi ổn định.
- Không đồng thời thay giao diện, kinh tế game, framework networking hoặc chuyển sang ECS/microservices.

## Trạng thái triển khai hiện tại

### Xác minh bổ sung sau review luồng bất đồng bộ

- Khôi phục trận qua REST kiểm tra phiên yêu cầu, generation của lobby/trận, tài khoản và số nước đã xác nhận trước khi cập nhật trạng thái; bỏ phản hồi đã bị chuyển cảnh hoặc yêu cầu mới thay thế.
- Thoát sau đầu hàng chỉ chạy một lần mỗi generation và kiểm tra lại trận sau thời gian chờ.
- MOVE_RESULT kiểm tra thứ tự/identity/duplicate trước khi xử lý thiếu dữ liệu ARAM, tránh khóa input do phản hồi cũ.
- Đã chạy lại: 83 domain/application checks, ARAM reference tests và Unity batch smoke đều pass. Smoke có tình huống ARAM result cũ và delayed resignation sau khi đổi trận. REST hồi đáp muộn chưa được thử end-to-end; backend deploy đang sập nên online hai client/reconnect thực tế vẫn chưa xác minh.

- **P0 — kiểm kê và baseline:** đã có kiểm kê cấu trúc chính, package/asmdef, asset LFS và các đường chạy cần kiểm thử. Domain runner, server ARAM runner và Unity batch smoke có thể chạy lại. Baseline profiler CPU/GPU, GC, memory và thời gian tải trên thiết bị mục tiêu vẫn chưa có, vì vậy chưa kết luận tăng FPS.
- **P1 — tách phần hiển thị:** đã trích factory/cache mesh, skin presenter, animation lifecycle và audio presenter; scene cũ vẫn dùng `ChessGame` làm facade. Unity smoke đã kiểm tra cache, restart, pause/resume, orientation và promotion.
- **P2 — lõi cờ chuẩn:** `com.chessbutweird.domain` và build .NET độc lập đã có state, FEN, luật chuẩn, king safety, castling, en passant, promotion và perft. Local classic giờ commit qua `MatchSession` với `BoardOrientation`, stable PieceId và change set; `ChessGame` chỉ project kết quả sang `ChessPiece[,]`. Bot classic đã đi qua adapter/coordinator ở P4; ARAM và online vẫn giữ compatibility adapter cho tới khi các caller còn lại được chuyển.
- **P3 — ARAM:** sáu buff có policy thuần domain, registry movement/attack dùng chung với mô phỏng king safety, snapshot counter và adapter Unity. Draft, marker, HUD và commit explosion vẫn nằm trong `AramBuffRuntime`/`ChessGame`; registry tùy biến đã được kiểm thử ở domain nhưng runtime hiện cấu hình bộ built-in ARAM-V1.
- **P4 — điều phối và adapter:** đang triển khai theo từng mode. `ClassicMatchCoordinator` sở hữu `MatchSession` cho local classic và bot classic, bao gồm pending promotion, snapshot import, restart boundary và projection stable ID; `Submit(Move, out result)` là command contract cho rejected/promotion-required/applied, nên presentation không tự quyết định thời điểm promotion. `StockfishMoveAdapter` đưa UCI vào cùng Domain command path, còn `StockfishBotController` loại kết quả cũ bằng generation, cancellation và FEN trước khi apply. `AramMatchCoordinator` đứng giữa `ChessGame` và `AramBuffRuntime`, giữ registry/domain policy và wire snapshot tương thích. `NetworkMatchSession` sở hữu pending/confirmed sequence, `IBackendWebSocketTransport` tách transport, còn `NetworkLobbySession` sở hữu room/match/mode/ready lifecycle; `ChessLanController` giữ vai trò facade cho UI và JSON protocol Spring. `MatchResultRecorder` chống ghi profile/history/reward lặp theo match identity; `PlayerProfileStore` lưu danh sách match identity đã ghi để chống lặp sau reload mà vẫn giữ save key hiện có. P4 vẫn chưa được đánh dấu hoàn tất vì chưa có live two-client/reconnect validation với backend và `ChessGame`/lobby facade còn caller legacy chưa chuyển hết.
- **P5 — tối ưu:** chưa có số đo trước/sau trên build và thiết bị mục tiêu. Smoke kiểm tra cache reuse, list isolation và gọi lifecycle `Dispose`; việc native mesh release chưa được đo/khẳng định độc lập, và không dùng kết quả nographics để tuyên bố tăng FPS.
- **P6 — thay Spring bằng ASP.NET Core:** **bị loại khỏi phạm vi phiên này theo yêu cầu người dùng**. Backend Java/Spring, endpoint và giao thức production chưa được chuyển hoặc sửa. Mốc này chỉ bắt đầu sau khi lõi và contract được duyệt riêng.

## 2. Căn cứ từ mã hiện tại (snapshot trước refactor)

- `ChessGame` vẫn là facade lớn chứa lifecycle, presentation, FEN và network; phần luật classic local và reward đã có boundary riêng nhưng chưa thu nhỏ facade hoàn toàn.
- Bàn cờ logic lưu ChessPiece[,] với phần tử là MonoBehaviour.
- AramBuffRuntime trộn luật, draft UI, marker, trạng thái quân và DTO backend.
- ChessLanController trộn lobby UI, REST, WebSocket và điều phối trận.
- BackendWebSocketClient dùng ClientWebSocket; có thể giữ hợp đồng JSON khi chuyển server.
- Bot dùng Stockfish UCI; profile và inventory có dữ liệu lưu PlayerPrefs.
- Có kiểm thử tham chiếu ARAM trong OnlineServer và công cụ auth smoke test; cần kiểm kê đầy đủ trước khi đặt baseline.
- Unity batch smoke đã chạy qua các fixture local classic, ARAM và LAN adapter; chưa có Profiler/build baseline trên thiết bị mục tiêu và chưa kiểm chứng backend đang triển khai. Không coi danh sách tính năng là bằng chứng chúng không có lỗi.

## 3. Kiến trúc đích

```text
Packages/com.chessbutweird.domain/    C# thuần, nguồn dùng chung duy nhất
  Runtime/State/                    BoardState, PieceState, MatchState
  Runtime/Rules/                    luật chuẩn, an toàn vua, kết quả
  Runtime/Variants/Aram/            hành vi buff và trạng thái
  Runtime/Serialization/           FEN và snapshot độc lập transport
  Tests/                           tình huống luật và tính xác định
Assets/Scripts/Chess/
  Application/                     MatchSession, điều phối command/result
  Presentation/                    board, piece, input, animation, camera, UI
  Infrastructure/                  backend, Stockfish, lưu trữ
  Bootstrap/                       khởi tạo và quản lý vòng đời
Server/                            ASP.NET Core, tạo ở giai đoạn sau
Tests/Fixtures/                    dữ liệu kiểm thử chung client/server
```

Application phụ thuộc Domain. Presentation và Infrastructure nối vào Application qua hợp đồng nhỏ. Domain không tham chiếu UnityEngine, UI, JSON của backend hoặc I/O. Bootstrap nối các thành phần bằng constructor/Initialize; chưa cần framework DI phía Unity.

Dùng asmdef phân định assembly sau khi kiểm kê tham chiếu; không di chuyển hàng loạt component đang gắn scene. Domain được build cho server từ cùng nguồn package, không sao chép thành hai bản. Xác minh tập API, ngôn ngữ C# và build target tương thích Unity hiện dùng bằng một spike trước khi chốt target framework.

### Trạng thái và nước đi

- PieceId ổn định xuyên suốt di chuyển, snapshot và phong cấp; tránh dùng tham chiếu GameObject làm danh tính.
- MatchState gồm bàn cờ, lượt, quyền nhập thành, en passant, bộ đếm hòa, lịch sử cần thiết, mode, rulesetVersion và AramState.
- MoveCommand chứa yêu cầu di chuyển/phong cấp. Domain trả trạng thái mới và MoveResult có các thay đổi có cấu trúc: move, capture, castle, promotion, explosion, cooldown, kết quả.
- Presentation hiển thị kết quả; hoàn tất animation không quyết định luật. Promotion là lựa chọn phải hoàn thiện trước khi commit nước đi.
- Mô phỏng kiểm tra vua và nước đi thật dùng chung quy trình áp dụng hiệu ứng. Bắt đầu với bản sao trạng thái đơn giản, chỉ chuyển sang make/unmake nếu benchmark chứng minh cần.
- FEN phục vụ cờ chuẩn/Stockfish; snapshot ARAM phải mang thêm PieceId, buff, hồi chiêu, số lần dùng và phiên bản. FEN không đủ để phục hồi ARAM.

### Hệ thống buff

- BuffDefinition: ID ổn định, tham số cân bằng, phiên bản. ScriptableObject là authoring/UI, chuyển sang dữ liệu thuần cho Domain/server.
- BuffBehavior: một lớp cho từng cơ chế; đăng ký theo ID, không tạo switch buff tập trung ngày càng lớn.
- BuffState: mục tiêu theo PieceId, số lần dùng, hồi chiêu, cờ kích hoạt riêng mỗi trận.
- Điểm mở rộng hữu hạn: sinh nước đi, thay thế kiểu đi, xác định tấn công, điều kiện đặc biệt, hiệu ứng bắt quân, bắt đầu lượt, đánh giá kết quả.
- Quy định thứ tự và chính sách cộng/thay thế/cấm; kiểm thử cụ thể Doppelganger + Freestyle Leap trước khi cho phép tổ hợp.
- RNG có seed và thuật toán xác định, không dùng UnityEngine.Random hoặc phụ thuộc thứ tự duyệt dictionary.
- Đóng băng cấu hình theo rulesetVersion của trận. Buff mới dùng cơ chế sẵn có có thể chỉ thêm dữ liệu; cơ chế mới vẫn cần code và triển khai đồng bộ.

### Online và dữ liệu

- Tách LobbyController, NetworkMatchSession, transport và view khỏi ChessLanController.
- Phân biệt authoritative snapshot và pending move. Server từ chối thì phục hồi snapshot, dừng animation cũ và hiển thị lý do.
- Mỗi command có requestId; state có sequence/version; xử lý trùng, cũ, sai thứ tự và reconnect bằng snapshot đầy đủ.
- Server là nguồn quyết định nước đi, buff, kết quả và phần thưởng online. Client có thể dùng cùng Domain để gợi ý/dự đoán.
- MatchFinished được xử lý riêng bởi reward/history service, chống ghi thưởng trùng. Dữ liệu local/offline và online cần chính sách chuyển đổi rõ ràng.
- WebSocket gửi qua hàng đợi tuần tự, quản lý cancellation/lifetime và chuyển cập nhật UI về main thread.

## 4. Các giai đoạn và điều kiện hoàn thành

### P0 — Baseline và kiểm kê

1. Kiểm kê scene/prefab, serialized reference, asset LFS, assembly, build target, bot executable, backend contract và save key.
2. Chạy luồng local, bot 5 mức, ARAM 6 buff, online hai client, pause, restart, reconnect, inventory và kết quả.
3. Ghi CPU/GPU frame time, GC allocation, memory, thời gian vào trận và sinh nước đi trên cùng máy/build/cảnh. Tách thời gian Stockfish khỏi engine luật.
4. Tạo fixture cho các luật đặc biệt và hành vi hiện tại; ghi lỗi sẵn có riêng.

Hoàn thành khi có bảng baseline, danh sách giới hạn môi trường, ma trận hồi quy và build tham chiếu tái chạy được.

### P1 — Tách phần hiển thị ít rủi ro

1. Trích PieceVisualFactory, PieceSkinPresenter, PieceAnimator và MatchAudioPresenter từ ChessGame.
2. Giữ ChessGame làm facade tương thích với scene/UI cũ; giữ GUID .meta và serialized fields hoặc migration rõ ràng.
3. Gom quản lý coroutine, event subscription, dispose/cancel khi restart/thoát trận.

Hoàn thành khi giao diện, camera, âm thanh, chọn quân, phong cấp và restart giữ hành vi baseline; không missing script/reference.

### P2 — Lõi cờ chuẩn C# thuần

1. Tạo package Domain và spike build trong Unity + .NET độc lập.
2. Tạo state, move pipeline, luật quân, an toàn vua, nhập thành, en passant, phong cấp, kết quả và serialization.
3. So sánh lõi cũ/mới trong chế độ kiểm thử/shadow, không cho hai lõi cùng cập nhật trận thật.
4. Chuyển local classic sang lõi mới qua adapter; kiểm thử perft với vị trí chuẩn đã kiểm chứng và các fixture luật đặc biệt.

Hoàn thành khi luật chuẩn, FEN round-trip và mô phỏng không làm thay đổi state gốc đều đạt; build Domain không kéo Unity vào.

### P3 — ARAM mở rộng được

1. Tách config/behavior/state và chuyển lần lượt cả sáu buff.
2. Tách draft/marker/HUD khỏi xử lý luật; giữ hành vi practice và network tương ứng.
3. Kiểm thử từng buff: mục tiêu, biên bàn, chiếu, capture, promotion, cooldown và phục hồi snapshot.
4. So sánh với aram-rules.js; khi có khác biệt, xác định quy tắc mong muốn thay vì mặc định bản cũ đúng.
5. Thử thêm một buff mẫu trong test để chứng minh không cần sửa MatchController; chưa đưa vào nội dung game.

Hoàn thành khi sáu buff đạt fixture, setup xác định theo seed, snapshot round-trip đủ state và quy tắc tổ hợp được ghi rõ.

### P4 — Điều phối thống nhất và adapter

1. `ClassicMatchCoordinator` đã đưa local classic move/promotion vào `Submit(Move, out result)` và giữ một writer cho `MatchSession`; bot classic dùng cùng coordinator/domain command path. Online vẫn giữ server authoritative và endpoint Spring.
2. Bot lấy FEN từ Domain qua `StockfishMoveAdapter`; `StockfishBotController` hủy turn cũ và kiểm tra generation, lượt và FEN sau restart/pause/đổi trận.
3. `IBackendWebSocketTransport`, `NetworkMatchSession` và `NetworkLobbySession` tách transport-independent state khỏi phần UI/protocol của `ChessLanController`; pending/confirm/reject/duplicate/stale snapshot đã có domain checks. Live two-client validation vẫn cần backend đang triển khai.
4. `MatchResultRecorder` tách lifecycle ghi terminal result khỏi facade; `PlayerProfileStore`/`PlayerAuthService` vẫn dùng save key `chess_but_weird_player_profile_`, bổ sung danh sách match identity tùy chọn để save JSON cũ vẫn đọc được. Migration version riêng chưa cần thêm vì field mới có default khi deserialize.
5. `ChessGame` và `ChessLanController` vẫn là compatibility facades cho serialized scene, UI và protocol cũ; cần chuyển caller/scene còn lại rồi mới xóa các đường legacy.

Hoàn thành khi local, bot và online hoạt động với kiến trúc mới; reconnect không mất ARAM state, thưởng không ghi trùng, save cũ còn đọc được.

#### Checklist triển khai P4 — cập nhật 2026-09-10

- [x] Contract classic `Rejected` / `PromotionRequired` / `Applied`; promotion sai không đổi state hoặc pending command.
- [x] Local classic và bot classic dùng `ClassicMatchCoordinator`/`MatchSession`; UCI đi qua `StockfishMoveAdapter`.
- [x] Bot async có cancellation, generation, turn và FEN guard trước khi apply kết quả.
- [x] ARAM có `AramMatchCoordinator`; draft, marker, HUD và payload cũ tiếp tục ở runtime adapter; registry/domain policy không đổi.
- [x] Network transport có interface; `NetworkMatchSession` xử lý requestId, pending, duplicate, stale result và snapshot sequence.
- [x] Lobby room/match/mode/ready lifecycle có `NetworkLobbySession`; `ChessLanController` chỉ là compatibility facade cho UI và endpoint JSON.
- [x] Terminal reward/history write có `MatchResultRecorder`; `PlayerProfileStore` giữ save key cũ và dedupe match identity qua field bổ sung tương thích với save JSON cũ.
- [x] Domain runner: 82 checks; ARAM server rule tests; Unity batch smoke gồm local classic, orientation, castling, en-passant, promotion, ARAM và LAN legacy.
- [ ] Unity smoke rerun sau snapshot fix đang bị chặn bởi Unity Editor đang mở cùng project; batch process thoát trước compile với exit code 1.
- [ ] Chưa chạy live two-client backend reconnect/duplicate command với server Spring đang triển khai; chưa thể xác nhận online ARAM reconnect giữ đủ state trên wire.
- [ ] Chưa chuyển hết caller/scene khỏi `ChessGame` và `ChessLanController`; chưa xóa compatibility paths.

Vì hai mục cuối chưa đạt, P4 hiện vẫn là **đang triển khai**, không ghi nhận hoàn tất. Java/Spring, endpoint và gameplay không nằm trong thay đổi này.

### P5 — Tối ưu dựa trên Profiler

1. Đo lại đúng các kịch bản P0 trong build, lấy nhiều lần chạy sau warm-up.
2. Nếu mesh splitting/runtime material là điểm nghẽn, bake prefab trong Editor hoặc cache theo lifecycle rõ ràng.
3. Nếu cấp phát khi sinh nước đi nổi bật, tái sử dụng buffer; cân nhắc make/unmake sau khi có test chống sai state.
4. Nếu UI cập nhật liên tục gây tốn CPU, chuyển phần liên quan sang cập nhật theo thay đổi state.
5. Chỉ pool marker/highlight/VFX khi số đo cho thấy có lợi. Không thêm pool toàn hệ thống mặc định.

Hoàn thành khi có bảng trước/sau trên cùng điều kiện, không hồi quy ngoài sai số đã xác định; đặt ngân sách cụ thể sau P0 theo thiết bị mục tiêu, không hứa FPS khi chưa đo.

### P6 — Thay Spring bằng ASP.NET Core

1. Thu thập mã/schema/backend contract và cách xác thực hiện tại; phần này không có đủ trong repo Unity để chốt migration.
2. Dựng server C# dùng cùng Domain; giữ REST/WebSocket contract khi khả thi, thêm version có tương thích rõ ràng.
3. Tuần tự hóa command theo trận, xác thực người chơi, persistence nguyên tử, idempotency và snapshot reconnect.
4. Port auth/room/history/reward thực sự đang dùng; kế hoạch chuyển tài khoản, mật khẩu/token, dữ liệu và rollback riêng.
5. Chạy contract test, hai client, restart server, mất kết nối, command trùng và load test theo lượng người chơi mục tiêu.
6. Chạy môi trường staging, chỉ chuyển production khi dữ liệu và bản deploy cụ thể đã được duyệt.

Hoàn thành khi chức năng đạt parity đã thống nhất, cùng fixture luật với Unity, dữ liệu chuyển đổi được kiểm chứng và có đường quay lại. Không đưa Redis, nhiều instance hoặc microservices vào trước khi có nhu cầu đo được.

## 5. Quy tắc triển khai và rollback

- Mỗi thay đổi nhỏ có thể build/review; không trộn refactor, đổi luật và cân bằng buff trong cùng thay đổi.
- Dùng adapter và lựa chọn implementation để chuyển từng chế độ; một trận chỉ có một nguồn state có quyền ghi.
- Giữ đường chạy cũ đến khi chế độ thay thế đạt ma trận hồi quy. Xóa code cũ theo mốc, tránh duy trì hai lõi lâu dài.
- CI: build Domain, test classic/ARAM, build Unity khi runner khả dụng; server thêm contract/integration test ở P6.
- Không sửa hàng loạt asset/scene hoặc xóa Mirror/relay chỉ vì tên có vẻ cũ; kiểm kê phụ thuộc trước.
- Khi không có Unity, backend hoặc thiết bị kiểm thử, ghi rõ mốc chưa đạt; không thay thế xác minh runtime bằng đọc code.

## 6. Thứ tự ưu tiên

P0 → P1 → P2 → P3 → P4 → P5 → P6.

Kết quả bàn giao ở mỗi mốc: mã đã chuyển, test liên quan, bằng chứng build/runtime, số đo nếu có tối ưu, lỗi còn lại và cách rollback. Chưa ước lượng lịch hoàn thành trước khi P0 xác nhận phạm vi scene, build và backend.
