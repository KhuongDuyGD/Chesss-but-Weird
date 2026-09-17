# ARAM buff update audit

Source: `D:/Duy Documents/7th Semester/GDC301/Buff Cờ Vua ARAM.docx`, read on 2026-09-17. The document contains 26 buffs. This is a work-in-progress implementation checklist, not a completion report.

## Existing implementation

| Buff | Finding |
| --- | --- |
| Commandant Pawn | Player selects three Pawns; extra two-square move requires clear intermediate and destination squares. |
| Strong Fortress | Waives attack checks before/across castling, but currently still requires an unmoved King. Clarification requested. |
| Freestyle Leap | Currently adds diagonal 2x2 jumps. The new description only mentions L-shaped movement; clarification requested. |
| The Doppelganger | Initial target selection and swapped movement exist. The new five-turn keep/swap action is missing. |
| Suicide Bomber | Original Queen identity and once-only explosion exist. Kings and capturer are currently immune, unlike the potentially broader new description. Clarification requested. |
| Flying Thunder God | Original Queen, five uses, empty destination and five owner-turn cooldown exist. Ordinary legal Queen moves do not spend a teleport charge. |

## Not yet implemented

Silver: Noble Sacrifice, Absolute Sniper, Gambling Leads to Misery, Loot Box.

Gold: Peace T-shirt, Rise of Pawn, Mobile Fortress, Hiding King, Gacha Banner.

Diamond: Substitute Ninjutsu, The High-tech Era, The Queen's Betrayal, The Definition of ARAM, RNG Fiesta, I-Frame Roll.

Legendary: Ghost Army, Plague Town, Customize Army, Pawn's Revolution, One Man Army.

## Rule decisions requested

- Meaning of a turn: individual move, full round, or buff owner's turn; how extra turns affect timers.
- Draft: three offers and one selection versus three granted buffs; later draft rounds; rarity rules.
- Freestyle Leap's additional behavior beyond a normal Knight.
- Bloodthirsty Pawn's three forward destinations and backward capture behavior.
- Strong Fortress: moved King eligibility and destination safety.
- Whether mines/explosions kill Kings, whether the capturer is inside the Queen explosion, and simultaneous King deaths.
- Gacha ticket conversion, roll cost, deployment area and turn cost.
- Ghost Army: friendly blockers only, and how its check exception should work.
- One Man Army: actual first-person mouse-aimed shooting minigame.

Other mechanics needing explicit documented defaults when implementation resumes: sniper activation versus ordinary capture, spawn/deployment choices and no-empty-square cases, cannon timeout per Rook, shrinking-board timing, betrayal timing, substitute safety/no-destination handling, I-Frame destination safety, customization confirmation/timeout and refund selection.

## Work completed during this audit

- All six existing built-in descriptions are now English and describe the current implemented behavior. They still need revision when the new rules are confirmed.
- Draft cards have a larger readable description region and separate rarity/name rows.
- ARAM draft/HUD/setup prompts/toasts share a fitted safe-area layout.
- Old draft cards become inactive immediately when switching to the other player's options; hidden/completed drafts reject selection callbacks.
- Restored the missing standalone Domain test project and excluded it from Unity's generated-project ignore rule.

Validation: Editor-runtime, Player-runtime and editor-tools C# compilation passed (four existing obsolete API warnings in CosmeticsRuntimeVerification). Existing Domain/application suite: 83 checks passed. Existing JavaScript ARAM-V1 reference tests passed. These tests verify the old six-buff contract, not the 20 missing buffs or the proposed rules. No new gameplay or device rendering validation is claimed.

## Integration constraints

The deployed online authority is a Spring backend whose implementation is not in this workspace. `OnlineServer/aram-rules.js` is a V1 reference. Changes to enum ordering, seeded pools, buff state, special actions and effect resolution need a versioned contract and coordinated server implementation; silently adding new buffs to the V1 seed pool would desynchronize clients and server.

Practice currently rolls one shared rarity and offers up to three distinct buffs from that tier. Gold currently has two eligible buffs and Diamond one, so the existing code does not always offer three. Do not fill a pool with definitions whose gameplay is unimplemented.
