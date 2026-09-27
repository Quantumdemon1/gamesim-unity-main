"""Brings GameSim UI Refinement Kit 6 into the Unity project, reproducibly, beside packs 1-5.

    python ArtSource/ui-packs/tools/bb_ui_kit6.py --zip <GameSim_UI_Refinement_Kit_6.zip> [--install]
        [--docs <folder holding GAMESIM_KIT6_UNITY_INTEGRATION.md / GAMESIM_REMAINING_MENUS_REVIEW.md>]

Without --install it only reads the kit, checks every sprite against the kit's manifest (size, hash,
border) and prints a summary. With --install it:
  * extracts the 71 runtime sprites to Assets/Gamesim/Resources/Packs/Kit6_Refinement/<Category>/,
    each with a two-line .meta whose GUID is derived from the path, as bb_ui_packs.py does - so the
    existing UiPackImporter imports them and UiTheme.Pack loads them by path like every other pack;
  * copies the reference material (layout previews, review boards, SVG sources, docs, the manifest
    and the kit's own Editor importer, which is NOT installed: UiPackImporter does its job without a
    dialog, including in batchmode) to ArtSource/ui-packs/Kit6_Refinement;
  * rewrites Assets/Gamesim/Editor/UiPackCatalogue.cs: the pack 1-5 entries it already holds, plus
    the kit's. A kit sprite's 9-slice border is the kit manifest's, not measured - the kit authored
    its borders exactly (left, bottom, right, top), and its fill/edge families share them.

The kit's folder is spelled GameSim; the project's is Gamesim. Nothing is written under the kit's
spelling: on Windows the two are one folder and in git they are two.

Existing files and metas are never overwritten, as in bb_ui_packs.py.
"""
import argparse, hashlib, io, json, os, re, shutil, sys, zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bb_ui_packs as packs  # noqa: E402

KIT = 'GameSim_UI_Refinement_Kit_6'
SPRITES = KIT + '/Assets/GameSim/UI/RefinementKit6/Sprites/'
MANIFEST = KIT + '/Assets/GameSim/UI/RefinementKit6/Settings/asset_manifest.json'
FOLDER = 'Kit6_Refinement'
UI_ROOT = packs.UI_ROOT + '/' + FOLDER
SOURCE_ROOT = packs.SOURCE_ROOT + '/' + FOLDER
DOCS = ['GAMESIM_KIT6_UNITY_INTEGRATION.md', 'GAMESIM_REMAINING_MENUS_REVIEW.md']
ENTRY = re.compile(r'new Entry\("([^"]+)", Kind\.(\w+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+)\),')


def existing_entries(repo):
    """The catalogue's current entries, less any kit entries, in bb_ui_packs' dictionary form."""
    text = open(os.path.join(repo, packs.CATALOGUE), encoding='utf-8').read()
    out = []
    for m in ENTRY.finditer(text):
        path = m.group(1)
        if path.startswith(UI_ROOT + '/'):
            continue
        n = [int(x) for x in m.groups()[2:]]
        out.append({'path': path, 'kind': m.group(2), 'width': n[0], 'height': n[1],
                    'border': tuple(n[2:6]), 'inset': tuple(n[6:10])})
    return out


def body_inset(img):
    rgba = img.convert('RGBA')
    w, h = rgba.size
    body = rgba.getchannel('A').point(lambda a: 255 if a >= packs.SOLID else 0).getbbox() or (0, 0, w, h)
    return (body[0], h - body[3], w - body[2], body[1])


def classify(zf):
    from PIL import Image
    manifest = json.loads(zf.read(MANIFEST))
    if manifest.get('borderOrder') != 'left,bottom,right,top':
        raise SystemExit('Unexpected border order: ' + str(manifest.get('borderOrder')))
    entries = []
    for a in manifest['assets']:
        member = SPRITES + a['path'][len('Sprites/'):]
        data = zf.read(member)
        if hashlib.sha256(data).hexdigest() != a['sha256']:
            raise SystemExit('Hash mismatch: ' + member)
        img = Image.open(io.BytesIO(data))
        if img.size != (a['width'], a['height']):
            raise SystemExit('Size mismatch: ' + member + ' ' + str(img.size))
        sliced = a['imageType'] == 'Sliced'
        border = tuple(a['border']) if sliced else (0, 0, 0, 0)
        if sliced and (min(border) <= 0 or border[0] + border[2] >= a['width'] or border[1] + border[3] >= a['height']):
            raise SystemExit('No stretchable centre in ' + member + ' ' + str(border))
        category, name = a['path'].split('/')[1], a['path'].split('/')[-1]
        entries.append({'member': member, 'path': '/'.join([UI_ROOT, category, name]),
                        'kind': 'UiSliced' if sliced else 'UiSprite', 'width': a['width'], 'height': a['height'],
                        'border': border, 'inset': body_inset(img), 'id': a['id']})
    return entries


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--zip', required=True)
    ap.add_argument('--docs', help='folder holding the two Kit 6 markdown documents')
    ap.add_argument('--repo', default=os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..')))
    ap.add_argument('--install', action='store_true')
    a = ap.parse_args()
    zf = zipfile.ZipFile(a.zip)
    kit = classify(zf)
    from collections import Counter
    print(len(kit), 'kit sprites,', dict(Counter(e['kind'] for e in kit)))
    if not a.install:
        return
    made = 0
    for e in kit:
        target = os.path.join(a.repo, e['path'])
        packs.folder_meta(a.repo, os.path.dirname(e['path']).replace('\\', '/'))
        if packs.write_if_absent(target, zf.read(e['member'])):
            made += 1
        packs.write_if_absent(target + '.meta', ('fileFormatVersion: 2\nguid: ' + packs.guid_for(e['path']) + '\n').encode())
    for info in zf.infolist():
        if info.is_dir() or info.filename.startswith(SPRITES) or not info.filename.startswith(KIT + '/'):
            continue
        rest = info.filename[len(KIT) + 1:]
        if rest.startswith('Assets/'):
            rest = 'Kit/' + rest[len('Assets/GameSim/UI/RefinementKit6/'):]
        packs.write_if_absent(os.path.join(a.repo, SOURCE_ROOT, rest), zf.read(info))
    if a.docs:
        for doc in DOCS:
            src = os.path.join(a.docs, doc)
            if os.path.exists(src):
                packs.write_if_absent(os.path.join(a.repo, SOURCE_ROOT, doc), open(src, 'rb').read())
    entries = existing_entries(a.repo) + kit
    with open(os.path.join(a.repo, packs.CATALOGUE), 'w', encoding='utf-8', newline='\n') as f:
        f.write(packs.catalogue_source(entries))
    print('installed', made, 'new kit sprites; catalogue now', len(entries), 'entries')


if __name__ == '__main__':
    main()
