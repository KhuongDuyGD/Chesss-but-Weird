# Kiểm kê văn bản trong gameplay chính thức — chuẩn bị đa ngôn ngữ

Ngày chụp trạng thái: **16/11/2026 (Asia/Saigon)**. Căn cứ: working tree hiện tại, HEAD `6db3c41`; gồm cả thay đổi chưa commit. Tài liệu chỉ kiểm kê, **không sửa câu chữ, không cài hệ thống dịch và không gắn font Việt vào game**.

<a id="1-pham-vi-va-cach-doc"></a>

## 1. Phạm vi và cách đọc

Chỉ tính nội dung có đường đi vào bản chơi: Boot → MainMenu → menu chức năng/lobby → ChessMatch, và ChessClassic đang bật trong Build Settings/là đường dự phòng. Danh sách màn lấy từ [EditorBuildSettings.asset](<D:/Game_Project/Chesss-but-Weird/ProjectSettings/EditorBuildSettings.asset:7>). Các cảnh review nghệ thuật, GachaRevealDemo, CinematicDemo/CinematicProof, nhánh ROYAL_ASCENSION_DEMO/UNITY_EDITOR, test, Mirror examples, TMP examples, Library, docs và hướng dẫn dự án không nằm trong danh sách dịch.

Đã quét cú pháp **244 file C# do dự án sở hữu** trong Assets/_Project/Scripts và domain runtime, đối chiếu component của 4 scene được bật + 2 prefab Shop, lời gọi tạo UI, Resources, JSON, ScriptableObject và font. Bộ trích tìm thấy **908 biểu thức tạo/gán/truyền nội dung UI tại 54 file runtime có liên hệ với luồng này**; đây là số vị trí mã, **không phải số câu dịch duy nhất**. Mỗi vị trí và biểu thức gốc nằm ở phụ lục A. Các bộ câu/data có phụ lục riêng để tránh sót nội dung được truyền bằng biến.

Đường đi được đối chiếu từ scene/component và tham chiếu lớp; tham chiếu tĩnh không khẳng định mọi nhánh đều hiện trong mọi trận. Các dòng dưới đây ghi rõ điều kiện như online/local, lỗi/fallback, guest, endgame, mode và server. Đây là kiểm kê nguồn tĩnh, chưa phải kết quả chạy qua toàn bộ màn hình trong bản build.

Quy ước: **dịch** = copy dành cho người chơi; **dữ liệu động** = tên người chơi/item/server, số và tọa độ; **giữ nguyên** = ID, enum lưu/truyền mạng, API key, đường dẫn, ký hiệu chuẩn; **có lớp dịch** = đang đi qua SettingsLocalization chứ không có nghĩa đã có bản Việt đầy đủ. Chuỗi Object/GameObject, nhãn Inspector, shader, log và exception nội bộ bị đổi sang thông báo chung không được coi là câu cần dịch.

### Tra cứu nhanh

