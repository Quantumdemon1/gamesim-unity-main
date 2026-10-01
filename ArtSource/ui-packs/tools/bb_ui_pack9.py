"""Brings the GameSim Ceremony + Finale Asset Pack (Pack 9) into the Unity project, reproducibly, beside packs 1-8 and Kit 6.

    python ArtSource/ui-packs/tools/bb_ui_pack9.py --zip <GameSim_Ceremony_Finale_Asset_Pack.zip> [--install]

Without --install it only reads the pack, checks it against the list below, measures every sliced
sprite and prints a summary. With --install it:
  * extracts the 62 sprites to Assets/Gamesim/Resources/Packs/Pack9_CeremonyFinale/<Category>/ -
    under Resources, like packs 1-4, Kit 6, Pack 7 and Pack 8, because the ceremony cards and the
    finale page are built in code and load their art by path (UiTheme.Pack, PackArt);
  * copies the reference material (README, preview sheet, the SVG twins of the icons) to
    ArtSource/ui-packs/Pack9_CeremonyFinale;
  * rewrites Assets/Gamesim/Editor/UiPackCatalogue.cs: everything it already holds, plus the pack's.

The list. The pack ships no manifest.json, so FILES below is its manifest: every image, by the zip
folder it comes from. An image in the zip the list lacks, or one the list names that the zip lacks,
stops the install, as Pack 7's and Pack 8's manifests did. Icons_PNG installs as Icons, the way
Pack 8's Icons/ twins came in; Icons_SVG is reference only and never goes under Assets
(UI-UX-PASS-PLAN decision 16).

Kinds. This pack names no *_9slice: a frame is an edge sprite over a fill sprite of the same size, so
a state changes a tint or a layer, never a rect (Kit 6's chrome works the same way). Every edge and
fill - the panels, cards, tiles, pills, tags and badges: card_edge_rest, panel_fill,
action_tile_focus_edge - is a UiSliced sprite whose border is measured from its own pixels
(bb_ui_packs.measure); the README's 24 / 18 / 30 px are nominal, and the measured values are what
UiPackImportTests asserts. Everything else - the icons, the rings, the halos, the progress dots, the
key's pedestal mask - is a UiSprite drawn whole. What each file is FOR in this project is
Assets/Plans/UI-UX-PASS-PLAN.md (P0, N0, N1, B0 and F0).

Every meta GUID is derived from the path (bb_ui_packs.guid_for), so a re-run never mints a new
identity for the same file, and existing files and metas are never overwritten. The metas it writes
are the two-line kind; import once in a batch run and commit the expanded metas.
"""
import argparse, io, os, re, sys, zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bb_ui_packs as packs  # noqa: E402

PACK = 'GameSim_Ceremony_Finale_Asset_Pack'
FOLDER = 'Pack9_CeremonyFinale'
UI_ROOT = packs.UI_ROOT + '/' + FOLDER
SOURCE_ROOT = packs.SOURCE_ROOT + '/' + FOLDER
ENTRY = re.compile(r'new Entry\("([^"]+)", Kind\.(\w+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+)\),')

# The pack's manifest: every image, by the zip folder it ships in. Icons_PNG installs as Icons.
FILES = {
    'Shared': [
        'card_edge_focus', 'card_edge_rest', 'card_fill', 'focus_halo', 'panel_edge_focus', 'panel_edge_rest',
        'panel_fill', 'pill_edge', 'pill_fill', 'portrait_ring', 'portrait_ring_glow',
    ],
    'LiveEviction': [
        'anonymous_badge_edge', 'anonymous_badge_fill', 'anonymous_icon', 'ballot_icon', 'lock_icon', 'nominee_ring',
        'progress_dot_active', 'progress_dot_inactive', 'tally_card_edge', 'tally_card_fill',
    ],
    'NominationCeremony': [
        'ceremony_panel_edge', 'ceremony_panel_fill', 'hoh_crown_icon', 'key_icon', 'key_pedestal_mask', 'nominee_ring',
        'safe_ring', 'status_tag_edge', 'status_tag_fill',
    ],
    'SeasonFinale': [
        'action_tile_edge', 'action_tile_fill', 'action_tile_focus_edge', 'crown_icon', 'finalist_card_edge',
        'finalist_card_fill', 'home_icon', 'jury_icon', 'jury_panel_edge', 'jury_panel_fill', 'people_icon',
        'replay_icon', 'report_icon', 'star_icon', 'stat_tile_edge', 'stat_tile_fill', 'trophy_icon',
        'winner_card_edge', 'winner_card_fill', 'winner_card_halo',
    ],
    'Icons_PNG': [
        'ic_anonymous', 'ic_ballot', 'ic_crown', 'ic_home', 'ic_jury', 'ic_key', 'ic_lock', 'ic_people', 'ic_replay',
        'ic_report', 'ic_star', 'ic_trophy',
    ],
}
CATEGORIES = {'Icons_PNG': 'Icons'}
COUNT = 62


def kind_for(name):
    """Every edge and fill is sliced - card_edge_rest and panel_edge_focus carry their state after the
    word, action_tile_focus_edge before it, so the test is a segment of the name, not its suffix - and
    the icons, rings, halos, dots and the pedestal mask are drawn whole."""
    segments = name[:-len('.png')].split('_')
    return 'UiSliced' if 'edge' in segments or 'fill' in segments else 'UiSprite'


def source_for(parts):
    """Where a reference file goes under ArtSource: the zip's own layout, except that the README sits
    at the pack's root as Pack 7's and Pack 8's do rather than under Docs/."""
    if parts == ['Docs', 'README.md']:
        return SOURCE_ROOT + '/README.md'
    return '/'.join([SOURCE_ROOT] + parts)


def existing_entries(repo):
    """The catalogue's current entries, less any pack-9 entries, in bb_ui_packs' dictionary form."""
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
    listed = {(folder, name + '.png') for folder, names in FILES.items() for name in names}
    assert len(listed) == COUNT, len(listed)
    entries, sources = [], []
    seen = set()
    for info in sorted(zf.infolist(), key=lambda i: i.filename):
        if info.is_dir() or not info.filename.startswith(PACK + '/'):
            continue
        parts = info.filename.split('/')[1:]
        name = parts[-1]
        folder = '/'.join(parts[:-1])
        if folder in ('', 'Preview') or not name.lower().endswith('.png'):
            sources.append((info.filename, source_for(parts)))
            continue
        if (folder, name) not in listed:
            raise SystemExit('Not in the pack list: ' + info.filename)
        seen.add((folder, name))
        img = Image.open(io.BytesIO(zf.read(info)))
        e = {'member': info.filename, 'path': UI_ROOT + '/' + CATEGORIES.get(folder, folder) + '/' + name,
             'kind': kind_for(name), 'width': img.size[0], 'height': img.size[1],
             'border': (0, 0, 0, 0), 'inset': (0, 0, 0, 0)}
        if e['kind'] == 'UiSliced':
            border, span, inset = packs.measure(img)
            if span[0] < 3 or span[1] < 3:
                raise SystemExit('No stretchable centre in ' + info.filename + ' ' + str(span))
            e['border'], e['inset'] = border, inset
        entries.append(e)
    missing = sorted(listed - seen)
    if missing:
        raise SystemExit('In the list and not in the zip: ' + str(missing))
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
            print('  %-50s %4dx%-4d border %s inset %s' % (e['path'][len(UI_ROOT) + 1:], e['width'], e['height'], e['border'], e['inset']))
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
