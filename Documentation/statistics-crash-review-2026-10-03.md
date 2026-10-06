> Historical note (updated 2026-10-06): this document describes work before the .NET online migration. Spring/Node endpoints, classes and validation results below refer to the retired backend. Use [OnlineDotNetIntegration.md](OnlineDotNetIntegration.md) for the current implementation.

# Statistics HUD: rà soát crash ngày 03/10/2026

## Kết luận có bằng chứng

Unity 6000.4.7f1 crash lúc 13:27:16 (Asia/Saigon). Log trong `C:/Users/Admin/AppData/Local/Temp/Unity/Editor/Crashes/Crash_2026-10-03_062716304/Editor.log` ghi:

```
d3d12: failed to Close a command list (80070057).
d3d12: Device failed error (80070057).
d3d12: GfxDevice was not out of Local memory
d3d12: GfxDevice was not out of Non-Local memory
d3d12: Unrecoverable GPU device error!
```

Native stack: `CheckDeviceStatus` → `D3D12CommandList::PrepareExecute` → `GfxTaskExecutorD3D12`. Đây là lỗi fatal ở lớp graphics, không phải một exception C# statistics. Tuy nhiên log không chứng minh driver, shader HUD, capture hoặc loading là tác nhân gốc duy nhất. Không kết luận “không liên quan code mới”.

Ngay trước lỗi, helper `MatchHudAudit.StartLocalPreview` bắt đầu `LoadingManager.StartMatch`, sau đó `Inspect` chạy khi `started=False`. Helper lên lịch capture bằng `EditorApplication.delayCall` thay vì chờ kết thúc frame. Đây là sai sót trong quy trình kiểm tra do agent tạo, cần gỡ khỏi Editor đang dùng. [Unity yêu cầu capture sau khi frame hoàn tất](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/screencapture/capturescreenshotastexture).

Log sau khi mở lại còn báo cổng 8090 đã được dùng khi MCP khởi động lại; người dùng chuyển sang 8091. Lỗi cổng là vấn đề kết nối riêng, chưa có bằng chứng gây GPU crash. Không tự sửa settings MCP, graphics API hoặc driver.

## Thay đổi quy trình

- Dừng gọi Play/Stop, StartMatch và screenshot tự động trong Editor của người dùng.
- Đã gỡ `Assets/Editor/MatchHudAudit.cs` và `.meta` sau khi người dùng xác nhận Stop Play Mode. Không còn menu preview/capture hoặc startup hook do helper này thêm vào project thật.
- Đọc scene/console qua MCP còn hoạt động chỉ khi cần, không gọi các menu có side effect để lấy ảnh.
- Không sửa script rồi kiểm chứng ngay bằng object cũ còn trong Play Mode. Hot reload đã giữ các rect UI cũ và làm ảnh preview sai.
- Nếu cần render tự động về sau, dùng project kiểm thử riêng, tải hoàn tất, chờ end-of-frame và timeout; chỉ launch sau khi quy trình đó được chuẩn bị và kiểm tra. Các phép kiểm tra bố cục/data fixture phải ghi rõ không thay thế gameplay/server test.

## Những gì đã xác minh và chưa xác minh

- Unity source compile Editor, Player, Editor tools: PASS sau khi gỡ helper (137/137/18 source files). Compile bằng Roslyn riêng, không launch Editor của game.
- Pure C# statistics: 14 checks PASS, gồm rollback, repeated snapshot không tăng revision, phong cấp, en passant và lượt thêm.
- Spring backend: 36 tests, 0 failures/errors; gồm 6 projector tests mới và 2 statistics integration tests.
- Domain suite: lỗi `Freestyle 2x2 leap` ở Program.cs:226 xuất hiện cả working tree và bản HEAD trích riêng. Đây là baseline failure đã có.
- Chụp Game View đã xác định name/status bị TMP overflow vì rect thấp hơn chiều cao dòng. Source đã sửa rect; chưa có cold-run screenshot của bản cuối.
- Preview đầu không đi qua content loader đầy đủ. Preview sau giữ object từ hot reload. Cả hai không phải bằng chứng rằng cosmetic hoặc bàn cờ của game chính bị hỏng.
- Helper cũ không chạy thành công các kiểm tra tương tác; không lấy kết quả từ helper đó làm bằng chứng.
- Bộ kiểm tra mới `Tests/Chess.Hud.Headless` chạy trong project riêng ở `Logs/hud-headless/<timestamp>`, bằng `-batchmode -nographics`; log xác nhận `NullGfxDevice`. Không có MCP, account, gameplay, scene game hoặc code screenshot. Ghi hash source UI đã copy và dùng RectTransform/Canvas/TMP thực với data fixture riêng. Không sửa Play Mode/settings của Editor game.
- Bộ kiểm tra này đã tìm lỗi nút ARAM hai dòng bị cắt (44 đơn vị chiều cao cho font 20). Source đã nới khung chữ. Nó cũng kiểm tra journal chừa vùng buffs/actions, 5 tỷ lệ màn hình, 0–10 actions, font metrics, scroll offset, Focus/journal không đổi FOV, stale action ID/context, giấu buff đối thủ và camera restore. Đây là kiểm tra layout/tương tác, không thay thế ảnh render thật hay ARAM gameplay.
- Bản cuối PASS 1.626 assertions, gồm adapter `ChessGame.Statistics.cs` production cho validation/dedupe/capture/recovery/timezone và camera replacement. Report: `Logs/hud-headless/20261003-140048/audit-result.txt`.
- Chưa xác nhận UI cuối, gameplay 2 client với backend mới hoặc ARAM runtime. Chưa merge, push hoặc deploy.

## Phạm vi code đang giữ

Giữ các thay đổi statistics/HUD: rect camera toàn màn hình, font scale 1280×720, text rect đủ chiều cao, journal không layout lại mỗi lượt, giữ offset khi đang đọc lịch sử, Focus giữ status, ARAM action ID/context và buff HUD tránh đè action panel, snapshot validation/dedupe, rollback dự đoán khi gửi/kết nối lỗi. Journal có tab History/Captures và tính chiều cao theo khoảng ARAM thực tế; không chồng cả summary vào vùng lịch sử nhỏ. Không sửa gameplay Freestyle để làm test xanh.

Các file settings/font đã có thay đổi từ Editor hoặc người dùng được giữ nguyên, không tự revert. Bản handoff gốc vẫn là lịch sử; tài liệu này bổ sung các kết quả mới và giới hạn sau crash.

## Cập nhật sau yêu cầu HUD/buff mới

Kết quả mới nằm trong [aram-update-2026-10-03.md](aram-update-2026-10-03.md): compile 141/141/18, 105 Domain checks, 15 statistics checks, 2.223 HUD assertions và 295 ARAM assertions. Runtime ARAM đã được kiểm tra trong project riêng dùng NullGfxDevice, có source hashes, không chạy game chính hay screenshot. Không tạo lại helper preview/capture trong Assets. Lỗi Freestyle 2×2 cũ không còn là luật local theo tài liệu Word mới; hành vi V1 online vẫn có test riêng. Chưa có cold-run raster QA hoặc hai-client network test.
