# Chess But Weird menu edge correction

Menu buttons, icons and solid UI primitives now calculate pixel coverage in their own shader. Their smoothing remains active when gameplay anti-aliasing is Off, FXAA, SMAA or MSAA. The camera, board, chess pieces, gameplay HUD texture imports and graphics-quality settings are unchanged by this correction.

## Files

- `Assets/Resources/UI/MenuEdgeAntialiasing.shader`: one lightweight shared coverage shader, stencil masks, RectMask2D softness, linear-space color handling and normal UI tint/fade support.
- `Assets/Scripts/Chess/UI/AntialiasedUIGraphic.cs`: material lifecycle and canvas channel configuration, including nested animated canvases.
- `Assets/Scripts/Chess/UI/UIEdgeMesh.cs`: coverage bands, joined strokes and end caps. The original handmade contours remain intact; small panel interiors are clamped against inverted geometry.
- `Assets/Scripts/Chess/UI/AntialiasedMenuImage.cs`: Image-compatible smoothing for menu rectangles, settings chevrons, sliders and other UI primitives. Bitmap sprites retain Unity's regular textured UI shader. Transparent hit areas keep their mesh and input depth.
- `Assets/Editor/MenuTextureImportSettings.cs`: a menu-only import policy. Mipmaps, Trilinear filtering, transparent RGB dilation, native non-power-of-two dimensions, clamp wrapping and high-quality GPU compression apply to the seven menu material folders and two menu resource folders. Existing size budgets remain in place.
- `Assets/Scripts/Chess/UI/MenuTextureSampling.cs`: matching sampling for Addressables/resources and alpha-weighted, color-space-aware mipmaps for runtime PNG fallbacks. The helper preserves alpha silhouettes, includes odd-sized edge rows/columns and releases CPU texture data after loading.
- `Assets/Editor/MenuAntialiasingAudit.cs`: isolated GPU verification.

The mesh correction covers rounded cards, Main Menu 1 ink accents, the eight mode/utility illustrations and shared sketchbook doodles. Image primitive constructors were upgraded in loading, authentication, notification, settings, inventory, profile, gacha/reveal, lobby, menu, pause and result views. The small repeating paper-grain texture also has mipmaps. All new filenames are descriptive.

## Preservation and performance

The edge correction initially updated 153 menu PNG importers and verified all 152 existing tracked GUIDs. The subsequent [asset cleanup](../MenuAssetCleanup/README.md) removed 33 unused textures and preserved all artwork still used by other screens. The original logo resource retains its established GUID and the recorded SHA-256 of the original drawing; the redundant material copy has been removed.

Geometry is cached and changes only when UI properties or dimensions change. There are no full-screen smoothing passes, particles, blur buffers or continuously regenerated textures. The eight mode illustrations use 6,603 vertices, down from 8,511 before the correction. High-quality compression and existing texture size limits are retained. Mip chains add texture storage, but avoid harsh downsampling and preserve stable minification. Target-device frame rate has not been benchmarked.

## Verification

- 30 GPU/sampling checks passed in Unity 6000.4.7f1 on D3D11: shader availability/compilation, nested canvas data, AA Off versus MSAA 4x, fractional scaling, standard UI color matching, CanvasGroup fading, button tinting, bitmap shader preservation, transparent input depth, stencil clipping, soft rectangle clipping, PNG mip-chain retention, NPOT boundaries, transparent RGB and linear-space ink averaging.
- AA Off versus MSAA 4x had a mean RGB byte difference of 0.033 across the fixture. The rotated single-color silhouette had 360 partial-coverage pixels with no multisampling, confined to its edges.
- Main Menu 1: 190 existing builder, interaction and layout checks passed.
- Mode selection: 467 existing builder, interaction and layout checks passed across seven resolutions, including hover/press states and controller navigation.
- Runtime editor/player sources and editor tools compiled against the installed Unity references.
- Every existing bitmap remained unchanged, every checked texture GUID was preserved, and the original logo's SHA-256 hashes match.

`MenuEdges-AAOff.png` and `MenuEdges-MSAA4x.png` show the actual shared primitives on the GPU. `MainMenu-Before.png` and `ModeSelection-Before.png` retain the previous renders; current captures are in the neighboring menu-redesign folders. The isolated fixture does not exercise full game startup, live account/network flows or target-device FPS.

```powershell
./tools/verify-main-menu.ps1 -Menu Antialiasing
./tools/verify-main-menu.ps1 -Menu Main
./tools/verify-main-menu.ps1 -Menu Modes
./tools/verify-loading-compile.ps1 -UnityPath 'D:/Unity Editor/6000.4.7f1/Editor/Unity.exe'
```

The shader is included through Resources in player builds. Rebuild existing Addressables content when producing a release so its bundled menu textures receive the updated imports. The importer enforces the menu policy on subsequent artwork imports.

Unity's [ScreenSpaceOverlay documentation](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/rendermode/screenspaceoverlay) explains why overlay UI can render without a camera. This correction supplies coverage directly at the UI silhouettes instead of depending on camera anti-aliasing.
