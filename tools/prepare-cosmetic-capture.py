"""Copy only render dependencies into a disposable Unity photography project."""
import json, re, shutil, sys
from pathlib import Path

source = Path(__file__).resolve().parents[1]
destination = Path(sys.argv[1]).resolve()
if not destination.is_relative_to((source / 'Logs').resolve()):
    raise SystemExit('Capture project must stay inside Logs.')
destination.mkdir(parents=True, exist_ok=True)
guid_pattern = re.compile(r'(?<![0-9a-f])[0-9a-f]{32}(?![0-9a-f])')
guid_paths = {}
for meta in (source / 'Assets').rglob('*.meta'):
    match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(errors='ignore'), re.M)
    if match:
        guid_paths[match[1]] = Path(str(meta)[:-5])
roots = [source / 'Assets/Game/Data/CosmeticCatalog.asset']
roots.extend((source / 'Assets/Settings').glob('*.asset'))
# Addressable references are GUID strings, so traverse them as well as YAML references.
pending = list(roots)
settings = ['ProjectVersion.txt', 'ProjectSettings.asset', 'QualitySettings.asset', 'GraphicsSettings.asset', 'TagManager.asset']
for name in settings:
    path = source / 'ProjectSettings' / name
    if path.exists():
        pending.extend(guid_paths[g] for g in guid_pattern.findall(path.read_text(errors='ignore')) if g in guid_paths)
seen = set()
text_extensions = {'.asset', '.prefab', '.mat', '.controller', '.anim', '.shadergraph', '.shadersubgraph', '.playable'}
while pending:
    path = pending.pop()
    if path in seen or not path.is_file() or path.suffix in {'.cs', '.asmdef', '.asmref'}:
        continue
    seen.add(path)
    text = ''
    if path.suffix in text_extensions:
        text += path.read_text(errors='ignore')
    meta = Path(str(path) + '.meta')
    if meta.exists():
        text += meta.read_text(errors='ignore')
    pending.extend(guid_paths[g] for g in guid_pattern.findall(text) if g in guid_paths)
# Include custom shader include siblings; HLSL includes aren't serialized GUID dependencies.
for folder in ['Assets/Shaders', 'Assets/Skins/KingdomRushCollab/Shaders']:
    if (source / folder).exists():
        seen.update(p for p in (source / folder).rglob('*') if p.is_file() and p.suffix != '.meta')
for name in ['CosmeticCatalog', 'PieceSkinData', 'BoardSkinData', 'EnvironmentData']:
    seen.add(source / 'Assets/Scripts/Chess/Cosmetics' / (name + '.cs'))
seen.add(source / 'Assets/Editor/CosmeticThumbnailCapture.cs')
size = sum(p.stat().st_size for p in seen)
for path in seen:
    relative = path.relative_to(source)
    target = destination / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(path, target)
    meta = Path(str(path) + '.meta')
    if meta.exists():
        shutil.copy2(meta, Path(str(target) + '.meta'))
    for parent in path.parents:
        if parent == source:
            break
        folder_meta = Path(str(parent) + '.meta')
        if folder_meta.is_file():
            shutil.copy2(folder_meta, destination / folder_meta.relative_to(source))
(destination / 'ProjectSettings').mkdir(exist_ok=True)
for name in settings:
    path = source / 'ProjectSettings' / name
    if path.exists():
        shutil.copy2(path, destination / 'ProjectSettings' / name)
manifest = json.loads((source / 'Packages/manifest.json').read_text())
manifest['dependencies'] = {k:v for k,v in manifest['dependencies'].items() if k.startswith('com.unity.modules.') or k in ['com.unity.render-pipelines.universal', 'com.unity.addressables', 'com.unity.ugui']}
(destination / 'Packages').mkdir(exist_ok=True)
(destination / 'Packages/manifest.json').write_text(json.dumps(manifest, indent=2))
(destination / 'Assets/CapturePorts.cs').write_text('''using UnityEngine;
public enum PieceTeam { White, Black }
public enum PieceType { King, Queen, Rook, Bishop, Knight, Pawn }
public static class CosmeticSelection { public const string LowPolyId="tazji_lowpoly", KingdomRushId="kingdom_rush"; }
public readonly struct PieceSkinPieceTuning {
 public static readonly PieceSkinPieceTuning Default=new PieceSkinPieceTuning(Vector3.zero,1,1);
 public PieceSkinPieceTuning(Vector3 rotationEuler,float heightMultiplier,float maxFootprintMultiplier){RotationEuler=rotationEuler;HeightMultiplier=heightMultiplier;MaxFootprintMultiplier=maxFootprintMultiplier;}
 public Vector3 RotationEuler{get;} public float HeightMultiplier{get;} public float MaxFootprintMultiplier{get;}
}
''')
(destination / 'capture-project.txt').write_text(str(source))
print(f'Isolated capture project: {destination}\n{len(seen)} render assets, {size / (1024*1024):.1f} MiB. No gameplay or account code.')
