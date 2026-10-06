> Historical note (updated 2026-10-06): this document describes work before the .NET online migration. Spring/Node endpoints, classes and validation results below refer to the retired backend. Use [OnlineDotNetIntegration.md](OnlineDotNetIntegration.md) for the current implementation.

# Handoff — Statistics Board, 2026-10-03

> Đây là handoff lịch sử của lượt trước. Yêu cầu mới là tự làm khi người dùng offline, không hỏi trong quá trình code. Kết quả hiện tại về HUD, 35 buff local, kiểm chứng và giới hạn online nằm trong [aram-update-2026-10-03.md](aram-update-2026-10-03.md); các số liệu/trạng thái bên dưới chỉ phản ánh thời điểm handoff.

## Trạng thái bàn giao

**Đang triển khai, chưa hoàn tất, chưa sẵn sàng merge/deploy.** Người dùng yêu cầu dừng để chuyển sang chat kế tiếp do giới hạn token. Không triển khai thêm ngoài lưu handoff. Không có commit/push/deploy được thực hiện trong lượt này.

Prompt tiếp tục: “Đọc Documentation/STATISTICS-BOARD-HANDOFF-2026-10-03.md, kiểm tra working tree của Unity và backend LAN, rồi tiếp tục hoàn thiện Statistics Board. Giữ các thay đổi hiện có; ưu tiên sửa HUD thực tế và kiểm thử dữ liệu Classic/ARAM. Hỏi tôi khi chưa rõ luật hoặc cần hỗ trợ.”

## Mục tiêu và quyền đã được người dùng cấp

- Thay toàn bộ Statistics Board cũ bằng HUD giấy ấm/mực/tím gọn: player strips, turn/status, journal mở khi cần, focus, phần ARAM phù hợp.
- Được sửa code frontend và backend liên quan statistics để hoạt động đúng cho solo/local/multiplayer và ARAM offline/online.
- Người dùng muốn xử lý toàn diện, ưu tiên chất lượng. Không tự mở rộng sang tính năng gameplay không liên quan.
- Cần giải thích vì sao statistics cũ vẫn hoạt động online/LAN dù chưa có module statistics trên server.
- Hỏi khi không chắc; không yêu cầu lại quyền đã có. Không triển khai backend lên server production nếu chưa được yêu cầu.
- Người dùng cho phép fork task mới để MCP nhận port mới. Đã tạo một fork CHỈ kiểm tra Unity, không sửa source.
- Không có AGENTS.md được tìm thấy trong các repo đã khảo sát. Không spawn subagent; không có quyền chủ động delegation ngoài fork cụ thể đã được người dùng cho phép.

## Các đường dẫn

1. Unity: `D:/Game_Project/Chesss-but-Weird` (repo chính).
2. **Backend trận đấu đúng:** `D:/Game_Project/ChessButWeirdLAN_BE/chess-lan-backend` (Spring Boot, Java, REST + WebSocket, JPA).
3. Backend tài khoản/vật phẩm: `D:/Game_Project/ChessButWeirdBE/ChessGame.Api` (ASP.NET Core/Mongo). Đã xem, **không sửa**. Không có API room/move/socket ở đây; Models/Match.cs hiện trống.
4. Unity Editor: `D:/Unity Editor/6000.4.7f1/Editor/Unity.exe` (không phải đường dẫn C:/Program Files mặc định trong script).
5. Tài liệu gốc đã có: `Documentation/match-hud-redesign-proposal.md`, `Documentation/statistics-board-current-state-audit.md`.
6. Mockup HTML gốc: `C:/Users/Admin/.codex/visualizations/2026/09/21/01a0c45b-5a53-79d1-ad12-75508320d991/chess-match-hud.html`. Đây là mockup tham khảo, không phải gameplay.
7. Ảnh người dùng báo lỗi HUD: `C:/Users/Admin/AppData/Local/Temp/codex-clipboard-6d2c7680-85ed-48a9-b9c8-bc47f6d5cead.png`.

## Kết luận backend đã xác minh