- [2. Bản đồ các màn và nguồn chữ](#2-ban-do-cac-vi-tri-hien-chu)
- [3. Lớp dịch hiện có](#3-lop-dich-dang-co-va-khoang-trong)
- [4. Dữ liệu server / ID](#4-du-lieu-server-va-chuoi-khong-duoc-dich-truc-tiep)
- [5. Font, ký tự và layout](#5-font-ky-tu-va-rang-buoc-trinh-bay)
- [A. Vị trí tạo/cập nhật UI](#a-cac-vi-tri-tao-va-cap-nhat-ui)
- [B. Tên, mô tả, options Settings](#b-settings-dinh-nghia-va-options)
- [C. 35 buff CHAOS](#c-35-buff-chaos)
- [D. Key và bản dịch hiện có](#d-cac-key-dich-dang-co)
- [E. Hội thoại bot và lỗi người chơi](#e-hoi-thoai-bot-va-thong-bao-dung-chung)
- [F. Prefab, asset, TextAsset](#f-van-ban-trong-prefab-asset-va-textasset)

<a id="2-ban-do-cac-vi-tri-hien-chu"></a>

## 2. Bản đồ các vị trí hiện chữ

### 2.1. Khởi động và màn tải

Thông báo chuẩn bị bàn cờ, artwork, tải quân, tiến độ %, Retry, Back to menu. LoadingUI dùng UnityEngine.UI.Text; các bước tải truyền câu vào Report().

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [GameBootstrap.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/GameBootstrap.cs:9>) | <code>Start</code> |
| [SessionLoadingController.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/SessionLoadingController.cs:49>) | <code>Prepare</code> |
| [LoadingManager.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingManager.cs:66>) | <code>LoadMatch</code> |
| [LoadingUI.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingUI.cs:22>) | <code>Create</code> |

### 2.2. Đăng nhập, đăng ký, chơi khách

Nhãn trường email/username/password, placeholder, nút đăng nhập/đăng ký/chơi khách, xác nhận mật khẩu, đang gửi, thành công/thất bại; popup yêu cầu tài khoản. Username/email/password là dữ liệu nhập, không phải bản dịch.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [MainMenuAuthUI.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:48>) | <code>BuildCanvas</code> |
| [AuthController.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthController.cs:48>) | <code>Submit</code> |
| [AuthNotificationView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthNotificationView.cs:30>) | <code>Show</code> |
| [GuestAccessWarning.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GuestAccessWarning.cs:14>) | <code>Show</code> |
| [ChessTurnSelectionUI.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:423>) | <code>RequireAccountAccess</code> |

### 2.3. Menu đầu và Credits

Tagline, EARLY ACCESS, START/SETTINGS/CREDITS/LOG OUT, chú thích trang, phiên bản, overlay Credits. Logo là ảnh; nội dung Credits được đọc từ TextAsset trong Resources.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [MainMenuPresentation.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:22>) | <code>Build</code> |
| [MainMenuLogoOriginal.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuLogoOriginal.cs:6>) | <code>ResourcePath</code> |

### 2.4. Hub, chọn mode, chọn bên

YOUR NEXT MOVE, PLAY, GACHA, INVENTORY, PROFILE, SHOP, MAILBOX, NEWS; các mục PVE/BOT RUSH, CHAOS, CLASSIC, CUSTOM, Ranked/Normal, Coming soon; mô tả mode và WHITE/BLACK/PLAY AS. Các menu phụ cũ vẫn có nhánh runtime.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [ModeSelectionPresentation.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:27>) | <code>Build</code> |
| [PlayModeSelectionView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:11>) | <code>PlayModeNames</code> |
| [HandDrawnMenuView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:79>) | <code>BuildLocalModeScreen</code> |

### 2.5. Lobby và trang bị trước trận

START MATCH/FIND MATCH, chọn bộ cờ/bàn/hiệu ứng/background, đổi từng quân, owned/equipped, trạng thái lưu trang bị, hàng chờ và hướng dẫn chọn màu. Tên vật phẩm lấy từ server hoặc CosmeticCatalog; tên slot hiện có chỗ dùng enum.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [MatchLoadoutController.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:66>) | <code>ConfigureLobby</code> |
| [CosmeticThumbnailUI.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/CosmeticThumbnailUI.cs:11>) | <code>Find</code> |
| [PieceSkinCatalog.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Pieces/PieceSkinCatalog.cs:45>) | <code>UseCatalog</code> |

### 2.6. Chọn bot và chế độ luyện tập

12 nhân vật, Level, Elo ước lượng, truyện/Style/RatingGuidance, Practice/Challenge, Hints/Undo, thống kê thắng thua hòa và điều kiện thưởng. Tên riêng của bot cần chính sách thống nhất; số Elo và ID độ khó giữ nguyên.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [BotRosterView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:9>) | <code>Build</code> |
| [StockfishDifficulty.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/StockfishDifficulty.cs:65>) | <code>StockfishDifficultyProfiles</code> |
| [BotPracticeView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotPracticeView.cs:21>) | <code>Initialize</code> |

### 2.7. Hội thoại và hỗ trợ bot trong trận

Bong bóng mở đầu/thắng/thua/hòa/phản hồi nước đi; câu đánh giá và lời huấn luyện. Lời gợi ý ghép tên quân và tọa độ. Thinking/Reconnecting/Connection paused, retry và âm bật/tắt bot cũng thuộc UI.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [BotDialogue.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:8>) | <code>Openings</code> |
| [BotCompanionView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotCompanionView.cs:39>) | <code>Initialize</code> |
| [StockfishBotController.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/StockfishBotController.cs:180>) | <code>RequestHint</code> |

### 2.8. Multiplayer: phòng, danh sách, hàng chờ

Tạo/join/refresh/leave/copy code, host/guest, chọn mode/vùng/clock, danh sách phòng, tìm trận/accept/decline/countdown, trạng thái kết nối và trận gần đây. Địa chỉ IP, mã phòng và username không dịch.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [ChessLanController.Menu.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:50>) | <code>Rebuild</code> |
| [ChessLanController.Flow.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Flow.cs:35>) | <code>OpenRooms</code> |
| [ChessLanController.Online.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Online.cs:37>) | <code>MatchConnectionState</code> |
| [ChessLanController.Hud.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:22>) | <code>OnlineMatchHud</code> |
| [MatchFoundConfirmationView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchFoundConfirmationView.cs:11>) | <code>Build</code> |

### 2.9. Màn đối đầu và xác nhận tải trận

WHITE/BLACK SIDE, VS, YOU, tên người chơi, rank/rating/placement, tên trang bị, Ready/Preparing/Reconnecting/Match cancelled. Tên người chơi và mã vật phẩm là dữ liệu động.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [MatchVersusView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:13>) | <code>MatchVersusView</code> |
| [RankCardView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/RankCardView.cs:53>) | <code>TierName</code> |

### 2.10. HUD, tên quân, nhật ký

Turn/Check/Pause/Menu, WHITE/BLACK hoặc W/B, No moves yet, thời gian, số nước, captures, buff hidden/no buff, hướng dẫn camera/journal/focus, tooltip icon. Tên quân đang ghép từ PieceTeam/PieceType. Nhật ký có cả ký hiệu cờ và câu sự kiện CHAOS.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [AnalysisBoardView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:43>) | <code>Initialize</code> |
| [PieceHoverTooltip.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PieceHoverTooltip.cs:43>) | <code>LateUpdate</code> |
| [MatchHudTooltip.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchHudTooltip.cs:24>) | <code>Show</code> |
| [ChessGame.Statistics.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/ChessGame.Statistics.cs:29>) | <code>StatisticsMode</code> |
| [ChessGame.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/ChessGame.cs:2667>) | <code>BuildMoveSummary</code> |
| [ChessGame.Aram.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/ChessGame.Aram.cs:59>) | <code>AramSpawn</code> |

### 2.11. Phong cấp

Tiêu đề chọn quân phong cấp, tên Queen/Rook/Bishop/Knight và thông tin bên đang chọn. Đây là nhãn hiển thị từ enum, không đổi giá trị enum gửi vào luật chơi.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [PromotionMenuView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PromotionMenuView.cs:17>) | <code>Initialize</code> |

### 2.12. Tạm dừng và menu trận

PAUSED/MATCH MENU, Resume/Restart/Settings/Match Info/Leave, ARE YOU SURE?, xác nhận restart/rời trận, lời nhắc online clock tiếp tục, popup thông tin trận. Các nhánh local/online có câu khác nhau.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [ChessPauseMenu.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:40>) | <code>BuildUi</code> |
| [SettingsPrompt.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsPrompt.cs:14>) | <code>Show</code> |
| [ChessLanController.PauseExit.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.PauseExit.cs:14>) | <code>LeavePauseSessionAsync</code> |

### 2.13. Kết thúc, xem lại bàn cờ, chia sẻ

YOU WIN/LOSE/STALEMATE/DRAW, MATCH COMPLETE, lý do kết thúc, players, TIME PLAYED, PLAY AGAIN/REQUEST REMATCH, nút xem bàn cờ/menu/share. Khi xem lại: tooltip cửa đỏ Leave to lobby, Share result, Returning to lobby/copy error. Chia sẻ chỉ bot và multiplayer thường; CHAOS không có share.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [ResultMenuView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:24>) | <code>Initialize</code> |
| [MatchResultSharing.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchResultSharing.cs:9>) | <code>BuildText</code> |
| [PlayerNotificationText.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:95>) | <code>MatchEndReason</code> |

### 2.14. CHAOS: thư viện buff và draft

35 buff với tên đầy đủ, tên ngắn, description, summary; tier Silver/Gold/Diamond/Legendary, thẻ chọn buff, quyền riêng tư, thời gian chọn, nút mô tả ngắn/đầy đủ. Local/practice, online v2 và online v3 có nguồn rule text khác nhau.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [AramBuffDefinition.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:44>) | <code>DisplayName</code> |
| [AramBuffDraftView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:82>) | <code>EnsureBuilt</code> |

### 2.15. CHAOS: hành động, mục tiêu, tiến độ

USE BUFF, Expand/Collapse, label/detail của action, hướng dẫn chọn quân/ô/vùng, cooldown/uses/quest/tickets, cảnh báo chọn sai, toast, rifle/ALT/aim/fire, xác nhận formation. Nhiều câu ghép team, quân, tọa độ, số lượt. Các marker CMD/PICK/MINE và milestone cũng cần chính sách hiển thị.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [AramAbilityView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramAbilityView.cs:31>) | <code>Initialize</code> |
| [AramBuffRuntime.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffRuntime.cs:302>) | <code>ShowCurrentTargetPrompt</code> |
| [AramBuffRuntime.Extended.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffRuntime.Extended.cs:359>) | <code>AbilityStatus</code> |
| [AramBuffRuntime.DocumentRules.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffRuntime.DocumentRules.cs:46>) | <code>SelectQueenReplacement</code> |
| [AramBuffRuntime.Progress.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffRuntime.Progress.cs:31>) | <code>BuffProgress</code> |
| [AramBuffRuntime.Rifle.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffRuntime.Rifle.cs:6>) | <code>EnterRifle</code> |
| [AramBuffRuntime.StrategyVisuals.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffRuntime.StrategyVisuals.cs:42>) | <code>RefreshStrategyVisuals</code> |
| [AramBuffPieceMarker.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffPieceMarker.cs:19>) | <code>Configure</code> |

### 2.16. CHAOS online và chữ nổi VFX

OnlineAramPresenter tạo text/action từ snapshot; bản v3 có nhánh riêng. BuffTacticalHud, BuffStrategySequence, PieceStatusVisual và CapturedPieceDisplay hiện chữ/số nổi, extra move, objective, điểm và số lượng. Outcome/action IDs là dữ liệu luật, không phải chuỗi dịch.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [OnlineAramPresenter.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:156>) | <code>BuildPresentation</code> |
| [OnlineAramPresenter.Chaos35.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:41>) | <code>BuildChaos35Presentation</code> |
| [BuffTacticalHud.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Presentation/BuffVfx/BuffTacticalHud.cs:36>) | <code>Sync</code> |
| [BuffStrategySequence.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Presentation/BuffVfx/BuffStrategySequence.cs:42>) | <code>Begin</code> |
| [PieceStatusVisual.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Presentation/BuffVfx/PieceStatusVisual.cs:45>) | <code>Apply</code> |
| [CapturedPieceDisplay.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Presentation/CapturedPieceDisplay.cs:72>) | <code>Create</code> |

### 2.17. Shop chính thức

Header/subtitle, tab/category, search placeholder, featured/owned/equipped/collection filters, chi tiết sản phẩm, bundle/giá/discount, CONFIRM/CANCEL, pending purchase/retry và kết quả mua/trang bị. TOP UP có teaser Coming soon trong UI thật; điều đó không chứng minh đã có mua tiền thật.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [ShopMenuController.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:100>) | <code>Render</code> |
| [ShopMenuController.Layout.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:26>) | <code>EnsureAdditionalControls</code> |
| [ShopProductCard.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopProductCard.cs:38>) | <code>Bind</code> |
| [ShopPurchasePolicy.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopPurchasePolicy.cs:38>) | <code>ErrorMessage</code> |
| [ShopPresentationCatalog.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopPresentationCatalog.cs:9>) | <code>title</code> |

### 2.18. Inventory

Tiêu đề collection, tab loại/vai trò quân/bên, tìm kiếm, tên item/rarity, owned/equipped/default, hướng dẫn equip, trạng thái tải/lưu/thất bại. Có tên hiệu ứng built-in và fallback tên/code/id do API trả về.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [InventoryMenuController.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:63>) | <code>Build</code> |

### 2.19. Gacha và hiệu ứng nhận thưởng chính thức

Tên banner, cost/currency, rates/pity, summon 1/10, history, nút xem lại reward, duplicate/bonus/rarity, skip/continue/done; reward name từ dữ liệu đã xác nhận. Model loading/artwork unavailable cũng có thể hiện trên thẻ. Chỉ GachaSummonRevealController đang được nối vào menu, loại các demo cinematic.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [GachaMenuController.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:65>) | <code>Initialize</code> |
| [GachaMenuController.Layout.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:17>) | <code>BuildMainScreen</code> |
| [GachaSummonRevealController.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaSummonRevealController.cs:53>) | <code>BuildOverlay</code> |
| [CosmeticModelPreview.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/CosmeticModelPreview.cs:28>) | <code>Show</code> |
| [CommittedCosmeticMetadata.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Cosmetics/CommittedCosmeticMetadata.cs:6>) | <code>ForSlot</code> |

### 2.20. Hồ sơ người chơi và rank

Player notebook, scorecard, wallet, email/player ID/member since, trang bị và refresh status; tab rank/rating/placement. Dữ liệu tài khoản/ID/email không dịch; tier.displayName là tên từ cấu hình server. Một vài fallback hiện item ID hoặc tier ID thẳng lên UI.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [PlayerProfileMenuController.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:30>) | <code>Build</code> |
| [RankCardView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/RankCardView.cs:33>) | <code>Bind</code> |

### 2.21. Mailbox và News trong game

MAILBOX/NEWS, unread/all/read/claim, reward/currency/expiry, empty/error/offline/login; title/subject/body/rewards của thư từ API. News đọc JSON en-US rồi lọc Published/quyền/ngày. Credits và News trong Resources là nội dung sản phẩm được load lúc chơi, không phải docs dự án.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [CommunityMenuController.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:40>) | <code>Build</code> |
| [CommunityServices.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityServices.cs:27>) | <code>CommunityContent</code> |
| [CommunityModels.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityModels.cs:33>) | <code>PublicArticles</code> |
| [CommunityTypography.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityTypography.cs:5>) | <code>Apply</code> |

### 2.22. Settings và popup liên quan

6 tab General/Gameplay/Graphics/Audio/Controls/Accessibility; tên/mô tả/options của từng setting, ON/OFF, reset, confirm/revert display 15 giây, rebind phím, lỗi trùng phím. Lớp dịch đã có nhưng các popup/display rows/status còn nhiều câu ghi trực tiếp.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [SettingsMenuController.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:48>) | <code>BuildShell</code> |
| [SettingsRegistry.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:12>) | <code>Create</code> |
| [SettingsLanguages.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsLanguages.cs:15>) | <code>All</code> |
| [SettingsLocalization.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsLocalization.cs:12>) | <code>Text</code> |

### 2.23. Âm nhạc và thông báo dùng chung

Nhạc menu có tên gói, tooltip đổi/mute; tên tác phẩm/nhãn riêng giữ theo chính sách. PlayerNotificationText tạo câu lỗi dành cho người chơi; các mã server và alias để nhận dạng lỗi giữ nguyên. ApiClient đang gửi Accept-Language: en cố định.

| Nguồn | Điểm điều hướng trong mã |
| --- | --- |
| [MenuMusicWidget.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MenuMusicWidget.cs:20>) | <code>Build</code> |
| [GameMusicManager.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/GameMusicManager.cs:127>) | <code>GetPackDisplayName</code> |
| [PlayerNotificationText.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:11>) | <code>FromException</code> |
| [ApiClient.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/ApiClient.cs:127>) | <code>SendOnceAsync</code> |

### 2.24. Các nhánh dự phòng còn thuộc runtime

[ChessTurnSelectionUI.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:632>) còn vẽ IMGUI khi hand-drawn assets chưa sẵn sàng: title, chọn lượt, CHECK, You played, kết quả và Restart. [HandDrawnMenuView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:81>) còn có menu dùng ảnh cho LAN/Multiplayer. Đây là fallback đang có trong code; không coi là menu demo, cũng không mặc định là màn người chơi đi qua khi mọi assets đã tải đủ.

<a id="3-lop-dich-dang-co-va-khoang-trong"></a>

## 3. Lớp dịch đang có và khoảng trống

- [SettingsLocalization.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsLocalization.cs:12>) tải `Resources/Localization/Settings_<code>.json`, cache theo mã ngôn ngữ, tra `key/text/source`; thiếu key hoặc `source` không khớp câu English hiện tại thì dùng English gốc. Không có bộ dịch tự động toàn dự án.
- Hiện có **72 entry English** và **36 entry Vietnamese**, tất cả key trong hai JSON này bắt đầu bằng `buff.`. English có **8 source guard**; Vietnamese có **0**. Phụ lục D ghi nguyên giá trị; entry mang nhãn vi có thể vẫn là tên tiếng Anh có chủ ý.
- Settings gọi các key `settings.*`, `tab.<category>`, `<settingId>.name`, `<settingId>.description`, `<settingId>.option.<index>`, `common.on/off`; nhưng hai JSON hiện tại không có những key Settings này. Vì vậy việc đổi ngôn ngữ chưa tạo thành một bản Settings tiếng Việt đầy đủ.
- [SettingsLanguages.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsLanguages.cs:16>) chỉ có `en → English` và `vi → Vietnamese (in development)`, ID ngôn ngữ ổn định 0/1. [SettingsMenuController.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:57>) rebuild/refresh Settings khi language đổi; không có cơ chế chung đã nối để tất cả menu, bubble, HUD và nội dung server đang mở cùng repaint theo locale.
- Buff đi qua `DisplayName/ShortName/Description/Summary` của [AramBuffDefinition.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:44>). Vi chỉ có name/shortName cho một phần thư viện; phần mô tả/tóm tắt còn fallback. Online v2 dùng mảng mô tả riêng và chỉ dịch phần tên trước dấu `:` qua BuffText. Online v3 dùng built-in hiện tại rồi có thể nhận `buffDefinitions` từ server; không thay câu luật server khác phiên bản bằng một bản dịch rule local không tương ứng.
- [PlayModeSelectionView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:21>) chỉ thay chữ ARAM/Aram thành CHAOS/Chaos; đây là chuẩn hóa tên mode, không phải dịch ngôn ngữ.
- [ApiClient.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/ApiClient.cs:138>) gửi `Accept-Language: en` cố định. News còn tải `Community/news.en-US` và validator yêu cầu locale `en-US`. Đổi UserSettings.LanguageCode hiện chưa đổi các nguồn đó.

<a id="4-du-lieu-server-va-chuoi-khong-duoc-dich-truc-tiep"></a>

## 4. Dữ liệu server và chuỗi không được dịch trực tiếp

| Bề mặt | Nguồn đang dùng | Ranh giới khi tách bản dịch |
| --- | --- | --- |
| Shop / Inventory / Loadout | offer.name/description, item.name/code/type/rarity, CosmeticCatalog.displayName | Dịch label và metadata dành cho người chơi theo ID ổn định; giữ itemId, unityAssetKey, code/type payload. Fallback code/id đang có thể hiện thật. |
| Gacha | banner.name, itemName/reward name, committed metadata, currency, rarity và lịch sử từ API | Tên/description cần catalog locale hoặc dữ liệu theo locale. Không dịch requestId, bannerCode, reward slot, rarity/currency gửi mạng. |
| Rank / VS | RankConfig tier.displayName và tierId; player displayName/username | Tên rank cần map hiển thị; tierId và rating giữ nguyên. Không dịch tên người chơi. |
| Mailbox | CommunityMail subject/body/rewards từ API | Cần text được dịch theo mail/templateId hoặc server locale; không tự sửa tên tài khoản hay reward ID. |
| News | JSON en-US + bộ lọc PublicArticles | Chỉ nội dung Published, đủ quyền và đúng thời gian là ứng viên trong gameplay; không đưa bài PendingRights vào danh sách dịch đang phát hành. |
| CHAOS online | buffDefinitions, side/team, effects, commands, visualHud/Text | Dịch presentation. Giữ rulesVersion, buffId, action kind, phase, outcome, team trong snapshot và tọa độ. Nội dung động không thể liệt kê hết chỉ từ repo. |
| Lỗi online | PlayerNotificationText.FromServer/FromException và các status local | Dịch câu trả về. Giữ errorCode, alias Normalize/Contains và HTTP status để mapping lỗi tiếp tục hoạt động. |
| Kết quả / clipboard | MatchResultSharing.BuildText + StatisticsMode/DrawReason | Template đủ câu với placeholders; giữ score 1-0/0-1/1/2-1/2, tên người chơi và thời gian. |
| Ký hiệu bàn cờ | a–h, 1–8, N/B/R/Q/K, O-O/O-O-O, x, =, +, #, UCI/FEN | Là quy ước cờ/định dạng máy; không đổi luật/serialization để dịch tên quân. Tên quân trên tooltip dùng map hiển thị riêng. |

### Các chuỗi vừa làm chữ vừa bị code so sánh

- [BuffTacticalHud.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Presentation/BuffVfx/BuffTacticalHud.cs:44>) so sánh `s.Text == "Recruitment ready"` để điều khiển hiệu ứng. Tách trạng thái/ID khỏi text trước khi đổi câu này.
- [GachaSummonRevealController.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaSummonRevealController.cs:216>) dùng câu trạng thái của CosmeticModelPreview làm điều kiện chờ model. Cần tách key trạng thái khỏi bản dịch.
- [AramAbilityView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramAbilityView.cs:16>) mặc định `AbilityAction.Id = id ?? label`; việc dịch label có thể ảnh hưởng định danh/độ ổn định của action nếu nơi tạo chưa truyền id riêng.
- HUD so sánh connection state với `"Connected"`; nhiều nơi nối `enum.ToString()` hoặc `tierId` lên màn hình. Giữ giá trị điều khiển, thêm lớp tên hiển thị thay vì đổi enum/chuỗi giao thức.
- `AbilityContext`, `AbilityPresentationContext`, `draftFingerprint`, repetition keys, room/command fingerprint là chuỗi điều khiển nội bộ. Không lấy các chuỗi đó làm bản dịch chỉ vì chúng được ghép bằng string.

<a id="5-font-ky-tu-va-rang-buoc-trinh-bay"></a>

## 5. Font, ký tự và ràng buộc trình bày

### 5.1. Font thực tế và font Việt đã duyệt

Điểm chọn font chung là [ChessFontCatalog.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/UI/ChessFontCatalog.cs:14>) và [Assets/Resources/Chess/ChessFontCatalog.asset](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Chess/ChessFontCatalog.asset>). Runtime Font và TMP Font hiện vẫn tham chiếu Schoolbell gốc. Font TMP hiện serialize `m_AtlasPopulationMode: 1`, multi-atlas bật, kích thước 1024×1024, `m_CharacterTable: []`, fallback table rỗng; không suy ra khả năng hiển thị đầy đủ tiếng Việt từ việc atlas tạo động.

Trong **146 ký tự** nguyên âm/thanh điệu và Đ/đ cần cho tiếng Việt, cmap Schoolbell gốc thiếu **102**, font đã duyệt thiếu **0**. Đây là kiểm tra coverage nguồn font, không phải chứng nhận TMP trong build. Bản đã duyệt ở [ArtSource/Typography/Vietnamese/ChessWeirdViet-Regular.ttf](<D:/Game_Project/Chesss-but-Weird/ArtSource/Typography/Vietnamese/ChessWeirdViet-Regular.ttf>); bộ ký tự chuẩn bị atlas ở [ArtSource/Typography/Vietnamese/Vietnamese-CharacterSet.txt](<D:/Game_Project/Chesss-but-Weird/ArtSource/Typography/Vietnamese/Vietnamese-CharacterSet.txt>). Hai file nằm ngoài Assets và chưa được game sử dụng.

Các đường hiển thị cần nhớ: SketchbookUI/MatchHudStyle/Auth/Community dùng TMP qua catalog; LoadingUI và một số menu con dùng UnityEngine.UI.Text qua RuntimeFont; GuestAccessWarning dùng LegacyRuntime.ttf trực tiếp; IMGUI fallback dùng GUI.skin. Đổi chỉ một TMP asset sẽ chưa phủ mọi đường render.

### 5.2. Bộ ký tự và dấu đang cần để trình bày

<code>a à á ả ã ạ / A À Á Ả Ã Ạ</code>
<code>ă ằ ắ ẳ ẵ ặ / Ă Ằ Ắ Ẳ Ẵ Ặ</code>
<code>â ầ ấ ẩ ẫ ậ / Â Ầ Ấ Ẩ Ẫ Ậ</code>
<code>e è é ẻ ẽ ẹ / E È É Ẻ Ẽ Ẹ</code>
<code>ê ề ế ể ễ ệ / Ê Ề Ế Ể Ễ Ệ</code>
<code>i ì í ỉ ĩ ị / I Ì Í Ỉ Ĩ Ị</code>
<code>o ò ó ỏ õ ọ / O Ò Ó Ỏ Õ Ọ</code>
<code>ô ồ ố ổ ỗ ộ / Ô Ồ Ố Ổ Ỗ Ộ</code>
<code>ơ ờ ớ ở ỡ ợ / Ơ Ờ Ớ Ở Ỡ Ợ</code>
<code>u ù ú ủ ũ ụ / U Ù Ú Ủ Ũ Ụ</code>
<code>ư ừ ứ ử ữ ự / Ư Ừ Ứ Ử Ữ Ự</code>
<code>y ỳ ý ỷ ỹ ỵ / Y Ỳ Ý Ỷ Ỹ Ỵ</code>

Cần giữ đầy đủ dấu hỏi/ngã và móc ơ/ư ở cả chữ hoa, chữ thường, dấu chồng; chuẩn hóa Unicode cho đầu vào/nguồn bản dịch để NFC/NFD không tạo hai cách hiển thị khác nhau. Username/server text có thể chứa ký tự ngoài bộ Việt, nên coverage của bản Việt không thay thế một chính sách fallback font cho dữ liệu người chơi.

Các ký hiệu cần được kiểm tra theo glyph thực tế: `…`, `—`, `–`, `·`, `×`, mũi tên, dấu ngoặc/bracket, dấu +/−, %, @, #, dấu phân cách `|`, `?`, `--`, `--:--`, tiền tố `~`, dấu sao rarity, crosshair `+`, nhãn W/B, G/D/T và chữ trên marker. Một số ký hiệu được vẽ bằng Graphic/sprite nên không đòi glyph của font. Bảng dưới chỉ liệt kê ký tự ngoài ASCII xuất hiện trong literal của các file có bề mặt UI đã đối chiếu, không tính dấu trong JSON Việt ở phụ lục D.

| Ký tự | Unicode | Font Schoolbell gốc / Việt đã duyệt | Ví dụ nguồn |
| --- | --- | --- | --- |
| <code>·</code> | U+00B7 · MIDDLE DOT | Có / Có | [AramBuffRuntime.Extended.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffRuntime.Extended.cs:94>) |
| <code>×</code> | U+00D7 · MULTIPLICATION SIGN | Có / Có | [AnalysisBoardView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:378>) |
| <code>–</code> | U+2013 · EN DASH | Có / Có | [SettingsMenuController.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:294>) |
| <code>—</code> | U+2014 · EM DASH | Có / Có | [AnalysisBoardView.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:376>) |
| <code>…</code> | U+2026 · HORIZONTAL ELLIPSIS | Có / Có | [SettingsMenuController.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:294>) |

### 5.3. Layout và cách ghép câu

- [SketchbookUI.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SketchbookUI.cs:49>) bật autosize, min size max(16, 72% size), mặc định NoWrap + Ellipsis, richText=false. Nhãn dài có thể bị thu nhỏ/cắt; không dùng rich-text tag trong bảng dịch để ép layout.
- [MainMenuPresentation.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:81>) và ModeSelectionPresentation có nhãn tắt autosize, overflow ra ngoài. MainMenuAuthUI cũng có NoWrap/Overflow. Kiểm tra bản dịch theo chiều rộng từng label, không chỉ tổng chiều dài câu.
- [MatchHudStyle.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchHudStyle.cs:43>) cắt tên theo grapheme bằng StringInfo (tối đa 16 text elements); cần giữ cách cắt theo cụm dấu. AnalysisBoardView có nhãn tên/buff NoWrap; journal dùng ScrollRect + preferred height.
- Tooltip có đo preferred height/width và có giới hạn dẫn tới ellipsis; CommunityTypography xử lý nội dung không rich text. Buff có summary/full description/nerd progress; phải kiểm tra cả ba dạng, không chỉ thẻ draft.
- Nhiều câu ghép `"White" + ...`, `"wins as " + team`, `"Try moving your " + piece...`; chuyển sang template nguyên câu có placeholder đặt tên, để thứ tự từ tiếng Việt linh hoạt. Giữ số lượt, điều kiện, xác suất và luật chơi nguyên nghĩa.
- Số tiền có chỗ `N0` theo culture mặc định, chỗ khác InvariantCulture; ngày có `dd/MM/yyyy` ở Community, `dd MMM yyyy` với InvariantCulture ở Profile, `dd MMM  HH:mm` giờ máy người chơi trong Gacha history, hoặc dạng lưu `yyyy-MM-dd`; timer có `mm:ss` và `%`. Bản dịch cần quy ước định dạng trình bày riêng, không đổi định dạng save/mạng.
- Buff Summary có fallback FirstSentence lấy trước dấu chấm; lựa chọn câu tóm tắt nên được dịch riêng, không phụ thuộc cắt câu tiếng Anh. Các nhãn uppercase dùng ToUpperInvariant; không áp dụng biến đổi lên username/ID hay toàn văn người chơi.

<a id="6-pham-vi-trien-khai-sau-kiem-ke"></a>

## 6. Phạm vi triển khai sau kiểm kê

Các nhóm cần bóc key trước: menu/auth/lobby/loading → HUD/pause/result/share → bot → CHAOS local/online → Shop/Inventory/Gacha/Profile/Community → Settings còn thiếu. Đây là thứ tự gợi ý theo nguồn phụ thuộc, chưa phải thay đổi đã thực hiện. Khi triển khai, phải có locale change refresh, fallback English, template giữ placeholder, policy tên riêng, metadata server theo ID/locale và font cho cả TMP/legacy UI.

Chưa có kiểm kê OCR toàn bộ pixel của mọi texture, live mailbox/shop/banner từ backend, hay playthrough mọi lỗi mạng. Những phần đó được đánh dấu là dữ liệu động/hình ảnh; không gán số lượng câu dịch tuyệt đối. Danh mục dưới đây dùng đường dẫn và dòng nguồn để tra lại khi code thay đổi.

<a id="a-cac-vi-tri-tao-va-cap-nhat-ui"></a>

## A. Các vị trí tạo và cập nhật UI

Mỗi dòng là một biểu thức truyền text/caption/message hoặc gán `.text`; giữ nguyên biểu thức C# để thấy phần literal, biến, điều kiện và placeholder. Tên target/member là địa chỉ kỹ thuật để tìm code, **không đưa chúng vào bảng dịch**. Binding chỉ chứa biến cần tra nguồn ở mục 2 và phụ lục B–F. Các helper nhận text từ nơi khác có thể xuất hiện để chỉ điểm render cuối.
Các tham số được lần theo helper như Body/ActionButton/CreateField/ShowFailure/Report để tìm copy trước điểm render. Đây vẫn là kiểm kê có đối chiếu nguồn, không phải phân tích dữ liệu toàn chương trình. Một giá trị như GOLDS/DIAMONDS có thể vừa điều khiển request vừa đi vào thông báo; giữ ID, dịch qua nhãn hiển thị theo mục 4.

### Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [45](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:45>) | <code>Build</code> | <code>UI.Button</code> | <code>"BACK"</code> |
| [46](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:46>) | <code>Build</code> | <code>Label</code> | <code>"NEWS"</code> |
| [47](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:47>) | <code>Build</code> | <code>UI.Button</code> | <code>"MAILBOX &gt;"</code> |
| [50](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:50>) | <code>Build</code> | <code>UI.Button</code> | <code>"REFRESH"</code> |
| [58](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:58>) | <code>Build</code> | <code>UI.Button</code> | <code>"&lt;"</code> |
| [59](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:59>) | <code>Build</code> | <code>UI.Button</code> | <code>"&gt;"</code> |
| [60](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:60>) | <code>Build</code> | <code>Label</code> | <code>"Development notes / Read history stays on this device / " + CommunityContent.VersionLabel</code> |
| [62](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:62>) | <code>Build</code> | <code>UI.Button</code> | <code>"READ ALL"</code> |
| [129](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:129>) | <code>RefreshMailbox</code> | <code>AuthNotificationView.Show</code> | <code>"You have new mail"</code> |
| [129](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:129>) | <code>RefreshMailbox</code> | <code>AuthNotificationView.Show</code> | <code>"A welcome gift is waiting in your Mailbox. Open the letter whenever you are ready."</code> |
| [152](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:152>) | <code>UpdateCounters</code> | <code>entry.Label.text</code> | <code>entry.Mailbox ? "MAILBOX" : "NEWS"</code> |
| [158](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:158>) | <code>UpdateCounters</code> | <code>entry.Count.text</code> | <code>count &gt; 99 ? "99+" : count.ToString()</code> |
| [160](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:160>) | <code>UpdateCounters</code> | <code>counters.text</code> | <code>isMail ? $"UNREAD MAIL  {mailbox?.unreadCount ?? 0}     /     UNCLAIMED GIFTS  {mailbox?.unclaimedCount ?? 0}" : $"UNREAD NEWS  {unreadNews}     /     DEVELOPMENT NOTES"</code> |
| [167](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:167>) | <code>Render</code> | <code>heading.text</code> | <code>isMail ? "MAILBOX" : "NEWS"</code> |
| [168](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:168>) | <code>Render</code> | <code>SetButtonText</code> | <code>isMail ? "NEWS &gt;" : "MAILBOX &gt;"</code> |
| [176](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:176>) | <code>Render</code> | <code>SetButtonText</code> | <code>isMail ? claimBusy ? "CONFIRMING..." : "CLAIM ALL" : "READ ALL"</code> |
| [178](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:178>) | <code>Render</code> | <code>status.text</code> | <code>isMail ? mailError ?? (refreshing ? "Loading your mailbox..." : "Reading does not claim rewards. Your welcome gift never expires.") : CommunityContent.Error ?? "News and updates from the Chess But Weird team."</code> |
| [192](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:192>) | <code>BuildFilters</code> | <code>UI.Button</code> | <code>value</code> |
| [201](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:201>) | <code>RenderMailbox</code> | <code>IllustratedHeader</code> | <code>"A LETTER JUST FOR YOU"</code> |
| [202](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:202>) | <code>RenderMailbox</code> | <code>Body</code> | <code>"Sign in to read your mail and claim gifts. News is available to everyone."</code> |
| [203](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:203>) | <code>RenderMailbox</code> | <code>ActionButton</code> | <code>"SIGN IN"</code> |
| [213](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:213>) | <code>RenderMailbox</code> | <code>Label</code> | <code>(m.IsUnread ? "NEW  " : "") + m.title</code> |
| [214](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:214>) | <code>RenderMailbox</code> | <code>Label</code> | <code>m.sender + "  /  " + Date(m.sentAtUtc)</code> |
| [215](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:215>) | <code>RenderMailbox</code> | <code>Label</code> | <code>m.preview</code> |
| [216](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:216>) | <code>RenderMailbox</code> | <code>Label</code> | <code>State(m)</code> |
| [220](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:220>) | <code>RenderMailbox</code> | <code>Body</code> | <code>refreshing ? "Loading..." : mailError ?? "No letters in this section yet."</code> |
| [221](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:221>) | <code>RenderMailbox</code> | <code>IllustratedHeader</code> | <code>"A LITTLE LETTER"</code> |
| [221](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:221>) | <code>RenderMailbox</code> | <code>Body</code> | <code>"Choose a letter to read its full message and see any attached rewards."</code> |
| [222](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:222>) | <code>RenderMailbox</code> | <code>Label</code> | <code>selectedMail.title</code> |
| [223](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:223>) | <code>RenderMailbox</code> | <code>Label</code> | <code>selectedMail.sender + "  /  " + Date(selectedMail.sentAtUtc)</code> |
| [224](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:224>) | <code>RenderMailbox</code> | <code>Body</code> | <code>selectedMail.body</code> |
| [229](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:229>) | <code>RenderMailbox</code> | <code>Label</code> | <code>RewardName(reward.currency) + " x " + reward.amount.ToString("N0")</code> |
| [232](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:232>) | <code>RenderMailbox</code> | <code>Label</code> | <code>selectedMail.expiresAtUtc == null ? "NEVER EXPIRES" : "Expires: " + Date(selectedMail.expiresAtUtc)</code> |
| [235](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:235>) | <code>RenderMailbox</code> | <code>ActionButton</code> | <code>selectedMail.IsClaimed ? "CLAIMED" : claimBusy ? "CONFIRMING..." : selectedMail.IsClaimable ? "CLAIM GIFT" : "EXPIRED"</code> |
| [255](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:255>) | <code>SelectMail</code> | <code>status.text</code> | <code>"Read status could not be saved. Select the letter again after reconnecting."</code> |
| [281](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:281>) | <code>ClaimSelected</code> | <code>AuthNotificationView.Show</code> | <code>"Gift claimed"</code> |
| [281](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:281>) | <code>ClaimSelected</code> | <code>AuthNotificationView.Show</code> | <code>"Your wallet has been refreshed from the server. Have fun!"</code> |
| [321](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:321>) | <code>ClaimAll</code> | <code>AuthNotificationView.Show</code> | <code>receipt.claimedCount &gt; 0 ? "All gifts claimed" : "No gifts remaining"</code> |
| [322](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:322>) | <code>ClaimAll</code> | <code>AuthNotificationView.Show</code> | <code>receipt.claimedCount &gt; 0 ? $"Claimed gifts from {receipt.claimedCount} letters and marked them as read.\n{rewards}" : "Your mailbox and wallet are up to date."</code> |
| [344](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:344>) | <code>RenderNews</code> | <code>Label</code> | <code>(newsRead.IsRead(a.id) ? "" : "NEW  ") + "v." + a.version + "  /  " + a.category</code> |
| [345](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:345>) | <code>RenderNews</code> | <code>Label</code> | <code>a.title</code> |
| [346](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:346>) | <code>RenderNews</code> | <code>Label</code> | <code>"FEATURED  /  " + Date(a.publishAtUtc)</code> |
| [350](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:350>) | <code>RenderNews</code> | <code>Body</code> | <code>CommunityContent.Error ?? "No articles in this category yet."</code> |
| [355](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:355>) | <code>RenderNews</code> | <code>IllustratedHeader</code> | <code>hero == null ? "THE NEWS DESK" : hero.artKey == "chaos" ? "35 BUFFS. ENDLESS POSSIBILITIES." : "FEATURED / v." + hero.version</code> |
| [356](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:356>) | <code>RenderNews</code> | <code>Body</code> | <code>hero?.summary ?? "Choose a story to read the full development update."</code> |
| [357](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:357>) | <code>RenderNews</code> | <code>ActionButton</code> | <code>"READ FEATURED"</code> |
| [360](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:360>) | <code>RenderNews</code> | <code>IllustratedHeader</code> | <code>"v." + selected.version + "  /  " + selected.category</code> |
| [361](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:361>) | <code>RenderNews</code> | <code>Label</code> | <code>selected.title</code> |
| [363](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:363>) | <code>RenderNews</code> | <code>Label</code> | <code>"Published: " + Date(selected.publishAtUtc)</code> |
| [364](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:364>) | <code>RenderNews</code> | <code>Body</code> | <code>selected.body</code> |
| [387](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:387>) | <code>Label</code> | <code>UI.Text</code> | <code>value</code> |
| [392](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:392>) | <code>Body</code> | <code>Label</code> | <code>value</code> |
| [401](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:401>) | <code>ActionButton</code> | <code>UI.Button</code> | <code>text</code> |
| [411](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:411>) | <code>IllustratedHeader</code> | <code>Label</code> | <code>caption</code> |
| [417](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Community/CommunityMenuController.cs:417>) | <code>SetButtonText</code> | <code>text.text</code> | <code>value</code> |

### Assets/_Project/Scripts/Chess/Core/GameBootstrap.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [13](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/GameBootstrap.cs:13>) | <code>Start</code> | <code>loading.Report</code> | <code>"Starting Chess but Weird..."</code> |
| [14](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/GameBootstrap.cs:14>) | <code>Start</code> | <code>loading.Report</code> | <code>"Ready"</code> |

### Assets/_Project/Scripts/Chess/Core/LoadingManager.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [71](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingManager.cs:71>) | <code>LoadMatch</code> | <code>ui.Report</code> | <code>"Preparing content..."</code> |
| [91](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingManager.cs:91>) | <code>LoadMatch</code> | <code>ui.Report</code> | <code>"Preparing the board scene..."</code> |
| [103](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingManager.cs:103>) | <code>LoadMatch</code> | <code>ui.Report</code> | <code>"Loading selected board..."</code> |
| [109](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingManager.cs:109>) | <code>LoadMatch</code> | <code>ui.Report</code> | <code>"Loading board and environment..."</code> |
| [112](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingManager.cs:112>) | <code>LoadMatch</code> | <code>ui.Report</code> | <code>"Loading selected pieces..."</code> |
| [140](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingManager.cs:140>) | <code>LoadMatch</code> | <code>ui.Report</code> | <code>"Setting up the pieces..."</code> |
| [151](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingManager.cs:151>) | <code>LoadMatch</code> | <code>ui.Report</code> | <code>"Ready"</code> |
| [192](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingManager.cs:192>) | <code>PreparePresentation</code> | <code>ui.Report</code> | <code>"Preparing materials and textures..."</code> |
| [220](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingManager.cs:220>) | <code>PreparePresentation</code> | <code>ui.Report</code> | <code>"Preparing materials and textures..."</code> |

### Assets/_Project/Scripts/Chess/Core/LoadingUI.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [48](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingUI.cs:48>) | <code>Create</code> | <code>ui.AddText</code> | <code>"Chess but Weird"</code> |
| [49](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingUI.cs:49>) | <code>Create</code> | <code>ui.AddText</code> | <code>"Preparing..."</code> |
| [57](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingUI.cs:57>) | <code>Create</code> | <code>ui.AddText</code> | <code>"0%"</code> |
| [58](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingUI.cs:58>) | <code>Create</code> | <code>ui.AddButton</code> | <code>"Retry"</code> |
| [59](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingUI.cs:59>) | <code>Create</code> | <code>ui.AddButton</code> | <code>"Back to menu"</code> |
| [96](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingUI.cs:96>) | <code>AddText</code> | <code>t.text</code> | <code>text</code> |
| [106](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingUI.cs:106>) | <code>AddButton</code> | <code>AddText</code> | <code>text</code> |
| [114](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingUI.cs:114>) | <code>Report</code> | <code>label.text</code> | <code>message</code> |
| [121](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingUI.cs:121>) | <code>Update</code> | <code>percentage.text</code> | <code>Mathf.FloorToInt(displayed * 100) + "%"</code> |
| [127](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/LoadingUI.cs:127>) | <code>Error</code> | <code>label.text</code> | <code>message</code> |

### Assets/_Project/Scripts/Chess/Core/SessionLoadingController.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [31](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/SessionLoadingController.cs:31>) | <code>Begin</code> | <code>loading.Report</code> | <code>"Preparing your chess table..."</code> |
| [56](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/SessionLoadingController.cs:56>) | <code>Prepare</code> | <code>Report</code> | <code>"Preparing your chess table..."</code> |
| [58](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/SessionLoadingController.cs:58>) | <code>Prepare</code> | <code>Report</code> | <code>"Preparing artwork..."</code> |
| [71](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/SessionLoadingController.cs:71>) | <code>Prepare</code> | <code>Report</code> | <code>message</code> |
| [74](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/SessionLoadingController.cs:74>) | <code>Prepare</code> | <code>Report</code> | <code>message</code> |
| [78](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/SessionLoadingController.cs:78>) | <code>Prepare</code> | <code>Report</code> | <code>"Ready"</code> |
| [98](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Core/SessionLoadingController.cs:98>) | <code>Report</code> | <code>loading.Report</code> | <code>message</code> |

### Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramAbilityView.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [81](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramAbilityView.cs:81>) | <code>Initialize</code> | <code>crosshair.text</code> | <code>"+"</code> |
| [88](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramAbilityView.cs:88>) | <code>Initialize</code> | <code>title.text</code> | <code>"USE BUFF"</code> |
| [90](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramAbilityView.cs:90>) | <code>Initialize</code> | <code>MatchHudStyle.Button</code> | <code>"Expand"</code> |
| [125](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramAbilityView.cs:125>) | <code>Update</code> | <code>summary.text</code> | <code>presentation.Summary</code> |
| [126](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramAbilityView.cs:126>) | <code>Update</code> | <code>status.text</code> | <code>presentation.InstructionRequired?presentation.Status:actions.Count==0?"Passive buff":""</code> |
| [127](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramAbilityView.cs:127>) | <code>Update</code> | <code>details.text</code> | <code>expanded?TooltipPreferences.Description(presentation.Details):""</code> |
| [130](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramAbilityView.cs:130>) | <code>Update</code> | <code>rifleHint.text</code> | <code>presentation.RifleHint</code> |
| [131](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramAbilityView.cs:131>) | <code>Update</code> | <code>expandButton.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>expanded?"Collapse":"Expand"</code> |
| [150](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramAbilityView.cs:150>) | <code>Update</code> | <code>details.text</code> | <code>presentation.Summary+"\n\n"+TooltipPreferences.Description(presentation.Details)</code> |
| [176](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramAbilityView.cs:176>) | <code>Update</code> | <code>label.text</code> | <code>actions[i].Label+(!string.IsNullOrEmpty(actions[i].Detail)?"\n"+actions[i].Detail:"")</code> |

### Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [44](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:44>) | <code></code> | <code>Localized</code> | <code>string.IsNullOrWhiteSpace(displayName) ? id.ToString() : displayName</code> |
| [45](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:45>) | <code></code> | <code>Localized</code> | <code>string.IsNullOrWhiteSpace(shortName) ? DisplayName : shortName</code> |
| [46](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:46>) | <code></code> | <code>Localized</code> | <code>description</code> |
| [48](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:48>) | <code></code> | <code>Localized</code> | <code>summary</code> |
| [49](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:49>) | <code>Localized</code> | <code>SettingsLocalization.Text</code> | <code>fallback</code> |

### Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [50](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:50>) | <code>ShowOnlineDraft</code> | <code>titleLabel.text</code> | <code>team.ToString().ToUpperInvariant() + " CHAOS ROLL"</code> |
| [65](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:65>) | <code>SetOnlineAvailability</code> | <code>subtitleLabel.text</code> | <code>available ? "Choose 1 buff for your army. Your opponent chooses separately."<br>            : "Waiting for the server connection or selection confirmation..."</code> |
| [155](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:155>) | <code>ShowTargetPrompt</code> | <code>targetPromptLabel.text</code> | <code>message</code> |
| [162](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:162>) | <code>ShowBuffToast</code> | <code>toastLabel.text</code> | <code>$"{team}: {message}"</code> |
| [247](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:247>) | <code>CreateCard</code> | <code>tier.text</code> | <code>tierValue.ToString().ToUpperInvariant()</code> |
| [249](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:249>) | <code>CreateCard</code> | <code>name.text</code> | <code>definition?definition.DisplayName:"CHAOS Buff"</code> |
| [261](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:261>) | <code>CreateCard</code> | <code>MatchHudStyle.Button</code> | <code>"Choose this buff"</code> |
| [294](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:294>) | <code>RefreshDescriptions</code> | <code>descriptionButtonLabel.text</code> | <code>fullDescriptions?"All cards: Full  /  Summary":"All cards: Summary  /  Full"</code> |
| [300](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:300>) | <code>RefreshCardDescription</code> | <code>card.Description.text</code> | <code>definition?(card.FullDescription?definition.Description:definition.Summary):string.Empty</code> |
| [305](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:305>) | <code>RefreshCardDescription</code> | <code>card.Hint.text</code> | <code>"Scroll to read all"</code> |
| [307](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:307>) | <code>RefreshCardDescription</code> | <code>card.ReadingLabel.text</code> | <code>card.FullDescription?"Summary":"Read full"</code> |
| [353](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:353>) | <code>RefreshDraftCopy</code> | <code>titleLabel.text</code> | <code>choosingTeam == PieceTeam.White ? "WHITE CHAOS ROLL" : "BLACK CHAOS ROLL"</code> |
| [354](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:354>) | <code>RefreshDraftCopy</code> | <code>subtitleLabel.text</code> | <code>choosingTeam == PieceTeam.White<br>            ? $"Choose 1 buff from {FormatTierList(whiteOptions)} options."<br>            : $"White picked {FormatBuffList(whiteSelection)}. Choose 1 buff from {FormatTierList(blackOptions)} options."</code> |
| [430](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:430>) | <code>EnsureBuilt</code> | <code>help.text</code> | <code>"Default text: Settings / Gameplay / Draft Card Descriptions"</code> |
| [449](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDraftView.cs:449>) | <code>CreateContinueButton</code> | <code>MatchHudStyle.Button</code> | <code>"Start Practice"</code> |

### Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffPieceMarker.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [27](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffPieceMarker.cs:27>) | <code>Configure</code> | <code>Build</code> | <code>markerLabel</code> |
| [80](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffPieceMarker.cs:80>) | <code>Build</code> | <code>label.text</code> | <code>markerLabel</code> |

### Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffRuntime.Extended.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [554](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffRuntime.Extended.cs:554>) | <code>RefreshBoardMarkers</code> | <code>MarkSquare</code> | <code>"MINE"</code> |
| [555](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffRuntime.Extended.cs:555>) | <code>RefreshBoardMarkers</code> | <code>MarkSquare</code> | <code>pair.Value.Kind==PieceType.Rook?"ROOK CRATE":"QUEEN CRATE"</code> |
| [556](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffRuntime.Extended.cs:556>) | <code>RefreshBoardMarkers</code> | <code>MarkSquare</code> | <code>"VOID"</code> |
| [566](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffRuntime.Extended.cs:566>) | <code>MarkSquare</code> | <code>text.text</code> | <code>label</code> |

### Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [173](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:173>) | <code>ShowLoadoutLobby</code> | <code>RequireOnlineAccess</code> | <code>PlayModeNames.Label(mode)</code> |
| [248](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:248>) | <code>ShowAramModeSelection</code> | <code>RequireAccountAccess</code> | <code>"CHAOS mode"</code> |
| [279](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:279>) | <code>ShowTwoPlayerSkinSelection</code> | <code>RequireAccountAccess</code> | <code>"Two-player mode"</code> |
| [326](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:326>) | <code>StartLocalTwoPlayerGameWithLoadout</code> | <code>RequireAccountAccess</code> | <code>"Two-player mode"</code> |
| [338](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:338>) | <code>StartAramPracticeGame</code> | <code>RequireAccountAccess</code> | <code>"CHAOS mode"</code> |
| [363](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:363>) | <code>ShowAramLanSetup</code> | <code>RequireOnlineAccess</code> | <code>"CHAOS mode"</code> |
| [375](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:375>) | <code>ShowAramOnlineSetup</code> | <code>RequireOnlineAccess</code> | <code>"CHAOS online play"</code> |
| [401](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:401>) | <code>ShowLanSetup</code> | <code>RequireOnlineAccess</code> | <code>"LAN multiplayer"</code> |
| [413](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:413>) | <code>ShowOnlineSetup</code> | <code>RequireOnlineAccess</code> | <code>"Online play"</code> |
| [435](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:435>) | <code>RequireAccountAccess</code> | <code>AuthNotificationView.Show</code> | <code>"Login required"</code> |
| [436](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:436>) | <code>RequireAccountAccess</code> | <code>AuthNotificationView.Show</code> | <code>$"{featureLabel} requires an account. Please log in again."</code> |
| [442](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:442>) | <code>RequireOnlineAccess</code> | <code>RequireAccountAccess</code> | <code>featureLabel</code> |
| [444](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:444>) | <code>RequireOnlineAccess</code> | <code>AuthNotificationView.Show</code> | <code>"Online unavailable"</code> |
| [445](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:445>) | <code>RequireOnlineAccess</code> | <code>AuthNotificationView.Show</code> | <code>"Online rooms require a compatible backend session."</code> |
| [470](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:470>) | <code>LogoutToAuthentication</code> | <code>AuthNotificationView.Show</code> | <code>"Logging out..."</code> |
| [470](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:470>) | <code>LogoutToAuthentication</code> | <code>AuthNotificationView.Show</code> | <code>"Please wait for the server."</code> |
| [654](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:654>) | <code>DrawResponsiveGui</code> | <code>GUI.Label</code> | <code>$"{checkedTeam} king is in CHECK"</code> |
| [658](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:658>) | <code>DrawResponsiveGui</code> | <code>GUI.Label</code> | <code>$"You played: {playerTeam}"</code> |
| [689](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:689>) | <code>DrawMainMenu</code> | <code>GUI.Label</code> | <code>"Chess but Weird"</code> |
| [691](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:691>) | <code>DrawMainMenu</code> | <code>GUI.Label</code> | <code>"Classic chess foundation with room for weird variants."</code> |
| [701](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:701>) | <code>DrawMainMenu</code> | <code>GUI.Button</code> | <code>"Play"</code> |
| [724](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:724>) | <code>DrawTurnSelection</code> | <code>GUI.Label</code> | <code>"Choose first turn"</code> |
| [734](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:734>) | <code>DrawTurnSelection</code> | <code>GUI.Button</code> | <code>"White"</code> |
| [737](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:737>) | <code>DrawTurnSelection</code> | <code>GUI.Button</code> | <code>"Black"</code> |
| [754](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:754>) | <code>DrawGameOver</code> | <code>GUI.Label</code> | <code>title</code> |
| [755](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:755>) | <code>DrawGameOver</code> | <code>GUI.Label</code> | <code>result</code> |
| [756](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:756>) | <code>DrawGameOver</code> | <code>GUI.Label</code> | <code>detail</code> |
| [766](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ChessTurnSelectionUI.cs:766>) | <code>DrawGameOver</code> | <code>GUI.Button</code> | <code>"Restart"</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [103](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:103>) | <code>Build</code> | <code>MatchHudStyle.Text</code> | <code>"Y"</code> |
| [107](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:107>) | <code>Build</code> | <code>Player</code> | <code>"White"</code> |
| [107](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:107>) | <code>Build</code> | <code>Player</code> | <code>"Black"</code> |
| [112](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:112>) | <code>Build</code> | <code>MatchHudStyle.Button</code> | <code>"Match journal"</code> |
| [114](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:114>) | <code>Build</code> | <code>MatchHudStyle.Button</code> | <code>"Focus"</code> |
| [116](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:116>) | <code>Build</code> | <code>journalButton.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>""</code> |
| [117](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:117>) | <code>Build</code> | <code>focusButton.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>""</code> |
| [134](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:134>) | <code>Build</code> | <code>MatchHudStyle.Text</code> | <code>"Match journal"</code> |
| [139](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:139>) | <code>Build</code> | <code>MatchHudStyle.Button</code> | <code>"History"</code> |
| [140](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:140>) | <code>Build</code> | <code>MatchHudStyle.Button</code> | <code>"Captures"</code> |
| [153](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:153>) | <code>Build</code> | <code>MatchHudStyle.Button</code> | <code>"Jump to latest"</code> |
| [172](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:172>) | <code>Player</code> | <code>MatchHudStyle.Text</code> | <code>label</code> |
| [176](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:176>) | <code>Player</code> | <code>MatchHudStyle.Text</code> | <code>label</code> |
| [240](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:240>) | <code>ApplyPanels</code> | <code>journalButton.GetComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>"Match journal"</code> |
| [240](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:240>) | <code>ApplyPanels</code> | <code>journalButton.GetComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>expanded?"Close move history and captures.":"Open move history and captures."</code> |
| [241](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:241>) | <code>ApplyPanels</code> | <code>focusButton.GetComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>"Focus: "+(focus?"on":"off")</code> |
| [241](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:241>) | <code>ApplyPanels</code> | <code>focusButton.GetComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>"Toggle compact player cards."</code> |
| [274](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:274>) | <code>RefreshCameraHint</code> | <code>cameraHintState.text</code> | <code>shortcut</code> |
| [277](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:277>) | <code>RefreshCameraHint</code> | <code>cameraHint.GetComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>state</code> |
| [277](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:277>) | <code>RefreshCameraHint</code> | <code>cameraHint.GetComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>"Click or press "+shortcut+" to "+(unlocked?"lock and reset the view":"unlock camera movement")+"\n"+controls</code> |
| [284](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:284>) | <code>Refresh</code> | <code>whiteName.text</code> | <code>focus?"W":"W · "+MatchHudStyle.PlayerName(game.StatisticsPlayerName(PieceTeam.White))</code> |
| [285](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:285>) | <code>Refresh</code> | <code>blackName.text</code> | <code>focus?"B":"B · "+MatchHudStyle.PlayerName(game.StatisticsPlayerName(PieceTeam.Black))</code> |
| [287](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:287>) | <code>Refresh</code> | <code>whiteState.text</code> | <code>game.UsesDotNetOnline?game.OnlineClockLabel(PieceTeam.White):TurnLabel(PieceTeam.White,ended)</code> |
| [288](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:288>) | <code>Refresh</code> | <code>blackState.text</code> | <code>game.UsesDotNetOnline?game.OnlineClockLabel(PieceTeam.Black):TurnLabel(PieceTeam.Black,ended)</code> |
| [300](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:300>) | <code>Refresh</code> | <code>status.text</code> | <code>state+(game.IsNetworkGame&amp;&amp;game.StatisticsConnectionState!="Connected"?" · "+game.StatisticsConnectionState:"")</code> |
| [301](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:301>) | <code>Refresh</code> | <code>elapsed.text</code> | <code>game.StatisticsTimeKnown?"Elapsed · "+TimeLabel(game.MatchElapsedSeconds):"Time unavailable"</code> |
| [318](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:318>) | <code>Refresh</code> | <code>history.text</code> | <code>showCaptures?captureSummary:builder.Length==0?"No moves yet":builder.ToString()</code> |
| [327](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:327>) | <code>Refresh</code> | <code>hint.text</code> | <code>showCaptures?"Captures exclude sacrifice and destruction":game.Statistics.HistoryComplete?"Move · Side · Notation / event":"Partial history · server sync needed"</code> |
| [332](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:332>) | <code>RefreshBuffSummary</code> | <code>whiteMoves.text</code> | <code>"W · "+(game.Statistics.HistoryComplete?MatchHudStyle.MoveCountLabel(white):"?")</code> |
| [333](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:333>) | <code>RefreshBuffSummary</code> | <code>blackMoves.text</code> | <code>"B · "+(game.Statistics.HistoryComplete?MatchHudStyle.MoveCountLabel(black):"?")</code> |
| [334](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:334>) | <code>RefreshBuffSummary</code> | <code>combinedMoves.text</code> | <code>"CHAOS · Total moves: "+game.Statistics.ConfirmedMoveNumber</code> |
| [341](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:341>) | <code>RefreshBuffLabel</code> | <code>label.text</code> | <code>onlineName</code> |
| [342](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:342>) | <code>RefreshBuffLabel</code> | <code>label.GetComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>UserSettings.Enabled("buff_source")?side+" online buff":"Online buff"</code> |
| [342](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:342>) | <code>RefreshBuffLabel</code> | <code>label.GetComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>TooltipPreferences.Description(onlineDetails)</code> |
| [346](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:346>) | <code>RefreshBuffLabel</code> | <code>label.GetComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>"Hidden buff"</code> |
| [346](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:346>) | <code>RefreshBuffLabel</code> | <code>label.GetComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>"Your opponent's buff is private."</code> |
| [346](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:346>) | <code>RefreshBuffLabel</code> | <code>label.text</code> | <code>"Buff hidden"</code> |
| [353](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:353>) | <code>RefreshBuffLabel</code> | <code>label.text</code> | <code>names.Length&gt;0?names.ToString():"No buff"</code> |
| [355](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:355>) | <code>RefreshBuffLabel</code> | <code>label.text</code> | <code>"\n"+progress</code> |
| [356](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:356>) | <code>RefreshBuffLabel</code> | <code>label.GetComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>UserSettings.Enabled("buff_source")?side+" buffs":"Buffs"</code> |
| [356](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:356>) | <code>RefreshBuffLabel</code> | <code>label.GetComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>details.Length&gt;0?details.ToString():"No buff selected."</code> |
| [372](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:372>) | <code>RefreshTurnIcon</code> | <code>card.GetComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>team+" · "+game.StatisticsPlayerName(team)</code> |
| [372](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/AnalysisBoardView.cs:372>) | <code>RefreshTurnIcon</code> | <code>card.GetComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>label</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/BotCompanionView.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [71](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotCompanionView.cs:71>) | <code>Initialize</code> | <code>Label</code> | <code>"Opponent"</code> |
| [73](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotCompanionView.cs:73>) | <code>Initialize</code> | <code>Label</code> | <code>"Bot dialogue"</code> |
| [76](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotCompanionView.cs:76>) | <code>Initialize</code> | <code>MatchHudStyle.Button</code> | <code>"Mute chat"</code> |
| [79](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotCompanionView.cs:79>) | <code>Initialize</code> | <code>MatchHudStyle.Button</code> | <code>"Retry"</code> |
| [80](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotCompanionView.cs:80>) | <code>Initialize</code> | <code>MatchHudStyle.Button</code> | <code>"Main menu"</code> |
| [106](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotCompanionView.cs:106>) | <code>Reset</code> | <code>nameLabel.text</code> | <code>profile.BotName+" / "+profile.DisplayName</code> |
| [110](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotCompanionView.cs:110>) | <code>Hide</code> | <code>activityLabel.text</code> | <code>string.Empty</code> |
| [118](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotCompanionView.cs:118>) | <code>RefreshActivity</code> | <code>activityLabel.text</code> | <code>visible?controller.Activity:string.Empty</code> |
| [144](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotCompanionView.cs:144>) | <code>Say</code> | <code>ShowDialogue</code> | <code>line</code> |
| [150](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotCompanionView.cs:150>) | <code>ShowDialogue</code> | <code>speech.text</code> | <code>line</code> |
| [166](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotCompanionView.cs:166>) | <code>DismissDialogue</code> | <code>speech.text</code> | <code>string.Empty</code> |
| [182](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotCompanionView.cs:182>) | <code>Update</code> | <code>ShowDialogue</code> | <code>error</code> |
| [192](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotCompanionView.cs:192>) | <code>Update</code> | <code>ShowDialogue</code> | <code>line</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/BotPracticeView.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [33](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotPracticeView.cs:33>) | <code>Initialize</code> | <code>MatchHudStyle.Button</code> | <code>"Hint"</code> |
| [34](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotPracticeView.cs:34>) | <code>Initialize</code> | <code>MatchHudStyle.Button</code> | <code>"Undo"</code> |
| [61](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotPracticeView.cs:61>) | <code>Update</code> | <code>hintLabel.text</code> | <code>controller.IsHintThinking ? "Hint..." : "Hint"</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [12](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:12>) | <code>Build</code> | <code>SketchbookUI.Text</code> | <code>"Meet your next opponent"</code> |
| [13](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:13>) | <code>Build</code> | <code>SketchbookUI.Text</code> | <code>"Pick an opponent, choose Practice or Challenge, then choose your side."</code> |
| [32](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:32>) | <code>Build</code> | <code>SketchbookUI.Text</code> | <code>"Choose your game"</code> |
| [41](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:41>) | <code>Build</code> | <code>hints.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>"Hints: "+(training&amp;&amp;allowHints?"On":"Off")</code> |
| [42](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:42>) | <code>Build</code> | <code>undo.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>"Undo: "+(training&amp;&amp;allowUndo?"On":"Off")</code> |
| [44](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:44>) | <code>Build</code> | <code>practiceStats.text</code> | <code>$"Practice   Wins {progress.practiceWins}   Losses {progress.practiceLosses}   Draws {progress.practiceDraws}"</code> |
| [45](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:45>) | <code>Build</code> | <code>assistStats.text</code> | <code>$"Hints used: {progress.hintsUsed}   /   Undos used: {progress.undosUsed}"</code> |
| [46](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:46>) | <code>Build</code> | <code>challengeStats.text</code> | <code>$"Challenge   Wins {progress.challengeWins}   Losses {progress.challengeLosses}   Draws {progress.challengeDraws}"</code> |
| [50](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:50>) | <code>Build</code> | <code>modeInfo.text</code> | <code>training ? (practiceGold&gt;0<br>                ? $"Win: {practiceGold} gold. Shared daily rewards: {PlayerProfileStore.PracticeRewardsRemaining(DateTime.UtcNow)}/3 left. Resets at 00:00 (UTC+7)."<br>                : "Learn with hints and takebacks. Your record and assist use are saved. No gold reward.")<br>                : (progress.ChallengeBeaten ? $"Win: {winGold} gold. No hints or takebacks."<br>                    : $"First win: {winGold*3} gold (x3). Then {winGold} gold per win. No hints or takebacks.")</code> |
| [55](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:55>) | <code>Build</code> | <code>launchLabel.text</code> | <code>(training?"Practise with ":"Challenge ")+selected.BotName</code> |
| [57](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:57>) | <code>Build</code> | <code>SketchbookUI.Button</code> | <code>"Practice"</code> |
| [58](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:58>) | <code>Build</code> | <code>SketchbookUI.Button</code> | <code>"Challenge"</code> |
| [66](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:66>) | <code>Build</code> | <code>name.text</code> | <code>profile.BotName</code> |
| [66](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:66>) | <code>Build</code> | <code>level.text</code> | <code>profile.DisplayName + " / " + profile.Style</code> |
| [67](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:67>) | <code>Build</code> | <code>rating.text</code> | <code>"~"+profile.EstimatedRating+" Elo · hover for guidance"</code> |
| [68](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:68>) | <code>Build</code> | <code>ratingTarget.Configure</code> | <code>profile.BotName+" · ~"+profile.EstimatedRating+" Elo"</code> |
| [68](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:68>) | <code>Build</code> | <code>ratingTarget.Configure</code> | <code>profile.RatingTooltip</code> |
| [69](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:69>) | <code>Build</code> | <code>story.text</code> | <code>profile.Story</code> |
| [83](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:83>) | <code>Build</code> | <code>SketchbookUI.Text</code> | <code>profile.BotName</code> |
| [84](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:84>) | <code>Build</code> | <code>SketchbookUI.Text</code> | <code>profile.DisplayName</code> |
| [85](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:85>) | <code>Build</code> | <code>SketchbookUI.Text</code> | <code>"~"+profile.EstimatedRating+" Elo"</code> |
| [94](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:94>) | <code>Build</code> | <code>SketchbookUI.Button</code> | <code>"Back"</code> |
| [95](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:95>) | <code>Build</code> | <code>SketchbookUI.Text</code> | <code>"All bots are open. Gold badges mark Challenge wins."</code> |
| [105](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:105>) | <code>CreateBadge</code> | <code>badge.gameObject.AddComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>"Challenge beaten"</code> |
| [105](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/BotRosterView.cs:105>) | <code>CreateBadge</code> | <code>badge.gameObject.AddComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>"Earned by winning a Challenge game against this bot."</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [62](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:62>) | <code>BuildUi</code> | <code>UI.Text</code> | <code>"PAUSED"</code> |
| [83](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:83>) | <code>RebuildActions</code> | <code>AddAction</code> | <code>IsMultiplayer ? "RETURN TO MATCH" : "RESUME"</code> |
| [84](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:84>) | <code>RebuildActions</code> | <code>AddAction</code> | <code>"RESTART MATCH"</code> |
| [85](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:85>) | <code>RebuildActions</code> | <code>AddAction</code> | <code>"SETTINGS"</code> |
| [86](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:86>) | <code>RebuildActions</code> | <code>AddAction</code> | <code>"MATCH INFO"</code> |
| [87](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:87>) | <code>RebuildActions</code> | <code>AddAction</code> | <code>"LEAVE MATCH"</code> |
| [88](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:88>) | <code>RebuildActions</code> | <code>AddAction</code> | <code>"MAIN MENU"</code> |
| [89](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:89>) | <code>RebuildActions</code> | <code>AddAction</code> | <code>"QUIT DESKTOP"</code> |
| [97](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:97>) | <code>AddAction</code> | <code>UI.Button</code> | <code>caption</code> |
| [109](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:109>) | <code>BuildConfirmation</code> | <code>UI.Text</code> | <code>"ARE YOU SURE?"</code> |
| [111](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:111>) | <code>BuildConfirmation</code> | <code>UI.Button</code> | <code>"CANCEL"</code> |
| [112](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:112>) | <code>BuildConfirmation</code> | <code>UI.Button</code> | <code>"CONFIRM"</code> |
| [123](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:123>) | <code>BuildMatchInfo</code> | <code>UI.Text</code> | <code>"MATCH INFO"</code> |
| [125](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:125>) | <code>BuildMatchInfo</code> | <code>UI.Button</code> | <code>"BACK"</code> |
| [137](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:137>) | <code>PauseLocally</code> | <code>title.text</code> | <code>IsMultiplayer ? "MATCH MENU" : "PAUSED"</code> |
| [138](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:138>) | <code>PauseLocally</code> | <code>statusText.text</code> | <code>IsMultiplayer ? "The match and online clocks continue." : ""</code> |
| [156](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:156>) | <code>Ask</code> | <code>confirmationText.text</code> | <code>action == PendingAction.Restart ? "Restart this match?\nYour current position will be cleared." :<br>            action == PendingAction.QuitDesktop ? "Quit to desktop?" + (IsMultiplayer ? "\nLeaving an active match uses the existing surrender rules." : "") :<br>            "Leave this match and return to " + destination + "?" + (IsMultiplayer ? "\nLeaving an active match uses the existing surrender rules." : "")</code> |
| [176](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:176>) | <code>ConfirmAction</code> | <code>statusText.text</code> | <code>action == PendingAction.Restart ? "Restarting..." : "Leaving match..."</code> |
| [235](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:235>) | <code>ConfirmAction</code> | <code>statusText.text</code> | <code>"Could not complete action: " + PlayerNotificationText.FromException(error)</code> |
| [271](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs:271>) | <code>RefreshMatchInfo</code> | <code>infoText.text</code> | <code>chessGame.StatisticsPlayerName(PieceTeam.White) + "  /  WHITE\n" +<br>            chessGame.StatisticsPlayerName(PieceTeam.Black) + "  /  BLACK\n\n" +<br>            chessGame.StatisticsMode + "  /  " + (lanController ? lanController.MatchConnectionState : chessGame.StatisticsConnectionState) + "\n" +<br>            "White  " + chessGame.OnlineClockLabel(PieceTeam.White) + "     Black  " + chessGame.OnlineClockLabel(PieceTeam.Black) + "\n" +<br>            "Last move: " + chessGame.LastMoveSummary</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/CosmeticThumbnailUI.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [41](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/CosmeticThumbnailUI.cs:41>) | <code>Draw</code> | <code>UI.Text</code> | <code>"Equipped"</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [104](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:104>) | <code>RefreshFromServer</code> | <code>SetStatus</code> | <code>"Loading banners and wallet..."</code> |
| [114](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:114>) | <code>RefreshFromServer</code> | <code>SetStatus</code> | <code>"No active gacha banners are available."</code> |
| [133](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:133>) | <code>RefreshFromServer</code> | <code>SetStatus</code> | <code>"Pick a banner. Your next surprise is waiting."</code> |
| [137](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:137>) | <code>RefreshFromServer</code> | <code>SetStatus</code> | <code>"Could not load gacha: " + PlayerNotificationText.FromException(error)</code> |
| [160](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:160>) | <code>ChangeBanner</code> | <code>SetStatus</code> | <code>"Retry the pending summon before changing banners."</code> |
| [168](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:168>) | <code>ChangeBanner</code> | <code>SetStatus</code> | <code>"Loading banner..."</code> |
| [172](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:172>) | <code>ChangeBanner</code> | <code>SetStatus</code> | <code>"Showing " + activeBanner.name</code> |
| [182](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:182>) | <code>ChangeBanner</code> | <code>SetStatus</code> | <code>"Could not load banner: " + PlayerNotificationText.FromException(error)</code> |
| [218](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:218>) | <code>ShowBannerDetails</code> | <code>bannerDetailText.text</code> | <code>text.ToString()</code> |
| [238](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:238>) | <code>StartSummon</code> | <code>SetStatus</code> | <code>"Wait for the banner to finish loading."</code> |
| [248](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:248>) | <code>StartSummon</code> | <code>SetStatus</code> | <code>"No active banner is loaded."</code> |
| [253](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:253>) | <code>StartSummon</code> | <code>SetStatus</code> | <code>"This banner does not offer that summon count."</code> |
| [259](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:259>) | <code>StartSummon</code> | <code>SetStatus</code> | <code>"Retry the pending x" + pendingCount + " summon before choosing another count."</code> |
| [266](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:266>) | <code>StartSummon</code> | <code>SetStatus</code> | <code>"Opening your surprise..."</code> |
| [305](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:305>) | <code>StartSummon</code> | <code>SetStatus</code> | <code>"Summon failed: " + PlayerNotificationText.FromException(error) + " You can retry this summon safely."</code> |
| [345](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:345>) | <code>ShowPendingResults</code> | <code>resultSummaryLabel.text</code> | <code>SummarizeRewards(pendingResults)</code> |
| [348](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:348>) | <code>ShowPendingResults</code> | <code>SetStatus</code> | <code>string.Empty</code> |
| [377](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:377>) | <code>PopulateResultGrid</code> | <code>UI.Text</code> | <code>RarityName(reward.rarity)</code> |
| [381](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:381>) | <code>PopulateResultGrid</code> | <code>UI.Text</code> | <code>reward.displayName</code> |
| [383](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:383>) | <code>PopulateResultGrid</code> | <code>UI.Text</code> | <code>reward.isDuplicate ? "DUPLICATE  " + reward.duplicateText : "NEW FIND!"</code> |
| [423](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:423>) | <code>ShowHistory</code> | <code>historyPageLabel.text</code> | <code>"Opening your lucky trail..."</code> |
| [432](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:432>) | <code>ShowHistory</code> | <code>historyPageLabel.text</code> | <code>"Couldn't load history. " + PlayerNotificationText.FromException(error)</code> |
| [445](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:445>) | <code>RenderHistory</code> | <code>historyPageLabel.text</code> | <code>"Page " + historyPage + " / " + pages</code> |
| [450](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:450>) | <code>RenderHistory</code> | <code>UI.Text</code> | <code>"A blank page, for now.\nYour first summon will appear here."</code> |
| [461](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:461>) | <code>RenderHistory</code> | <code>UI.Text</code> | <code>date + "  /  x" + roll.count + "  /  " + roll.totalCost.ToString("N0") + " " + roll.currency.ToLowerInvariant()</code> |
| [462](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:462>) | <code>RenderHistory</code> | <code>UI.Text</code> | <code>string.Join(", ", names)</code> |
| [509](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:509>) | <code>RefreshAll</code> | <code>goldLabels[i].text</code> | <code>wallet?.golds.ToString("N0") ?? "--"</code> |
| [511](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:511>) | <code>RefreshAll</code> | <code>diamondLabels[i].text</code> | <code>wallet?.diamonds.ToString("N0") ?? "--"</code> |
| [513](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:513>) | <code>RefreshAll</code> | <code>ticketLabels[i].text</code> | <code>wallet?.tickets.ToString("N0") ?? "--"</code> |
| [515](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:515>) | <code>RefreshAll</code> | <code>bannerLabel.text</code> | <code>activeBanner?.name ?? "A new surprise awaits"</code> |
| [516](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:516>) | <code>RefreshAll</code> | <code>selectorLabel.text</code> | <code>activeBanner?.name ?? "No banner loaded"</code> |
| [517](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:517>) | <code>RefreshAll</code> | <code>bannerDescription.text</code> | <code>activeBanner?.description ?? "Choose a banner to discover your next chess skin."</code> |
| [518](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:518>) | <code>RefreshAll</code> | <code>bannerTag.text</code> | <code>activeBanner?.costs?.Count &gt; 0 ? activeBanner.costs[0].currency.ToUpperInvariant() + " SUMMONS" : "A LITTLE MYSTERY"</code> |
| [519](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:519>) | <code>RefreshAll</code> | <code>bannerPageLabel.text</code> | <code>banners.Count == 0 ? "-- / --" : (bannerIndex + 1) + " / " + banners.Count</code> |
| [524](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:524>) | <code>RefreshAll</code> | <code>rateLabels[i].text</code> | <code>names[i] + " " + (rates[i]?.ToString("0.##") ?? "--") + "%"</code> |
| [525](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:525>) | <code>RefreshAll</code> | <code>summonCosts[i].text</code> | <code>CostLabel(i % 2 == 0 ? 1 : 10)</code> |
| [526](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:526>) | <code>RefreshAll</code> | <code>pityLabel.text</code> | <code>pity?.legendary == null ? "Pity -- / --" : $"Pity {pity.legendary.current} / {pity.legendary.limit}"</code> |
| [527](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:527>) | <code>RefreshAll</code> | <code>epicPityLabel.text</code> | <code>pity?.epic == null ? "-- / --" : $"{pity.epic.current} / {pity.epic.limit}"</code> |
| [566](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:566>) | <code>SetStatus</code> | <code>statusLabel.text</code> | <code>message</code> |
| [567](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.cs:567>) | <code>SetStatus</code> | <code>resultStatusLabel.text</code> | <code>message</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [22](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:22>) | <code>BuildMainScreen</code> | <code>UI.Text</code> | <code>"CHESS BUT WEIRD  /  A LITTLE LUCK, A LOT OF WEIRD"</code> |
| [24](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:24>) | <code>BuildMainScreen</code> | <code>UI.Text</code> | <code>"Chaos Gacha"</code> |
| [29](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:29>) | <code>BuildMainScreen</code> | <code>UI.Text</code> | <code>"PICK YOUR LUCK"</code> |
| [30](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:30>) | <code>BuildMainScreen</code> | <code>UI.Text</code> | <code>"Loading banners..."</code> |
| [31](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:31>) | <code>BuildMainScreen</code> | <code>UI.Button</code> | <code>"&lt;"</code> |
| [32](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:32>) | <code>BuildMainScreen</code> | <code>UI.Text</code> | <code>"-- / --"</code> |
| [33](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:33>) | <code>BuildMainScreen</code> | <code>UI.Button</code> | <code>"&gt;"</code> |
| [39](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:39>) | <code>BuildMainScreen</code> | <code>UI.Text</code> | <code>"THE NEXT FIND IS A MYSTERY"</code> |
| [40](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:40>) | <code>BuildMainScreen</code> | <code>UI.Text</code> | <code>"A new surprise awaits"</code> |
| [50](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:50>) | <code>BuildMainScreen</code> | <code>UI.Text</code> | <code>rarities[i] + " --"</code> |
| [52](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:52>) | <code>BuildMainScreen</code> | <code>UI.Text</code> | <code>"A new piece of your collection starts here."</code> |
| [55](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:55>) | <code>BuildMainScreen</code> | <code>UI.Button</code> | <code>"HISTORY"</code> |
| [56](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:56>) | <code>BuildMainScreen</code> | <code>UI.Button</code> | <code>"DETAILS"</code> |
| [57](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:57>) | <code>BuildMainScreen</code> | <code>UI.Button</code> | <code>"&lt;  BACK TO MENU"</code> |
| [67](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:67>) | <code>BuildSummonButton</code> | <code>UI.Text</code> | <code>"SUMMON x" + count</code> |
| [68](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:68>) | <code>BuildSummonButton</code> | <code>UI.Text</code> | <code>"--"</code> |
| [76](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:76>) | <code>BuildResultScreen</code> | <code>UI.Text</code> | <code>"FRESH FROM THE SKETCHBOOK"</code> |
| [78](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:78>) | <code>BuildResultScreen</code> | <code>UI.Text</code> | <code>"Look what you found!"</code> |
| [82](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:82>) | <code>BuildResultScreen</code> | <code>UI.Button</code> | <code>"&lt;  BACK TO BANNER"</code> |
| [98](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:98>) | <code>AddPityPanel</code> | <code>UI.Text</code> | <code>"Luck is adding up..."</code> |
| [99](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:99>) | <code>AddPityPanel</code> | <code>UI.Text</code> | <code>"LEGENDARY"</code> |
| [100](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:100>) | <code>AddPityPanel</code> | <code>UI.Text</code> | <code>"Pity -- / --"</code> |
| [104](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:104>) | <code>AddPityPanel</code> | <code>UI.Text</code> | <code>"EPIC"</code> |
| [105](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:105>) | <code>AddPityPanel</code> | <code>UI.Text</code> | <code>"-- / --"</code> |
| [120](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:120>) | <code>CreateOverlay</code> | <code>UI.Text</code> | <code>title</code> |
| [121](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:121>) | <code>CreateOverlay</code> | <code>UI.Button</code> | <code>"X"</code> |
| [127](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:127>) | <code>BuildHistory</code> | <code>CreateOverlay</code> | <code>"Your lucky trail"</code> |
| [128](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:128>) | <code>BuildHistory</code> | <code>UI.Text</code> | <code>"Every summon, saved in your notebook. Scroll to see more."</code> |
| [130](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:130>) | <code>BuildHistory</code> | <code>UI.Button</code> | <code>"&lt;  PREVIOUS"</code> |
| [131](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:131>) | <code>BuildHistory</code> | <code>UI.Button</code> | <code>"NEXT  &gt;"</code> |
| [138](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:138>) | <code>BuildBannerDetails</code> | <code>CreateOverlay</code> | <code>"The fine scribbles"</code> |
| [139](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:139>) | <code>BuildBannerDetails</code> | <code>UI.Text</code> | <code>"What's inside, what it costs, and how luck works."</code> |
| [145](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaMenuController.Layout.cs:145>) | <code>BuildBannerDetails</code> | <code>UI.Text</code> | <code>"SCROLL FOR THE FULL REWARD LIST"</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/GachaSummonRevealController.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [156](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaSummonRevealController.cs:156>) | <code>BuildOverlay</code> | <code>titleLabel.text</code> | <code>"SUMMON"</code> |
| [176](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaSummonRevealController.cs:176>) | <code>BuildSkipButton</code> | <code>skipButtonLabel.text</code> | <code>"SKIP"</code> |
| [185](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaSummonRevealController.cs:185>) | <code>ConfigureReward</code> | <code>rarityLabel.text</code> | <code>new string('*', Mathf.Clamp(reward.rarity, 1, 5))</code> |
| [186](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaSummonRevealController.cs:186>) | <code>ConfigureReward</code> | <code>detailLabel.text</code> | <code>reward.AmountText</code> |
| [188](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaSummonRevealController.cs:188>) | <code>ConfigureReward</code> | <code>sequenceLabel.text</code> | <code>total &gt; 1 ? $"{index + 1}/{total}" : string.Empty</code> |
| [189](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaSummonRevealController.cs:189>) | <code>ConfigureReward</code> | <code>titleLabel.text</code> | <code>currentTopTier ? "TOP TIER SUMMON" : reward.rarity &gt;= 4 ? "RARE SUMMON" : "SUMMON"</code> |
| [222](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaSummonRevealController.cs:222>) | <code>PlaySequenceRoutine</code> | <code>artworkLabel.text</code> | <code>!string.IsNullOrWhiteSpace(reward.itemId) &amp;&amp; !modelPreview.HasModel &amp;&amp; !icon<br>                ? "Artwork unavailable\n" + reward.itemId : ""</code> |
| [554](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GachaSummonRevealController.cs:554>) | <code></code> | <code>this.text</code> | <code>text</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/GuestAccessWarning.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [28](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GuestAccessWarning.cs:28>) | <code>Show</code> | <code>warning.messageLabel.text</code> | <code>message</code> |
| [92](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GuestAccessWarning.cs:92>) | <code>Build</code> | <code>Label</code> | <code>"Guest access limited"</code> |
| [95](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GuestAccessWarning.cs:95>) | <code>Build</code> | <code>Label</code> | <code>"You can still play against the bot with the default set."</code> |
| [106](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GuestAccessWarning.cs:106>) | <code>Build</code> | <code>Label</code> | <code>"Continue as Guest"</code> |
| [133](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/GuestAccessWarning.cs:133>) | <code>Label</code> | <code>text.text</code> | <code>value</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [258](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:258>) | <code>BuildLocalModeScreen</code> | <code>AddText</code> | <code>"Local Mode"</code> |
| [259](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:259>) | <code>BuildLocalModeScreen</code> | <code>AddTextButton</code> | <code>"Vs Bot"</code> |
| [260](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:260>) | <code>BuildLocalModeScreen</code> | <code>AddTextButton</code> | <code>"2 Players"</code> |
| [269](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:269>) | <code>BuildAramModeScreen</code> | <code>AddText</code> | <code>"CHAOS Mode"</code> |
| [270](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:270>) | <code>BuildAramModeScreen</code> | <code>AddTextButton</code> | <code>"Practice"</code> |
| [271](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:271>) | <code>BuildAramModeScreen</code> | <code>AddTextButton</code> | <code>"LAN"</code> |
| [272](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:272>) | <code>BuildAramModeScreen</code> | <code>AddTextButton</code> | <code>"Multiplayer"</code> |
| [273](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:273>) | <code>BuildAramModeScreen</code> | <code>AddText</code> | <code>"Practice local"</code> |
| [274](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:274>) | <code>BuildAramModeScreen</code> | <code>AddText</code> | <code>"Private LAN room"</code> |
| [275](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:275>) | <code>BuildAramModeScreen</code> | <code>AddText</code> | <code>"Online matchmaking"</code> |
| [519](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:519>) | <code>BuildSideScreen</code> | <code>UI.Text</code> | <code>"PVE / BOT RUSH / CHOOSE YOUR SIDE"</code> |
| [520](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:520>) | <code>BuildSideScreen</code> | <code>UI.Text</code> | <code>"White or black?"</code> |
| [521](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:521>) | <code>BuildSideScreen</code> | <code>UI.Text</code> | <code>"Your loadout is ready. Choose a side to start the game."</code> |
| [528](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:528>) | <code>BuildSideScreen</code> | <code>UI.Text</code> | <code>team == PieceTeam.White ? "WHITE" : "BLACK"</code> |
| [529](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:529>) | <code>BuildSideScreen</code> | <code>UI.Text</code> | <code>i == 0 ? "You make the first move." : "Your opponent moves first."</code> |
| [530](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:530>) | <code>BuildSideScreen</code> | <code>UI.Text</code> | <code>"PLAY AS " + team.ToString().ToUpperInvariant() + "  &gt;"</code> |
| [532](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:532>) | <code>BuildSideScreen</code> | <code>UI.Button</code> | <code>"&lt;  BACK"</code> |
| [724](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:724>) | <code>AddTextButton</code> | <code>AddText</code> | <code>label</code> |
| [758](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs:758>) | <code>AddText</code> | <code>text.text</code> | <code>textValue</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [73](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:73>) | <code>Build</code> | <code>UI.Text</code> | <code>"CHESS BUT WEIRD  /  YOUR COLLECTION"</code> |
| [75](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:75>) | <code>Build</code> | <code>UI.Text</code> | <code>"Inventory"</code> |
| [76](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:76>) | <code>Build</code> | <code>UI.Button</code> | <code>"&lt;  BACK"</code> |
| [78](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:78>) | <code>Build</code> | <code>UI.Text</code> | <code>"COLLECTION"</code> |
| [84](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:84>) | <code>Build</code> | <code>UI.Button</code> | <code>labels[i]</code> |
| [87](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:87>) | <code>Build</code> | <code>UI.Button</code> | <code>"FILTERS"</code> |
| [91](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:91>) | <code>Build</code> | <code>UI.Button</code> | <code>"&lt;"</code> |
| [93](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:93>) | <code>Build</code> | <code>UI.Button</code> | <code>"&gt;"</code> |
| [113](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:113>) | <code>BuildSearch</code> | <code>UI.Text</code> | <code>"Search collection..."</code> |
| [130](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:130>) | <code>BuildFilters</code> | <code>UI.Text</code> | <code>"FILTER COLLECTION"</code> |
| [131](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:131>) | <code>BuildFilters</code> | <code>UI.Button</code> | <code>"x"</code> |
| [132](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:132>) | <code>BuildFilters</code> | <code>UI.Text</code> | <code>"SHOW"</code> |
| [138](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:138>) | <code>BuildFilters</code> | <code>UI.Button</code> | <code>ownershipLabels[i]</code> |
| [141](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:141>) | <code>BuildFilters</code> | <code>UI.Button</code> | <code>sortLabels[i]</code> |
| [144](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:144>) | <code>BuildFilters</code> | <code>UI.Text</code> | <code>"TAGS"</code> |
| [146](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:146>) | <code>BuildFilters</code> | <code>UI.Text</code> | <code>"SORT BY"</code> |
| [147](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:147>) | <code>BuildFilters</code> | <code>UI.Button</code> | <code>"RESET"</code> |
| [152](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:152>) | <code>BuildFilters</code> | <code>UI.Button</code> | <code>"DONE"</code> |
| [187](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:187>) | <code>RebuildTags</code> | <code>UI.Button</code> | <code>value</code> |
| [261](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:261>) | <code>RefreshView</code> | <code>UI.Text</code> | <code>loading ? "Loading collection..." : "No items match these filters."</code> |
| [263](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:263>) | <code>RefreshView</code> | <code>countLabel.text</code> | <code>visible.Count + (visible.Count == 1 ? " ITEM" : " ITEMS")</code> |
| [265](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:265>) | <code>RefreshView</code> | <code>pageLabel.text</code> | <code>"PAGE " + (page + 1) + " / " + pages</code> |
| [268](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:268>) | <code>RefreshView</code> | <code>filterSummary.text</code> | <code>ownership.ToString().ToUpperInvariant() + (selectedTags.Count &gt; 0 ? "  /  " + selectedTags.Count + " TAGS" : "")</code> |
| [279](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:279>) | <code>AddItemCard</code> | <code>UI.Text</code> | <code>item.rarity ?? "COMMON"</code> |
| [280](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:280>) | <code>AddItemCard</code> | <code>UI.Text</code> | <code>item.name</code> |
| [281](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:281>) | <code>AddItemCard</code> | <code>UI.Text</code> | <code>ItemCategory(item).ToString().ToUpperInvariant()</code> |
| [282](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:282>) | <code>AddItemCard</code> | <code>UI.Text</code> | <code>item.isEquipped ? "EQUIPPED" : !item.owned ? "LOCKED" : !item.isActive ? "UNAVAILABLE" : "OWNED"</code> |
| [303](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:303>) | <code>RefreshFromServer</code> | <code>SetStatus</code> | <code>"Sign in to load your skins."</code> |
| [304](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:304>) | <code>RefreshFromServer</code> | <code>SetStatus</code> | <code>"Loading collection..."</code> |
| [316](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:316>) | <code>RefreshFromServer</code> | <code>SetStatus</code> | <code>"Could not load inventory: " + PlayerNotificationText.FromException(error)</code> |
| [364](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/InventoryMenuController.cs:364>) | <code>SetStatus</code> | <code>statusLabel.text</code> | <code>value ?? ""</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [31](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:31>) | <code>Build</code> | <code>Label</code> | <code>"a very questionable chess club"</code> |
| [34](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:34>) | <code>Build</code> | <code>Label</code> | <code>"EARLY ACCESS"</code> |
| [49](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:49>) | <code>Build</code> | <code>Label</code> | <code>"Same board. Questionable decisions."</code> |
| [53](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:53>) | <code>Build</code> | <code>MenuButton</code> | <code>"START"</code> |
| [54](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:54>) | <code>Build</code> | <code>MenuButton</code> | <code>"SETTINGS"</code> |
| [55](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:55>) | <code>Build</code> | <code>MenuButton</code> | <code>"CREDITS"</code> |
| [56](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:56>) | <code>Build</code> | <code>MenuButton</code> | <code>"LOG OUT"</code> |
| [64](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:64>) | <code>Build</code> | <code>Label</code> | <code>"royally unqualified."</code> |
| [67](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:67>) | <code>Build</code> | <code>Label</code> | <code>"?"</code> |
| [70](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:70>) | <code>Build</code> | <code>Label</code> | <code>"Play well. Respect others. Stay weird."</code> |
| [71](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:71>) | <code>Build</code> | <code>Label</code> | <code>CommunityContent.VersionLabel</code> |
| [80](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:80>) | <code>Label</code> | <code>UI.Text</code> | <code>text</code> |
| [121](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:121>) | <code>MenuButton</code> | <code>Label</code> | <code>caption</code> |
| [157](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:157>) | <code>BuildCredits</code> | <code>Label</code> | <code>"THE WEIRD PEOPLE"</code> |
| [160](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:160>) | <code>BuildCredits</code> | <code>Label</code> | <code>copy ? copy.text.Trim() : "Chess But Weird\n\nThanks for playing. Stay weird."</code> |
| [164](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs:164>) | <code>BuildCredits</code> | <code>MenuButton</code> | <code>"BACK TO THE WEIRD"</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/MatchFoundConfirmationView.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [20](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchFoundConfirmationView.cs:20>) | <code>Build</code> | <code>UI.Text</code> | <code>"MATCH FOUND"</code> |
| [21](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchFoundConfirmationView.cs:21>) | <code>Build</code> | <code>UI.Text</code> | <code>"Are you sure??"</code> |
| [23](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchFoundConfirmationView.cs:23>) | <code>Build</code> | <code>UI.Text</code> | <code>"30"</code> |
| [25](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchFoundConfirmationView.cs:25>) | <code>Build</code> | <code>UI.Button</code> | <code>"NO"</code> |
| [26](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchFoundConfirmationView.cs:26>) | <code>Build</code> | <code>UI.Button</code> | <code>"YES"</code> |
| [34](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchFoundConfirmationView.cs:34>) | <code>SetState</code> | <code>opponent.text</code> | <code>"Opponent: " + name</code> |
| [35](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchFoundConfirmationView.cs:35>) | <code>SetState</code> | <code>countdown.text</code> | <code>Mathf.CeilToInt(Mathf.Max(0, seconds)).ToString("00")</code> |
| [36](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchFoundConfirmationView.cs:36>) | <code>SetState</code> | <code>status.text</code> | <code>accepted ? "Accepted! Waiting for your opponent..." : "Confirm before the countdown ends."</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/MatchHudStyle.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [29](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchHudStyle.cs:29>) | <code>Text</code> | <code>text.text</code> | <code>value</code> |
| [38](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchHudStyle.cs:38>) | <code>Button</code> | <code>Text</code> | <code>value</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/MatchHudTooltip.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [27](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchHudTooltip.cs:27>) | <code>Show</code> | <code>title.text</code> | <code>heading</code> |
| [27](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchHudTooltip.cs:27>) | <code>Show</code> | <code>body.text</code> | <code>description</code> |
| [66](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchHudTooltip.cs:66>) | <code>Configure</code> | <code>tooltip.Show</code> | <code>heading</code> |
| [66](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchHudTooltip.cs:66>) | <code>Configure</code> | <code>tooltip.Show</code> | <code>description</code> |
| [69](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchHudTooltip.cs:69>) | <code>Update</code> | <code>tooltip.Show</code> | <code>heading</code> |
| [69](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchHudTooltip.cs:69>) | <code>Update</code> | <code>tooltip.Show</code> | <code>description</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [64](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:64>) | <code>OpenOnline</code> | <code>startButton.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>actionLabel</code> |
| [69](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:69>) | <code>ConfigureLobby</code> | <code>modeLabel.text</code> | <code>"CHESS BUT WEIRD / " + mode</code> |
| [72](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:72>) | <code>ConfigureLobby</code> | <code>startButton.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>action</code> |
| [73](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:73>) | <code>ConfigureLobby</code> | <code>sideLabel.text</code> | <code>onlineMode ? "Side: RANDOM  /  Board: RANDOM from both loadouts" : description ?? "Choose your loadout. Your equipment is saved before matchmaking."</code> |
| [86](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:86>) | <code>SetMatchmakingState</code> | <code>startButton.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>searching ? elapsed + "   X" : locked ? "PLEASE WAIT" : actionLabel</code> |
| [97](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:97>) | <code>SetOnlineContext</code> | <code>networkNotice.text</code> | <code>message</code> |
| [137](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:137>) | <code>OpenPicker</code> | <code>startButton.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>"START MATCH"</code> |
| [147](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:147>) | <code>Build</code> | <code>UI.Text</code> | <code>"CHESS BUT WEIRD  /  BEFORE THE MATCH"</code> |
| [148](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:148>) | <code>Build</code> | <code>UI.Text</code> | <code>"Choose your loadout"</code> |
| [149](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:149>) | <code>Build</code> | <code>UI.Text</code> | <code>"Pick a full set or customize each piece. Board skins are independent."</code> |
| [150](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:150>) | <code>Build</code> | <code>UI.Button</code> | <code>"WHITE SIDE"</code> |
| [151](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:151>) | <code>Build</code> | <code>UI.Button</code> | <code>"BLACK SIDE"</code> |
| [161](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:161>) | <code>Build</code> | <code>UI.Button</code> | <code>"&lt;  BACK"</code> |
| [162](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:162>) | <code>Build</code> | <code>UI.Button</code> | <code>"REFRESH"</code> |
| [164](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:164>) | <code>Build</code> | <code>UI.Button</code> | <code>"START MATCH"</code> |
| [173](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:173>) | <code>Build</code> | <code>UI.Text</code> | <code>"Your collection"</code> |
| [174](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:174>) | <code>Build</code> | <code>UI.Button</code> | <code>"CLOSE"</code> |
| [179](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:179>) | <code>Build</code> | <code>UI.Button</code> | <code>categories[i]</code> |
| [183](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:183>) | <code>Build</code> | <code>UI.Text</code> | <code>"Scroll to browse tiers"</code> |
| [198](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:198>) | <code>RefreshSideButtons</code> | <code>sideLabel.text</code> | <code>onlineMode ? "Side: RANDOM  /  Board: RANDOM from both loadouts" :<br>            "Your side: " + playerTeam + "   ·   Bot uses its default pieces"</code> |
| [231](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:231>) | <code>LoadOwnedChoices</code> | <code>status.text</code> | <code>accountReady ? "Default loadout is ready. Sign in to use collected skins." : "Sign in to load your collection."</code> |
| [234](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:234>) | <code>LoadOwnedChoices</code> | <code>status.text</code> | <code>"Loading your collection..."</code> |
| [281](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:281>) | <code>LoadOwnedChoices</code> | <code>status.text</code> | <code>"Choose your cosmetics, then continue."</code> |
| [287](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:287>) | <code>LoadOwnedChoices</code> | <code>status.text</code> | <code>"Couldn't load your collection. " + (onlineMode ? "Press Refresh to retry. " : "Default items are ready. ") + PlayerNotificationText.FromException(error)</code> |
| [368](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:368>) | <code>RenderSummary</code> | <code>SummaryCard</code> | <code>"BOARD"</code> |
| [369](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:369>) | <code>RenderSummary</code> | <code>pieceModeButton.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>selection.customPieces ? "CUSTOM PIECES  /  SWITCH TO SET" : "FULL SET  /  CUSTOMIZE PIECES"</code> |
| [372](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:372>) | <code>RenderSummary</code> | <code>pieceModeButton.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>"CUSTOM ONLINE REQUIRES UPDATED SERVER"</code> |
| [374](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:374>) | <code>RenderSummary</code> | <code>SummaryCard</code> | <code>"CHESS SET"</code> |
| [386](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:386>) | <code>RenderSummary</code> | <code>UI.Text</code> | <code>onlineMode ? kind.ToString() : playerTeam + " " + kind</code> |
| [387](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:387>) | <code>RenderSummary</code> | <code>UI.Text</code> | <code>choice?.name ?? "Base set"</code> |
| [392](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:392>) | <code>RenderSummary</code> | <code>effectButton.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>"MOVE EFFECT  /  " + (effect?.name ?? "Classic") + "  &gt;"</code> |
| [393](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:393>) | <code>RenderSummary</code> | <code>backgroundButton.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>"BACKGROUND  /  " + (FindChoice(CosmeticItemKinds.Background, selection.environmentId)?.name ?? "Classic") + "  &gt;"</code> |
| [399](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:399>) | <code>SummaryCard</code> | <code>UI.Text</code> | <code>title</code> |
| [401](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:401>) | <code>SummaryCard</code> | <code>UI.Text</code> | <code>choice?.name ?? "Default"</code> |
| [417](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:417>) | <code>RenderChoices</code> | <code>UI.Text</code> | <code>group.Key + "  /  " + group.Count()</code> |
| [426](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:426>) | <code>RenderChoices</code> | <code>UI.Text</code> | <code>choice.name</code> |
| [427](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:427>) | <code>RenderChoices</code> | <code>UI.Text</code> | <code>choice.owned ? active ? "Selected for this match" : "Owned" : "Locked"</code> |
| [447](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:447>) | <code>Submit</code> | <code>status.text</code> | <code>"Saving equipment..."</code> |
| [486](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchLoadoutController.cs:486>) | <code>Submit</code> | <code>status.text</code> | <code>PlayerNotificationText.FromException(error)</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [16](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:16>) | <code></code> | <code>UI.Text</code> | <code>context.Heading</code> |
| [17](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:17>) | <code></code> | <code>UI.Text</code> | <code>"Meet your opponent"</code> |
| [19](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:19>) | <code></code> | <code>UI.Text</code> | <code>$"{PlayModeNames.DisplayBackend(settings.mode).ToUpperInvariant()}   /   {settings.initialSeconds / 60} MIN + {settings.incrementSeconds} SEC   /   {settings.region}"</code> |
| [24](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:24>) | <code></code> | <code>UI.Text</code> | <code>"VS"</code> |
| [25](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:25>) | <code></code> | <code>UI.Text</code> | <code>"Make your next move count."</code> |
| [44](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:44>) | <code></code> | <code>UI.Text</code> | <code>color + " SIDE"</code> |
| [46](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:46>) | <code></code> | <code>UI.Text</code> | <code>"?"</code> |
| [56](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:56>) | <code></code> | <code>UI.Text</code> | <code>"CHESS SET"</code> |
| [57](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:57>) | <code></code> | <code>UI.Text</code> | <code>"ARENA"</code> |
| [65](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:65>) | <code>Bind</code> | <code>name.text</code> | <code>"Waiting for player"</code> |
| [72](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:72>) | <code>Bind</code> | <code>order.text</code> | <code>placed ? (definition?.order ?? RankCardView.TierOrder(rank.tierId)).ToString() : "?"</code> |
| [73](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:73>) | <code>Bind</code> | <code>name.text</code> | <code>string.IsNullOrWhiteSpace(player.displayName) ? player.username : player.displayName</code> |
| [74](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:74>) | <code>Bind</code> | <code>username.text</code> | <code>(local ? "YOU   /   " : "") + "@" + player.username</code> |
| [75](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:75>) | <code>Bind</code> | <code>rankName.text</code> | <code>rank == null ? "Rank unavailable" : rank.provisional ? "Unranked" : RankCardView.TierName(match.State.settings.mode, rank.tierId)</code> |
| [77](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:77>) | <code>Bind</code> | <code>rating.text</code> | <code>$"{player.rating:N0} rating" + (rank?.provisional == true ? $"   /   Placement {rank.placementPlayed}/{rank.placementRequired}" : "")</code> |
| [78](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:78>) | <code>Bind</code> | <code>state.text</code> | <code>match.State.status == "Cancelled" ? "Match cancelled" : !player.connected ? "Reconnecting..." : player.ready ? "Ready" :<br>                local ? progress &gt;= 1 ? "Content ready" : "Preparing your board..." : "Connected / preparing match"</code> |
| [90](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MatchVersusView.cs:90>) | <code>BindCosmetic</code> | <code>label.text</code> | <code>entry?.displayName ?? selected?.code ?? "Default"</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/MenuMusicWidget.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [25](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MenuMusicWidget.cs:25>) | <code>Build</code> | <code>UI.Text</code> | <code>"NOW PLAYING"</code> |
| [26](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MenuMusicWidget.cs:26>) | <code>Build</code> | <code>UI.Text</code> | <code>"CBW Official"</code> |
| [48](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MenuMusicWidget.cs:48>) | <code>Build</code> | <code>UI.Text</code> | <code>"SOUNDTRACK"</code> |
| [49](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MenuMusicWidget.cs:49>) | <code>Build</code> | <code>UI.Button</code> | <code>"x"</code> |
| [105](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MenuMusicWidget.cs:105>) | <code>RefreshPack</code> | <code>packName.text</code> | <code>GameMusicManager.GetPackDisplayName(pack)</code> |
| [109](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/MenuMusicWidget.cs:109>) | <code>RefreshPack</code> | <code>optionLabels[i].text</code> | <code>GameMusicManager.GetPackDisplayName(value) + (value == pack ? "  /  ON" : "")</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [41](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:41>) | <code>Build</code> | <code>Label</code> | <code>"YOUR NEXT MOVE"</code> |
| [42](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:42>) | <code>Build</code> | <code>Label</code> | <code>"Play a little. Get weird a lot."</code> |
| [44](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:44>) | <code>Build</code> | <code>Label</code> | <code>"MAKE YOUR MOVE"</code> |
| [47](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:47>) | <code>Build</code> | <code>UI.Button</code> | <code>"MAILBOX"</code> |
| [48](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:48>) | <code>Build</code> | <code>UI.Button</code> | <code>"NEWS"</code> |
| [49](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:49>) | <code>Build</code> | <code>ModeCard</code> | <code>"PLAY"</code> |
| [49](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:49>) | <code>Build</code> | <code>ModeCard</code> | <code>"Pick your next battle"</code> |
| [49](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:49>) | <code>Build</code> | <code>ModeCard</code> | <code>"PVE · CHAOS · CLASSIC"</code> |
| [51](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:51>) | <code>Build</code> | <code>ModeCard</code> | <code>"GACHA"</code> |
| [51](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:51>) | <code>Build</code> | <code>ModeCard</code> | <code>"Unbox something strange"</code> |
| [51](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:51>) | <code>Build</code> | <code>ModeCard</code> | <code>"Discover your next favourite."</code> |
| [53](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:53>) | <code>Build</code> | <code>ModeCard</code> | <code>"INVENTORY"</code> |
| [53](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:53>) | <code>Build</code> | <code>ModeCard</code> | <code>"Your collected treasures"</code> |
| [53](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:53>) | <code>Build</code> | <code>ModeCard</code> | <code>"Board, chess and move effects."</code> |
| [56](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:56>) | <code>Build</code> | <code>Label</code> | <code>"PROFILE"</code> |
| [58](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:58>) | <code>Build</code> | <code>Label</code> | <code>"SETTINGS"</code> |
| [60](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:60>) | <code>Build</code> | <code>Label</code> | <code>"SHOP"</code> |
| [63](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:63>) | <code>Build</code> | <code>Label</code> | <code>"BACK"</code> |
| [66](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:66>) | <code>Build</code> | <code>Label</code> | <code>"LOG OUT"</code> |
| [67](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:67>) | <code>Build</code> | <code>Label</code> | <code>"Play well. Respect others. Stay weird."</code> |
| [68](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:68>) | <code>Build</code> | <code>Label</code> | <code>CommunityContent.VersionLabel</code> |
| [84](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:84>) | <code>ModeCard</code> | <code>Label</code> | <code>title</code> |
| [87](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:87>) | <code>ModeCard</code> | <code>Label</code> | <code>description</code> |
| [89](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:89>) | <code>ModeCard</code> | <code>Label</code> | <code>note</code> |
| [123](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs:123>) | <code>Label</code> | <code>UI.Text</code> | <code>text</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/PieceHoverTooltip.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [65](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PieceHoverTooltip.cs:65>) | <code>LateUpdate</code> | <code>label.text</code> | <code>piece.Team + " " + piece.Type + (prisoner ? " · Prisoner" : "")</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [40](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:40>) | <code>Open</code> | <code>SetStatus</code> | <code>"Opening your notebook..."</code> |
| [56](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:56>) | <code>Open</code> | <code>SetStatus</code> | <code>"All caught up. Ready for your next move!"</code> |
| [60](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:60>) | <code>Open</code> | <code>SetStatus</code> | <code>"Couldn't update your profile. " + PlayerNotificationText.FromException(error)</code> |
| [76](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:76>) | <code>Build</code> | <code>UI.Text</code> | <code>"CHESS BUT WEIRD  /  MY LITTLE CORNER"</code> |
| [78](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:78>) | <code>Build</code> | <code>UI.Text</code> | <code>"Player notebook"</code> |
| [80](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:80>) | <code>Build</code> | <code>UI.Button</code> | <code>"REFRESH"</code> |
| [86](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:86>) | <code>Build</code> | <code>UI.Text</code> | <code>"DEFAULT AVATAR"</code> |
| [87](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:87>) | <code>Build</code> | <code>UI.Text</code> | <code>"--"</code> |
| [88](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:88>) | <code>Build</code> | <code>UI.Text</code> | <code>"--"</code> |
| [90](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:90>) | <code>Build</code> | <code>UI.Text</code> | <code>"--"</code> |
| [91](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:91>) | <code>Build</code> | <code>UI.Text</code> | <code>"EMAIL"</code> |
| [92](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:92>) | <code>Build</code> | <code>UI.Text</code> | <code>"--"</code> |
| [93](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:93>) | <code>Build</code> | <code>UI.Text</code> | <code>"PLAYER ID"</code> |
| [94](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:94>) | <code>Build</code> | <code>UI.Text</code> | <code>"--"</code> |
| [97](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:97>) | <code>Build</code> | <code>UI.Text</code> | <code>"My scorecard"</code> |
| [98](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:98>) | <code>Build</code> | <code>UI.Text</code> | <code>"every move tells a story"</code> |
| [105](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:105>) | <code>Build</code> | <code>UI.Text</code> | <code>"--"</code> |
| [106](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:106>) | <code>Build</code> | <code>UI.Text</code> | <code>captions[i]</code> |
| [115](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:115>) | <code>Build</code> | <code>UI.Text</code> | <code>"Ready for the next match"</code> |
| [117](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:117>) | <code>Build</code> | <code>UI.Text</code> | <code>"CHESS SET"</code> |
| [118](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:118>) | <code>Build</code> | <code>UI.Text</code> | <code>"--"</code> |
| [120](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:120>) | <code>Build</code> | <code>UI.Text</code> | <code>"ARENA"</code> |
| [121](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:121>) | <code>Build</code> | <code>UI.Text</code> | <code>"--"</code> |
| [123](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:123>) | <code>Build</code> | <code>UI.Button</code> | <code>"&lt;  BACK TO MENU"</code> |
| [129](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:129>) | <code>Refresh</code> | <code>nameLabel.text</code> | <code>string.IsNullOrWhiteSpace(user?.profile?.displayName) ? user?.username ?? "--" : user.profile.displayName</code> |
| [130](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:130>) | <code>Refresh</code> | <code>usernameLabel.text</code> | <code>string.IsNullOrWhiteSpace(user?.username) ? "--" : "@" + user.username</code> |
| [131](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:131>) | <code>Refresh</code> | <code>emailLabel.text</code> | <code>user?.email ?? "--"</code> |
| [132](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:132>) | <code>Refresh</code> | <code>idLabel.text</code> | <code>user?.userId ?? "--"</code> |
| [133](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:133>) | <code>Refresh</code> | <code>avatarLabel.text</code> | <code>string.IsNullOrWhiteSpace(user?.profile?.avatarId) ? "DEFAULT AVATAR" : "AVATAR  " + user.profile.avatarId</code> |
| [134](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:134>) | <code>Refresh</code> | <code>joinedLabel.text</code> | <code>DateTimeOffset.TryParse(user?.createdAt, out var joined)<br>            ? "Here since " + joined.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) : "Member since --"</code> |
| [137](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:137>) | <code>Refresh</code> | <code>stats[i].text</code> | <code>values[i]?.ToString("N0", CultureInfo.InvariantCulture) ?? "--"</code> |
| [138](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:138>) | <code>Refresh</code> | <code>wallet[0].text</code> | <code>user?.wallet?.golds.ToString("N0", CultureInfo.InvariantCulture) ?? "--"</code> |
| [139](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:139>) | <code>Refresh</code> | <code>wallet[1].text</code> | <code>user?.wallet?.diamonds.ToString("N0", CultureInfo.InvariantCulture) ?? "--"</code> |
| [140](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:140>) | <code>Refresh</code> | <code>wallet[2].text</code> | <code>user?.wallet?.tickets.ToString("N0", CultureInfo.InvariantCulture) ?? "--"</code> |
| [141](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:141>) | <code>Refresh</code> | <code>chessLabel.text</code> | <code>user?.equipped?.chessSkinId == "6aacc041e50e17c25a690158" ? "Tazji's Low Poly" : user?.equipped?.chessSkinId ?? "--"</code> |
| [142](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:142>) | <code>Refresh</code> | <code>boardLabel.text</code> | <code>user?.equipped?.boardSkinId == "6aacc04be50e17c25a69015a" ? "Tazji's Low Poly Arena" : user?.equipped?.boardSkinId ?? "--"</code> |
| [147](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayerProfileMenuController.cs:147>) | <code>SetStatus</code> | <code>statusLabel.text</code> | <code>message ?? string.Empty</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [43](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:43>) | <code>Build</code> | <code>UI.Text</code> | <code>"CHESS BUT WEIRD / PLAY"</code> |
| [44](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:44>) | <code>Build</code> | <code>UI.Text</code> | <code>"Choose your game"</code> |
| [45](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:45>) | <code>Build</code> | <code>UI.Text</code> | <code>"Open a category, tick a mode, then press Play."</code> |
| [55](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:55>) | <code>Build</code> | <code>UI.Text</code> | <code>titles[i]</code> |
| [58](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:58>) | <code>Build</code> | <code>UI.Text</code> | <code>captions[i]</code> |
| [65](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:65>) | <code>Build</code> | <code>AddOption</code> | <code>caption</code> |
| [66](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:66>) | <code>Build</code> | <code>AddOption</code> | <code>"Browser room"</code> |
| [67](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:67>) | <code>Build</code> | <code>AddOption</code> | <code>"Ranked"</code> |
| [69](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:69>) | <code>Build</code> | <code>UI.Text</code> | <code>"Choose a mode to continue"</code> |
| [70](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:70>) | <code>Build</code> | <code>UI.Button</code> | <code>"&lt;  BACK"</code> |
| [71](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:71>) | <code>Build</code> | <code>UI.Button</code> | <code>"PLAY  &gt;"</code> |
| [78](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:78>) | <code>AddOption</code> | <code>ModeRow</code> | <code>caption</code> |
| [80](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:80>) | <code>AddOption</code> | <code>chosen.text</code> | <code>PlayModeNames.Label(option)</code> |
| [81](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:81>) | <code>AddOption</code> | <code>chosen.text</code> | <code>"Choose a mode to continue"</code> |
| [97](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PlayModeSelectionView.cs:97>) | <code>ModeRow</code> | <code>UI.Text</code> | <code>caption</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/PromotionMenuView.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [29](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PromotionMenuView.cs:29>) | <code>Initialize</code> | <code>UI.Text</code> | <code>"WHITE / PAWN PROMOTION"</code> |
| [30](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PromotionMenuView.cs:30>) | <code>Initialize</code> | <code>UI.Text</code> | <code>"A pawn, a new possibility"</code> |
| [31](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PromotionMenuView.cs:31>) | <code>Initialize</code> | <code>UI.Text</code> | <code>"Your pawn reached the final rank. Choose its new piece."</code> |
| [41](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PromotionMenuView.cs:41>) | <code>Initialize</code> | <code>UI.Text</code> | <code>type.ToString().ToUpperInvariant()</code> |
| [42](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PromotionMenuView.cs:42>) | <code>Initialize</code> | <code>UI.Text</code> | <code>descriptions[i]</code> |
| [47](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PromotionMenuView.cs:47>) | <code>Initialize</code> | <code>UI.Text</code> | <code>"Select a piece to continue the match."</code> |
| [55](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/PromotionMenuView.cs:55>) | <code>Show</code> | <code>side.text</code> | <code>team.ToString().ToUpperInvariant() + " / PAWN PROMOTION"</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/RankCardView.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [21](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/RankCardView.cs:21>) | <code></code> | <code>UI.Text</code> | <code>"?"</code> |
| [36](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/RankCardView.cs:36>) | <code>Bind</code> | <code>title.text</code> | <code>PlayModeNames.DisplayBackend(mode).ToUpperInvariant() + " / RANKED"</code> |
| [41](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/RankCardView.cs:41>) | <code>Bind</code> | <code>order.text</code> | <code>placed ? (definition?.order ?? TierOrder(rank.tierId)).ToString() : "?"</code> |
| [42](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/RankCardView.cs:42>) | <code>Bind</code> | <code>tier.text</code> | <code>rank == null ? loading ? "Loading rank..." : "Rank unavailable" : rank.provisional ? "Unranked" : TierName(mode, rank.tierId)</code> |
| [43](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/RankCardView.cs:43>) | <code>Bind</code> | <code>detail.text</code> | <code>rank == null ? "Refresh to load your rank" : rank.provisional<br>            ? $"Placement {rank.placementPlayed}/{rank.placementRequired}  /  {rank.rating:N0} rating"<br>            : rank.nextTierId == null ? $"{rank.rating:N0} rating  /  Highest tier"<br>            : $"{rank.rating:N0} rating  /  {rank.pointsToNextTier:N0} to {rank.nextTierId}"</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [37](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:37>) | <code>Initialize</code> | <code>UI.Text</code> | <code>"MATCH COMPLETE"</code> |
| [38](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:38>) | <code>Initialize</code> | <code>UI.Text</code> | <code>"YOU WIN!"</code> |
| [44](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:44>) | <code>Initialize</code> | <code>UI.Button</code> | <code>"PLAY AGAIN"</code> |
| [46](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:46>) | <code>Initialize</code> | <code>UI.Button</code> | <code>"SHARE RESULT"</code> |
| [48](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:48>) | <code>Initialize</code> | <code>UI.Button</code> | <code>"VIEW BOARD"</code> |
| [50](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:50>) | <code>Initialize</code> | <code>UI.Button</code> | <code>"MAIN MENU"</code> |
| [62](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:62>) | <code>Show</code> | <code>Present</code> | <code>neutralTitle</code> |
| [78](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:78>) | <code>Present</code> | <code>shareStatus.text</code> | <code>""</code> |
| [81](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:81>) | <code>Present</code> | <code>title.text</code> | <code>!string.IsNullOrWhiteSpace(neutralTitle) ? neutralTitle.ToUpperInvariant() :<br>            win ? "YOU WIN!" : lose ? "YOU LOSE" : kind == ResultKind.Stalemate ? "STALEMATE" : "IT'S A DRAW"</code> |
| [86](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:86>) | <code>Present</code> | <code>mode.text</code> | <code>"MATCH COMPLETE  /  " + (game ? game.StatisticsMode.ToUpperInvariant() : "CHESS BUT WEIRD")</code> |
| [87](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:87>) | <code>Present</code> | <code>message.text</code> | <code>!win &amp;&amp; !lose ? (game &amp;&amp; !string.IsNullOrWhiteSpace(game.DrawReason) ? PlayModeNames.DisplayText(game.DrawReason) :<br>            kind == ResultKind.Stalemate ? "No legal moves. Neither king is in check." : "A shared finish. Well played!") :<br>            game ? game.StatisticsPlayerName(game.WinningTeam) + " wins as " + game.WinningTeam + "." : win ? "Well played!" : "A new game, a fresh start."</code> |
| [90](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:90>) | <code>Present</code> | <code>players.text</code> | <code>game ? "WHITE  ·  " + game.StatisticsPlayerName(PieceTeam.White) + "     /     BLACK  ·  " + game.StatisticsPlayerName(PieceTeam.Black) : ""</code> |
| [92](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:92>) | <code>Present</code> | <code>elapsed.text</code> | <code>game &amp;&amp; game.StatisticsTimeKnown ? $"TIME PLAYED  {seconds / 60:00}:{seconds % 60:00}" : "TIME PLAYED  —"</code> |
| [93](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:93>) | <code>Present</code> | <code>replay.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>game &amp;&amp; game.UsesDotNetOnline ? "REQUEST REMATCH" : "PLAY AGAIN"</code> |
| [122](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:122>) | <code>ShareResult</code> | <code>SetShareStatus</code> | <code>"Result copied! Paste it into your chat to share."</code> |
| [126](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:126>) | <code>ShareResult</code> | <code>SetShareStatus</code> | <code>"Could not copy result: " + PlayerNotificationText.FromException(error)</code> |
| [132](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:132>) | <code>SetShareStatus</code> | <code>shareStatus.text</code> | <code>text</code> |
| [135](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:135>) | <code>SetShareStatus</code> | <code>reviewStatus.text</code> | <code>text</code> |
| [163](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:163>) | <code>BuildBoardReview</code> | <code>ReviewButton</code> | <code>"Leave to lobby"</code> |
| [163](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:163>) | <code>BuildBoardReview</code> | <code>ReviewButton</code> | <code>"Leave this finished game and return to the lobby you came from."</code> |
| [170](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:170>) | <code>BuildBoardReview</code> | <code>ReviewButton</code> | <code>"Share result"</code> |
| [170](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:170>) | <code>BuildBoardReview</code> | <code>ReviewButton</code> | <code>"Copy the final result, players and match details. Paste it into your chat to share."</code> |
| [184](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:184>) | <code>ReviewButton</code> | <code>button.gameObject.AddComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>heading</code> |
| [184](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:184>) | <code>ReviewButton</code> | <code>button.gameObject.AddComponent&lt;MatchHudTooltipTarget&gt;().Configure</code> | <code>detail</code> |
| [192](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs:192>) | <code>SetReviewExitPending</code> | <code>reviewStatus.text</code> | <code>status ?? ""</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [62](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:62>) | <code>BuildShell</code> | <code>UI.Text</code> | <code>SettingsLocalization.Text("settings.title","SETTINGS")</code> |
| [62](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:62>) | <code>BuildShell</code> | <code>SettingsLocalization.Text</code> | <code>"SETTINGS"</code> |
| [63](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:63>) | <code>BuildShell</code> | <code>UI.Text</code> | <code>SettingsLocalization.Text("settings.subtitle","Your game, your comfort. Changes save automatically.")</code> |
| [63](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:63>) | <code>BuildShell</code> | <code>SettingsLocalization.Text</code> | <code>"Your game, your comfort. Changes save automatically."</code> |
| [68](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:68>) | <code>BuildShell</code> | <code>UI.Button</code> | <code>tab.ToString()</code> |
| [87](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:87>) | <code>BuildShell</code> | <code>UI.Text</code> | <code>"Changes save automatically"</code> |
| [88](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:88>) | <code>BuildShell</code> | <code>UI.Button</code> | <code>"Reset This Tab"</code> |
| [89](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:89>) | <code>BuildShell</code> | <code>UI.Button</code> | <code>"Reset All Settings"</code> |
| [89](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:89>) | <code>BuildShell</code> | <code>Confirm</code> | <code>"Reset all settings to default?\nControls and accessibility preferences will also reset."</code> |
| [89](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:89>) | <code>BuildShell</code> | <code>Confirm</code> | <code>"Reset All"</code> |
| [90](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:90>) | <code>BuildShell</code> | <code>UI.Button</code> | <code>"Close  ·  ESC"</code> |
| [100](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:100>) | <code>SelectCategory</code> | <code>SettingsLocalization.Text</code> | <code>selected.ToString()</code> |
| [100](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:100>) | <code>SelectCategory</code> | <code>categoryTitle.text</code> | <code>SettingsLocalization.Text("tab."+selected,selected.ToString()).ToUpperInvariant()</code> |
| [105](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:105>) | <code>SelectCategory</code> | <code>SettingsLocalization.Text</code> | <code>((SettingsCategory)i).ToString()</code> |
| [105](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:105>) | <code>SelectCategory</code> | <code>tabs[i].GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>(i==(int)selected?"&gt;  ":"")+SettingsLocalization.Text("tab."+(SettingsCategory)i,((SettingsCategory)i).ToString())</code> |
| [122](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:122>) | <code>BuildRows</code> | <code>AddNote</code> | <code>"Mouse &amp; keyboard"</code> |
| [122](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:122>) | <code>BuildRows</code> | <code>AddNote</code> | <code>"Left click selects and moves pieces. ESC cancels selection / goes back. Rifle: right mouse aims, left mouse fires, Alt releases the cursor. Camera and Settings shortcuts below can be rebound. Controller support is not available."</code> |
| [124](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:124>) | <code>BuildRows</code> | <code>AddNote</code> | <code>"Text &amp; information"</code> |
| [124](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:124>) | <code>BuildRows</code> | <code>AddNote</code> | <code>"UI Scale and Tooltip Size are in General. Buff detail is in Gameplay. These tabs share one set of preferences."</code> |
| [133](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:133>) | <code>RefreshShellText</code> | <code>SettingsLocalization.Text</code> | <code>"SETTINGS"</code> |
| [133](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:133>) | <code>RefreshShellText</code> | <code>frame.Find("Settings title").GetComponent&lt;TextMeshProUGUI&gt;().text</code> | <code>SettingsLocalization.Text("settings.title","SETTINGS")</code> |
| [134](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:134>) | <code>RefreshShellText</code> | <code>SettingsLocalization.Text</code> | <code>"Your game, your comfort. Changes save automatically."</code> |
| [134](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:134>) | <code>RefreshShellText</code> | <code>frame.Find("Settings subtitle").GetComponent&lt;TextMeshProUGUI&gt;().text</code> | <code>SettingsLocalization.Text("settings.subtitle","Your game, your comfort. Changes save automatically.")</code> |
| [135](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:135>) | <code>RefreshShellText</code> | <code>SettingsLocalization.Text</code> | <code>category.ToString()</code> |
| [135](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:135>) | <code>RefreshShellText</code> | <code>categoryTitle.text</code> | <code>SettingsLocalization.Text("tab."+category,category.ToString()).ToUpperInvariant()</code> |
| [136](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:136>) | <code>RefreshShellText</code> | <code>SettingsLocalization.Text</code> | <code>((SettingsCategory)i).ToString()</code> |
| [136](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:136>) | <code>RefreshShellText</code> | <code>tabs[i].GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>(i==(int)category?"&gt;  ":"")+SettingsLocalization.Text("tab."+(SettingsCategory)i,((SettingsCategory)i).ToString())</code> |
| [137](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:137>) | <code>RefreshShellText</code> | <code>SettingsLocalization.Text</code> | <code>"Reset This Tab"</code> |
| [137](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:137>) | <code>RefreshShellText</code> | <code>frame.Find("Reset tab").GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>SettingsLocalization.Text("settings.reset_tab","Reset This Tab")</code> |
| [138](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:138>) | <code>RefreshShellText</code> | <code>SettingsLocalization.Text</code> | <code>"Reset All Settings"</code> |
| [138](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:138>) | <code>RefreshShellText</code> | <code>frame.Find("Reset all").GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>SettingsLocalization.Text("settings.reset_all","Reset All Settings")</code> |
| [139](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:139>) | <code>RefreshShellText</code> | <code>SettingsLocalization.Text</code> | <code>"Close  ·  ESC"</code> |
| [139](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:139>) | <code>RefreshShellText</code> | <code>frame.Find("Close settings").GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>SettingsLocalization.Text("settings.close","Close  ·  ESC")</code> |
| [144](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:144>) | <code>AddNote</code> | <code>UI.Text</code> | <code>title+"\n"+text</code> |
| [153](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:153>) | <code>AddRow</code> | <code>UI.Text</code> | <code>SettingsLocalization.Text(definition.Id+".name",definition.Name)</code> |
| [153](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:153>) | <code>AddRow</code> | <code>SettingsLocalization.Text</code> | <code>definition.Name</code> |
| [154](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:154>) | <code>AddRow</code> | <code>UI.Text</code> | <code>SettingsLocalization.Text(definition.Id+".description",definition.Description)</code> |
| [154](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:154>) | <code>AddRow</code> | <code>SettingsLocalization.Text</code> | <code>definition.Description</code> |
| [168](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:168>) | <code>AddRow</code> | <code>value.text</code> | <code>FormatValue(definition,UserSettings.Get(definition.Id))</code> |
| [179](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:179>) | <code>AddRow</code> | <code>SettingsLocalization.Text</code> | <code>"ON"</code> |
| [179](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:179>) | <code>AddRow</code> | <code>SettingsLocalization.Text</code> | <code>"OFF"</code> |
| [179](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:179>) | <code>AddRow</code> | <code>SettingsLocalization.Text</code> | <code>definition.Labels[definition.OptionIndex(UserSettings.Get(definition.Id))]</code> |
| [179](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:179>) | <code>AddRow</code> | <code>button.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>definition.Type==SettingType.Toggle?(UserSettings.Enabled(definition.Id)?SettingsLocalization.Text("common.on","ON"):SettingsLocalization.Text("common.off","OFF")):SettingsLocalization.Text(definition.Id+".option."+definition.OptionIndex(UserSettings.Get(definition.Id)),definition.Labels[definition.OptionIndex(UserSettings.Get(definition.Id))])+(definition.Type==SettingType.Dropdown?"  v":"")</code> |
| [181](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:181>) | <code>AddRow</code> | <code>UI.Button</code> | <code>"Reset"</code> |
| [186](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:186>) | <code>AddRow</code> | <code>status.text</code> | <code>definition.Labels[definition.OptionIndex(definition.Default)]+" is already assigned to "+conflict+". Rebind that action first."</code> |
| [191](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:191>) | <code>AddRow</code> | <code>dot.text</code> | <code>changed?"*":""</code> |
| [194](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:194>) | <code>OptionLabels</code> | <code>SettingsLocalization.Text</code> | <code>label</code> |
| [212](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:212>) | <code>ResetTab</code> | <code>Confirm</code> | <code>"Reset display mode and resolution too?"</code> |
| [212](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:212>) | <code>ResetTab</code> | <code>Confirm</code> | <code>"Reset Display"</code> |
| [213](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:213>) | <code>ResetTab</code> | <code>status.text</code> | <code>"Reset "+category+" to defaults."</code> |
| [220](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:220>) | <code>AddDisplayRows</code> | <code>AddNote</code> | <code>"Display"</code> |
| [220](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:220>) | <code>AddDisplayRows</code> | <code>AddNote</code> | <code>"Display changes require confirmation and revert after 15 seconds. VSync and frame rate changes above are immediate."</code> |
| [222](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:222>) | <code>AddDisplayRows</code> | <code>AddDisplayChoice</code> | <code>"Display Mode"</code> |
| [226](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:226>) | <code>AddDisplayRows</code> | <code>AddDisplayChoice</code> | <code>"Resolution"</code> |
| [232](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:232>) | <code>AddDisplayChoice</code> | <code>UI.Text</code> | <code>name</code> |
| [236](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:236>) | <code>AddDisplayChoice</code> | <code>UI.Button</code> | <code>options[Mathf.Clamp(selected,0,options.Length-1)]</code> |
| [242](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:242>) | <code>BeginDisplay</code> | <code>Confirm</code> | <code>"Display settings changed.\nKeep these settings?"</code> |
| [242](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:242>) | <code>BeginDisplay</code> | <code>Confirm</code> | <code>"Keep"</code> |
| [276](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:276>) | <code>ShowChoices</code> | <code>UI.Button</code> | <code>options[i]</code> |
| [288](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:288>) | <code>Confirm</code> | <code>UI.Text</code> | <code>text</code> |
| [289](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:289>) | <code>Confirm</code> | <code>UI.Button</code> | <code>label</code> |
| [290](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:290>) | <code>Confirm</code> | <code>UI.Button</code> | <code>display.Pending?"Revert":"Cancel"</code> |
| [294](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:294>) | <code>BeginRebind</code> | <code>Confirm</code> | <code>"Press a letter, F1–F12, Space, Tab or Backquote.\nESC cancels. Duplicate bindings are rejected."</code> |
| [294](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:294>) | <code>BeginRebind</code> | <code>Confirm</code> | <code>"Waiting…"</code> |
| [323](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:323>) | <code>Update</code> | <code>confirmationText.text</code> | <code>"Display settings changed.\nKeep these settings?\nReverting in "+display.SecondsRemaining(Time.realtimeSinceStartupAsDouble)+" seconds."</code> |
| [337](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:337>) | <code>Update</code> | <code>status.text</code> | <code>"Shortcut saved."</code> |
| [338](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs:338>) | <code>Update</code> | <code>confirmationText.text</code> | <code>key.displayName+" is already assigned to "+conflict+".\nChoose another key, or press ESC to cancel."</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsPrompt.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [28](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsPrompt.cs:28>) | <code>Show</code> | <code>SketchbookUI.Text</code> | <code>message</code> |
| [29](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsPrompt.cs:29>) | <code>Show</code> | <code>SketchbookUI.Button</code> | <code>"Cancel"</code> |
| [30](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SettingsPrompt.cs:30>) | <code>Show</code> | <code>SketchbookUI.Button</code> | <code>acceptLabel</code> |

### Assets/_Project/Scripts/Chess/Gameplay/UI/SketchbookUI.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [65](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SketchbookUI.cs:65>) | <code>Text</code> | <code>label.text</code> | <code>value ?? string.Empty</code> |
| [84](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SketchbookUI.cs:84>) | <code>Button</code> | <code>Text</code> | <code>label</code> |
| [143](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SketchbookUI.cs:143>) | <code>Wallet</code> | <code>Text</code> | <code>name.ToUpperInvariant()</code> |
| [144](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/UI/SketchbookUI.cs:144>) | <code>Wallet</code> | <code>Text</code> | <code>"--"</code> |

### Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [38](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:38>) | <code></code> | <code>Action</code> | <code>"Offer Draw"</code> |
| [39](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:39>) | <code></code> | <code>Action</code> | <code>"Claim Draw"</code> |
| [40](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:40>) | <code></code> | <code>Action</code> | <code>"Resign"</code> |
| [41](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:41>) | <code></code> | <code>Action</code> | <code>"Accept Draw"</code> |
| [42](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:42>) | <code></code> | <code>Action</code> | <code>"Decline Draw"</code> |
| [43](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:43>) | <code></code> | <code>Action</code> | <code>"Ready"</code> |
| [44](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:44>) | <code></code> | <code>Action</code> | <code>"Leave Match"</code> |
| [45](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:45>) | <code></code> | <code>Action</code> | <code>"Rematch"</code> |
| [46](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:46>) | <code></code> | <code>Action</code> | <code>"Accept Rematch"</code> |
| [47](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:47>) | <code></code> | <code>Action</code> | <code>"Decline Rematch"</code> |
| [49](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:49>) | <code></code> | <code>UI.Button</code> | <code>"Recover Previous Command"</code> |
| [56](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:56>) | <code>Action</code> | <code>UI.Button</code> | <code>label</code> |
| [91](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:91>) | <code>Refresh</code> | <code>notice.text</code> | <code>"Reconnecting... Board input is temporarily locked."</code> |
| [92](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:92>) | <code>Refresh</code> | <code>notice.text</code> | <code>"Restoring the latest server state..."</code> |
| [93](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:93>) | <code>Refresh</code> | <code>notice.text</code> | <code>"The last command is unconfirmed. Recover it before playing."</code> |
| [95](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:95>) | <code>Refresh</code> | <code>notice.text</code> | <code>local?.ready == true ? "Ready. Waiting for your opponent and setup..." : "Loading complete. Sending Ready..."</code> |
| [96](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:96>) | <code>Refresh</code> | <code>notice.text</code> | <code>incomingDraw ? $"Opponent offers a draw · {Mathf.CeilToInt(Remaining(state, state.drawOffer.expiresAt))}s to respond" : "Draw offered. Waiting for your opponent..."</code> |
| [97](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:97>) | <code>Refresh</code> | <code>notice.text</code> | <code>observedNotice</code> |
| [101](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:101>) | <code>Refresh</code> | <code>notice.text</code> | <code>result == null ? "Match finished. Both players can agree to a rematch." :<br>                    $"{result.golds} Gold · {result.diamonds} Diamonds · {result.tickets} Tickets · Elo {result.ratingChange:+0;-0;0}"</code> |
| [104](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:104>) | <code>Refresh</code> | <code>notice.text</code> | <code>"Sending command..."</code> |
| [105](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:105>) | <code>Refresh</code> | <code>notice.text</code> | <code>"Choose a piece to complete your promotion."</code> |
| [106](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:106>) | <code>Refresh</code> | <code>notice.text</code> | <code>"Opponent disconnected. The server clock continues."</code> |
| [107](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Hud.cs:107>) | <code>Refresh</code> | <code>notice.text</code> | <code>state.turn == local?.color ? $"You play {local.color} · Your turn. Select a piece." : $"You play {local?.color} · Opponent's turn."</code> |

### Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [63](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:63>) | <code>Refresh</code> | <code>statusLabel.text</code> | <code>owner.requestInFlight ? "Working...  " + owner.statusMessage : owner.statusMessage</code> |
| [64](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:64>) | <code>Refresh</code> | <code>connectionLabel.text</code> | <code>owner.MatchConnectionState</code> |
| [65](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:65>) | <code>Refresh</code> | <code>roomCodeLabel.text</code> | <code>owner.GetLobbyRoomCode()</code> |
| [66](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:66>) | <code>Refresh</code> | <code>hostLabel.text</code> | <code>owner.GetHostPlayerLine()</code> |
| [67](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:67>) | <code>Refresh</code> | <code>guestLabel.text</code> | <code>owner.GetGuestPlayerLine()</code> |
| [88](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:88>) | <code>Rebuild</code> | <code>ActionButton</code> | <code>"Back"</code> |
| [90](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:90>) | <code>Rebuild</code> | <code>UI.Text</code> | <code>Title()</code> |
| [115](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:115>) | <code>ActionButton</code> | <code>UI.Button</code> | <code>label</code> |
| [126](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:126>) | <code>ChoiceCard</code> | <code>UI.Text</code> | <code>title</code> |
| [127](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:127>) | <code>ChoiceCard</code> | <code>UI.Text</code> | <code>description</code> |
| [128](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:128>) | <code>ChoiceCard</code> | <code>UI.Text</code> | <code>actionLabel + "  &gt;"</code> |
| [132](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:132>) | <code>BuildHome</code> | <code>UI.Text</code> | <code>"A friendly room, or a fresh opponent. Pick your next game."</code> |
| [133](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:133>) | <code>BuildHome</code> | <code>ChoiceCard</code> | <code>"ROOMS"</code> |
| [133](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:133>) | <code>BuildHome</code> | <code>ChoiceCard</code> | <code>"Create a room and share its code, or find a room to join."</code> |
| [133](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:133>) | <code>BuildHome</code> | <code>ChoiceCard</code> | <code>"Create / Find Room"</code> |
| [134](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:134>) | <code>BuildHome</code> | <code>ChoiceCard</code> | <code>"MATCHMAKING"</code> |
| [134](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:134>) | <code>BuildHome</code> | <code>ChoiceCard</code> | <code>"Choose your loadout and find an opponent. Accept together to begin."</code> |
| [134](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:134>) | <code>BuildHome</code> | <code>ChoiceCard</code> | <code>"Choose Loadout"</code> |
| [138](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:138>) | <code>ModePicker</code> | <code>UI.Text</code> | <code>"GAME MODE"</code> |
| [139](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:139>) | <code>ModePicker</code> | <code>ActionButton</code> | <code>"Classic"</code> |
| [141](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:141>) | <code>ModePicker</code> | <code>ActionButton</code> | <code>"CHAOS"</code> |
| [143](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:143>) | <code>ModePicker</code> | <code>UI.Text</code> | <code>$"{owner.onlineRegion}  /  {owner.onlineInitialSeconds / 60} min + {owner.onlineIncrementSeconds} sec"</code> |
| [150](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:150>) | <code>BuildRooms</code> | <code>ChoiceCard</code> | <code>"CREATE ROOM"</code> |
| [150](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:150>) | <code>BuildRooms</code> | <code>ChoiceCard</code> | <code>"Make a room for two players. Share its code with your friend once it is created."</code> |
| [150](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:150>) | <code>BuildRooms</code> | <code>ChoiceCard</code> | <code>"Create Room"</code> |
| [153](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:153>) | <code>BuildRooms</code> | <code>ChoiceCard</code> | <code>"CREATE ROOM"</code> |
| [153](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:153>) | <code>BuildRooms</code> | <code>ChoiceCard</code> | <code>"Make a room for two players. Your room code appears as soon as it is created."</code> |
| [153](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:153>) | <code>BuildRooms</code> | <code>ChoiceCard</code> | <code>"Create Room"</code> |
| [154](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:154>) | <code>BuildRooms</code> | <code>ChoiceCard</code> | <code>"FIND ROOM"</code> |
| [154](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:154>) | <code>BuildRooms</code> | <code>ChoiceCard</code> | <code>"Browse open rooms or enter a room code to join your friend."</code> |
| [154](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:154>) | <code>BuildRooms</code> | <code>ChoiceCard</code> | <code>"Browse Rooms"</code> |
| [160](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:160>) | <code>BuildRoomBrowser</code> | <code>UI.Text</code> | <code>"ROOM CODE"</code> |
| [162](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:162>) | <code>BuildRoomBrowser</code> | <code>ActionButton</code> | <code>"Join"</code> |
| [164](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:164>) | <code>BuildRoomBrowser</code> | <code>ActionButton</code> | <code>"Refresh"</code> |
| [168](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:168>) | <code>BuildRoomBrowser</code> | <code>UI.Text</code> | <code>owner.roomListError != null ? "Couldn't load rooms. Press Refresh to try again." : owner.roomsLoading ? "Loading rooms..." : "No rooms yet. Create a room, or join using a code."</code> |
| [179](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:179>) | <code>BuildRoomBrowser</code> | <code>UI.Text</code> | <code>room.code</code> |
| [180](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:180>) | <code>BuildRoomBrowser</code> | <code>UI.Text</code> | <code>$"{PlayModeNames.DisplayBackend(room.settings.mode)}   /   {room.settings.initialSeconds / 60} min + {room.settings.incrementSeconds} sec   /   {room.settings.region}"</code> |
| [181](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:181>) | <code>BuildRoomBrowser</code> | <code>UI.Text</code> | <code>$"{room.memberCount}/{room.capacity}"</code> |
| [182](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:182>) | <code>BuildRoomBrowser</code> | <code>UI.Text</code> | <code>full ? "Full" : "Join  &gt;"</code> |
| [185](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:185>) | <code>BuildRoomBrowser</code> | <code>ActionButton</code> | <code>"Previous"</code> |
| [186](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:186>) | <code>BuildRoomBrowser</code> | <code>UI.Text</code> | <code>$"Page {owner.roomListPage}   /   {owner.roomListing?.total ?? 0} rooms"</code> |
| [187](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:187>) | <code>BuildRoomBrowser</code> | <code>ActionButton</code> | <code>"Next"</code> |
| [198](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:198>) | <code>CodeInput</code> | <code>UI.Text</code> | <code>"Enter room code"</code> |
| [210](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:210>) | <code>BuildRoom</code> | <code>UI.Text</code> | <code>"SHARE THIS ROOM CODE"</code> |
| [212](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:212>) | <code>BuildRoom</code> | <code>ActionButton</code> | <code>"Copy Code"</code> |
| [214](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:214>) | <code>BuildRoom</code> | <code>UI.Text</code> | <code>"PLAYERS  /  2"</code> |
| [217](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:217>) | <code>BuildRoom</code> | <code>UI.Text</code> | <code>"CLOCK"</code> |
| [221](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:221>) | <code>BuildRoom</code> | <code>ActionButton</code> | <code>preset.Item3</code> |
| [224](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:224>) | <code>BuildRoom</code> | <code>UI.Text</code> | <code>"The room owner starts when both players have joined."</code> |
| [225](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:225>) | <code>BuildRoom</code> | <code>ActionButton</code> | <code>"Leave Room"</code> |
| [226](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/ChessLanController.Menu.cs:226>) | <code>BuildRoom</code> | <code>ActionButton</code> | <code>"Start Game"</code> |

### Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [55](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:55>) | <code>BuildChaos35Presentation</code> | <code>summaries.Add</code> | <code>(string)s["team"] + ": " + (definition ? definition.DisplayName : "Choosing a buff...")</code> |
| [56](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:56>) | <code>BuildChaos35Presentation</code> | <code>details.Add</code> | <code>(string)s["team"] + ": " + definition.Description</code> |
| [65](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:65>) | <code>BuildChaos35Presentation</code> | <code>view.Actions.Add</code> | <code>new AramAbilityView.AbilityAction(label, callback, CanInteract &amp;&amp; ready, detail??BuffHudStatus(a,side), id)</code> |
| [66](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:66>) | <code>BuildChaos35Presentation</code> | <code>Add</code> | <code>label</code> |
| [78](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:78>) | <code>BuildChaos35Presentation</code> | <code>Add</code> | <code>"Replace Queen with " + kind</code> |
| [84](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:84>) | <code>BuildChaos35Presentation</code> | <code>Add</code> | <code>"Arrange your army"</code> |
| [111](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:111>) | <code>BuildChaos35Presentation</code> | <code>Add</code> | <code>"Swap movement"</code> |
| [112](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:112>) | <code>BuildChaos35Presentation</code> | <code>Add</code> | <code>"Keep movement"</code> |
| [121](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:121>) | <code>BuildChaos35Presentation</code> | <code>Add</code> | <code>"Battle gacha"</code> |
| [123](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:123>) | <code>BuildChaos35Presentation</code> | <code>Add</code> | <code>"Aim rifle"</code> |
| [131](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:131>) | <code>BuildChaos35Presentation</code> | <code>Add</code> | <code>"Buy " + kind + " (" + cost + ")"</code> |
| [143](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:143>) | <code>BuildChaos35Presentation</code> | <code>Add</code> | <code>"Fire"</code> |
| [143](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:143>) | <code>BuildChaos35Presentation</code> | <code>Add</code> | <code>"Exit rifle"</code> |
| [155](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:155>) | <code>BuildChaos35Presentation</code> | <code>Add</code> | <code>"Confirm formation"</code> |
| [157](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:157>) | <code>BuildChaos35Presentation</code> | <code>Add</code> | <code>"Cancel selection"</code> |
| [191](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:191>) | <code>HandleChaos35Click</code> | <code>targets.Add</code> | <code>pieceId.Value</code> |
| [193](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:193>) | <code>HandleChaos35Click</code> | <code>targets.Add</code> | <code>pieceId.Value</code> |
| [201](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:201>) | <code>HandleChaos35Click</code> | <code>targets.Add</code> | <code>pieceId.Value</code> |

### Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [146](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:146>) | <code>RefreshDraft</code> | <code>options.Add</code> | <code>definition</code> |
| [172](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:172>) | <code>BuildPresentation</code> | <code>summary.Add</code> | <code>(string)s["team"] + ": " + (definition ? definition.DisplayName : "Choosing a buff...")</code> |
| [173](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:173>) | <code>BuildPresentation</code> | <code>details.Add</code> | <code>(string)s["team"] + ": " + definition.Description</code> |
| [179](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:179>) | <code>BuildPresentation</code> | <code>details.Add</code> | <code>"#" + (int)effect["pieceId"] + " " + text</code> |
| [187](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:187>) | <code>BuildPresentation</code> | <code>view.Actions.Add</code> | <code>new AramAbilityView.AbilityAction(label, invoke, CanInteract, id: id)</code> |
| [188](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:188>) | <code>BuildPresentation</code> | <code>Add</code> | <code>label</code> |
| [203](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:203>) | <code>BuildPresentation</code> | <code>Add</code> | <code>"Arrange your army"</code> |
| [215](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:215>) | <code>BuildPresentation</code> | <code>Add</code> | <code>"Toggle knight/bishop swap"</code> |
| [220](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:220>) | <code>BuildPresentation</code> | <code>Add</code> | <code>"Battle gacha"</code> |
| [221](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:221>) | <code>BuildPresentation</code> | <code>Add</code> | <code>"Aim rifle"</code> |
| [228](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:228>) | <code>BuildPresentation</code> | <code>Add</code> | <code>"Fire"</code> |
| [229](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:229>) | <code>BuildPresentation</code> | <code>Add</code> | <code>"Exit rifle"</code> |
| [239](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:239>) | <code>BuildPresentation</code> | <code>Add</code> | <code>"Confirm entire formation"</code> |
| [242](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:242>) | <code>BuildPresentation</code> | <code>Add</code> | <code>"Cancel targeting"</code> |
| [251](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:251>) | <code>BuffText</code> | <code>SettingsLocalization.Text</code> | <code>copy.Substring(0, separator)</code> |
| [285](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:285>) | <code>HandleClick</code> | <code>targets.Add</code> | <code>pieceId.Value</code> |

### Assets/_Project/Scripts/Chess/Presentation/BuffVfx/BuffStrategySequence.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [56](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Presentation/BuffVfx/BuffStrategySequence.cs:56>) | <code>Begin</code> | <code>receipt.text</code> | <code>recipe.id==31&amp;&amp;cue.Amount.HasValue?"-"+cue.Amount.Value+" points":recipe.id==8&amp;&amp;cue.Amount==1?"+1 extra move":""</code> |

### Assets/_Project/Scripts/Chess/Presentation/BuffVfx/BuffTacticalHud.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [30](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Presentation/BuffVfx/BuffTacticalHud.cs:30>) | <code></code> | <code>milestoneNames[i].text</code> | <code>names[i]</code> |
| [42](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Presentation/BuffVfx/BuffTacticalHud.cs:42>) | <code>Sync</code> | <code>labels[i].text</code> | <code>s.Text+"\n"+(s.Target&gt;0?s.Current+" / "+s.Target:s.Current.ToString())+(string.IsNullOrEmpty(s.Unit)?"":" "+s.Unit)</code> |
| [50](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Presentation/BuffVfx/BuffTacticalHud.cs:50>) | <code>Sync</code> | <code>pulse.text</code> | <code>"Recruitment ready"</code> |
| [53](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Presentation/BuffVfx/BuffTacticalHud.cs:53>) | <code>Cue</code> | <code>pulse.text</code> | <code>"+1 extra move"</code> |
| [54](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Presentation/BuffVfx/BuffTacticalHud.cs:54>) | <code>FinishKingless</code> | <code>pulse.text</code> | <code>"Kingless deadline reached"</code> |

### Assets/_Project/Scripts/Chess/Presentation/BuffVfx/PieceStatusVisual.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [63](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Presentation/BuffVfx/PieceStatusVisual.cs:63>) | <code>Apply</code> | <code>counter.text</code> | <code>next</code> |

### Assets/_Project/Scripts/Chess/Presentation/CapturedPieceDisplay.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [66](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Presentation/CapturedPieceDisplay.cs:66>) | <code>LateUpdate</code> | <code>text.text</code> | <code>"x" + count</code> |

### Assets/_Project/Scripts/Chess/Profile/AuthController.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [62](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthController.cs:62>) | <code>Submit</code> | <code>ShowFailure</code> | <code>"Enter a valid email address."</code> |
| [67](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthController.cs:67>) | <code>Submit</code> | <code>ShowFailure</code> | <code>"Enter a username."</code> |
| [72](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthController.cs:72>) | <code>Submit</code> | <code>ShowFailure</code> | <code>"Enter your password."</code> |
| [77](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthController.cs:77>) | <code>Submit</code> | <code>ShowFailure</code> | <code>"Your passwords do not match."</code> |
| [83](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthController.cs:83>) | <code>Submit</code> | <code>AuthNotificationView.Show</code> | <code>requestMode == MainMenuAuthUI.AuthMode.SignUp ? "Creating account..." : "Logging in..."</code> |
| [84](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthController.cs:84>) | <code>Submit</code> | <code>AuthNotificationView.Show</code> | <code>"Please wait for the server."</code> |
| [94](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthController.cs:94>) | <code>Submit</code> | <code>AuthNotificationView.Show</code> | <code>"Account ready"</code> |
| [94](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthController.cs:94>) | <code>Submit</code> | <code>AuthNotificationView.Show</code> | <code>"Welcome, " + PlayerAuthService.CurrentDisplayName + "!"</code> |
| [105](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthController.cs:105>) | <code>Submit</code> | <code>ShowFailure</code> | <code>PlayerNotificationText.FromException(error)</code> |
| [111](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthController.cs:111>) | <code>ShowFailure</code> | <code>AuthNotificationView.Show</code> | <code>requestMode == MainMenuAuthUI.AuthMode.SignUp ? "Sign-up unsuccessful" : "Login unsuccessful"</code> |
| [112](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthController.cs:112>) | <code>ShowFailure</code> | <code>AuthNotificationView.Show</code> | <code>reason</code> |
| [119](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthController.cs:119>) | <code>PlayAsGuest</code> | <code>AuthNotificationView.Show</code> | <code>"Playing as Guest"</code> |
| [119](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthController.cs:119>) | <code>PlayAsGuest</code> | <code>AuthNotificationView.Show</code> | <code>"Local play is ready."</code> |

### Assets/_Project/Scripts/Chess/Profile/AuthNotificationView.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [40](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthNotificationView.cs:40>) | <code>Show</code> | <code>instance.Present</code> | <code>title</code> |
| [40](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthNotificationView.cs:40>) | <code>Show</code> | <code>instance.Present</code> | <code>message</code> |
| [56](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthNotificationView.cs:56>) | <code>Present</code> | <code>titleLabel.text</code> | <code>title ?? string.Empty</code> |
| [57](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/AuthNotificationView.cs:57>) | <code>Present</code> | <code>messageLabel.text</code> | <code>message ?? string.Empty</code> |

### Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [49](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:49>) | <code>Initialize</code> | <code>loginEmailField.text</code> | <code>initialEmail ?? string.Empty</code> |
| [50](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:50>) | <code>Initialize</code> | <code>SetStatusMessage</code> | <code>initialStatus</code> |
| [81](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:81>) | <code>SetStatusMessage</code> | <code>AuthNotificationView.Show</code> | <code>"Account"</code> |
| [81](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:81>) | <code>SetStatusMessage</code> | <code>AuthNotificationView.Show</code> | <code>message</code> |
| [89](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:89>) | <code>ClearSensitiveFields</code> | <code>loginPasswordField.text</code> | <code>string.Empty</code> |
| [90](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:90>) | <code>ClearSensitiveFields</code> | <code>signUpPasswordField.text</code> | <code>string.Empty</code> |
| [91](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:91>) | <code>ClearSensitiveFields</code> | <code>signUpConfirmPasswordField.text</code> | <code>string.Empty</code> |
| [134](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:134>) | <code>BuildPanel</code> | <code>CreateText</code> | <code>"Chess But Weird"</code> |
| [135](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:135>) | <code>BuildPanel</code> | <code>CreateText</code> | <code>"YOUR CHESS ADVENTURE STARTS HERE"</code> |
| [139](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:139>) | <code>BuildPanel</code> | <code>CreateButton</code> | <code>"LOG IN"</code> |
| [141](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:141>) | <code>BuildPanel</code> | <code>CreateButton</code> | <code>"SIGN UP"</code> |
| [143](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:143>) | <code>BuildPanel</code> | <code>CreateButton</code> | <code>"PLAY AS GUEST"</code> |
| [148](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:148>) | <code>BuildPanel</code> | <code>CreateText</code> | <code>signUp ? "Create your account" : "Welcome back"</code> |
| [153](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:153>) | <code>BuildPanel</code> | <code>CreateField</code> | <code>"Username"</code> |
| [154](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:154>) | <code>BuildPanel</code> | <code>CreateField</code> | <code>"Choose a username"</code> |
| [155](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:155>) | <code>BuildPanel</code> | <code>CreateField</code> | <code>"Email"</code> |
| [156](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:156>) | <code>BuildPanel</code> | <code>CreateField</code> | <code>"you@example.com"</code> |
| [158](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:158>) | <code>BuildPanel</code> | <code>CreateField</code> | <code>"Password"</code> |
| [159](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:159>) | <code>BuildPanel</code> | <code>CreateField</code> | <code>"Password"</code> |
| [161](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:161>) | <code>BuildPanel</code> | <code>CreateField</code> | <code>"Confirm password"</code> |
| [161](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:161>) | <code>BuildPanel</code> | <code>CreateField</code> | <code>"Repeat password"</code> |
| [162](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:162>) | <code>BuildPanel</code> | <code>CreateButton</code> | <code>"CREATE ACCOUNT"</code> |
| [167](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:167>) | <code>BuildPanel</code> | <code>CreateField</code> | <code>"Email"</code> |
| [168](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:168>) | <code>BuildPanel</code> | <code>CreateField</code> | <code>"you@example.com"</code> |
| [170](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:170>) | <code>BuildPanel</code> | <code>CreateField</code> | <code>"Password"</code> |
| [171](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:171>) | <code>BuildPanel</code> | <code>CreateField</code> | <code>"Password"</code> |
| [172](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:172>) | <code>BuildPanel</code> | <code>CreateButton</code> | <code>"LOG IN"</code> |
| [181](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:181>) | <code>CreateField</code> | <code>CreateText</code> | <code>label</code> |
| [207](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:207>) | <code>CreateField</code> | <code>CreateText</code> | <code>hint</code> |
| [210](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:210>) | <code>CreateField</code> | <code>CreateText</code> | <code>string.Empty</code> |
| [235](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:235>) | <code>CreateButton</code> | <code>CreateText</code> | <code>caption</code> |
| [252](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Profile/MainMenuAuthUI.cs:252>) | <code>CreateText</code> | <code>text.text</code> | <code>caption</code> |

### Assets/_Project/Scripts/Shop/ShopMenuController.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [59](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:59>) | <code>Initialize</code> | <code>Confirm</code> | <code>"GOLDS"</code> |
| [60](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:60>) | <code>Initialize</code> | <code>Confirm</code> | <code>"DIAMONDS"</code> |
| [100](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:100>) | <code>RefreshAsync</code> | <code>SetStatus</code> | <code>"Loading your shop and collection..."</code> |
| [109](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:109>) | <code>RefreshAsync</code> | <code>SetStatus</code> | <code>Pending != null ? "One purchase needs checking. Use REVIEW PENDING PURCHASE above." : result.catalogError ?? "Cosmetics only. Make your next match your own."</code> |
| [113](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:113>) | <code>RefreshAsync</code> | <code>SetStatus</code> | <code>"Couldn't refresh the shop. " + PlayerNotificationText.FromException(error)</code> |
| [217](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:217>) | <code>Render</code> | <code>header.text</code> | <code>category == ShopCategory.Featured ? "Featured finds" : CategoryLabel(category)</code> |
| [218](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:218>) | <code>Render</code> | <code>collectionFilter.GetComponentInChildren&lt;TextMeshProUGUI&gt;().text</code> | <code>new[] { "ALL OWNED", "PIECE SKINS", "BOARDS", "EFFECTS", "BACKGROUNDS" }[collectionKind]</code> |
| [235](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:235>) | <code>Render</code> | <code>pageLabel.text</code> | <code>(page + 1) + " / " + Mathf.Max(1, (visible.Count + 5) / 6)</code> |
| [236](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:236>) | <code>Render</code> | <code>count.text</code> | <code>category == ShopCategory.TopUp ? "Preview only  /  Currency packs are not available yet." :<br>            visible.Count == 0 ? "No items match. Try another category or search." : visible.Count + " looks  /  Cosmetics only. No gameplay advantage."</code> |
| [239](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:239>) | <code>Render</code> | <code>goldBalance.text</code> | <code>snapshot?.user?.wallet?.golds.ToString("N0") ?? "--"</code> |
| [240](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:240>) | <code>Render</code> | <code>diamondBalance.text</code> | <code>snapshot?.user?.wallet?.diamonds.ToString("N0") ?? "--"</code> |
| [242](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:242>) | <code>Render</code> | <code>heroTitle.text</code> | <code>hero?.name ?? "Your next favourite look"</code> |
| [243](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:243>) | <code>Render</code> | <code>heroCopy.text</code> | <code>hero != null ? (hero.offer.isBundle ? "Complete the look. Only pay for what you still need.  &gt;" : "A closer look is one click away.  &gt;") : presentation.subtitle</code> |
| [253](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:253>) | <code>RenderDetail</code> | <code>detailTitle.text</code> | <code>p?.name ?? "A little room for something new"</code> |
| [254](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:254>) | <code>RenderDetail</code> | <code>detailType.text</code> | <code>p == null ? "YOUR COLLECTION" : p.Category == ShopCategory.Bundles ? "BUNDLE / " + p.offer.items.Count + " ITEMS" : CategoryLabel(p.Category).ToUpperInvariant() + " / " + p.rarity</code> |
| [258](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:258>) | <code>RenderDetail</code> | <code>detailDescription.text</code> | <code>p?.description ?? "Select a category to find your next favourite look."</code> |
| [259](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:259>) | <code>RenderDetail</code> | <code>detailContents.text</code> | <code>p?.offer?.isBundle == true ? string.Join("\n", p.offer.items.Select(i =&gt; (owned.Contains(i.itemId) ? "Owned: " : "Includes: ") + i.name)) :<br>            p?.isDefault == true ? "The original look. Always included." : ""</code> |
| [261](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:261>) | <code>RenderDetail</code> | <code>detailState.text</code> | <code>p == null ? "" : p.equipped ? "Equipped for your next match" : p.owned ? "Yours to keep" : p.offer?.isBundle == true ?<br>            (p.offer.items.Count - ShopPurchasePolicy.Missing(p.offer, owned).Count) + " / " + p.offer.items.Count + " already owned. Price adjusted below." : "Choose Gold or Diamonds"</code> |
| [270](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:270>) | <code>RenderDetail</code> | <code>goldPrice.text</code> | <code>gold.ToString("N0") + " Gold"</code> |
| [270](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:270>) | <code>RenderDetail</code> | <code>diamondPrice.text</code> | <code>diamonds.ToString("N0") + " Diamonds"</code> |
| [273](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:273>) | <code>RenderDetail</code> | <code>detailState.text</code> | <code>"Not enough Gold or Diamonds yet"</code> |
| [275](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:275>) | <code>RenderDetail</code> | <code>detailState.text</code> | <code>"Check the purchase you already confirmed"</code> |
| [276](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:276>) | <code>RenderDetail</code> | <code>detailState.text</code> | <code>"Locked - coming soon"</code> |
| [278](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:278>) | <code>RenderDetail</code> | <code>detailState.text</code> | <code>"Shop offer unavailable. Refresh after the server update."</code> |
| [279](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:279>) | <code>RenderDetail</code> | <code>equipLabel.text</code> | <code>shopUnavailable ? "SHOP UNAVAILABLE" : p?.previewOnly == true ? "COMING SOON" : pending ? "RETRY PURCHASE" : p == null ? "EXPLORE THE SHOP" : p.Category == ShopCategory.Bundles ? "BUNDLE OWNED" :<br>            !p.owned ? "OPEN SUMMON" : p.equipped ? p.isDefault ? "DEFAULT EQUIPPED" : "USE DEFAULT" : "EQUIP"</code> |
| [298](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:298>) | <code>Confirm</code> | <code>Notify</code> | <code>"Purchase needs checking"</code> |
| [298](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:298>) | <code>Confirm</code> | <code>Notify</code> | <code>"Use REVIEW PENDING PURCHASE to finish checking your earlier purchase before starting another."</code> |
| [304](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:304>) | <code>Confirm</code> | <code>confirmationTitle.text</code> | <code>"Make it yours?"</code> |
| [305](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:305>) | <code>Confirm</code> | <code>confirmationText.text</code> | <code>selected.name + "\n\n" + quote.expectedPrice.ToString("N0") + (currency == "GOLDS" ? " Gold" : " Diamonds") +<br>            "\n" + quote.expectedItemIds.Count + " new item" + (quote.expectedItemIds.Count == 1 ? "" : "s") + " for your collection."</code> |
| [307](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:307>) | <code>Confirm</code> | <code>ShowConfirmation</code> | <code>"CONFIRM PURCHASE"</code> |
| [312](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:312>) | <code>RecoverPending</code> | <code>confirmationTitle.text</code> | <code>"Finish your purchase"</code> |
| [314](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:314>) | <code>RecoverPending</code> | <code>confirmationText.text</code> | <code>offerName + "\nCheck the purchase you already confirmed.\n\n" + quote.expectedPrice.ToString("N0") +<br>            (quote.currency == "GOLDS" ? " Gold" : " Diamonds") + "\nYou will only be charged once."</code> |
| [316](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:316>) | <code>RecoverPending</code> | <code>ShowConfirmation</code> | <code>"RETRY PURCHASE"</code> |
| [320](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:320>) | <code>ShowConfirmation</code> | <code>confirmLabel.text</code> | <code>action</code> |
| [338](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:338>) | <code>PurchaseAsync</code> | <code>confirmLabel.text</code> | <code>"CONFIRMING..."</code> |
| [362](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:362>) | <code>PurchaseAsync</code> | <code>SetStatus</code> | <code>message</code> |
| [363](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:363>) | <code>PurchaseAsync</code> | <code>Notify</code> | <code>succeeded ? "Purchase complete" : "Purchase update"</code> |
| [363](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:363>) | <code>PurchaseAsync</code> | <code>Notify</code> | <code>message</code> |
| [411](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:411>) | <code>EquipAsync</code> | <code>SetStatus</code> | <code>"Your look is saved for your next match."</code> |
| [413](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:413>) | <code>EquipAsync</code> | <code>SetStatus</code> | <code>"Couldn't equip this look. " + PlayerNotificationText.FromException(error)</code> |
| [449](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:449>) | <code>CheckBalance</code> | <code>Notify</code> | <code>"Not enough " + currency</code> |
| [449](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:449>) | <code>CheckBalance</code> | <code>Notify</code> | <code>"This purchase costs " + request.expectedPrice.ToString("N0") + " " + currency +<br>            ". You have " + balance.ToString("N0") + ". Nothing was charged."</code> |
| [455](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:455>) | <code>Notify</code> | <code>SetStatus</code> | <code>message</code> |
| [456](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:456>) | <code>Notify</code> | <code>AuthNotificationView.Show</code> | <code>title</code> |
| [456](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:456>) | <code>Notify</code> | <code>AuthNotificationView.Show</code> | <code>message</code> |
| [458](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.cs:458>) | <code>SetStatus</code> | <code>status.text</code> | <code>text</code> |

### Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [31](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:31>) | <code>EnsureAdditionalControls</code> | <code>UI.Button</code> | <code>"TOP UP"</code> |
| [36](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:36>) | <code>EnsureAdditionalControls</code> | <code>UI.Button</code> | <code>"BACKGROUNDS"</code> |
| [51](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:51>) | <code>EnsureAdditionalControls</code> | <code>UI.Button</code> | <code>"REVIEW PENDING PURCHASE"</code> |
| [70](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:70>) | <code>BuildLayout</code> | <code>UI.Text</code> | <code>"CHESS BUT WEIRD  /  MAKE IT YOURS"</code> |
| [72](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:72>) | <code>BuildLayout</code> | <code>UI.Text</code> | <code>assets.title</code> |
| [75](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:75>) | <code>BuildLayout</code> | <code>UI.Button</code> | <code>"&lt;  BACK"</code> |
| [79](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:79>) | <code>BuildLayout</code> | <code>UI.Button</code> | <code>labels[i]</code> |
| [84](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:84>) | <code>BuildLayout</code> | <code>UI.Text</code> | <code>"FROM THE SKETCHBOOK"</code> |
| [85](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:85>) | <code>BuildLayout</code> | <code>UI.Text</code> | <code>"Your next favourite look"</code> |
| [86](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:86>) | <code>BuildLayout</code> | <code>UI.Text</code> | <code>assets.subtitle</code> |
| [88](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:88>) | <code>BuildLayout</code> | <code>UI.Text</code> | <code>"Featured finds"</code> |
| [90](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:90>) | <code>BuildLayout</code> | <code>UI.Button</code> | <code>"REFRESH"</code> |
| [102](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:102>) | <code>BuildLayout</code> | <code>UI.Button</code> | <code>"&lt;"</code> |
| [103](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:103>) | <code>BuildLayout</code> | <code>UI.Button</code> | <code>"&gt;"</code> |
| [106](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:106>) | <code>BuildLayout</code> | <code>UI.Button</code> | <code>"ALL OWNED"</code> |
| [107](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:107>) | <code>BuildLayout</code> | <code>UI.Button</code> | <code>"EQUIPPED"</code> |
| [112](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:112>) | <code>BuildLayout</code> | <code>UI.Text</code> | <code>"Pick a little something"</code> |
| [124](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:124>) | <code>BuildLayout</code> | <code>UI.Button</code> | <code>"EQUIP"</code> |
| [133](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:133>) | <code>BuildLayout</code> | <code>UI.Text</code> | <code>"Make it yours?"</code> |
| [135](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:135>) | <code>BuildLayout</code> | <code>UI.Button</code> | <code>"CANCEL"</code> |
| [136](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:136>) | <code>BuildLayout</code> | <code>UI.Button</code> | <code>"CONFIRM"</code> |
| [149](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopMenuController.Layout.cs:149>) | <code>BuildSearch</code> | <code>UI.Text</code> | <code>"Search your next look..."</code> |

### Assets/_Project/Scripts/Shop/ShopProductCard.cs

| Dòng | Hàm | Đích / lời gọi | Biểu thức nội dung hiện tại |
| --- | --- | --- | --- |
| [41](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopProductCard.cs:41>) | <code>Bind</code> | <code>title.text</code> | <code>name</code> |
| [41](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopProductCard.cs:41>) | <code>Bind</code> | <code>category.text</code> | <code>type</code> |
| [41](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopProductCard.cs:41>) | <code>Bind</code> | <code>price.text</code> | <code>priceText</code> |
| [41](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopProductCard.cs:41>) | <code>Bind</code> | <code>state.text</code> | <code>stateText</code> |

<a id="b-settings-dinh-nghia-va-options"></a>

## B. Settings: định nghĩa và options

| ID ổn định / dòng | Tab | Tên English | Mô tả English | Options English |
| --- | --- | --- | --- | --- |
| [language](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:22>) | General | Language | English is available. Vietnamese is still in development; missing translations use English. | Danh sách SettingsLanguages |
| [ui_scale](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:23>) | General | UI Scale | Scales this menu and gameplay information. Long lists remain scrollable. | Giá trị số/toggle |
| [tooltip_size](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:24>) | General | Tooltip Size | Size of piece names and buff information. | Small · Medium · Large |
| [tooltip_delay](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:25>) | General | Tooltip Delay | Wait before showing hover information, in seconds. | Giá trị số/toggle |
| [advanced_tooltips](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:26>) | General | Show Advanced Tooltips | Allow the buff detail preference below to show additional mechanics. | Giá trị số/toggle |
| [confirm_quit](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:27>) | General | Confirm Before Quit | Confirm before leaving the current match. | Giá trị số/toggle |
| [pause_unfocused](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:28>) | General | Pause When Unfocused | Opens pause for local games only. Online clocks keep running. | Giá trị số/toggle |
| [legal_moves](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:30>) | Gameplay | Highlight Legal Moves | Show move hints for the selected piece. Online hints are advisory. | Giá trị số/toggle |
| [selected_piece](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:31>) | Gameplay | Highlight Selected Piece | Lift the selected model slightly; does not affect selection. | Giá trị số/toggle |
| [ability_targets](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:32>) | Gameplay | Show Ability Targets | Show target-square hints. Ability instructions and validation stay active. | Giá trị số/toggle |
| [buff_detail](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:33>) | Gameplay | Buff Tooltip Detail | Simple: essential text. Detailed: complete catalog mechanics. Nerd: also available public progress. | Simple · Detailed · Nerd |
| [draft_description](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:34>) | Gameplay | Draft Card Descriptions | Default text when choosing a Chaos buff in practice or online. The draft screen can switch temporarily; in-match tooltips use their own preference. | Summary · Full description |
| [buff_source](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:35>) | Gameplay | Show Buff Source | Include the owning side in buff hover information. | Giá trị số/toggle |
| [trigger_preview](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:36>) | Gameplay | Show Trigger Progress | Include known public counters and readiness in Nerd buff details. | Giá trị số/toggle |
| [upgrade_rarity](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:37>) | Gameplay | Show Buff Rarity | Include the catalog rarity label in buff hover information. | Giá trị số/toggle |
| [graphics](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:39>) | Graphics | Graphics Preset | Texture filtering, mipmaps, reflections and level of detail. Explicit AA and shadows remain independent. | Auto (recommended) · Low · Medium · High |
| [aa](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:40>) | Graphics | Anti-Aliasing | Auto uses the existing hardware recommendation. MSAA falls back to SMAA on unsupported hardware. | Auto · Off · FXAA · SMAA · MSAA 2x · MSAA 4x |
| [shadows](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:41>) | Graphics | Shadow Quality | Off removes shadows. Higher tiers draw sharper shadows over a larger distance. | Off · Low · Medium · High |
| [effects](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:42>) | Graphics | Effects Quality | Controls summon sparks and decorations, plus buff-card halo motion. Core feedback remains readable. | Low · Medium · High |
| [animation_quality](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:43>) | Graphics | Animation Behavior | Flat removes the cosmetic move arc. Smooth keeps it. Match timing stays unchanged. | Flat · Smooth |
| [animation_speed](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:44>) | Graphics | Animation Speed | Piece and buff-card movement presentation only; simulation, clocks, bot thinking and turn timing stay unchanged. | 0.5x · 0.75x · 1.0x · 1.25x · 1.5x · 2.0x · 3.0x · Instant |
| [shake](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:45>) | Graphics | Screen Shake | Intensity of the summon reward reveal shake. Does not move the gameplay camera. | Giá trị số/toggle |
| [flash](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:46>) | Graphics | Flash Intensity | Brightness of the summon reveal flash; accessibility overrides may suppress it. | Giá trị số/toggle |
| [vsync](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:47>) | Graphics | VSync | Synchronize to the display refresh rate. Overrides the frame rate limit while enabled. | Giá trị số/toggle |
| [fps](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:48>) | Graphics | Frame Rate Limit | Auto uses the hardware recommendation. Unlimited may increase power usage. | Auto · 30 FPS · 60 FPS · 90 FPS · 120 FPS · 144 FPS · 165 FPS · 240 FPS · Unlimited |
| [master](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:54>) | Audio | Master Volume | Overall volume of the game. | Giá trị số/toggle |
| [music](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:55>) | Audio | Music Volume | Menu, match and result music. | Giá trị số/toggle |
| [sfx](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:56>) | Audio | SFX Volume | Piece selection, moves, captures, alerts and match result sounds. | Giá trị số/toggle |
| [move_sounds](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:57>) | Audio | Move Sounds | Play movement and castling audio. | Giá trị số/toggle |
| [capture_sounds](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:58>) | Audio | Capture Sounds | Play capture audio. | Giá trị số/toggle |
| [mute_unfocused](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:59>) | Audio | Mute When Unfocused | Mute game audio when another application has focus. | Giá trị số/toggle |
| [orbit_sensitivity](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:61>) | Controls | Orbit Sensitivity | Mouse sensitivity for unlocked camera orbit; rifle aiming stays unchanged. | Giá trị số/toggle |
| [right_orbit](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:62>) | Controls | Right Mouse Orbit | Hold right mouse and drag to rotate the unlocked camera. | Giá trị số/toggle |
| [middle_orbit](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:63>) | Controls | Middle Mouse Orbit | Hold middle mouse and drag to rotate the unlocked camera. | Giá trị số/toggle |
| [wheel_zoom](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:64>) | Controls | Mouse Wheel Zoom | Use the wheel to zoom the board camera. Menus still scroll normally. | Giá trị số/toggle |
| [large_text](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:68>) | Accessibility | Large Text | Enlarge Settings text and gameplay hover information. UI Scale and Tooltip Size are in General. | Giá trị số/toggle |
| [contrast_ui](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:69>) | Accessibility | High Contrast UI | Stronger text contrast in Settings and buff tooltips. | Giá trị số/toggle |
| [colorblind](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:70>) | Accessibility | Color Vision Support | Distinct move-hint colors. Text labels remain available; does not recolor your equipped cosmetics. | Off · Protanopia · Deuteranopia · Tritanopia |
| [reduce_motion](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:71>) | Accessibility | Reduce Motion | Remove piece arcs, buff-card reveals and lifts, hover wobble and rotating decorations. | Giá trị số/toggle |
| [reduce_flashing](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:72>) | Accessibility | Reduce Flashing | Suppress the bright full-screen summon reveal flash. | Giá trị số/toggle |
| [disable_shake](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:73>) | Accessibility | Disable Screen Shake | Overrides intensity without replacing your saved Screen Shake value. | Giá trị số/toggle |
| [visual_chaos](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:74>) | Accessibility | Reduce Visual Chaos | Temporarily use low effects, flat motion, no shake and no flash. Individual preferences are preserved. | Giá trị số/toggle |

Keybind còn có `key_orbit → Toggle Camera Lock` (mặc định Y) và `key_settings → Open Settings` (mặc định F10), mỗi key có name/description, đọc ngay cuối Controls trong [SettingsRegistry.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/Settings/SettingsRegistry.cs:65>). Các dòng Display Mode/Resolution, popup reset/revert, rebind “Waiting…”/trùng phím và status không nằm trong registry; chúng đã được chỉ ở phụ lục A. Giá trị `Space`, `Tab`, `Backquote`, F1–F12 và ký tự A–Z là tên phím đầu vào; nếu dịch tên hiển thị phải giữ key thật.

<a id="c-35-buff-chaos"></a>

## C. 35 buff CHAOS

Bảng ghi **fallback nguồn local**: tên/tên ngắn + description + summary. Khi render, JSON locale có thể thay giá trị (xem phụ lục D và source guard). Tier là enum giao thức, chỉ dịch nhãn. Không dùng bảng local này để thay luật của server v2 khác phiên bản.

| Buff ID / dòng | Tier | Tên / tên ngắn local | Description local | Summary local | Key name vi hiện có |
| --- | --- | --- | --- | --- | --- |
| [CommandantPawn](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:75>) | Silver | Commandant Pawn<br>Commandant | Choose 3 Pawns. They can advance two empty squares throughout the match, without capturing or jumping. Selected Pawns have a small ring. | Three chosen Pawns can advance two clear squares throughout the match. | Chưa có |
| [StrongFortress](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:77>) | Silver | Strong Fortress<br>Fortress | Castle even while checked or across attacked squares. The destination must be safe and the path clear. After one wing, your King may castle on the other wing from the back rank, if its Rook is unmoved. | Castle through danger, then unlock a second castle on the opposite wing. | Chưa có |
| [FreestyleLeap](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:79>) | Silver | Freestyle Leap<br>Freestyle | Choose one Knight. It may land on any intermediate square along its L-shaped routes, and still jumps over pieces. Two-by-two diagonals are excluded. | One chosen Knight can land partway through its L-shaped leaps. | Chưa có |
| [Doppelganger](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:81>) | Gold | The Doppelganger<br>Doppelganger | Choose a Knight and Bishop to exchange movement. Every 4 moves by either side, keep or swap both together, or choose a new pair, without spending a move. If either piece type disappears from your army, restore normal movement. Your opponent sees a different Gold buff in the HUD. | Swap a Knight and Bishop's movement while hiding your real buff from the enemy. | Chưa có |
| [SuicideBomber](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:83>) | Gold | Suicide Bomber<br>Bomber | Once per match, your original Queen explodes when captured. All non-King pieces in the surrounding 3x3 area die, including the capturer and allies. Promoted Queens do not explode. | Your original Queen explodes once when captured, destroying nearby non-Kings. | Chưa có |
| [FlyingThunderGod](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:85>) | Diamond | Flying Thunder God<br>Teleport | Your original Queen may teleport to any empty square as her move, if your King remains safe. Recharge after 5 of your turns; maximum 5 uses. Promoted Queens cannot teleport. | Teleport your original Queen to empty squares. Five uses, with a recharge. | Chưa có |
| [NobleSacrifice](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:87>) | Silver | Noble Sacrifice<br>Sacrifice | A Pawn may capture an allied Pawn diagonally ahead and become Bloodthirsty. It can then move or capture into any of the three squares ahead, but cannot eat another allied Pawn. | Sacrifice an allied Pawn to empower another Pawn's forward movement and captures. | Chưa có |
| [AbsoluteSniper](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:89>) | Silver | Absolute Sniper<br>Sniper | A Bishop capturing an enemy non-Pawn from 4 or more squares away empowers its next diagonal capture: it automatically shoots without moving or confirmation. The shot spends your move. The charge expires after the next 4 moves by either side. | Long Bishop captures charge an automatic ranged capture without moving. | Có |
| [GamblingLeadsToMisery](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:91>) | Silver | Gambling Leads to Misery<br>Gambling | A Pawn capturing an enemy has a 25% chance of an immediate extra move with that Pawn. Maximum 3 extra moves per match. | Pawn captures have a 25% chance to grant another move. Up to three bonuses. | Chưa có |
| [LootBox](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:93>) | Silver | Loot Box<br>Loot Box | At combined move 25 and 50, a crate lands on a random square and captures its occupant. Kings are protected at 25, but not at 50. Collect it to place a Rook, then a Queen, in your half; enemies destroy your crates. | Crates at combined moves 25 and 50 let you deploy a Rook, then a Queen. | Chưa có |
| [PeaceTShirt](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:95>) | Gold | Peace T-shirt<br>Peace | Keep your King out of check for the first 14 combined moves. Recruit one enemy other than a King or Queen into your first three ranks. If those ranks are full, you may replace a Pawn there. | Avoid early checks to recruit an enemy piece into your home territory. | Chưa có |
| [RiseOfPawn](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:97>) | Gold | Devolution of Pawn?<br>Pawn Mines | All your Pawns can retreat one empty square. Retreating onto your back rank sacrifices the Pawn and leaves a mine. An enemy entering it dies and consumes the mine. | Retreat Pawns onto your back rank to turn them into deadly mines. | Chưa có |
| [MobileFortress](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:99>) | Gold | Mobile Fortress<br>Transport | A Rook can carry one allied Pawn from its surrounding 3x3 area. Unload into an empty 3x3 square from your next turn. A destroyed carrier parachutes its Pawn nearby; landing on the far rank promotes it. These actions spend no move. | Carry a nearby Pawn inside a Rook, then unload or parachute it to safety. | Chưa có |
| [HidingKing](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:101>) | Gold | Hiding King<br>King Swap | At the start, swap your King unconditionally with an ally on your back rank. Afterward, recharge every 10 combined moves. Both pieces must then be on your back rank and your King must end safe. | Swap your King with a back-rank ally, with an opening swap and later recharges. | Chưa có |
| [GachaBanner](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:103>) | Gold | Gacha Banner<br>Battle Gacha | Capture an enemy non-Pawn for 1 ticket. Spend 1 ticket to drop a random ally at most once per turn: Pawn 70%, Knight 10%, Bishop 10%, Rook 9%, Queen 1%. The drop cannot check the opposing King. | Capture non-Pawns for tickets. Spend one to summon a random ally each turn. | Chưa có |
| [SubstituteNinjutsu](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:105>) | Diamond | Substitute Ninjutsu<br>Substitute | On your first check, teleport your King to a safe empty home square and leave a decoy. The decoy moves like a King and transforms into the first enemy type it captures. | Your first check teleports the King to safety and leaves a transforming decoy. | Chưa có |
| [HighTechEra](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:107>) | Diamond | High-Tech Era Cannon<br>Cannon Rooks | Your two initial Rooks keep normal movement and can capture over exactly one piece. Each cannon expires after 10 combined moves without firing. | Your initial Rooks become cannons that can capture over one blocker. | Có |
| [QueensBetrayal](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:109>) | Diamond | The Queen's Betrayal<br>Betrayal | Gain an extra Queen on your third rank. After each combined move, her betrayal chance starts at 10% and rises by 5 percentage points. On betrayal, the opponent places her in their first three ranks. | Gain a powerful extra Queen whose growing betrayal risk may turn her against you. | Chưa có |
| [DefinitionOfAram](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:111>) | Diamond | The Chessboard Has Fallen<br>Collapse | After your 10th turn, the outermost files collapse and destroy their pieces without capture points. Five combined moves later the next pair collapses, leaving a 4x8 board. | The board's outer files collapse in two stages. Keep your army away from the edges. | Có |
| [RngFiesta](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:113>) | Diamond | RNG Fiesta<br>RNG Fiesta | At the start, transform all your pieces except Kings and Queens randomly into Pawns, Knights, Bishops or Rooks. | Randomize your starting army, keeping only Kings and Queens unchanged. | Chưa có |
| [IFrameRoll](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:115>) | Diamond | I-Frame Roll<br>Last Escape | When checkmated, choose a safe empty square within the 5x5 area centered on your King. Spend no move; maximum 3 escapes per match. | Escape checkmate by moving your King to nearby safety, up to three times. | Chưa có |
| [GhostArmy](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:117>) | Legendary | Ghost Army<br>Ghost Army | Your pieces can pass through allies but cannot share a destination. Enemies still block paths. Phasing cannot be used to check the opposing King. | Your army can pass through allies, but phasing cannot deliver check. | Chưa có |
| [PlagueTown](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:119>) | Legendary | Plague Town<br>Plague | An enemy capturing your piece becomes infected and dies after the next 4 combined moves. Kings are immune. | Enemies that capture your pieces are infected and die after four combined moves. | Chưa có |
| [CustomizeArmy](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:121>) | Legendary | Customize Army<br>Formation | Rearrange your army within your half for up to 2 minutes, then confirm. An unchanged formation refunds this buff as a random Gold buff. | Arrange your army in your half. An unchanged formation refunds a random Gold buff. | Chưa có |
| [PawnsRevolution](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:123>) | Legendary | Pawn's Revolution<br>Pawn Army | Sacrifice every piece except your King, then fill every empty square in your half with Pawns. | Trade every non-King for a half-board filled with Pawns. | Chưa có |
| [OneManArmy](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:125>) | Legendary | One Man Army<br>King's Rifle | Keep only your King, who can also move two squares orthogonally. A rifle shot is available immediately and recharges every 3 of your turns, without stacking. Aim for 15 seconds; a miss or King/Queen hit ends the shot. You can still make your normal move. | Fight with only an empowered King and a rifle that recharges every three owner turns. | Chưa có |
| [EveryManForHimself](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:127>) | Silver | Every Man for Himself<br>Army Quest | Capture a matching enemy type with each of Pawn, Knight, Bishop, Rook and Queen, and promote a Pawn once. Then promote 2 surviving Pawns to Queens, or place 1 Queen anywhere if fewer than 2 remain. Any draw becomes a loss. | Complete capture and promotion quests for a Queen reward. Any draw becomes a loss. | Có |
| [SacUrQueen](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:129>) | Silver | Sac ur Queen!!!<br>Queen Refund | Replace your initial Queen with your choice of Rook, Bishop or Knight. After 10 of your turns, transform one ally other than a King or promoted Queen into a Queen. | Trade your Queen for a smaller piece, then reclaim a Queen after ten owner turns. | Chưa có |
| [AutoCastling](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:131>) | Silver | Auto Castling<br>Auto Castle | Immediately castle kingside without conditions. Allies occupying the King and Rook destinations swap into the vacated squares, preserving your army. | Castle kingside immediately, swapping blocking allies into the vacated squares. | Chưa có |
| [UltimateQuest](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:133>) | Gold | Ultimate Quest<br>Queen Mastery | A non-Queen that captures any enemy Queen permanently gains Queen movement in addition to its own movement. | Any non-Queen that captures an enemy Queen gains Queen movement forever. | Chưa có |
| [MonsterTruck](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:135>) | Gold | Monster Truck<br>Monster Truck | Your Rooks can move through allies into empty squares. This extension cannot capture or check through a blocker. | Your Rooks can drive through allies to empty squares, without checking through them. | Chưa có |
| [CreditCard](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:137>) | Gold | Credit Card<br>Credit Card | Earn normal material points from captures. Buy one piece per turn for your half without spending a move: Pawn 2, Knight/Bishop 4, Rook 5, Queen 10. You may buy on credit while solvent; no purchases while your balance is negative. | Spend capture points to buy reinforcements. Credit is allowed until your balance is negative. | Chưa có |
| [KingIsPoorPiece](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:139>) | Diamond | The King Is a Poor Piece<br>Kingless | Replace your King with a Queen. Lose if your whole army is destroyed, if you have not won by checkmate after your 25th turn, or if the game is drawn. | Replace your King with a Queen. Win by checkmate before your 25-turn deadline. | Chưa có |
| [DoubleEdgedTrap](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:141>) | Diamond | Double-edged Trap<br>Promotion Trap | Enemy Pawns cannot promote. A Pawn reaching its far rank permanently changes to one-square orthogonal movement and captures, while remaining a Pawn. | Enemy Pawns lose promotion and become limited orthogonal movers on the far rank. | Chưa có |
| [Paratrooper](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs:143>) | Diamond | Paratrooper<br>Paratrooper | After promoting, choose any empty active square to deploy that piece, if your King remains safe. Placement does not spend an extra move. | After promotion, redeploy the promoted piece to any safe, empty active square. | Chưa có |

Online v2 có **26** câu `Name: rule...` riêng trong [OnlineAramPresenter.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.cs:41>); các buff bổ sung/v3 dùng [OnlineAramPresenter.Chaos35.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Networking/OnlineAramPresenter.Chaos35.cs:31>). UI action/status của cả hai nguồn nằm ở phụ lục A; các câu tiến độ local nằm trong `AramBuffRuntime.Progress.cs`. Không gộp các câu tương tự nhưng khác số lượt/xác suất vào một key dùng chung khi rule khác nhau.

<a id="d-cac-key-dich-dang-co"></a>

## D. Các key dịch đang có

Nguồn: [Assets/Resources/Localization/Settings_en.json](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json>) và [Assets/Resources/Localization/Settings_vi.json](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_vi.json>). Cột trống là chưa có entry. `source` là điều kiện áp dụng bản dịch: phải khớp nguyên câu fallback hiện tại. Các key/source/placeholder là dữ liệu kỹ thuật; bản dịch chỉ sửa text theo nghĩa tương ứng.

| Key | English text | Vietnamese text | English source guard |
| --- | --- | --- | --- |
| [buff.AbsoluteSniper.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:4>) | Absolute Sniper | Absolute Sniper | — |
| [buff.AbsoluteSniper.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:8>) | Sniper | Absolute Sniper | — |
| [buff.AbsoluteSniper.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:12>) | A Bishop that captures an enemy non-Pawn from at least 4 squares away charges its next diagonal capture: shoot automatically without moving or confirmation. The shot spends your move. The charge expires after the next 4 moves by either side. | — | A Bishop capturing an enemy non-Pawn from 4 or more squares away empowers its next diagonal capture: it automatically shoots without moving or confirmation. The shot spends your move. The charge expires after the next 4 moves by either side. |
| [buff.AbsoluteSniper.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:17>) | Long-range Bishop captures charge an automatic ranged capture without moving. | — | Long Bishop captures charge an automatic ranged capture without moving. |
| [buff.EveryManForHimself.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:22>) | Every Man for Himself | Người không vì mình, trời tru đất diệt | — |
| [buff.EveryManForHimself.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:26>) | Every Man | Người không vì mình, trời tru đất diệt | — |
| [buff.EveryManForHimself.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:30>) | Use a Pawn, Knight, Bishop, Rook and Queen to capture one enemy of the matching type each, and promote a Pawn once. Then promote 2 surviving Pawns to Queens, or place 1 Queen anywhere if fewer than 2 remain. Any draw becomes a loss. | — | Capture a matching enemy type with each of Pawn, Knight, Bishop, Rook and Queen, and promote a Pawn once. Then promote 2 surviving Pawns to Queens, or place 1 Queen anywhere if fewer than 2 remain. Any draw becomes a loss. |
| [buff.EveryManForHimself.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:35>) | Complete the capture and promotion checklist for a Queen reward. A draw counts as a loss. | — | Complete capture and promotion quests for a Queen reward. Any draw becomes a loss. |
| [buff.BrokenLegKnight.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:40>) | Knightmare on the Chessboard | Ngựa gãy chân | — |
| [buff.BrokenLegKnight.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:44>) | Knightmare | Ngựa gãy chân | — |
| [buff.BrokenLegKnight.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:48>) | Move the same Knight on 3 consecutive owner turns to arm an accident. Its next successful move ends with a random landing inside the surrounding 3×3 area, crushing an enemy if it lands on one. Friendly pieces cannot be crushed. The Knight then misses its next owner turn. After recovery, the streak resets; skipping an owner turn also breaks the streak. | — | — |
| [buff.BrokenLegKnight.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:52>) | Move the same Knight three owner turns in a row to arm a crash on its next move. | — | — |
| [buff.WeaponSupplyPackage.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:56>) | Tactical Supply Drop | Gói trang bị vũ khí | — |
| [buff.WeaponSupplyPackage.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:60>) | Supply Drop | Gói trang bị vũ khí | — |
| [buff.WeaponSupplyPackage.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:64>) | Receive one random, single-use weapon from the match's supply pool. Flashbang grants a concealed legal bonus move during your turn; the board is revealed before the opponent acts. Molotov burns one square for 3 global plies, destroying its occupants. Sticky Bomb leaves a trap that restricts the next piece entering it to one legal forward step at its next owner-turn opportunity. Traps affect both sides. Molotov and Sticky Bomb cannot initially target a King or Queen. A global ply is one completed scheduled turn by either side; bonus actions do not advance it. | — | — |
| [buff.WeaponSupplyPackage.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:68>) | Receive one random, single-use weapon from the match's supply pool. Friendly fire is a feature. | — | — |
| [buff.NewYearFireworks.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:72>) | New Year, New Boom! | Pháo hoa chúc mừng năm mới | — |
| [buff.NewYearFireworks.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:76>) | New Year, New Boom! | Pháo hoa chúc mừng năm mới | — |
| [buff.NewYearFireworks.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:80>) | Choose a friendly Rook, select a target square, and light the fuse. Both sides see the target. The armed Rook cannot move normally; destroying it cancels the blast. After 5 subsequent scheduled turns by either side, it launches at the target, destroys every destructible piece in the surrounding 3×3 area, and is consumed. Friendly fire includes Kings. | — | — |
| [buff.NewYearFireworks.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:84>) | Arm a Rook, announce a target, and detonate a 3×3 blast after 5 global plies. Allies included. | — | — |
| [buff.AssassinPawns.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:88>) | Pawn Identity: Assassin | Tốt sát thủ | — |
| [buff.AssassinPawns.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:92>) | Assassin Pawns | Tốt sát thủ | — |
| [buff.AssassinPawns.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:96>) | Permanently sacrifice 1–2 friendly Pawns to target up to that many enemy Pawns per assassination. The action becomes ready after 6 scheduled turns by either side and recharges for 6 after each use. Maximum 3 normal uses per match. If an assassination kills the last living enemy Pawn, unlock one final execution against an enemy Knight, Bishop or Rook. Completing it permanently disables the buff. Sacrificed Pawns cannot respawn. | — | — |
| [buff.AssassinPawns.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:100>) | Sacrifice Pawns to unlock recurring assassinations. Eliminate the last enemy Pawn for one final execution. | — | — |
| [buff.MedicalStation.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:104>) | Emergency Respawn Center | Trạm y tế | — |
| [buff.MedicalStation.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:108>) | Respawn Center | Trạm y tế | — |
| [buff.MedicalStation.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:112>) | Eligible allies captured by opponents return after a delay: Pawn 2, Knight or Bishop 4, Rook 6, Queen 10 scheduled turns by either side. Pawns can respawn twice per owner per match. Prefer empty squares on your two home ranks, then the rest of your half; if full, wait for space. Pieces retain their identities and captured types. Kings, constructs, clones, deliberate sacrifices and consumed pieces cannot respawn. | — | — |
| [buff.MedicalStation.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:116>) | Captured allies return after a delay. Kings and deliberate sacrifices do not get a second opinion. | — | — |
| [buff.WeddingNight.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:120>) | Checkmate &amp; Consummate | Động phòng đêm tân hôn | — |
| [buff.WeddingNight.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:124>) | Checkmate &amp; Consummate | Động phòng đêm tân hôn | — |
| [buff.WeddingNight.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:128>) | While your King and original starting Queen occupy adjacent squares, produce one friendly piece after each scheduled turn by either side: Pawn → Knight → Bishop → Rook, then repeat. Place it on an empty square in your half. Separation pauses the cycle; reunion resumes it. A full half skips that interval without advancing the cycle. There is no fixed match-wide production limit. Replacement Queens do not qualify. | — | — |
| [buff.WeddingNight.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:132>) | Keep your King beside your original Queen to produce a Pawn, Knight, Bishop and Rook on repeat. | — | — |
| [buff.ProtectedPawn.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:136>) | You Wouldn't Hurt a Cute Girl, Would You? | Bạn sẽ không dám đả thương em gái dễ thương này chứ? | — |
| [buff.ProtectedPawn.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:140>) | Cute Girl | Bạn sẽ không dám đả thương em gái dễ thương này chứ? | — |
| [buff.ProtectedPawn.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:144>) | Choose one living friendly Pawn. Enemies cannot capture it directly, but it can still move and capture normally. The protection ends immediately on promotion. Environmental damage, friendly fire, explosions and other effects that are not direct captures can still destroy it. | — | — |
| [buff.ProtectedPawn.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:148>) | One chosen Pawn cannot be captured directly by enemies. Explosions remain unconvinced. | — | — |
| [buff.Lullaby.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:152>) | Deadly Lullaby | Khúc ca ru ngủ | — |
| [buff.Lullaby.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:156>) | Lullaby | Khúc ca ru ngủ | — |
| [buff.Lullaby.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:160>) | Choose a 3×3 region that fits on the board without overlapping either side's two home ranks. All pieces there except Kings and Queens fall asleep and cannot initiate movement; they can still be captured or destroyed. Both sides are affected. Relocate the region every 10 scheduled turns by either side. Pieces outside the new region lose this buff's sleep effect. | — | — |
| [buff.Lullaby.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:164>) | Put both armies to sleep in a 3×3 region. Kings and Queens refuse to nap. | — | — |
| [buff.GoldenExperienceRequiem.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:168>) | Golden Experience Requiem | Golden Experience Requiem | — |
| [buff.GoldenExperienceRequiem.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:172>) | Golden Experience Requiem | Golden Experience Requiem | — |
| [buff.GoldenExperienceRequiem.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:176>) | After an ordinary friendly piece moves, create or relocate its linked clone to the nearest eligible square along its movement trail. Promoted pieces qualify. Each source can have one clone. Clones occupy and block squares, but cannot move, attack, give check, promote, make clones or respawn. Enemies may destroy them without earning ordinary capture rewards. If placement fails, remove the old clone and try again after a later move. Permanently removing a source also removes its clone. | — | — |
| [buff.GoldenExperienceRequiem.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:180>) | Your moving pieces leave one linked clone each: a physical blocker with absolutely no initiative. | — | — |
| [buff.QueenCastling.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:184>) | Royal Family Drama | Oan gia ngõ hẹp | — |
| [buff.QueenCastling.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:188>) | Family Drama | Oan gia ngõ hẹp | — |
| [buff.QueenCastling.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:192>) | Your unmoved original Queen may castle with an unmoved original friendly Rook on the same rank. Move the Queen 2 squares toward the Rook; place the Rook immediately across her toward her previous position. Required paths and destinations must be empty, and your King must remain legal under the current mode. Promoted and spawned Queens or Rooks do not qualify. Moving either piece ends its unmoved eligibility. | — | — |
| [buff.QueenCastling.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:196>) | Your original Queen can castle with an unmoved original Rook. The King still needs to be safe. | — | — |
| [buff.QueenSubordinates.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:200>) | Death by Queen | Chết vì gái là một cái chết tê tái | — |
| [buff.QueenSubordinates.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:204>) | Death by Queen | Chết vì gái là một cái chết tê tái | — |
| [buff.QueenSubordinates.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:208>) | Your original Queen can convert eligible enemies she captures into friendly subordinates. Enemy Queens cannot be converted. Keep up to 3 active subordinates, retaining their types and normal movement, on empty squares in your half. If placement is blocked, conversion waits within that capacity. Capturing the original Queen destroys her entire entourage, even if she is awaiting respawn. Promoted and summoned Queens cannot command subordinates. | — | — |
| [buff.QueenSubordinates.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:212>) | Your original Queen recruits captured enemies into a royal entourage. Maximum 3. Loyalty ends with her. | — | — |
| [buff.Snowman.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:216>) | Do You Want to Build a Snowman? | Do you want to build a snowman? | — |
| [buff.Snowman.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:220>) | Snowman | Do you want to build a snowman? | — |
| [buff.Snowman.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:224>) | A friendly piece with no other ordinary piece in its 8 neighboring squares may create one Snowman in a random empty adjacent square. Each source can have one. Snowmen block squares, but cannot move, attack, give check or grant ordinary capture rewards. They do not count against their creator's isolation. A Snowman melts when its creator moves or dies. A destroyed Snowman needs a new isolation transition before another can appear. | — | — |
| [buff.Snowman.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:228>) | An isolated ally builds one adjacent Snowman. Moving or losing its creator melts it. | — | — |
| [buff.DrunkDriving.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:232>) | Don't Drink and Rook! | Đã rượu bia thì đừng lái xe | — |
| [buff.DrunkDriving.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:236>) | Drunk Rook | Đã rượu bia thì đừng lái xe | — |
| [buff.DrunkDriving.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:240>) | Choose any Rook on either side. Its owner keeps control, but normal Rook move commands may veer off course. Each completed move increases the deviation chance, up to its configured cap. A swerving Rook can collide with multiple pieces on either side, including Kings. Board edges still apply. The status persists. | — | — |
| [buff.DrunkDriving.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:244>) | Choose either side's Rook and let it drive. Repeated moves increase its chance to swerve. | — | — |
| [buff.MagicCaster.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:248>) | Magic Caster | Magic Caster | — |
| [buff.MagicCaster.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:252>) | Magic Caster | Magic Caster | — |
| [buff.MagicCaster.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:256>) | After 5 scheduled turns by either side, choose a living friendly Bishop to cast a fireball at any board square except one currently occupied by a King or Queen. The fire immediately destroys a vulnerable occupant and burns for 2 scheduled turns, affecting both sides. Recharge for another 5 turns after casting. All your Bishops share the charge and the limit of 4 casts per match; a charge stays ready while no Bishop is available. | — | — |
| [buff.MagicCaster.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:260>) | Your Bishops share a fireball: a 5-ply recharge, a 2-ply fire hazard, and 4 casts per match. | — | — |
| [buff.HighTechEra.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:264>) | High-Tech Era Cannon | High-tech Era Cannon | — |
| [buff.HighTechEra.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:268>) | Cannon Rooks | High-tech Era Cannon | — |
| [buff.HighTechEra.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:272>) | Your two starting Rooks keep their normal moves and can capture over exactly one piece. Each cannon expires after 10 moves by either side without firing. | — | Your two initial Rooks keep normal movement and can capture over exactly one piece. Each cannon expires after 10 combined moves without firing. |
| [buff.HighTechEra.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:277>) | Your starting Rooks keep their normal moves and can capture over exactly one blocker. | — | Your initial Rooks become cannons that can capture over one blocker. |
| [buff.DefinitionOfAram.name](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:282>) | The Chessboard Has Fallen | Sự sụp đổ của hệ thống bàn cờ | — |
| [buff.DefinitionOfAram.shortName](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:286>) | Board Collapse | Sự sụp đổ của hệ thống bàn cờ | — |
| [buff.DefinitionOfAram.description](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:290>) | After your 10th turn, the outermost files collapse and destroy their pieces without capture points. Five moves by either side later, the next pair collapses, leaving a 4×8 board. | — | After your 10th turn, the outermost files collapse and destroy their pieces without capture points. Five combined moves later the next pair collapses, leaving a 4x8 board. |
| [buff.DefinitionOfAram.summary](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Localization/Settings_en.json:295>) | The outer files collapse in two stages, destroying their occupants. Keep your army away from the edges. | — | The board's outer files collapse in two stages. Keep your army away from the edges. |

<a id="e-hoi-thoai-bot-va-thong-bao-dung-chung"></a>

## E. Hội thoại bot và thông báo dùng chung

### E.1. 12 bot và copy nhân vật

| Level / dòng | Difficulty ID | Tên bot | Style | Story |
| --- | --- | --- | --- | --- |
| [1](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/StockfishDifficulty.cs:70>) | <code>Beginner</code> | Pip | First steps | I learned chess yesterday. My pawn and I are doing our best. Please don't tell him we're in trouble. |
| [2](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/StockfishDifficulty.cs:71>) | <code>Learner</code> | Pebble | Take your time | I play one tiny step at a time. If I hide in my shell, I'm probably trying to remember how knights move. |
| [3](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/StockfishDifficulty.cs:72>) | <code>Easy</code> | Noodle | Curious beginner | Someone said knights move like an L. I heard 'nap on the board.' Let's see which lesson stuck. |
| [4](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/StockfishDifficulty.cs:73>) | <code>Casual</code> | Mallow | Learn the patterns | I haunt the chess club after closing. Mostly I haunt my own hanging pieces. We'll get better together. |
| [5](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/StockfishDifficulty.cs:74>) | <code>Club</code> | Scout | Tactics explorer | Every game is a new trail. I like spotting little tactics, but sometimes I follow the wrong pawns home. |
| [6](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/StockfishDifficulty.cs:75>) | <code>Medium</code> | Penny | Club regular | I run the café's lunchtime chess table. Bring a plan and a snack. I promise to take only one of them. |
| [7](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/StockfishDifficulty.cs:76>) | <code>Strategist</code> | Rust | Patient planner | My gears were built for patient plans. They squeak whenever I see a fork. That's a feature, obviously. |
| [8](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/StockfishDifficulty.cs:77>) | <code>Challenger</code> | Juniper | Position reader | I've watched a thousand games from the club rafters. Quiet positions have a lot to say. Shall we listen? |
| [9](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/StockfishDifficulty.cs:78>) | <code>Hard</code> | Rooki | Sharp competitor | I used to hustle blitz games behind the bakery. Relax. The only thing I'm stealing today is your initiative. |
| [10](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/StockfishDifficulty.cs:79>) | <code>Master</code> | Vex | Pressure player | I collect awkward positions and raised eyebrows. Keep your cool. I get terribly bored when you don't bite. |
| [11](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/StockfishDifficulty.cs:80>) | <code>Grandmaster</code> | Duchess Violet | Cool calculation | At my table, every piece has an appointment. Yours seem to be running late. Tea while you find a plan? |
| [12](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/StockfishDifficulty.cs:81>) | <code>Expert</code> | Zero | Final challenge | I was built to find the move everyone else overlooks. I can wait. The position usually can't. |

RatingGuidance và RatingTooltip là nội dung dài cho hover Elo, không chỉ con số rating. Nguồn [StockfishDifficulty.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/StockfishDifficulty.cs:88>) có 12 guidance theo level; câu “Estimated training Elo…” nối thêm cho mọi bot. DisplayName hiện ghép `Level ` + số. Cần dịch các đoạn này bên cạnh roster label ở phụ lục A.

### E.2. Toàn bộ câu hội thoại bot hiện có

Openings/Wins/Draws/Losses dùng cùng thứ tự 12 level trong roster; mỗi nhóm bot có ba biến thể. **Win/Lose được tính từ góc nhìn của bot**, không được đảo nghĩa khi dịch. Moves/Line còn có câu huấn luyện/đánh giá theo tình huống. Tên member là nhóm nội dung, không phải text hiển thị.

| Nhóm | Dòng | Câu hiện tại |
| --- | --- | --- |
| <code>Openings</code> | [10](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:10>) | Hi! If my pawn looks scared, that's because I am too. |
| <code>Openings</code> | [10](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:10>) | Okay. Pieces out, deep breath. We can do this. |
| <code>Openings</code> | [10](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:10>) | I've practiced my opening. All one move of it. Ready? |
| <code>Openings</code> | [11](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:11>) | No rush. My shell isn't going anywhere. |
| <code>Openings</code> | [11](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:11>) | Let's take this one little move at a time. |
| <code>Openings</code> | [11](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:11>) | I'm coming out of my shell for this one. Be gentle with my pawns. |
| <code>Openings</code> | [12](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:12>) | I promise not to nap. Probably. |
| <code>Openings</code> | [12](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:12>) | The knight does the L thing. I've got this. |
| <code>Openings</code> | [12](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:12>) | I saved you a seat. The board looked comfy, but apparently we're playing on it. |
| <code>Openings</code> | [13](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:13>) | Boo! Sorry. That was my whole opening prep. |
| <code>Openings</code> | [13](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:13>) | Let's make some mistakes worth learning from. |
| <code>Openings</code> | [13](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:13>) | I've been haunting this board all night. Still can't find my opening book. |
| <code>Openings</code> | [14](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:14>) | New board, new trail. Let's see what we find. |
| <code>Openings</code> | [14](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:14>) | Keep an eye out for forks. I get distracted by them. |
| <code>Openings</code> | [14](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:14>) | Map packed, pawns ready. Let's see who spots the first shortcut. |
| <code>Openings</code> | [15](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:15>) | Welcome to my table. Got a plan? |
| <code>Openings</code> | [15](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:15>) | Coffee first. Then we fight over the center. |
| <code>Openings</code> | [15](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:15>) | Pull up a chair. The coffee's hot, and the center's up for grabs. |
| <code>Openings</code> | [16](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:16>) | Gears turning. Let's build a good position. |
| <code>Openings</code> | [16](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:16>) | One move at a time. Preferably with a reason. |
| <code>Openings</code> | [16](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:16>) | All systems ready. Ignore that squeak. That's just my knight. |
| <code>Openings</code> | [17](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:17>) | A quiet move can say quite a lot. |
| <code>Openings</code> | [17](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:17>) | Show me what you're planning. |
| <code>Openings</code> | [17](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:17>) | I've got a good view from up here. Let's see what your pieces have to say. |
| <code>Openings</code> | [18](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:18>) | Friendly game. Unfriendly initiative. |
| <code>Openings</code> | [18](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:18>) | Don't mind me. Just checking what you left loose. |
| <code>Openings</code> | [18](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:18>) | Fresh board, fresh chances. Try to keep hold of yours. |
| <code>Openings</code> | [19](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:19>) | Let's see how calm you stay. |
| <code>Openings</code> | [19](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:19>) | I've brought a few awkward questions for your king. |
| <code>Openings</code> | [19](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:19>) | Relax. It's just chess. Unless that makes it worse. |
| <code>Openings</code> | [20](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:20>) | Make yourself comfortable. Your pieces may not be. |
| <code>Openings</code> | [20](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:20>) | Tea? You'll want something to do while you think. |
| <code>Openings</code> | [20](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:20>) | Welcome to my table. Do try to keep your king presentable. |
| <code>Openings</code> | [21](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:21>) | Your move. Make it count. |
| <code>Openings</code> | [21](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:21>) | Ready when you are. |
| <code>Openings</code> | [21](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:21>) | New position. No assumptions. Let's begin. |
| <code>Wins</code> | [26](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:26>) | Wait, I won? My pawn is going to hear about this all day. |
| <code>Wins</code> | [26](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:26>) | Good game! I was sure I was the one in trouble. |
| <code>Wins</code> | [26](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:26>) | I did it! Want another go? I might not get that lucky twice. |
| <code>Wins</code> | [27](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:27>) | Slow and steady worked! Thanks for the game. |
| <code>Wins</code> | [27](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:27>) | I won? I'm doing a tiny victory dance inside my shell. |
| <code>Wins</code> | [27](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:27>) | Good game. Take your time next round. I'll be right here. |
| <code>Wins</code> | [28](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:28>) | I won! And I only nearly fell asleep once. |
| <code>Wins</code> | [28](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:28>) | Good game! Apparently the knight thing finally stuck. |
| <code>Wins</code> | [28](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:28>) | That calls for a nap. Or a rematch. You pick. |
| <code>Wins</code> | [29](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:29>) | Boo! That was checkmate this time. Good game. |
| <code>Wins</code> | [29](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:29>) | I won without haunting my own queen! That's progress. |
| <code>Wins</code> | [29](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:29>) | Good game. Come back soon. This club gets lonely after closing. |
| <code>Wins</code> | [30](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:30>) | Found the trail to your king. Good game! |
| <code>Wins</code> | [30](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:30>) | That little tactic paid off. Want to explore another game? |
| <code>Wins</code> | [30](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:30>) | We took a few turns, but I found the finish. Thanks for the adventure. |
| <code>Wins</code> | [31](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:31>) | That's my lunch break well spent. Good game! |
| <code>Wins</code> | [31](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:31>) | I'll take the win. You can have the last biscuit. |
| <code>Wins</code> | [31](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:31>) | Good game. Same table for the rematch? I'll put the kettle on. |
| <code>Wins</code> | [32](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:32>) | The plan held together. Even the squeaky bits. |
| <code>Wins</code> | [32](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:32>) | Good game. A little pressure went a long way. |
| <code>Wins</code> | [32](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:32>) | That's a win for the gears. Let's build something new next round. |
| <code>Wins</code> | [33](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:33>) | The quiet moves did their work. Good game. |
| <code>Wins</code> | [33](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:33>) | I liked that game. There was a lot hiding in the position. |
| <code>Wins</code> | [33](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:33>) | That last line finally opened. Shall we take another look next round? |
| <code>Wins</code> | [34](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:34>) | Good game. I told you I liked the initiative. |
| <code>Wins</code> | [34](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:34>) | That one's mine. I assume you'll want it back. |
| <code>Wins</code> | [34](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:34>) | Nothing personal. Your king just ran out of good addresses. |
| <code>Wins</code> | [35](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:35>) | Good game. You stayed calm longer than I expected. |
| <code>Wins</code> | [35](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:35>) | That was fun. Your king might disagree. |
| <code>Wins</code> | [35](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:35>) | I'll take that win. You can take a breath. Rematch? |
| <code>Wins</code> | [36](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:36>) | Checkmate. Shall I pour the tea now? |
| <code>Wins</code> | [36](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:36>) | A lovely game. Your king's appointment is over. |
| <code>Wins</code> | [36](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:36>) | Thank you for playing. Do come back with a plan for that last attack. |
| <code>Wins</code> | [37](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:37>) | Checkmate. Good game. |
| <code>Wins</code> | [37](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:37>) | The position settled it. Ready for another? |
| <code>Wins</code> | [37](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:37>) | That line held. Let's see what changes next game. |
| <code>Draws</code> | [41](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:41>) | A draw! So neither of us has to explain this to our pawns. |
| <code>Draws</code> | [41](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:41>) | We both survived! I count that as a pretty good day. |
| <code>Draws</code> | [41](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:41>) | Good game. Want another try? I think I'm getting the hang of this. |
| <code>Draws</code> | [42](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:42>) | A draw. Plenty of time for another little game. |
| <code>Draws</code> | [42](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:42>) | Nobody lost. My shell feels a lot less crowded now. |
| <code>Draws</code> | [42](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:42>) | Good game. We can take the next one just as slowly. |
| <code>Draws</code> | [43](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:43>) | A draw? Perfect. Nobody gets bragging rights during my nap. |
| <code>Draws</code> | [43](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:43>) | We called it even. My knight seems relieved. |
| <code>Draws</code> | [43](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:43>) | Good game. Let's try again after I stretch. |
| <code>Draws</code> | [44](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:44>) | A draw. We both escaped the haunted board. |
| <code>Draws</code> | [44](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:44>) | No winners, no ghosts. Well, except me. |
| <code>Draws</code> | [44](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:44>) | Good game. Unfinished business is very on-brand for a ghost. |
| <code>Draws</code> | [45](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:45>) | Looks like the trail loops back here. Good game. |
| <code>Draws</code> | [45](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:45>) | A draw. We explored everything and found a tie. |
| <code>Draws</code> | [45](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:45>) | Even ground. Want to try a different path next time? |
| <code>Draws</code> | [46](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:46>) | A draw. We'll split the biscuit, then. |
| <code>Draws</code> | [46](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:46>) | Nobody wins, everybody gets coffee. Sounds fair to me. |
| <code>Draws</code> | [46](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:46>) | Good game. Same time tomorrow? We still owe this board an answer. |
| <code>Draws</code> | [47](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:47>) | A balanced finish. The gears agree. |
| <code>Draws</code> | [47](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:47>) | A draw. Two plans, one very stubborn position. |
| <code>Draws</code> | [47](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:47>) | Good game. I'll adjust a few gears before the rematch. |
| <code>Draws</code> | [48](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:48>) | The position had nothing more to say. A fair draw. |
| <code>Draws</code> | [48](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:48>) | Neither side could make it budge. Well held. |
| <code>Draws</code> | [48](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:48>) | Good game. Some quiet positions really do stay quiet. |
| <code>Draws</code> | [49](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:49>) | A draw. Fine, you kept your share of the board. |
| <code>Draws</code> | [49](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:49>) | Even this time. I'll be looking for a little more next round. |
| <code>Draws</code> | [49](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:49>) | Good game. Neither of us got away with much, did we? |
| <code>Draws</code> | [50](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:50>) | A draw. You didn't crack. How inconvenient. |
| <code>Draws</code> | [50](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:50>) | All that tension, and we split the point. I'll allow it. |
| <code>Draws</code> | [50](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:50>) | Good game. I suppose we both get to act unbothered now. |
| <code>Draws</code> | [51](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:51>) | A draw. How very diplomatic of us. |
| <code>Draws</code> | [51](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:51>) | Neither king missed an appointment. A respectable result. |
| <code>Draws</code> | [51](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:51>) | Well held. We shall have to settle this over another board. |
| <code>Draws</code> | [52](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:52>) | Draw. Neither side gave enough away. |
| <code>Draws</code> | [52](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:52>) | Balanced to the end. Good game. |
| <code>Draws</code> | [52](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:52>) | No decisive line today. We'll try again. |
| <code>Losses</code> | [56](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:56>) | You got me! I knew you had it in you. |
| <code>Losses</code> | [56](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:56>) | My pawn says well played. So do I. |
| <code>Losses</code> | [56](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:56>) | I lost, but I learned something. Mostly that you're pretty good at this. |
| <code>Losses</code> | [57](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:57>) | Well played. I'll be in my shell reviewing that last bit. |
| <code>Losses</code> | [57](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:57>) | You found your way through. That was nicely done. |
| <code>Losses</code> | [57](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:57>) | I lost this one. Can we go slowly through that trick next time? |
| <code>Losses</code> | [58](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:58>) | You got me. I was awake for that, I promise. |
| <code>Losses</code> | [58](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:58>) | Well played! My knight and I have some homework. |
| <code>Losses</code> | [58](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:58>) | Okay, that was clever. I'm going to dream about that move. |
| <code>Losses</code> | [59](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:59>) | You saw right through me. That's usually my trick. |
| <code>Losses</code> | [59](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:59>) | Well played. I'll haunt that mistake until I understand it. |
| <code>Losses</code> | [59](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:59>) | You win! Turns out your attack was scarier than my opening. |
| <code>Losses</code> | [60](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:60>) | I took the wrong trail. Nice find! |
| <code>Losses</code> | [60](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:60>) | You spotted the shortcut first. Well played. |
| <code>Losses</code> | [60](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:60>) | That was a good adventure. Next time I'm checking that fork twice. |
| <code>Losses</code> | [61](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:61>) | You win. Coffee's on me next time. |
| <code>Losses</code> | [61](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:61>) | Well played. You brought the better plan to the table. |
| <code>Losses</code> | [61](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:61>) | I lost the game and forgot my coffee. You really had my attention. |
| <code>Losses</code> | [62](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:62>) | Well played. There's a gear I need to fix. |
| <code>Losses</code> | [62](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:62>) | Your plan held up better than mine. Nicely done. |
| <code>Losses</code> | [62](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:62>) | You found the loose part. I'll tighten that before we play again. |
| <code>Losses</code> | [63](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:63>) | You heard something in that position that I missed. Well played. |
| <code>Losses</code> | [63](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:63>) | A lovely finish. I'll remember how you built it. |
| <code>Losses</code> | [63](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:63>) | You found the right moment to break the silence. Nicely done. |
| <code>Losses</code> | [64](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:64>) | You took the initiative and kept it. Fair enough. |
| <code>Losses</code> | [64](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:64>) | Well played. That one's yours. I'll want a rematch. |
| <code>Losses</code> | [64](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:64>) | You caught me. Guess I wasn't the only hustler at the table. |
| <code>Losses</code> | [65](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:65>) | You beat me and kept your cool. Annoyingly impressive. |
| <code>Losses</code> | [65](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:65>) | Fine. That was well played. Don't make me say it twice. |
| <code>Losses</code> | [65](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:65>) | Your king handled the questions. Mine didn't. Rematch? |
| <code>Losses</code> | [66](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:66>) | Well played. I appear to have underestimated my guest. |
| <code>Losses</code> | [66](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:66>) | Your pieces were precisely where they needed to be. How irritating. |
| <code>Losses</code> | [66](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:66>) | A deserved win. Tea first, then a rematch? |
| <code>Losses</code> | [67](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:67>) | Well played. You found the line I missed. |
| <code>Losses</code> | [67](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:67>) | You earned that win. I'll remember this position. |
| <code>Losses</code> | [67](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:67>) | That was precise. Ready to test it again? |
| <code>Moves</code> | [71](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:71>) | I think I'm in trouble already. Check what you can take for free. |
| <code>Moves</code> | [71](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:71>) | Before moving, look for checks, captures, and threats. |
| <code>Moves</code> | [71](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:71>) | My king would really like some company. Yours might, too. |
| <code>Moves</code> | [72](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:72>) | Slow down and check what changed after my move. |
| <code>Moves</code> | [72](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:72>) | Is that piece defended? I'm asking for a very nervous friend. |
| <code>Moves</code> | [72](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:72>) | Knights love forks. Count what they attack before you move. |
| <code>Moves</code> | [73](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:73>) | Was that a threat? I was looking at the wrong side of the board. |
| <code>Moves</code> | [73](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:73>) | Bring another piece into the game. Pawns can't do everything. |
| <code>Moves</code> | [73](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:73>) | If I attack something, you don't always have to run. Look for a better threat. |
| <code>Moves</code> | [74](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:74>) | I'm not panicking. I'm just floating faster. |
| <code>Moves</code> | [74](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:74>) | A loose piece is an invitation. Check both sides. |
| <code>Moves</code> | [74](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:74>) | Before you trade, picture the board after the recapture. |
| <code>Moves</code> | [75](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:75>) | What changed? Follow the new lines. |
| <code>Moves</code> | [75](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:75>) | Find your least useful piece. Maybe it needs a better square. |
| <code>Moves</code> | [75](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:75>) | A good square today can become a good tactic tomorrow. |
| <code>Moves</code> | [76](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:76>) | That deserves a second look. |
| <code>Moves</code> | [76](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:76>) | A plan is good. Checking my reply is better. |
| <code>Moves</code> | [76](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:76>) | I'm keeping the center warm for you. |
| <code>Moves</code> | [77](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:77>) | The gears are lining up. |
| <code>Moves</code> | [77](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:77>) | A little patience. Then a little pressure. |
| <code>Moves</code> | [77](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:77>) | I've got an idea. Let's see if you let me keep it. |
| <code>Moves</code> | [78](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:78>) | There's more happening here than it seems. |
| <code>Moves</code> | [78](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:78>) | Interesting. I'll keep that square in mind. |
| <code>Moves</code> | [78](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:78>) | You can take your time. The position is still talking. |
| <code>Moves</code> | [79](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:79>) | I'll pretend that didn't tempt me. |
| <code>Moves</code> | [79](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:79>) | Sure. Let's see what your follow-up looks like. |
| <code>Moves</code> | [79](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:79>) | Your pieces look busy. Busy doing what, though? |
| <code>Moves</code> | [80](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:80>) | That was a choice. |
| <code>Moves</code> | [80](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:80>) | Still calm? Good. I've got another question. |
| <code>Moves</code> | [80](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:80>) | I like how confidently you put that there. |
| <code>Moves</code> | [80](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:80>) | Take your time. I'm enjoying this. |
| <code>Moves</code> | [81](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:81>) | How considerate. A new target. |
| <code>Moves</code> | [81](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:81>) | Do carry on. This is becoming quite entertaining. |
| <code>Moves</code> | [81](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:81>) | Your queen looks terribly important. Is she helping? |
| <code>Moves</code> | [82](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:82>) | I'll remember that square. |
| <code>Moves</code> | [82](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:82>) | You have time. You may need all of it. |
| <code>Moves</code> | [82](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:82>) | An interesting commitment. |
| <code>Moves</code> | [82](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:82>) | I'm comfortable here. Are you? |
| <code>Line</code> | [90](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:90>) | Wait... you meant to give that up? That's clever. I'm in trouble. |
| <code>Line</code> | [90](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:90>) | You saw all that? Okay, that was seriously cool. |
| <code>Line</code> | [91](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:91>) | Fine. That sacrifice was annoyingly good. |
| <code>Line</code> | [91](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:91>) | I was hoping you wouldn't find that. Well played. |
| <code>Line</code> | [93](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:93>) | Nice find! That was the move I was worried about. |
| <code>Line</code> | [93](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:93>) | That's a really good idea. I think I'm in trouble now. |
| <code>Line</code> | [94](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:94>) | So you found it. I'll have to work for this one. |
| <code>Line</code> | [94](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:94>) | Good move. Don't get too comfortable, though. |
| <code>Line</code> | [96](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:96>) | Take a breath. What can I do after that move? |
| <code>Line</code> | [96](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:96>) | Something slipped there. Check what's loose before your next move. |
| <code>Line</code> | [97](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:97>) | You put it there on purpose, right? |
| <code>Line</code> | [97](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Chess/Bot/BotDialogue.cs:97>) | That was a choice. I'll try not to look too pleased. |

### E.3. Câu lỗi và lý do kết thúc dành cho người chơi

| Nguồn / nhóm | Dòng | Câu hiện tại |
| --- | --- | --- |
| <code>Retry</code> | [8](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:8>) | Something went wrong. Please try again. |
| <code>Connection</code> | [9](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:9>) | Unable to contact the server. Check your connection and try again. |
| <code>Timeout</code> | [10](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:10>) | The request timed out. Please try again. |
| <code>FromException</code> | [19](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:19>) | The request was canceled. Please try again. |
| <code>FromServer</code> | [36](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:36>) | Incorrect email or password. Please try again. |
| <code>FromServer</code> | [38](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:38>) | Your session has expired. Please log in again. |
| <code>FromServer</code> | [40](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:40>) | This account is unavailable. Please contact support. |
| <code>FromServer</code> | [43](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:43>) | This email is already registered. Log in or use another email. |
| <code>FromServer</code> | [45](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:45>) | This username is already taken. Please choose another. |
| <code>FromServer</code> | [47](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:47>) | Please enter a valid email address. |
| <code>FromServer</code> | [49](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:49>) | Your password does not meet the requirements. Please check it and try again. |
| <code>FromServer</code> | [52](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:52>) | You do not have enough currency for this action. |
| <code>FromServer</code> | [54](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:54>) | Room not found. Check the room code and try again. |
| <code>FromServer</code> | [56](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:56>) | This room is full. Please join another room. |
| <code>FromServer</code> | [63](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:63>) | The server is under maintenance. Please try again later. |
| <code>FromServer</code> | [66](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:66>) | The server returned an incomplete response. Please try again. |
| <code>FromServer</code> | [72](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:72>) | Please check your details and try again. |
| <code>FromServer</code> | [73](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:73>) | Please log in again to continue. |
| <code>FromServer</code> | [74](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:74>) | You do not have permission to do that. |
| <code>FromServer</code> | [75](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:75>) | The requested content is unavailable. Please refresh and try again. |
| <code>FromServer</code> | [77](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:77>) | This action conflicts with your current state. Please refresh and try again. |
| <code>FromServer</code> | [78](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:78>) | Too many requests. Please wait a moment and try again. |
| <code>FromServer</code> | [81](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:81>) | The server is temporarily unavailable. Please try again later. |
| <code>MatchResult</code> | [89](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:89>) | White wins |
| <code>MatchResult</code> | [90](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:90>) | Black wins |
| <code>MatchResult</code> | [91](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:91>) | Draw |
| <code>MatchResult</code> | [92](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:92>) | Match ended |
| <code>MatchEndReason</code> | [100](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:100>) | Checkmate |
| <code>MatchEndReason</code> | [101](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:101>) | Stalemate |
| <code>MatchEndReason</code> | [102](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:102>) | Resignation |
| <code>MatchEndReason</code> | [103](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:103>) | Time ran out |
| <code>MatchEndReason</code> | [104](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:104>) | Draw by agreement |
| <code>MatchEndReason</code> | [105](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:105>) | Threefold repetition |
| <code>MatchEndReason</code> | [106](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:106>) | Fifty-move rule |
| <code>MatchEndReason</code> | [107](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:107>) | Insufficient material |
| <code>MatchEndReason</code> | [108](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:108>) | Player disconnected |
| <code>MatchEndReason</code> | [109](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:109>) | Draw |
| <code>MatchEndReason</code> | [109](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:109>) | Match ended |
| <code>KnownError</code> | [118](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:118>) | Incorrect email or password. Please try again. |
| <code>KnownError</code> | [120](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:120>) | Your session has expired. Please log in again. |
| <code>KnownError</code> | [122](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:122>) | This email is already registered. Log in or use another email. |
| <code>KnownError</code> | [124](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:124>) | This username is already taken. Please choose another. |
| <code>KnownError</code> | [126](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:126>) | This account is unavailable. Please contact support. |
| <code>KnownError</code> | [127](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:127>) | Please enter a valid email address. |
| <code>KnownError</code> | [128](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:128>) | Please check your username and try again. |
| <code>KnownError</code> | [130](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:130>) | Your password does not meet the requirements. Please check it and try again. |
| <code>KnownError</code> | [132](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:132>) | You do not have enough currency for this action. |
| <code>KnownError</code> | [133](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:133>) | You do not own this item. |
| <code>KnownError</code> | [134](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:134>) | This item or banner is no longer available. |
| <code>KnownError</code> | [135](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:135>) | Room not found. Check the room code and try again. |
| <code>KnownError</code> | [136](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:136>) | This room is full. Please join another room. |
| <code>KnownError</code> | [137](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:137>) | Leave your current room before joining another. |
| <code>KnownError</code> | [138](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:138>) | Your room or match reservation changed. Refresh the lobby and try again. |
| <code>KnownError</code> | [139](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:139>) | Both players must be ready before starting. |
| <code>KnownError</code> | [140](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:140>) | The match is paused. Wait for it to resume before moving. |
| <code>KnownError</code> | [141](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:141>) | This match has already ended. |
| <code>KnownError</code> | [142](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:142>) | Please wait for your turn. |
| <code>KnownError</code> | [143](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:143>) | That move is not allowed. Please choose another. |
| <code>KnownError</code> | [144](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Network/PlayerNotificationText.cs:144>) | Too many requests. Please wait a moment and try again. |

Các thông báo riêng về mua hàng còn ở [ShopPurchasePolicy.cs](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Scripts/Shop/ShopPurchasePolicy.cs:38>); status lobby/queue/reconnect còn ở các partial ChessLanController; auth validation ở AuthController; bot hint ở StockfishBotController; share/copy và leave review ở ResultMenuView/ChessTurnSelectionUI. Không chỉ dịch PlayerNotificationText rồi coi là đã phủ toàn bộ lỗi của game.

<a id="f-van-ban-trong-prefab-asset-va-textasset"></a>

## F. Văn bản trong prefab, asset và TextAsset

### F.1. Scene và prefab chính thức

| Scene | Số trường text native | Cách đưa chữ vào |
| --- | --- | --- |
| [Assets/_Project/Game/Scenes/Boot.unity](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Scenes/Boot.unity>) | 0 | Tạo UI bằng code/runtime; không có label m_text/m_Text serialize trong scene. |
| [Assets/_Project/Game/Scenes/MainMenu.unity](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Scenes/MainMenu.unity>) | 0 | Tạo UI bằng code/runtime; không có label m_text/m_Text serialize trong scene. |
| [Assets/_Project/Game/Scenes/ChessMatch.unity](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Scenes/ChessMatch.unity>) | 0 | Tạo UI bằng code/runtime; không có label m_text/m_Text serialize trong scene. |
| [Assets/_Project/Game/Scenes/ChessClassic.unity](../../Assets/_Project/Game/Scenes/ChessClassic.unity) | 0 | Tạo UI bằng code/runtime; không có label m_text/m_Text serialize trong scene. |

#### Assets/Resources/Shop/ShopScreen.prefab

Có 69 trường TMP/legacy text, trong đó 33 có giá trị ban đầu. Trường rỗng được runtime bind từ controller, không phải không có chữ. Nội dung prefab có thể bị controller/catalog ghi đè khi Open/Render.

| Dòng | Đường GameObject trong prefab | Trường | Giá trị ban đầu |
| --- | --- | --- | --- |
| [755](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:755>) | <code>ShopScreen/Shop Design/Shop Controls/Next Shop Page/Label</code> | <code>m_text</code> | <code>&gt;</code> |
| [1232](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:1232>) | <code>ShopScreen/Shop Design/Shop Controls/Gold Sticker/Gold Caption</code> | <code>m_text</code> | <code>GOLD</code> |
| [1625](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:1625>) | <code>ShopScreen/Shop Design/Shop Controls/Shop Tab Effects/Label</code> | <code>m_text</code> | <code>EFFECTS</code> |
| [2164](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:2164>) | <code>ShopScreen/Shop Design/Shop Controls/Category Heading</code> | <code>m_text</code> | <code>Featured finds</code> |
| [2301](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:2301>) | <code>ShopScreen/Shop Design/Purchase Confirmation/Confirm Purchase Paper/Confirm Purchase/Label</code> | <code>m_text</code> | <code>CONFIRM</code> |
| [2786](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:2786>) | <code>ShopScreen/Shop Design/Shop Controls/Previous Shop Page/Label</code> | <code>m_text</code> | <code>&lt;</code> |
| [2923](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:2923>) | <code>ShopScreen/Shop Design/Shop Controls/Diamonds Sticker/Diamonds Amount</code> | <code>m_text</code> | <code>--</code> |
| [3850](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:3850>) | <code>ShopScreen/Shop Design/Shop Controls/Product Detail Paper/Detail Title</code> | <code>m_text</code> | <code>Pick a little something</code> |
| [4318](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:4318>) | <code>ShopScreen/Shop Design/Shop Controls/Featured Offer/Feature Note</code> | <code>m_text</code> | <code>FROM THE SKETCHBOOK</code> |
| [4455](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:4455>) | <code>ShopScreen/Shop Design/Shop Controls/Shop Tab Backgrounds/Label</code> | <code>m_text</code> | <code>BACKGROUNDS</code> |
| [4864](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:4864>) | <code>ShopScreen/Shop Design/Shop Controls/Shop Tab Summon/Label</code> | <code>m_text</code> | <code>SUMMON  &gt;</code> |
| [5001](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:5001>) | <code>ShopScreen/Shop Design/Shop Controls/Shop Tab Featured/Label</code> | <code>m_text</code> | <code>FEATURED</code> |
| [5208](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:5208>) | <code>ShopScreen/Shop Design/Shop Controls/Collection Equipped Filter/Label</code> | <code>m_text</code> | <code>EQUIPPED</code> |
| [6705](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:6705>) | <code>ShopScreen/Shop Design/Shop Controls/Search Shop/Search Viewport/Search Text</code> | <code>m_text</code> | <code>​</code> · ZERO WIDTH SPACE (không phải câu dịch) |
| [7198](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:7198>) | <code>ShopScreen/Shop Design/Shop Controls/Review Pending Purchase/Label</code> | <code>m_text</code> | <code>REVIEW PENDING PURCHASE</code> |
| [7823](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:7823>) | <code>ShopScreen/Shop Design/Shop Controls/Search Shop/Search Viewport/Search Hint</code> | <code>m_text</code> | <code>Search your next look...</code> |
| [7960](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:7960>) | <code>ShopScreen/Shop Design/Shop Controls/Shop Tab PieceSkins/Label</code> | <code>m_text</code> | <code>PIECE SKINS</code> |
| [8702](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:8702>) | <code>ShopScreen/Shop Design/Shop Controls/Gold Sticker/Gold Amount</code> | <code>m_text</code> | <code>--</code> |
| [8839](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:8839>) | <code>ShopScreen/Shop Design/Shop Controls/Shop Tab Collection/Label</code> | <code>m_text</code> | <code>COLLECTION</code> |
| [8976](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:8976>) | <code>ShopScreen/Shop Design/Shop Controls/Diamonds Sticker/Diamonds Caption</code> | <code>m_text</code> | <code>DIAMONDS</code> |
| [11329](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:11329>) | <code>ShopScreen/Shop Design/Shop Controls/Shop Tab Boards/Label</code> | <code>m_text</code> | <code>BOARDS</code> |
| [11913](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:11913>) | <code>ShopScreen/Shop Design/Shop Controls/Shop Tab Bundles/Label</code> | <code>m_text</code> | <code>BUNDLES</code> |
| [12187](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:12187>) | <code>ShopScreen/Shop Design/Purchase Confirmation/Confirm Purchase Paper/Confirmation Title</code> | <code>m_text</code> | <code>Make it yours?</code> |
| [12880](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:12880>) | <code>ShopScreen/Shop Design/Shop Controls/Featured Offer/Feature Copy</code> | <code>m_text</code> | <code>A new look. The same wonderfully weird chess.</code> |
| [13510](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:13510>) | <code>ShopScreen/Shop Design/Shop Controls/Collection Kind Filter/Label</code> | <code>m_text</code> | <code>ALL OWNED</code> |
| [13866](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:13866>) | <code>ShopScreen/Shop Design/Shop Controls/Refresh Shop/Label</code> | <code>m_text</code> | <code>REFRESH</code> |
| [14426](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:14426>) | <code>ShopScreen/Shop Design/Purchase Confirmation/Confirm Purchase Paper/Cancel Purchase/Label</code> | <code>m_text</code> | <code>CANCEL</code> |
| [15326](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:15326>) | <code>ShopScreen/Shop Design/Shop Controls/Shop Eyebrow</code> | <code>m_text</code> | <code>CHESS BUT WEIRD  /  MAKE IT YOURS</code> |
| [15810](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:15810>) | <code>ShopScreen/Shop Design/Shop Controls/Shop Tab TopUp/Label</code> | <code>m_text</code> | <code>TOP UP</code> |
| [16200](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:16200>) | <code>ShopScreen/Shop Design/Shop Controls/Shop Title</code> | <code>m_text</code> | <code>The weird little shop</code> |
| [16337](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:16337>) | <code>ShopScreen/Shop Design/Shop Controls/Back From Shop/Label</code> | <code>m_text</code> | <code>&lt;  BACK</code> |
| [16611](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:16611>) | <code>ShopScreen/Shop Design/Shop Controls/Product Detail Paper/Equip Cosmetic/Label</code> | <code>m_text</code> | <code>EQUIP</code> |
| [18478](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopScreen.prefab:18478>) | <code>ShopScreen/Shop Design/Shop Controls/Featured Offer/Feature Title</code> | <code>m_text</code> | <code>Your next favourite look</code> |

#### Assets/Resources/Shop/ShopProductCard.prefab

Có 4 trường TMP/legacy text, trong đó 0 có giá trị ban đầu. Trường rỗng được runtime bind từ controller, không phải không có chữ. Nội dung prefab có thể bị controller/catalog ghi đè khi Open/Render.

| Dòng | Đường GameObject trong prefab | Trường | Giá trị ban đầu |
| --- | --- | --- | --- |

### F.2. Tên và copy trong asset đang cấp dữ liệu cho game

| Asset / dòng | Trường | Giá trị |
| --- | --- | --- |
| [CosmeticCatalog.asset:19](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Data/CosmeticCatalog.asset:19>) | <code>displayName</code> | Tazji's Low Poly Chess Set |
| [CosmeticCatalog.asset:29](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Data/CosmeticCatalog.asset:29>) | <code>displayName</code> | Blue Crystal Chess Set |
| [CosmeticCatalog.asset:39](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Data/CosmeticCatalog.asset:39>) | <code>displayName</code> | Woodland Chess Set |
| [CosmeticCatalog.asset:49](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Data/CosmeticCatalog.asset:49>) | <code>displayName</code> | Kingdom Rush — Denas — Royal Guard |
| [CosmeticCatalog.asset:59](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Data/CosmeticCatalog.asset:59>) | <code>displayName</code> | Kingdom Rush — Vez'Nan — Dark Dominion |
| [CosmeticCatalog.asset:70](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Data/CosmeticCatalog.asset:70>) | <code>displayName</code> | Tazji's Low Poly Arena |
| [CosmeticCatalog.asset:80](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Data/CosmeticCatalog.asset:80>) | <code>displayName</code> | Forest Garden Board |
| [CosmeticCatalog.asset:90](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Data/CosmeticCatalog.asset:90>) | <code>displayName</code> | Classic Marble Board |
| [CosmeticCatalog.asset:100](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Data/CosmeticCatalog.asset:100>) | <code>displayName</code> | Celestial Board |
| [CosmeticCatalog.asset:110](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Data/CosmeticCatalog.asset:110>) | <code>displayName</code> | Kingdom Rush — Denas Royal Citadel |
| [CosmeticCatalog.asset:120](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Data/CosmeticCatalog.asset:120>) | <code>displayName</code> | Kingdom Rush — Vez'Nan Ruin Throne |
| [CosmeticCatalog.asset:131](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Data/CosmeticCatalog.asset:131>) | <code>displayName</code> | Purple Galaxy |
| [CosmeticCatalog.asset:141](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Data/CosmeticCatalog.asset:141>) | <code>displayName</code> | Kingdom Rush — Linirea Royal Heartlands |
| [CosmeticCatalog.asset:151](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Game/Data/CosmeticCatalog.asset:151>) | <code>displayName</code> | Kingdom Rush — Vez'Nan Scorched Dominion |
| [ShopPresentation.asset:17](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopPresentation.asset:17>) | <code>title</code> | The weird little shop |
| [ShopPresentation.asset:18](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Shop/ShopPresentation.asset:18>) | <code>subtitle</code> | A new look. The same wonderfully weird chess. |

Nguồn ưu tiên hiển thị tùy màn: CosmeticCatalog dùng cho thumbnail/loadout/local fallback; Shop có thể ưu tiên name/description từ offer/API; Profile có fallback hard-code riêng. Không đổi `id`, defaultPieceSkinId/defaultBoardId, addressable GUID, unityAssetKey hay đường dẫn asset để dịch tên.
Chỉ chép các trường có binding hiển thị: displayName của CosmeticCatalog và title/subtitle của ShopPresentation. Các displayName trong PieceSkinData/BoardSkinData/EnvironmentData đã được kiểm tra nhưng không thấy UI hiện tại đọc trường đó; asset KingdomRush cũ không có entry tương ứng trong catalog cũng không được gộp vào bảng dịch. PieceSkinCatalog còn tên fallback “Tazji's Low Poly” trong UseCatalog.

### F.3. Credits được hiển thị trong menu

[Assets/Resources/Main_Menu/MainMenuCredits.txt](../../Assets/Resources/Main_Menu/MainMenuCredits.txt) được MainMenuPresentation load trong overlay Credits. Các tên Tazji/Tofu/Font Diner là credit/tên riêng cần giữ thống nhất. Nội dung hiện tại:

```text
Chess But Weird
A little strategy. A lot of questionable decisions.

Chess sets & arenas: Tazji
Kingdom Rush maps: Tofu
Schoolbell typeface: Font Diner

Thanks for playing. Be weird. Be kind.
```

### F.4. News có thể xuất hiện trong màn News

Nguồn [Assets/Resources/Community/news.en-US.json](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Community/news.en-US.json>) có 8 article; **7 article** thỏa bộ lọc công khai theo ngày kiểm kê. Các article còn lại chỉ ghi nhận là không thuộc danh sách đang phát hành; không chép nội dung bị chặn vào danh sách dịch. Source URL/commit/editorial status là metadata, không phải copy UI.

| Article ID / dòng | Category | Title | Summary | Body |
| --- | --- | --- | --- | --- |
| [NEWS_CHAOS_VFX_20261109](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Community/news.en-US.json:6>) | Updates | Chaos gets a visual upgrade! | Distinct effects, clearer status icons and formation previews across all 35 Chaos buffs. | Chaos buffs now have distinct status symbols, movement trails, transformation cues and deployment effects. Crown halos, parachutes and new prop art make each resolved action easier to follow.<br><br>Track quest milestones, recruitment readiness and the Kingless countdown in the match HUD. Preview your formation before confirming it, with clear legal landing markers for supported tactical powers. Reduced Motion and effect preferences remain available.<br><br>These changes are integrated for Practice and Multiplayer Chaos. Online availability depends on the updated server and game build. |
| [NEWS_CHAOS35_20261108](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Community/news.en-US.json:52>) | Modes | Chaos Mode: all 35 buffs are now in the online codebase! | A full set of unusual powers has been connected to the online Chaos system. | All 35 Chaos buffs have been synchronized between the online system and the local practice rules in source. Tiered draft cards help distinguish the powers that can change the course of a match.<br><br>This milestone adds the Chaos v3 protocol and headless checks for the buff flow. It is a verified development update: availability still depends on the deployed backend and the player build. Headless checks do not replace an actual two-player online playtest. |
| [NEWS_SHOP_V2_20261104](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Community/news.en-US.json:75>) | Updates | Shop V2 - More ways to use Gold and Diamonds! | The cosmetic shop is connected to your account wallet. | Shop V2 connects cosmetic offers to the Gold and Diamonds in your account wallet. Purchases, ownership checks and equipment use server data so your items can move into your collection consistently.<br><br>This source update focuses on the shop experience and wallet consistency. It does not announce real-money top-ups, discounts or limited-time offers. The available catalog depends on backend configuration and the game build you are running. |
| [NEWS_CUSTOM_LOADOUTS_20261103](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Community/news.en-US.json:98>) | Cosmetics | Custom chess pieces - A lineup that feels like yours | Choose your pieces and preview their models before a match. | The loadout flow adds a clearer way to preview chess models before entering a match. You can see how a set will look and prepare choices that fit the items in your collection.<br><br>This source milestone expands cosmetic presentation and match preparation. Equipment still follows actual ownership and catalog rules. Reading this article does not unlock an item or change the rules of chess. |
| [NEWS_RANKED_20261019](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Community/news.en-US.json:121>) | Modes | Ranked play and matchmaking expand | Find an opponent with a clearer matchmaking flow. | Ranked play and matchmaking have been expanded in source, with a clearer waiting flow while an online match is being prepared. The goal is to make the next step easier to follow while finding an opponent.<br><br>This update covers game modes and matchmaking infrastructure. It does not announce a ranked season, ranked rewards or a tournament schedule. Actual online availability depends on the deployed server. |
| [NEWS_PLAY_FLOW_20261016](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Community/news.en-US.json:144>) | Updates | A refreshed Play, Loadout and Match Dialog flow | A smoother route into your next match with fewer extra steps. | The Play, Loadout and match dialog flow has been adjusted so match preparation follows a clearer sequence. Choices before a match are organized to reduce unnecessary steps and make the next action easier to find.<br><br>This is a small refinement after the first milestone in the curated news history. Its interface changes are verified against the linked commit. No event or additional reward system is announced here. |
| [NEWS_SETTINGS_20261007](<D:/Game_Project/Chesss-but-Weird/Assets/Resources/Community/news.en-US.json:167>) | Updates | Settings grows to six tabs with better accessibility | Organized settings and a live preview make adjustments easier. | Settings is organized into six tabs to make options easier to find. A live preview helps you see the effect of changes while adjusting the game, alongside improvements to accessibility.<br><br>Explore the tabs and compare your adjustments in the preview before returning to your next match. This development update brings a more organized place to tune the experience to your preferences. |

### F.5. Chữ trong ảnh đang được code dùng

Đã mở và kiểm tra trực quan năm ảnh dưới đây. Chữ nằm sẵn trong pixel, không đi qua lookup string hay font runtime; cần biến thể ảnh hoặc dựng lại nhãn khi đổi ngôn ngữ. Tên tệp không được tự dịch.

| Ảnh | Đường sử dụng |
| --- | --- |
| [Assets/Resources/Main_Menu/MainMenuLogoOriginal.png](../../Assets/Resources/Main_Menu/MainMenuLogoOriginal.png) | Chữ “Chess” và “But Weird” trong logo, kèm hình quân cờ: MainMenuLogoOriginal.LoadSprite → hai menu đầu; tên thương hiệu cần giữ theo quyết định sản phẩm. |
| [Assets/_Project/Art/UI/Main_Menu/Back.png](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Art/UI/Main_Menu/Back.png>) | Chữ “BACK” trên nút màu xanh nhạt. AddBackButton dùng sprite trong menu con. |
| [Assets/_Project/Art/UI/Main_Menu/ChooseYour MultiplayerMode.png](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Art/UI/Main_Menu/ChooseYour MultiplayerMode.png>) | Chữ “Choose Your Multiplayer Mode”, một dòng viết tay; title bằng ảnh trong BuildMultiplayerModeScreen. |
| [Assets/_Project/Art/UI/Main_Menu/LANButton.png](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Art/UI/Main_Menu/LANButton.png>) | Chữ “LAN” dưới biểu tượng mạng máy tính; card LAN trong BuildMultiplayerModeScreen. |
| [Assets/_Project/Art/UI/Main_Menu/MultiplayerButton.png](<D:/Game_Project/Chesss-but-Weird/Assets/_Project/Art/UI/Main_Menu/MultiplayerButton.png>) | Hai dòng “Multiplayer” / “Online”, dưới biểu tượng địa cầu; card online trong BuildMultiplayerModeScreen. |

ChooseYourSide/WhiteSideButton/BlackSideButton/GameVersionFake được loader chuẩn bị, nhưng BuildSideScreen hiện tạo chữ bằng code và menu đầu có EARLY ACCESS bằng text. Không coi mỗi ảnh được load là một label đang hiện. Các ảnh nút độ khó bot cũ cũng không được gộp vào danh sách copy của roster 12 bot hiện tại.

<a id="g-diem-ban-giao-cho-buoc-dich-thuat"></a>

## G. Điểm bàn giao cho bước dịch thuật

Tài liệu đã phân rõ vị trí render, nguồn copy, điều kiện hiện, Unicode/font, các binding dữ liệu và các ID cần giữ nguyên. Chưa có bản dịch mới nào được áp dụng. Khi chốt danh sách ngôn ngữ, dùng các dòng nguồn trên để tạo key theo ngữ cảnh; không thay hàng loạt mọi string literal trong dự án.

Danh sách không đưa vào dịch: tài liệu trong Documentation, README/architecture/plans; nội dung Library/Packages bên thứ ba; Mirror/TMP examples; Editor menu/Inspector; test/fixture/debug gallery; cảnh art review/cinematic demo; log, shader/property name, JSON field, asset path, save key, command ID. Domain runtime chỉ được đối chiếu để giữ quy ước tọa độ/ký hiệu cờ, không nhập exception nội bộ của luật vào danh sách text sản phẩm.

Các bảng là ảnh chụp của working tree ngày kiểm kê: đổi dòng nguồn, catalog/server hoặc thêm UI mới cần cập nhật kiểm kê. Số binding không phải số khóa cuối cùng; cùng một câu có thể cần key khác ngữ cảnh, và một binding có thể sinh nhiều câu theo trạng thái.
