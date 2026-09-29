"""Brings GameSim Season Complete Pack 7 into the Unity project, reproducibly, beside packs 1-6 and Kit 6.

    python ArtSource/ui-packs/tools/bb_ui_pack7.py --zip <gamesim_season_complete_pack_7.zip> [--install]

Without --install it only reads the pack, checks it against its own manifest, measures every 9-slice
and prints a summary. With --install it:
  * extracts the 66 sprites to Assets/Gamesim/Resources/Packs/Pack7_SeasonComplete/<Category>/ - under
    Resources, like packs 1-4 and Kit 6, because the season report and the weekly recap are built in
    code and load their art by path (UiTheme.Pack, PackArt);
  * copies the reference material (preview sheet, README, manifest) to ArtSource/ui-packs/Pack7_SeasonComplete;
  * rewrites Assets/Gamesim/Editor/UiPackCatalogue.cs: everything it already holds, plus the pack's.

Kinds. A file named *_9slice.png is a UiSliced sprite whose border is measured from its own pixels, as
packs 1-5's are (bb_ui_packs.measure): the pack's manifest names its files but gives no borders.
Everything else - badges, stat icons, timeline nodes, the crown and trophy, the chart strips - is a
UiSprite drawn whole. What each file is FOR in this project is Assets/Plans/ASSET-PACKS.md (Pack 7).

Every meta GUID is derived from the path (bb_ui_packs.guid_for), so a re-run never mints a new
identity for the same file, and existing files and metas are never overwritten. The metas it writes
are the two-line kind; import once in the editor (or a batch run) and commit the expanded metas.
"""
import argparse, io, json, os, re, sys, zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bb_ui_packs as packs  # noqa: E402

PACK = 'GameSim_SeasonComplete_Pack7'
FOLDER = 'Pack7_SeasonComplete'
UI_ROOT = packs.UI_ROOT + '/' + FOLDER
SOURCE_ROOT = packs.SOURCE_ROOT + '/' + FOLDER
ENTRY = re.compile(r'new Entry\("([^"]+)", Kind\.(\w+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+)\),')


def existing_entries(repo):
    """The catalogue's current entries, less any pack-7 entries, in bb_ui_packs' dictionary form."""
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
        e = {'member': info.filename, 'path': UI_ROOT + '/' + category + '/' + name,
             'kind': 'UiSliced' if name.endswith('_9slice.png') else 'UiSprite',
             'width': img.size[0], 'height': img.size[1], 'border': (0, 0, 0, 0), 'inset': (0, 0, 0, 0)}
        if e['kind'] == 'UiSliced':
            border, span, inset = packs.measure(img)
            if span[0] < 3 or span[1] < 3:
                raise SystemExit('No stretchable centre in ' + info.filename + ' ' + str(span))
            e['border'], e['inset'] = border, inset
        entries.append(e)
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
    for e in entries:
        if e['kind'] == 'UiSliced':
            print('  %-60s %4dx%-4d border %s inset %s' % (e['path'][len(UI_ROOT) + 1:], e['width'], e['height'], e['border'], e['inset']))
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