Backend LAN đúng đã có:
- MatchServiceImpl.startMatch/submitMove/syncState/active/history/findById/resign.
- Server kiểm tra luật/turn, lưu MatchMoveEntity (player, moveNumber, requestId, from/to/promotion, notation, fenAfter), MatchEntity (startedAt/finishedAt/moveCount/currentFen/status/ARAM state).
- Dedupe requestId, authority cho nước đi, kết quả, Elo/rewards.
- Classic engine và ARAM engine. ARAM online hiện là tập buff V1 giới hạn; offline Unity có nhiều buff/ability hơn. Không mở online extended abilities bằng cách xóa điều kiện `!networkMatch`.
- DTO REST đã có moves/startedAt nhưng Unity cũ bỏ qua khi khôi phục trận.

**Vì sao UI cũ chạy được online/LAN:** client ChessGame tự giữ history/captures/time. ChessLanController gửi MOVE, nhận FEN/notation từ server, cập nhật board và chuỗi cuối. Không cần API statistics riêng để hiển thị trong một phiên liên tục. Tuy nhiên dữ liệu local không khôi phục đúng sau reconnect/rollback; captures từ FEN là số quân thiếu so với đội hình ban đầu, sai khi promotion/ARAM. Tên ChessLanController gây nhầm: hiện dùng dịch vụ Spring cho đường mạng đang khảo sát. Node relay trong OnlineServer là legacy; không tuyên bố đã kiểm thử mọi đường LAN.

## Thay đổi Unity đã thực hiện (working tree, chưa commit)

### Model statistics

