# Gacha skin assets

Unity uses the existing `items.unityAssetKey` values supplied from MongoDB. Item IDs, ownership, rarity and equip validation remain server-controlled.

| MongoDB item ID | Code | Unity catalog key | Local asset folder |
| --- | --- | --- | --- |
| `6aacc056e50e17c25a69015c` | `CHESS_WOODLAND` | `ChessSets/Woodland` | `Assets/Skins/Tazji'sWoodlandChessSet` |
| `6aacc062e50e17c25a69015e` | `BOARD_CLASSIC_MARBLE` | `Boards/ClassicMarble` | `Assets/Skins/Tazji'sMarbleBoard` |
| `6aacc06be50e17c25a690160` | `CHESS_CRYSTAL_BLUE` | `ChessSets/CrystalBlue` | `Assets/Skins/Tazji'sCrystalChessSet` |
| `6aacc072e50e17c25a690162` | `BOARD_FOREST_GARDEN` | `Boards/ForestGarden` | `Assets/Skins/Tazji'sForestGardenBoard` |
| From server catalog | From server catalog | `Boards/Celestial` | `Assets/Skins/Tazji'sCelestialBoard` |

Crystal uses Glacier models for White and Amethyst models for Black. Woodland uses Oak models for White and Nightgrove models for Black. These sets retain their original materials rather than applying the uniform Low Poly team tint. Low Poly keeps its existing models and team colors.

`PieceSkinData` keeps its existing six model fields and adds optional Black-side overrides. Missing overrides retain the original shared-model behavior. Each side has its own loaded model set, even when both sides select the same skin ID.

Skin data under `Assets/Game/Data/PieceSkins` and `Assets/Game/Data/Boards` register models in local Addressables groups. `CosmeticCatalog.asset` contains the exact backend keys and small Sprite thumbnails, which live in the separate `CosmeticThumbnails` group so menus do not load model bundles. Inventory and loadout hide Board/Chess entries that have no local model registration or thumbnail. Halloween's thumbnails are imported, but its skins await backend keys before registration.

Bot, local 2 Players, ARAM practice, online/LAN rooms and matchmaking share `MatchLoadoutController`: two central Board/Chess thumbnail cards open a modal with Chess, Board and Move Effect tabs. The modal groups skins by the server's rarity, keeps unowned skins locked, and marks selected thumbnails with a gold border and an Equipped badge. Local games keep match selection separate from account equipment. Online confirms the selected chess and board item IDs through the existing equip API before entering rooms or matchmaking; a failed save blocks continuation. Move effects remain a local preference. Inventory remains browsing/filtering only.

Setup detects explicit `Prisoner_` rail geometry in the updated default, falling back to `Reserve_Inlays` or the original `Arena_Floor_Inlays` layout. Forest Garden receives a -90-degree Unity model rotation; the updated default, Marble and Celestial retain their native front/back orientation with no extra rotation. `BoardSkinData.prisonLayoutYaw` describes the baked pad orientation separately from the model transform.

Celestial uses the supplied `Boards/Celestial` key and its FBX's 12-second animation. Import is configured as Legacy with looped clips. `BoardSkinManager` enables and starts its Animation component when the model is instantiated.

Cosmetic height scales with the geometric mean of the logical tile width/depth. In `ChessClassic`, tiles are approximately 0.477 units: leaving Y at 1 while shrinking X/Z made Celestial about 2.1 times too tall. `CosmeticArtAudit.VerifyBoardProportions` uses both actual scene layouts and samples six points across the animation loop, checking mesh-transform proportions, all 64 logical tile centers and all 12 prisoner pads. It does not contact the backend or save scene changes.

White's captures use the near rail and Black's captures use the far rail. The six positions retain the existing Pawn, Knight, Bishop, Rook, Queen, King order. The logical 8x8 grid stays unchanged. Camera framing includes the actual twelve prisoner positions for either board orientation.

## Updating assets

Keep the FBX files and their existing `.meta` GUIDs when replacing model content. Run **Chess > Update Cosmetic Art and Animated Board** to refresh all mapped skins, previews and Celestial animation import settings. Build updated Addressables content before a Player build. `CosmeticArtAudit.SetupAndVerify` performs setup, local checks and an Addressables build without contacting backend APIs.

## Verification

`GachaSkinAssetsAudit.Run` builds Addressables and checks catalog loading, all 24 piece models with separate team variants, all three boards, four sprite previews, and the Low Poly shared-model fallback. It also verifies that every prisoner position lands on the actual pad geometry under a translated, rotated and nonuniformly scaled board transform, and checks front/back assignment and camera coverage from both teams at landscape and portrait aspect ratios. Results are written under `Logs/gacha-skins`. This local verification does not contact backend APIs or modify account equipment.

`GachaSkinAssetsAudit.VerifyInventory` checks that installed gacha keys can be selected from the match loadout and that inventory no longer exposes equipment actions. It repeats setup to check for duplicate catalog entries. The report is written to `Logs/gacha-skins/match-loadout-checks.txt`; this fixture makes no backend API calls.

On 2026-10-09, Unity 6000.4.7f1 completed the Addressables build and all 118 content/layout/camera checks. The earlier 14 inventory checks also passed. The reports are `Logs/gacha-skins/checks.txt` and `Logs/gacha-skins/inventory-checks.txt`.

Authenticated manual checks:

1. Sign in with an account owning one of the four items, or obtain it through the existing gacha flow.
2. Choose a Bot or local two-player match and verify the picker offers owned, locally installed skins for each player side, the board and the move effect.
3. Start the match and confirm the selected board and piece models appear. Capture pieces and confirm prisoners sit on the front/back pads with their quantity badges. Repeat with the other items and Low Poly.
4. Open online loadout selection and check that these owned skins remain available. Verify each player's piece set and the selected board with two clients.

Authenticated equip, gacha rewards, two-client behavior and visual camera coverage require those manual checks; a content build alone does not establish them.

## Cosmetic UI update, 2026-10-15

`CosmeticArtAudit.SetupAndVerify` writes `Logs/skin-update/checks.txt`. It verifies thumbnail/model registrations, separate thumbnail bundles, default/Celestial prisoner pad geometry, Celestial loop configuration and sampled decoration motion, modal input blocking, tabs, rarity groups, ownership locks, equipped badges and Inventory filtering. It then builds Addressables. Fixtures use an isolated account object and do not call server APIs. Live Game View appearance, authenticated equipment requests and real online matches still require manual validation.
