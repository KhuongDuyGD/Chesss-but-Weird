# Menu, mailbox, loadout and legacy quality fixes

This change spans the Unity client and `nguyxntai/ChessGame.Api` backend. Deploy the backend before distributing the updated client, and rebuild Addressables for the updated catalog and galaxy environment.

## Behavior

- Retire only the old `kingdom_rush` Linirea Valley environment from the selectable catalog, shop and content builds. Keep the Denas Royal Heartlands and Vez'Nan Scorched Dominion environments. Old saved Valley selections fall back to the galaxy. Original art and ownership audit records remain archived.
- News **READ ALL** marks every currently public article, across filters, in the existing account/device read scope. It does not mark future or unpublished articles.
- Mailbox **CLAIM ALL** calls `POST /api/mailbox/claim-all` with an empty JSON object. The server claims eligible, unexpired rewards across all pages in one MongoDB transaction, marks those letters read, updates the wallet and writes unique per-mail ledger entries. Concurrent single/bulk claims and retries cannot grant a gift twice. Expired and reward-free letters are left alone. Invalid rewards or wallet overflow abort the transaction. The UI refreshes the authoritative wallet/mailbox and confirms the letter count and reward totals; uncertain responses require recovery through Refresh.
- Add **QUIT** at the top right of main menus 1/2 and login/register. In the Editor it exits Play Mode; in a desktop player it quits the application. Move Logout slightly left to avoid overlap.
- The default `classic` background explicitly loads the existing VoidPix purple galaxy, including when the match scene begins with Unity's default skybox. Its thumbnail now uses the existing galaxy artwork.
- Online loadout hides the White/Black and background controls. Background equipment is neither submitted nor applied in multiplayer. Local choices remain available.
- Server coin tosses assign opposing colors and choose one of the two frozen board loadouts when the match is created. `sharedBoardAssetKey` is persisted and sent to both clients before content loading. Readiness and reconnects cannot reroll the board or replace an already loading loadout. Old snapshots deterministically use White's board. Full-set custom piece overrides selected online are mirrored to both colors.
- Audit only the original Low Poly, Crystal, Woodland, Forest Garden, Marble and Celestial collections. Their imported materials already support URP Lit shadows. Fix the shadowless scene Sun, apply per-camera AA/shadows after loading a legacy board, and add HDR/ACES with quality-controlled bloom. Existing faction board presentation remains separate. Respect shadow Off, Low effects and reduced flashing preferences.

## Verification

- Backend: 488/488 tests, including real isolated MongoDB replica-set integration tests. Bulk claims cover 65 reward letters, concurrent bulk/single calls, expired/foreign mail, replay, invalid rewards and overflow rollback.
- `Tools/MultiplayerVerification`: real local store/service transactions verify shared board selection, background exclusion, frozen loadouts through readiness/reconnect, rooms, queues, settlement and outbox delivery.
- `tools/verify-loading-compile.ps1`: runtime Editor/player and Editor tools compile against the installed Unity 6000.4.7f1 references.
- `AddressableCosmeticsSetup.BuildExistingContent`: local Windows Addressables build succeeds, without rerunning the art/model setup generators.
- `Tests/Chess.Multiplayer.Headless/MultiplayerAudit.cs`: production assemblies verify the shared board and deterministic legacy fallback alongside existing menu, SFX, check and queue regressions.
- `tools/verify-community.ps1`: 113 UI assertions pass with isolated transport/auth ports, including Read All, Claim All, confirmation, account switching and both main menu quit callbacks. Direct3D11 captures were inspected. The isolated Editor logged an unrelated Search database indexing exception on startup.
- `tools/verify-legacy-quality.ps1`: verifies the original scene Sun at High/Off, camera SMAA, all four original board shadow/effect settings, galaxy loading without a prefab, retirement boundaries, online control visibility and login/register Quit. Inspects 267 material assignments using URP Lit with receive-shadows enabled. Captures of the four original boards were inspected. The wrapper restores native quality and MCP settings after Unity's batch serialization.

Generated captures and logs live under `Logs/legacy-quality`, `Logs/community-qa` and `Logs/multiplayer-headless`; they are local verification artifacts. These are asset/UI/service fixtures, not a two-machine internet playtest or a production deployment.
