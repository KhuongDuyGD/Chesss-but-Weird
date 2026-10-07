# Inventory and Main Menu 2

Inventory now contains chess skins, board skins and move effects. The default view
shows owned cosmetics. Search, category selection, ownership, equipped status,
rarity and optional catalog tags can narrow the collection. Multiple selected tags
match any selected tag. Filters apply immediately; Reset returns to owned items.

Classic, Ink sparks and Confetti are built-in effects, saved per account on this
device. They use the piece movement presenter and respect reduced motion and
effect quality settings. Skin ownership and equipment continue to use the existing
inventory API. Effects are local because the current equipment API has no effect
slot. Music items do not appear in the cosmetic collection.

The rotating record in Main Menu 2 opens a small picker for the five existing music
packs. Selection uses GameMusicManager, persists the preference and refreshes the
current music context. Escape, the close button and clicking outside dismiss it.
The record stops rotating when music volume is zero or reduced motion is enabled.

Run `Chess > UI Audit > Capture Inventory and Music Redesign` for offline fixtures,
filter and equipment checks, music selection checks and screenshots at 1920x1080,
1280x720, 800x600, 2560x1080 and 1080x1920. The fixture restores account references
and saved preferences afterward. Skin names in these captures are fixture data.

All new UI artwork uses the existing pixel-coverage antialiasing shader and mesh
primitives. Screenshots are rendered with MSAA disabled. The separate
`Chess > UI Audit > Verify Menu Edge Antialiasing` audit verifies coverage, fades,
clipping and shader compilation.
