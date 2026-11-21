# Custom piece loadouts

The normal pre-match picker supports a full chess set or twelve independent cosmetic slots: King, Queen, Rook, Bishop, Knight and Pawn for White and Black. Every piece of the same kind and color shares its slot. Board selection remains independent. Owned full sets can supply their existing models to custom slots; this does not grant individual inventory items.

`CosmeticSelection` saves a deep copy of the selected local keys and server ItemIds in the existing account-scoped preferences. Switching to full-set mode retains the custom choices. Match loading resolves each custom slot through its `PieceSkinData`, including side-specific prefabs, tuning and team color. Missing match visuals retain the base-set visual; this never replaces a committed reward or grants a substitute item.

## Backend compatibility

Online custom mode requires `equipped.loadoutVersion == 1` from the authenticated backend and saves through `PUT /api/inventory/loadout`. Older backends retain the existing full-set `/equip` route; online custom selection is disabled. The server validates active items, ownership and exact side/kind for every slot. Online match snapshots include a `slot` field and freeze the actual selected ItemIds. Match color is still assigned by the server.

Supported raw types are exactly `CHESS_PIECE_SKIN`, `BOARD_SKIN`, and `CHESS_{WHITE|BLACK}_{KING|QUEEN|ROOK|BISHOP|KNIGHT|PAWN}_SKIN`. Type names are protocol tokens, not new catalog records. No substring/name/rarity inference is used.

## Committed gacha presentation

The existing reveal can show the board prefab or an individual piece model from a correlated V1 `presentationSnapshot`. It validates every slot's ItemId and ItemCode before using its raw type/key. Historical or malformed metadata receives no invented key. White/Black piece types select the corresponding prefab; a scoped camera and URP RenderTexture feed a RawImage. Unknown, absent or timed-out artwork uses neutral text and preserves the reward identity. Whole-set legacy rewards retain the existing presentation.

`CHESS_SHADOW_KNIGHT`, `BOARD_VOLCANO` and `CHESS_DRAGON_RED` are explicitly backend-only records with no supplied Unity artwork. They are excluded from selectable cosmetics and model preview; no placeholder production assets or catalog entries are added.

## Release boundaries

No item records, pool entries, probabilities, pity, currency, grants, immutable receipt schema or recovery algorithm are changed. Individual-piece gacha requires separately configured real ItemIds and a reviewed banner pool; none are invented or seeded by this change. Production presentation authority remains disabled. This does not activate the standalone Royal Ascension demo in the live gacha route.

Editor offline validation uses synthetic receipt/ownership data and the real local Addressables models. Backend tests cover ownership, exact slot matching, legacy BSON and V1 receipt compatibility. A Windows Player test and live authenticated/multiplayer deployment validation remain separate checks; Editor rendering alone does not certify them.
