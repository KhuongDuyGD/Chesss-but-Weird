# Ranked lobby, profile and versus loading

## Client flow

Classic and Chaos now each expose Normal and Ranked in Play selection. Ranked
opens the shared loadout lobby with a current-rank card in the header. It displays
the server's tier, rating, placement count or progress toward the next tier.
The existing cosmetic picker, queue cancellation and acceptance popup are reused.
Going back from a match retains the selected Normal/Ranked destination.

Profile displays independent Classic and Chaos rank cards. Rank configuration
comes from `GET /api/ranks/config?mode=Classic|Chaos`; account rank comes from
`GET /api/users/me` (`ranks.classic` and `ranks.chaos`). Refresh updates these values.
Rank refresh completion is guarded by account identity, lifecycle and request
version. A match result invalidates earlier lobby requests and refreshes the
account rather than applying rating changes locally.

Rank crests use crisp vector shields, stars and the nine tier numbers. Config
colors/display names are used when available. Visual fallbacks contain only tier
metadata, never rating thresholds. Unknown account data is displayed as unavailable;
it is not inferred to mean Unranked. Placement and nullable next-tier fields follow
the BE contract. Existing `stats.elo` is labelled Classic Elo in the scorecard.

## Online pre-match presentation

The shared LoadingManager accepts an optional MatchLoadingContext. Online matches
show two portrait-style player banners: White and Black, display name/username,
local-player marker, pre-match rank and rating, placement progress, selected chess
set/arena thumbnails and server connection/ready status. The central percentage
reports the local content operation only; no opponent loading percentage is invented.
Default cosmetic doodles are shown until the existing catalog is available.

The presentation follows the existing parchment/sketchbook UI and fits a safe
1672 x 941 design frame. It reuses the established asset loading, material/texture
preparation and Retry/Back behavior. A fast initial load shows the player cards
for at least two seconds while the match is AwaitingReady. Rejoining an already
running match adds no reading delay. Input and Ready remain blocked until content
loading finishes. Cancellation during loading returns through content cleanup
to the lobby. Authentication changes also cancel the old online presentation.

All outgoing Chaos modes use `Chaos`. Online state, rules, HUD and ability
presenter read `chaos`, matching the new backend. Internal local-practice ARAM
types are retained to avoid changing gameplay or serialized Unity references.

## BE supplement approved by the user

The original rank handoff exposed account ranks but PlayerSnapshot contained only
rating, not public rank or placement information. With explicit approval, these
three files were updated in `D:/Stuurdy/Chess_But_Weird_BE/ChessGame.Api`:

- `Online/Models/MatchPlayer.cs`: nullable AvatarId and RankProfile Rank.
- `Online/DTOs/PlayerSnapshot.cs`: additive optional avatarId and rank fields.
- `Online/Services/OnlineService.cs`: freeze public rank and avatar when the match
  is created, and include them in snapshots/history/events through the shared mapper.

The rank snapshot is obtained from the BE RankService using that match's mode.
It is the pre-match rank for both participants and contains no email, wallet or
private account data. Gameplay, matchmaking, rating formulas and settlement are
unchanged. Existing saved matches without the new fields remain readable; their
opponent rank is displayed as unavailable, not reconstructed from current thresholds.
The client can fall back to its own account rank for the local player only.
The additive avatarId is ready for a future avatar-art catalog; the current loading
cards use rank crests and cosmetic art rather than assigning unrelated avatar art.

Deploy the updated BE to populate rank on newly created matches. Build alone does
not confirm MongoDB round-trip serialization of the new embedded RankProfile.

## Current Normal/Ranked contract limitation

BE still has one matchmaking queue per matching settings and marks its matches
rated. There is no queue-type request field. Normal and Ranked are separate client
entries/presentations, but both use the existing server matchmaking policy and may
change rating. The lobby explicitly says online matchmaking affects rating.
An unrated Normal queue requires a separate backend contract change.

## Validation and manual checks

Source C# compilation passed for runtime Editor (186 files), runtime Player (186)
and Editor tools (25). BE Release compilation passed with zero warnings/errors.
The existing PlayFlowAudit source expectations were updated for enabled Ranked
choices and canonical Chaos; the audit itself was not executed.

No game server, API calls, MongoDB operations, migration, automated fixture tests,
screenshots or two-account match were run in this work session.

Manual checks after deploying the BE supplement:

1. Select Classic Ranked and Chaos Ranked. Verify the corresponding rank, placement
   count, rating and progress; Normal should keep the ordinary loadout header.
2. Open/refresh Profile; compare both cards with `/users/me`. Check an unranked
   account, an established account and Celestial (no next tier).
3. Queue and cancel; accept/decline; return from a Ranked match and verify its lobby
   destination and retained loadout. Verify the rank refresh after settlement.
4. With two accounts, inspect both loading banners against the match snapshot,
   including different ranks, long names, chess/board skins and connection status.
5. Test cancellation/ready timeout during content loading, load failure Retry/Back,
   reconnect into a running match, and sign-out/session replacement during loading.
6. Verify actual Game View at 1920x1080, 1280x720, 800x600 and portrait/narrow layouts.
   Confirm readability, TMP Vietnamese glyphs, no overlaps and visible vector crests.
7. Play Chaos through draft/formation/abilities after the new `chaos` state mapping.
8. Confirm persisted MatchPlayer.Rank can be read after a BE restart and is emitted
   in REST state, SignalR replay and match history. Old matches may have rank=null.
