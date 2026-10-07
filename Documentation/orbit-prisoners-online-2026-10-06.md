# Orbit, online motion, prisoners and clocks

Piece hover names: moving the mouse over either team's live model shows its
team and kind (for example White Pawn or Black Knight) in a paper tooltip near
the pointer. Prisoner models also show their kind and a Prisoner suffix, using
renderer bounds without enabling their colliders. Hover is independent of whose
turn it is, never blocks clicks, and is hidden over interactive HUD, during
camera drag or local pause. Unity Editor/player source compilation passed after
this addition; in-game hover appearance remains for manual verification.

The scene camera is saved as Orthographic, and the board camera sets Orthographic
immediately on activation, even before the board has loaded. Wheel zoom changes
orthographic size. The gameplay camera starts locked at the previous 50-degree board view on the
assigned player's side (Black rotates 180 degrees). Press Y to unlock orbit,
then hold the right or middle mouse button and drag. Orbit sensitivity is .14
degrees per mouse pixel (previously .22). Press Y again to lock and restore the
original assigned-side orientation while keeping the current zoom. Every new
match starts locked. Scroll zoom remains available in either mode. Y is ignored
while typing in a text field, paused, or aiming with the ARAM rifle camera.
Vertical orbit is limited to 25-85 degrees. The pivot stays at the board and
turns never reset the orbit. HUD input is excluded; ARAM rifle aiming continues
to take exclusive control of its camera.

A noninteractive paper card at the lower left, above the existing match actions,
shows `Camera locked · Y to unlock` or `Camera unlocked · Y to lock` from the
actual camera state. The unlocked state has an accent border and adds right/middle
mouse drag guidance; both states show scroll zoom guidance. It remains visible in
Focus mode and hides during pause or the separate ARAM rifle view.

The board rig also writes an explicit Matrix4x4.Ortho projection before URP
renders its camera, disables physical lens projection, and uses the same matrix
for non-jittered projection. Framing is computed from the locked view, so orbit
does not automatically change zoom. At extreme angles or close zoom, use the
wheel to fit the desired board area. Disabling the rig releases its explicit
matrix; local and online rifle views reset both projection matrices when entering
their separate first-person mode.
HUD layout does not adjust the board camera's perspective FOV. These changes
enforce parallel projection in source; actual Game output has not been inspected
because the Unity bridge configuration could not connect to the current Editor.
After this camera update, source compilation passed for runtime Editor/player
branches (146 files each) and Editor tools (18 files), with no asset or render check.

Online snapshots commit logical positions immediately. Existing relocated
pieces animate with the shared move duration/arc after a consecutive server
update, including both castling pieces and promotion. Initial snapshots and
version gaps place pieces directly at their recovered positions. Duplicate
clock/presence snapshots do not cancel a motion already running.

Settings > Display > Shadows selects Off or On (default Off), saves to
PlayerPrefs, and updates existing arena, environment, live piece and prisoner
model renderers immediately. Newly instantiated/reskinned models inherit it.
URP camera shadow rendering and Unity shadow quality follow this setting too;
graphics preset changes keep the user's shadow choice. Auto-detect resets it
to Off. Lighting and material colors remain intact; ProjectSettings files were
not edited by this implementation.

Prisoner pads were measured from the current Tazji arena FBX: X = +/-4.93,
Z = 3.15, 1.89, .63, -.63, -1.89, -3.15; surface Y = .036, relative to the
cosmetic board center. The White capturer uses its left column; Black uses its
own left column after rotating the view 180 degrees. Slots run from top to
bottom in that player's starting view: Pawn, Knight, Bishop, Rook, Queen, King.
One victim-colored model is fitted inside each occupied pad, with an xN paper
badge next to it. The original 3D quantity labels were too small to read; badges
now render in a noninteractive overlay canvas with consistent readable size,
projected from each pad after camera orbit/zoom has updated. They cannot be
occluded by arena geometry and stay within the screen safe area. Prisoners have invalid board coordinates
and disabled colliders, and sit outside the live pieces root.

Online counts read the new authoritative `captures` array from the .NET match
snapshot: `{ pieceId, kind, team, capturedBy }`. The API persists the ledger
with the match, and replays/restores snapshots without duplicating counts.
Normal/en-passant captures, ARAM Sniper and successful Rifle hits add entries;
sacrifices, explosions, infections, mines, recruitment and collapse do not.
Old matches return null: Classic can use recovered move history; ARAM has no
reliable legacy capture history. Start a new match after deploying the API
update to validate the full prisoner feature. Local games use their existing
capture records.

BE clocks already exist (`clocks.whiteMilliseconds`, `blackMilliseconds`,
`runningColor`, `serverTime`). Each player's paper card displays MM:SS in larger
text, highlights the side to move, and stays visible in Focus mode. Display
countdowns interpolate between server snapshots, including during local pause;
the backend continues to own increments and timeout adjudication.

Validation: Unity C# Editor/player branches and Editor tools compiled; the .NET
API built without warnings or errors. No server/API/MongoDB calls, asset build,
rendering or two-client runtime tests were performed. Manual checks should
cover camera drag over the board/UI, opposite player starting views, movement,
castling, promotion, repeated captures, en passant, reconnect, ARAM removals
versus captures, Focus mode, local pause, and clock increments/timeouts.