Mới `Assets/Scripts/Chess/Application/MatchStatistics.cs` (pure C#, namespace ChessButWeird.Application):
- Entry immutable: id, moveNumber, actor Team, text, isMove, pending, captured PieceKind nullable.
- Revision; HistoryComplete/CapturesComplete; confirmed move number.
- AddLocal/AddEvent/ReviseLastMove/RejectPending/Confirm/ObserveSnapshot/Restore.
- Captures theo sự kiện, không tính từ quân thiếu. TryClassicCapture dùng FEN trước nước đi + from/to, hỗ trợ en passant/promotion.

Mới `Assets/Scripts/Chess/Core/ChessGame.Statistics.cs`:
- Adapter statistics với ChessGame, tên người chơi, mode, network/check flags.
- Record local move/events; đồng bộ legacy lists từ ledger khi server thay thế/rollback.
- RestoreStatistics fallback REST moves, identity, fenAfter; có đánh dấu partial.
- ApplyStatisticsSnapshot phiên bản 1 từ server; kiểm tra id/team/piece enum, không nhận snapshot moveCount cũ hơn confirmed.
- Clock authority nhận elapsedSeconds + nội suy local; local game dùng logic elapsed/pause cũ.

Mới `Assets/Scripts/Chess/Backend/BackendMatchStatistics.cs` + thêm field `statistics` vào BackendMatchDto, BackendGameStartPayload, BackendMoveResultPayload, BackendGameStatePayload, BackendGameOverPayload.

Sửa ChessGame.cs:
- ResetStatistics ở begin/reset, RecordStatisticsMove ở normal move, revision khi phong cấp.
- Không gọi RebuildCapturedPiecesFromBoard khi ApplyFenState nữa (các hàm cũ vẫn còn dead code, nên dọn sau khi xác nhận không có call).
- MatchElapsedSeconds ưu tiên authority clock nếu có.
- Ghi event destroyed cho nạn nhân explosion.
- SetExternalLastMoveSummary đồng bộ ledger nhưng luồng server mới không gọi nó nữa. Cần xem xét loại bỏ giả định đảo currentTurn trong fallback này nếu vẫn có consumer mới.

Sửa ChessGame.Aram.cs: ghi sự kiện spawned/removed/relocated. Sửa AramBuffRuntime.Extended.cs Say để ghi event offline; không log private network buff.

Sửa ChessLanController.cs:
- RestoreStatistics(match) khi LoadActiveMatchCoroutine.
- ApplyStatisticsSnapshot ở GAME_START, GAME_STATE, GAME_OVER.
- ConfirmStatisticsMove thay SetLatestMoveText; giữ previous authoritative FEN trước cập nhật.
- RejectStatisticsPrediction khi lỗi request rollback.
- Cần tiếp tục xem các nhánh disconnect/send failure/malformed ARAM result và lifecycle khác (xem TODO).

### UI

`AnalysisBoardView.cs` đã viết lại, giữ class và public API Initialize/SetVisible/SetInteractionEnabled để không phá caller:
- Canvas runtime + TMP/LiberationSans, paper/ink theme; không còn texture lớn hoặc 3 nút Coming soon.
- Hai player strips, turn/check/pause/result, journal toggle/focus, elapsed/captures/history, Jump to latest.
- History mỗi hàng có số/phe rõ, không giả định trắng–đen luân phiên; không cắt 13 ký tự.
- Rich text tắt cho tên/notation tránh tên người chơi trở thành markup.
- Giữ interaction xem sau trận (scroll/toggle) khi caller khóa interaction gameplay.
- Preference journal/focus theo StatisticsMode bằng PlayerPrefs.

Mới `MatchHudStyle.cs`: helper Rect/Image/Outline/TMP/button. Font body Resources `Fonts & Materials/LiberationSans SDF` (asset có sẵn).

Sửa ChessTurnSelectionUI thêm GetMatchPlayerName.

Sửa AramAbilityView:
- Paper theme, tạo nút động thay 6 slot cố định, tối đa 4 cột, nhãn lý do/cooldown.
- Re-query action khi click, polling 0.1s. CHƯA đủ chống stale identity (TODO).
- Vẫn chỉ dùng runtime capability offline; không thay luật.

Sửa AramBuffRuntime.Extended.cs: AbilityAction detail Ready / owner turns / no tickets / turn limit / condition failed.
Sửa AramBuffDraftView: buff strip giấy ở bottom, giữ ShowPrivateHud giấu buff đối thủ; draft card flow giữ nguyên.

## Lỗi ảnh người dùng và sửa mới nhất — CHƯA KIỂM CHỨNG TRỰC QUAN

Ảnh cho thấy hai dải kem lớn top/bottom, font quá nhỏ (~12px trong Game View 1168×541), bàn cờ 3D/cosmetic bị bố cục không đẹp.

Nguyên nhân: bản UI đầu thay camera.rect sang vùng giữa nhưng không có background camera clear các vùng ngoài viewport. Canvas reference 1920×1080 làm text nhỏ khi scale xuống.

Sửa mới nhất (sau lần compile Unity thành công):
- CanvasScaler HUD đổi reference 1280×720; player bars 760×70, status y=-104, controls y=-148, journal y bắt đầu -180.
- Camera render toàn màn hình rect(0,0,1,1); tạo khoảng trống bằng FOV ổn định theo mode, không theo toggle journal: `2*atan(tan(originalFov/2)/safeFraction)`, .78 Classic / .62 ARAM.
- Restore rect/FOV khi HUD ẩn/destroy. Camera lifecycle này còn cần kiểm tra thật, nhất là domain reload khi Play Mode và camera ARAM rifle.
- Chưa có ảnh mới xác nhận đã đẹp/không che quân. Đây là thử nghiệm implementation, cần review và chỉnh tiếp, không phải final design.

## Backend LAN đã thay đổi

Mới:
- `model/dto/match/MatchStatisticsDTO.java`: version=1, moveCount, elapsedSeconds, historyComplete, capturesComplete, entries (id, moveNumber, actor, text, isMove, captured).
- `service/impl/MatchStatisticsProjector.java`: dựng ledger deterministic từ persisted ordered moves + FEN trước/sau; capture target/en passant, promotion không giả thành mất tốt; board effects còn lại ghi riêng (removed/transformed/spawned-like generic).

Sửa:
- MatchResponseDTO thêm statistics; GameMapper inject projector và đưa statistics vào REST detail/active/history.
- MatchServiceImpl inject projector; addModeState đưa snapshot vào GAME_START/MOVE_RESULT/GAME_STATE; gameOverPayload đưa snapshot terminal.
- Không thêm DB migration vì tận dụng history đã lưu. Snapshot hiện gửi full history, O(n) mỗi lần; engine Classic cũng đã replay history. Cần đánh giá payload/performance nếu history rất dài.
- Elapsed backend dùng Duration(startedAt, finishedAt hoặc LocalDateTime.now), gồm toàn bộ wall duration; server hiện không có pause accounting đáng tin. Cần ghi rõ semantics, không gọi nó clock thi đấu.
- Projector chỉ lấy public board transitions, không thêm buff/cooldown/seed vào statistics. Existing aramState payload vốn có riêng cả hai phía; scope này giữ UI private HUD, chưa audit bảo mật toàn giao thức.

## Kiểm thử đã chạy

1. Unity compile script:
   `./tools/verify-loading-compile.ps1 -UnityPath 'D:/Unity Editor/6000.4.7f1/Editor/Unity.exe'`
   PASS runtime-editor (137 source files), runtime-player (137), editor-tools (18) **trước** các chỉnh sửa AramAbilityView/buff strip/camera/font mới nhất. PHẢI chạy lại cho trạng thái cuối.
   Script chỉ compile, không chứng minh rendering/Play Mode.
2. `dotnet run --project Tests/Chess.Statistics.Tests/Chess.Statistics.Tests.csproj`: **12 checks PASS**. Project mới pure C#.
   Bao phủ promotion in-place, extra turns, destroyed != captured, rollback pending capture/history, confirm/dedup, partial snapshot, authoritative restore, en passant, promotion/capture promotion, malformed FEN, reset.
3. `dotnet run --project Tests/Chess.Domain.Tests/Chess.Domain.Tests.csproj`: nhiều checks pass rồi FAIL `Freestyle 2x2 leap`, Program.cs dòng 226. Chưa sửa domain rules/tests. Có vẻ drift sẵn giữa test cũ và luật Freestyle mới, nhưng chưa chạy baseline riêng để khẳng định chắc chắn. Không sửa luật ARAM ngoài phạm vi chỉ để làm test xanh.
4. Backend `./mvnw.cmd test -q`: suite ban đầu pass; sau thêm MatchStatisticsIntegrationTest cũng pass.
   Surefire tại handoff: **30 tests total, 0 failures/errors**: AramRules16 + AramWebSocket3 + Auth1 + ChessRules2 + MatchFlow4 + Statistics2 + Reward2.
   Statistics test cover capture, duplicate, reconnect REST/SYNC parity, outsider forbidden, terminal elapsed, ARAM recovery/public fields.
   Backend log tạm `statistics-test.log` untracked; đưa sang target hoặc xóa file log này sau khi giữ evidence cần thiết, không commit log.
5. Chưa test Java projector riêng cho promotion, en passant, explosion/castling hoặc JSON serialized key `isMove`; nên bổ sung tests có ý nghĩa.
6. Chưa chạy Unity 2-client với backend mới; chưa deploy backend live.

## Unity MCP / task fork

- Task gốc id: `01a0c45b-5a53-79d1-ad12-75508320d991`.
- Fork id: `01a10051-fbf5-7523-8166-ae70c4676719`, local, same-directory. Nó được giao CHỈ kiểm tra scene/console, không sửa source/scene/trạng thái match.
- Fork đã trả final: MCP port8091 kết nối được; scene ChessClassic loaded, không dirty, chỉ camera/light/volume/board (không có HUD/pieces runtime lúc đọc); console không error/warning; camera rect full, Game View1168×541. Không có công cụ capture Game View overlay trong MCP đang nối. Plugin khác `unity_mcp` báo Connection revoked. Fork không sửa gì.
- Task gốc get_scene_info trước đó timeout; functions cell24 còn một lời gọi pending ở lần cuối (không cần tiếp tục chờ cho task mới).
- Người dùng báo mở port8091, nhưng lần đọc settings cuối thấy port8090, timeout100, AutoStartServer=true, AllowBatchModeServer=true, AllowRemoteConnections=true. **Đây không phải thay đổi do task gốc thực hiện. Không tự ghi đè.** Xác nhận port hiện tại/connector bằng tool và hỏi người dùng nếu cần.
- Project đang mở Editor. Không tự thoát Play Mode/reset trận người dùng để tiện test. Có thể xin người dùng mở Play Mode khi cần, hoặc test scene isolated phù hợp.
- Đã đọc protocol MCP package ở `Library/PackageCache/com.gamelovers.mcp-unity@382a43a30f4d`; không gọi bridge bằng shell. Không bypass plugin Connection revoked.

## TODO quan trọng trước khi coi là xong

1. **Review UI actual và sửa lỗi ảnh** trên window1168×541, 1280×720,1080p,4:3/ultrawide. Xác minh camera không để dải trống, board không mất góc, chữ dễ đọc, overlay không che điểm chọn. Cân nhắc đổi chiến lược FOV nếu ảnh thật vẫn xấu. Không tự tuyên bố đạt chỉ vì compile.
2. **Bug đã nhận thấy cần sửa:** `SetTurn` gọi `SetVisible(true)` mỗi nước; view hiện gọi Layout và Layout đặt `lastRevision=-1`. Điều này khiến follow-scroll luôn về cuối dù người đang đọc nước cũ, triệt tiêu mục tiêu scroll preservation. Chỉ layout khi visibility/size/mode đổi, và phân biệt dirty layout vs reset lifecycle.
3. **Bug cần review ở AramAbilityView:** helper Label mới tạo rect cha + text con; Update đang chỉnh status.rectTransform (con) thay vì rect wrapper (cha). Dễ khiến status lệch/kích thước sai. Dùng text trực tiếp hoặc lưu status rect đúng.
4. **Stale ability click:** re-query rồi gọi lại cùng index có thể gọi skill khác nếu list đổi ngay trước click. Cần id/label ổn định + turn/phase token, không chỉ index.
5. ARAM text sizes còn dựa frame1920×1080; so sánh với main HUD reference1280×720. Buff strip bottom có thể đè action panel trong cửa sổ hẹp/nhiều actions. Ensure nút động >6 không bị che.
6. Focus hiện chủ yếu đóng journal, cần xem nó có giá trị rõ hơn toggle không; luôn giữ turn/check/pause/kỹ năng bắt buộc.
7. Status mạng chưa nối đầy đủ disconnect/reconnect vào HUD. Expose read-only connection state từ ChessLanController qua UI/game và hiển thị, không fabricate ping.
8. Snapshot restore: validate completeness/count/order tương ứng contract, unknown versions degrade explicit; stale/duplicate updates không reset scroll vô ích. Clock terminal/reconnect cần kiểm thử Unity thực tế.
9. Các nhánh reject/malformed ARAM/network send failure cần thống nhất ledger rollback. BeginServerMatch trên SYNC recovery cũ không có statistics phải đánh dấu unknown time/history thay vì count0 đầy đủ giả.
10. Local ARAM event ordering trước normal move (BeforeNormalMove có thể remove), captured vs sacrificed/destroyed semantics phải rõ. Không đếm every removed thành captured. King captures/remove icon handling cần xem.
11. Remove dead RebuildCapturedPiecesFromBoard/AppendMissing methods sau xác minh consumers. Legacy moveHistory cần giữ nếu BuildFen/other gameplay dùng count; tránh thay đổi luật vô ý.
12. Model TryClassicCapture currently infers diagonal empty pawn en-passant based adjacent pawn, không kiểm tra enPassant field; only called for accepted moves, nhưng test/validation tốt hơn nên check exact EP state.
13. Backend projector starts standard initial board; phù hợp server hiện start standard kể cả ARAM V1. Nếu hỗ trợ custom initial positions/new ARAM setup cần persisted initialFen/event ledger, không assume future modes.
14. Add meaningful Java tests promotion/EP/castling/explosion/gaps; verify serialized contract keys and consumer C# parse. Consider projector acting side matching actual moving piece validation.
15. Restore integration does not itself introduce any new online buff gameplay. Document online/offline capabilities chính xác.
16. Update documentation explaining old flow/new ownership, payload example, rollout order backend before client, limitations, tests. Existing September audit becomes historical reference.
17. Run compile again; tests; git diff/check across both repos. No commit/push/deploy unless user asks. Final user explanation Vietnamese with evidence/limitations and actual screenshot when possible.

## Working tree / tránh ghi đè việc của người dùng

- Unity modified code files trên + new .meta đã Unity tạo tự động.
- Schoolbell TMP.asset, QualitySettings.asset và McpUnitySettings.json đã bị thay đổi trong lúc Editor/user thao tác. Task này không chủ ý sửa ba file đó; không revert/commit lẫn vào feature mà chưa xem nguồn thay đổi.
- ASP.NET backend không sửa.
- Spring backend modified mapper, DTO, service; new projector/DTO/integration test; log untracked.
- Không có branch/worktree riêng mới cho implementation. Fork task dùng cùng directory.

## Tiếp tục có hiệu quả

Đọc git diff thực tế trước, không chỉ tin handoff. Ưu tiên chốt UI lỗi ảnh và các bug đã nêu, sau đó hợp nhất test/contract/network states. Kết thúc phải có code thật hoạt động cả Classic và ARAM trong capability hiện hữu, không chỉ mockup hoặc backend pass. Người dùng đã cho phép chỉnh code rộng trong phạm vi statistics và muốn được hỏi nhanh khi không rõ.
