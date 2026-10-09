# Gacha skin assets

Unity uses the existing `items.unityAssetKey` values supplied from MongoDB. Item IDs, ownership, rarity and equip validation remain server-controlled.

| MongoDB item ID | Code | Unity catalog key | Local asset folder |
| --- | --- | --- | --- |
| `6aacc056e50e17c25a69015c` | `CHESS_WOODLAND` | `ChessSets/Woodland` | `Assets/Skins/Tazji'sWoodlandChessSet` |
| `6aacc062e50e17c25a69015e` | `BOARD_CLASSIC_MARBLE` | `Boards/ClassicMarble` | `Assets/Skins/Tazji'sMarbleBoard` |
| `6aacc06be50e17c25a690160` | `CHESS_CRYSTAL_BLUE` | `ChessSets/CrystalBlue` | `Assets/Skins/Tazji'sCrystalChessSet` |
| `6aacc072e50e17c25a690162` | `BOARD_FOREST_GARDEN` | `Boards/ForestGarden` | `Assets/Skins/Tazji'sForestGardenBoard` |

Crystal uses Glacier models for White and Amethyst models for Black. Woodland uses Oak models for White and Nightgrove models for Black. These sets retain their original materials rather than applying the uniform Low Poly team tint. Low Poly keeps its existing models and team colors.

`PieceSkinData` keeps its existing six model fields and adds optional Black-side overrides. Missing overrides retain the original shared-model behavior. Each side has its own loaded model set, even when both sides select the same skin ID.

Four data assets under `Assets/Game/Data/PieceSkins` and `Assets/Game/Data/Boards` register the models and previews in local Addressables groups. `CosmeticCatalog.asset` contains the exact backend keys. Inventory uses the loaded catalog to recognize supported board and piece skins; online loadout snapshots already use those same catalog keys.

Both gacha boards and the existing Low Poly default board place the six prisoner pads on each player's front/back side. Setup detects the baked orientation of the `Reserve_Inlays` mesh, or `Arena_Floor_Inlays` in the original arena. Forest Garden and Low Poly currently receive a -90-degree Unity model rotation; Marble already has front/back pads in its FBX and receives no additional rotation. `BoardSkinData.prisonLayoutYaw` describes the baked pad orientation separately, so prisoner positions follow both the model's baked layout and its actual instance transform. Updating an FBX and rerunning setup avoids applying a second rotation to an already rotated asset.

White's captures use the near rail and Black's captures use the far rail. The six positions retain the existing Pawn, Knight, Bishop, Rook, Queen, King order. The logical 8x8 grid stays unchanged. Camera framing includes the actual twelve prisoner positions for either board orientation.

## Updating assets

Keep the FBX files and their existing `.meta` GUIDs when replacing model content. Run **Chess > Setup Gacha Skin Assets** to refresh the four registrations, **Chess > Setup Tazji Low Poly Default Cosmetics** to refresh the original set and board, or **Chess > Build Addressable Cosmetics** to refresh and build all cosmetic content. A Player build needs updated Addressables content.

## Verification

`GachaSkinAssetsAudit.Run` builds Addressables and checks catalog loading, all 24 piece models with separate team variants, all three boards, four sprite previews, and the Low Poly shared-model fallback. It also verifies that every prisoner position lands on the actual pad geometry under a translated, rotated and nonuniformly scaled board transform, and checks front/back assignment and camera coverage from both teams at landscape and portrait aspect ratios. Results are written under `Logs/gacha-skins`. This local verification does not contact backend APIs or modify account equipment.

`GachaSkinAssetsAudit.VerifyInventory` checks local visual selection and persistence using an isolated fixture account, verifies that changing one equipment slot preserves the other, retains the fallback for uninstalled art, and repeats setup to check for duplicate catalog entries. It restores the prior account context and fixture preferences afterward.

On 2026-10-09, Unity 6000.4.7f1 completed the Addressables build and all 118 content/layout/camera checks. The earlier 14 inventory checks also passed. The reports are `Logs/gacha-skins/checks.txt` and `Logs/gacha-skins/inventory-checks.txt`.

Authenticated manual checks:

1. Sign in with an account owning one of the four items, or obtain it through the existing gacha flow.
2. Open Inventory, equip the item, and confirm the server reports success without the local-art-unavailable message.
3. Start a local match and confirm the selected board or both team variants appear. Capture pieces and confirm prisoners sit on the front/back pads with their quantity badges. Repeat with the other items and Low Poly.
4. Open online loadout selection and check that these owned skins are available. Verify each player's piece set and the selected board with two clients.

Authenticated equip, gacha rewards, two-client behavior and visual camera coverage require those manual checks; a content build alone does not establish them.
