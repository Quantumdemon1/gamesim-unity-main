"""Brings GameSim Room Finish Pack 6 into the Unity project, reproducibly, beside packs 1-5 and Kit 6.

    python ArtSource/ui-packs/tools/bb_room_pack6.py --zip <gamesim_room_finish_pack_6.zip> [--install]

Without --install it only reads the pack, checks it against its own manifest and prints a summary.
With --install it:
  * extracts the 162 world textures to Assets/Gamesim/Art/Packs/Pack6_RoomFinish/<Category>/ - outside
    Resources, like pack 5's world textures, because the house is dressed by editor scripts that put
    texture references into the saved scene, so a build ships only what a room actually wears;
  * extracts the 12 editor interaction markers to Assets/Gamesim/Editor/Gizmos/Pack6_RoomFinish/ - an
    Editor folder, so they can never enter a build (the pack's README: development reference only);
  * copies the reference material (preview sheet, README, manifest) to ArtSource/ui-packs/Pack6_RoomFinish;
  * rewrites Assets/Gamesim/Editor/UiPackCatalogue.cs: everything it already holds, plus the pack's.

Kinds. The pack is flat finish graphics, and three import rules cover it. A surface meant to repeat
across a wall, a duvet, a counter or a sofa (WallTreatments, Upholstery, the plain duvets, the
backsplashes, the diary's padded wall, slats, acoustic panel and curtain) is a WorldTile: colour,
mipmapped, and wrapped Repeat. A *_mask.png (the diary eye halo, the nomination gold linework) is a
NeonMask: data, linear, clamped. Everything else - rugs, signs, frames, labels, box art, displays,
markers - is placed once and is WorldColour: colour, mipmapped, clamped. What each file is FOR, room
by room, is Assets/Plans/ROOM-FINISH-PLAN.md.

Every meta GUID is derived from the path (bb_ui_packs.guid_for), so a re-run never mints a new
identity for the same file, and existing files and metas are never overwritten.
"""
import argparse, io, json, os, re, sys, zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bb_ui_packs as packs  # noqa: E402

PACK = 'GameSim_RoomFinish_Pack6'
FOLDER = 'Pack6_RoomFinish'
WORLD_ROOT = packs.WORLD_ROOT + '/' + FOLDER
GIZMO_ROOT = 'Assets/Gamesim/Editor/Gizmos/' + FOLDER
SOURCE_ROOT = packs.SOURCE_ROOT + '/' + FOLDER
MARKERS = 'EditorInteractionMarkers'
TILING = {
    'WallTreatments': None,   # every file
    'Upholstery': None,
    'Bedding': ('duvet_',),
    'Kitchen': ('backsplash_',),
    'DiaryRoom': ('diary_violet_padded_wall', 'diary_dark_wood_slats', 'acoustic_panel_texture', 'curtain_fabric'),
}
ENTRY = re.compile(r'new Entry\("([^"]+)", Kind\.(\w+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+)\),')


def kind_for(category, name):
    if name.endswith('_mask.png'):
        return 'NeonMask'
    if category in TILING:
        prefixes = TILING[category]
        if prefixes is None or any(name.startswith(p) for p in prefixes):
            return 'WorldTile'
    return 'WorldColour'


def home(category, name):
    return (GIZMO_ROOT if category == MARKERS else WORLD_ROOT + '/' + category) + '/' + name


def existing_entries(repo):
    """The catalogue's current entries, less any pack-6 entries, in bb_ui_packs' dictionary form."""
    text = open(os.path.join(repo, packs.CATALOGUE), encoding='utf-8').read()
    out = []
    for m in ENTRY.finditer(text):
        path = m.group(1)
        if path.startswith(WORLD_ROOT + '/') or path.startswith(GIZMO_ROOT + '/'):
            continue
        n = [int(x) for x in m.groups()[2:]]
        out.append({'path': path, 'kind': m.group(2), 'width': n[0], 'height': n[1],
                    'border': tuple(n[2:6]), 'inset': tuple(n[6:10])})
    return out


def classify(zf):
    from PIL import Image
    manifest = json.loads(zf.read(PACK + '/manifest.json'))
    listed = {(category, name) for category, names in manifest['folders'].items() for name in names}
    entries, sources = [], []
    seen = set()
    for info in sorted(zf.infolist(), key=lambda i: i.filename):
        if info.is_dir() or not info.filename.startswith(PACK + '/'):
            continue
        parts = info.filename.split('/')
        name = parts[-1]
        category = parts[1] if len(parts) >= 3 else ''
        if category in ('', 'Preview') or not name.lower().endswith('.png'):
            sources.append((info.filename, '/'.join([SOURCE_ROOT] + parts[1:])))
            continue
        if (category, name) not in listed:
            raise SystemExit('Not in the pack manifest: ' + info.filename)
        seen.add((category, name))
        img = Image.open(io.BytesIO(zf.read(info)))
        entries.append({'member': info.filename, 'path': home(category, name), 'kind': kind_for(category, name),
                        'width': img.size[0], 'height': img.size[1], 'border': (0, 0, 0, 0), 'inset': (0, 0, 0, 0)})
    missing = sorted(listed - seen - {('Preview', n) for n in manifest['folders'].get('Preview', [])})
    if missing:
        raise SystemExit('In the manifest and not in the zip: ' + str(missing))
    return entries, sources


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--zip', required=True)
    ap.add_argument('--repo', default=os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..')))
    ap.add_argument('--install', action='store_true')
    a = ap.parse_args()
    zf = zipfile.ZipFile(a.zip)
    entries, sources = classify(zf)
    from collections import Counter
    print(len(entries), 'images,', len(sources), 'reference files,', dict(Counter(e['kind'] for e in entries)))
    if not a.install:
        return
    made = 0
    for e in entries:
        target = os.path.join(a.repo, e['path'])
        packs.folder_meta(a.repo, os.path.dirname(e['path']).replace('\\', '/'))
        if packs.write_if_absent(target, zf.read(e['member'])):
            made += 1
        packs.write_if_absent(target + '.meta', ('fileFormatVersion: 2\nguid: ' + packs.guid_for(e['path']) + '\n').encode())
    for member, target in sources:
        packs.write_if_absent(os.path.join(a.repo, target), zf.read(member))
    catalogue = existing_entries(a.repo) + entries
    with open(os.path.join(a.repo, packs.CATALOGUE), 'w', encoding='utf-8', newline='\n') as f:
        f.write(packs.catalogue_source(catalogue))
    print('installed', made, 'new images; catalogue now', len(catalogue), 'entries')


if __name__ == '__main__':
    main()
