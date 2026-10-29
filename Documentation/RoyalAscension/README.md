# Royal Ascension — development demo

Offline committed synthetic receipts only. This is not authenticated backend integration or an approved production artwork registry. Ordinary game scenes, startup, gacha routing, wallet, inventory, pity and network services are unchanged. Production feature configuration defaults OFF.

## Reproduce

Use Unity 6000.4.7f1 with the committed package manifest/lockfile and Git LFS installed. Fetch LFS content before opening. PR A cosmetic reference repairs are a prerequisite.

Open `Assets/CinematicDemo/FinalSprint/Scenes/RoyalAscension_CommittedDemo.unity` explicitly and enter Play Mode, then click **Enter demo**. Do not add this scene to ordinary game build settings. Models/Timeline/URP/RenderTexture and artwork use the existing local catalog. The three missing artwork entries stay text-only with their original synthetic reward IDs.

For an isolated Windows Development Player, with no other Editor using that checkout:

```powershell
& '<Unity6000.4.7f1>/Editor/Unity.exe' -batchmode -quit -projectPath '<clean-checkout>' -executeMethod CBWRelease.RoyalDemoBuild.Build '-cbwEvidence=<external-output>/verify.json' '-cbwPlayer=<external-output>/Player/RoyalDemo.exe' -logFile '<external-output>/build.log'
```

The builder creates fresh local Addressables content inside the checkout, uses only the demo scene, adds `ROYAL_ASCENSION_DEMO` to this Development build, temporarily disables Engine Diagnostics and verifies exact ProjectSettings restoration. Generated linker/content state remain ignored and must never become a production update baseline. Output must be fresh and outside the checkout. It does not change the ordinary scene list or persistent scripting defines. Never execute the historical Deadline builders; they are intentionally excluded.

Launch normally and click **Enter demo**, or pass `--royal-demo`. x1/x10, Replay, Skip, Reopen, accessibility controls and Close are local only. For the executed diagnostic suite, use fresh output/data paths:

```powershell
& '<external-output>/Player/RoyalDemo.exe' -force-d3d11 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 --royal-demo --demo-validate '--demo-art-spec=<clean-checkout>/Assets/CinematicDemo/FinalSprint/Fixtures/demo-only-art-spec.json' '--demo-output=<fresh-results-directory>' '--demo-data=<fresh-store-directory>' -logFile '<external-output>/player.log'
```

The suite writes actual captures, missing-art observations, resource ownership results and exits 0 only on success. Managed success followed by native crash is failure. Historical 58/58 evidence belongs to the frozen local FinalDemo; consult the external release handoff for fresh results from this source tree.

All newly shipped cinematic C# is compiled only in the Editor or when both `DEVELOPMENT_BUILD` and `ROYAL_ASCENSION_DEMO` are defined. A scene-processing guard rejects including the fixture scene in a non-development build. Human visual/audio approval, mobile hardware, IL2CPP and live authenticated gacha integration are outside this demo certification.

## Asset provenance

The standalone M35 `.blend` is preserved authoring source for the exported FBX. Geometry originated in the project's procedural M2 Blender blockout, was refined in M3 and choreographed in M3.5; no external mesh, Geometry Nodes or per-vertex animation transfer is required. Export uses metres, forward -Z/up Y, baked transforms at 30 fps, frames 1–211, step 1, simplify 0, no NLA/all-actions/leaf bones. Unity owns the URP shaders, materials, effects and Timeline setup. Preserve importer GUIDs.

Ten WAV cues are byte-preserved original mathematical synthesis, matching retained M3 cue files. `generate_audio_archival.py` is the original synthesis source retained for provenance; its old output layout is archival and it refuses overwriting files. It is not run on import/build. The authoring evidence remains local under the original `CinematicProof` directories. No historical logs, crash dumps, Player binaries, source backups or private fixtures are distributed.

Demo artwork references existing Tazji assets already committed to the project. This PR adds no Tazji binaries or third-party plugins/fonts. Their pre-existing redistribution rights are not newly certified by this demo. Existing packages remain unchanged and governed by their own licenses.

## Rollback and limitations

Revert this PR normally, after preserving any later work. Revert PR B before PR A if reverting both. Never reset/clean the original dirty workspace. The frozen accepted FinalDemo and 412-file manifest remain outside Git and are unchanged.

Known historical demo limitations: native immediate memory-leak warning, duplicate EventSystem warning and pale idle/results backdrop. Do not claim zero whole-process memory leaks from balanced owned handles. Human input/visual acceptance, audio mastering and production deployment are separate decisions. No production manifest, account identity or fallback activation is authorized by this source delivery.
