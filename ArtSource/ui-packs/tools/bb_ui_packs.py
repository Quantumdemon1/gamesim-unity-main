"""Brings the five GameSim UI/art asset packs into the Unity project, reproducibly.

    python ArtSource/ui-packs/tools/bb_ui_packs.py --downloads <folder holding the zips> [--install]

Without --install it only classifies and measures, printing a summary. With --install it:
  * extracts every PNG to its home in Assets (see HOME below), with a two-line .meta whose GUID is
    derived from the path, so re-running never mints a new identity for the same file;
  * copies the reference material (previews, READMEs, manifests, SVG sources) to ArtSource/ui-packs;
  * writes Assets/Gamesim/Editor/UiPackCatalogue.cs - every file, its import kind, its size and, for
    a 9-slice sprite, the border measured from its own pixels - which UiPackImporter and the tests read.

Existing files and metas are never overwritten: the Unity editor fills a .meta in on import, and a
second run must not throw that away.

Why the two homes. The UI here is built in code, so anything a panel or a HUD draws is loaded by path
and lives under Resources. The house is dressed by editor scripts that reference textures from the
saved scene, so the world textures of pack 5 live under Art, outside Resources - which ships every
file whether anything uses it or not.
"""
import argparse, io, json, os, uuid, zipfile

PACKS = [
    (1, 'gamesim_ui_asset_pack.zip', 'Pack1_Foundation'),
    (2, 'gamesim_ui_asset_pack_2.zip', 'Pack2_Gameplay'),
    (3, 'gamesim_ui_asset_pack_3.zip', 'Pack3_Systems'),
    (4, 'gamesim_ui_asset_pack_4.zip', 'Pack4_Presentation'),
    (5, 'gamesim_ui_asset_pack_5.zip', 'Pack5_HouseBroadcast'),
]
UI_ROOT = 'Assets/Gamesim/Resources/Packs'
WORLD_ROOT = 'Assets/Gamesim/Art/Packs'
SOURCE_ROOT = 'ArtSource/ui-packs'
CATALOGUE = 'Assets/Gamesim/Editor/UiPackCatalogue.cs'
GUIDE = 'GAMESIM_UI_PACKS_1-4_INVENTORY_AND_IMPLEMENTATION_GUIDE.md'

TOL = 2      # per-channel difference still counted as the same pixel
MARGIN = 2   # pixels kept in the fixed border beyond the measured curve, where the span allows
SOLID = 128  # alpha from which a pixel is part of the visible body rather than glow or padding


def guid_for(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'gamesim:' + path.replace('\\', '/')).hex


def kind_for(pack, category, name):
    if pack <= 4 or category == 'Broadcast':
        return 'UiSliced' if name.endswith('_9slice.png') else 'UiSprite'
    if category == 'NeonMasks':
        return 'NeonMask' if name.endswith('_mask.png') else 'WorldColour'
    if category == 'SurfaceDecals':
        return 'DetailMask'
    if category == 'VFXTextures':
        return 'Particle'
    return 'WorldColour'


def home(pack, folder, category, name):
    root = UI_ROOT if (pack <= 4 or category == 'Broadcast') else WORLD_ROOT
    return '/'.join([root, folder, category, name])


def uniform_span(lines, center):
    ref = lines[center]

    def same(line):
        return all(abs(a - b) <= TOL for a, b in zip(line, ref))
    lo = center
    while lo - 1 >= 0 and same(lines[lo - 1]):
        lo -= 1
    hi = center
    while hi + 1 < len(lines) and same(lines[hi + 1]):
        hi += 1
    return lo, hi


def measure(img):
    """The 9-slice border (left, bottom, right, top) and the solid body's inset, from the pixels."""
    rgba = img.convert('RGBA')
    w, h = rgba.size
    px = rgba.tobytes()
    rows = [px[y * w * 4:(y + 1) * w * 4] for y in range(h)]
    cols = [bytes(b for y in range(h) for b in px[(y * w + x) * 4:(y * w + x) * 4 + 4]) for x in range(w)]
    lo_x, hi_x = uniform_span(cols, w // 2)
    lo_y, hi_y = uniform_span(rows, h // 2)
    span_x, span_y = hi_x - lo_x + 1, hi_y - lo_y + 1
    grow_x = min(MARGIN, max(0, (span_x - 2) // 2))
    grow_y = min(MARGIN, max(0, (span_y - 2) // 2))
    left, right = lo_x + grow_x, (w - 1 - hi_x) + grow_x
    top, bottom = lo_y + grow_y, (h - 1 - hi_y) + grow_y
    body = rgba.getchannel('A').point(lambda a: 255 if a >= SOLID else 0).getbbox() or (0, 0, w, h)
    inset = (body[0], h - body[3], w - body[2], body[1])   # left, bottom, right, top
    return (left, bottom, right, top), (span_x, span_y), inset


def classify(downloads):
    from PIL import Image
    entries, sources = [], []
    for pack, zname, folder in PACKS:
        zf = zipfile.ZipFile(os.path.join(downloads, zname))
        for info in sorted(zf.infolist(), key=lambda i: i.filename):
            if info.is_dir():
                continue
            parts = info.filename.split('/')
            name = parts[-1]
            category = parts[1] if len(parts) >= 3 else ''
            if category == 'Preview' or not name.lower().endswith('.png'):
                sources.append((zname, info.filename, '/'.join([SOURCE_ROOT, folder] + parts[1:])))
                continue
            img = Image.open(io.BytesIO(zf.read(info)))
            kind = kind_for(pack, category, name)
            e = {'pack': pack, 'zip': zname, 'member': info.filename, 'folder': folder, 'category': category,
                 'file': name, 'width': img.size[0], 'height': img.size[1], 'kind': kind,
                 'path': home(pack, folder, category, name), 'border': (0, 0, 0, 0), 'inset': (0, 0, 0, 0)}
            if kind == 'UiSliced':
                border, span, inset = measure(img)
                if span[0] < 3 or span[1] < 3:
                    raise SystemExit('No stretchable centre in ' + info.filename + ' ' + str(span))
                e['border'], e['inset'] = border, inset
            entries.append(e)
    return entries, sources


def write_if_absent(path, data, mode='wb'):
    if os.path.exists(path):
        return False
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, mode) as f:
        f.write(data)
    return True


def folder_meta(repo, folder):
    """A folder meta for every folder from Assets/Gamesim down, created only where missing."""
    parts = folder.split('/')
    for i in range(3, len(parts) + 1):
        sub = '/'.join(parts[:i])
        meta = os.path.join(repo, sub + '.meta')
        write_if_absent(meta, ('fileFormatVersion: 2\nguid: ' + guid_for(sub) + '\nfolderAsset: yes\nDefaultImporter:\n'
                               '  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n').encode())


def catalogue_source(entries):
    lines = []
    for e in sorted(entries, key=lambda x: x['path']):
        b, s = e['border'], e['inset']
        lines.append('            new Entry("%s", Kind.%s, %d, %d, %d, %d, %d, %d, %d, %d, %d, %d),'
                     % (e['path'], e['kind'], e['width'], e['height'], b[0], b[1], b[2], b[3], s[0], s[1], s[2], s[3]))
    return '''using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Every file of the five imported UI/art asset packs, of UI Refinement Kit 6, of Room Finish Pack 6
    /// and of Season Complete Pack 7: where it lives, how it imports, and - for a 9-slice sprite - its
    /// border: measured from its own pixels for packs 1-5 and 7, the kit manifest's own for Kit 6.
    ///
    /// <para>GENERATED by <c>ArtSource/ui-packs/tools/bb_ui_packs.py</c> (packs 1-5),
    /// <c>bb_ui_kit6.py</c> (Kit 6), <c>bb_room_pack6.py</c> (Room Finish Pack 6) and
    /// <c>bb_ui_pack7.py</c> (Season Complete Pack 7); re-run them rather than editing this by hand.
    /// <see cref="UiPackImporter"/> applies it and <c>UiPackImportTests</c> holds the imported
    /// textures to it. What each file is FOR in this project is <c>Assets/Plans/ASSET-PACKS.md</c>
    /// (packs 1-5, Kit 6, Pack 7) and <c>Assets/Plans/ROOM-FINISH-PLAN.md</c> (Pack 6).</para>
    /// </summary>
    public static class UiPackCatalogue
    {
        /// <summary>How a file imports.</summary>
        public enum Kind
        {
            /// <summary>A UI sprite drawn stretched through its 9-slice border.</summary>
            UiSliced,
            /// <summary>A UI sprite drawn whole: icons, badges, rings, glows, full-screen cards.</summary>
            UiSprite,
            /// <summary>A colour texture for the house: wall graphics, signage, displays, competition kit.</summary>
            WorldColour,
            /// <summary>A colour texture that repeats across a surface: a wall treatment, a fabric, a backsplash.</summary>
            WorldTile,
            /// <summary>A white emission/alpha mask for a neon sign; data, not colour.</summary>
            NeonMask,
            /// <summary>A tiling grayscale detail mask for materials; data, not colour.</summary>
            DetailMask,
            /// <summary>A particle texture.</summary>
            Particle,
        }

        public readonly struct Entry
        {
            public readonly string Path;
            public readonly Kind Kind;
            public readonly int Width, Height;
            /// <summary>Unity's order: left, bottom, right, top. Zero for anything not 9-sliced.</summary>
            public readonly Vector4 Border;
            /// <summary>
            /// How far the solid body (alpha from 128) sits inside the image, left, bottom, right, top -
            /// the glow and padding a rect has to allow for so the visible edge lands where it should.
            /// </summary>
            public readonly Vector4 BodyInset;

            public Entry(string path, Kind kind, int width, int height, int left, int bottom, int right, int top,
                int insetLeft, int insetBottom, int insetRight, int insetTop)
            {
                Path = path; Kind = kind; Width = width; Height = height;
                Border = new Vector4(left, bottom, right, top);
                BodyInset = new Vector4(insetLeft, insetBottom, insetRight, insetTop);
            }
        }

        public static readonly Entry[] All =
        {
''' + '\n'.join(lines) + '''
        };

        private static Dictionary<string, Entry> byPath;

        public static bool TryGet(string path, out Entry entry)
        {
            if (byPath == null)
            {
                byPath = new Dictionary<string, Entry>(StringComparer.Ordinal);
                foreach (var item in All) byPath[item.Path] = item;
            }
            return byPath.TryGetValue(path ?? string.Empty, out entry);
        }
    }
}
'''


def other_entries(repo):
    """Catalogue entries written by another tool (bb_ui_kit6.py), kept when this one rewrites the file."""
    import re
    pattern = re.compile(r'new Entry\("([^"]+)", Kind\.(\w+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (\d+)\),')
    ours = tuple(root + '/' + folder + '/' for _, _, folder in PACKS for root in (UI_ROOT, WORLD_ROOT))
    path = os.path.join(repo, CATALOGUE)
    if not os.path.exists(path):
        return []
    out = []
    for m in pattern.finditer(open(path, encoding='utf-8').read()):
        if m.group(1).startswith(ours):
            continue
        n = [int(x) for x in m.groups()[2:]]
        out.append({'path': m.group(1), 'kind': m.group(2), 'width': n[0], 'height': n[1],
                    'border': tuple(n[2:6]), 'inset': tuple(n[6:10])})
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--downloads', required=True)
    ap.add_argument('--repo', default=os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..')))
    ap.add_argument('--install', action='store_true')
    ap.add_argument('--catalogue-only', action='store_true', help='write only the C# catalogue (and its meta)')
    ap.add_argument('--json', help='also write the classification as JSON here')
    a = ap.parse_args()
    entries, sources = classify(a.downloads)
    from collections import Counter
    print(len(entries), 'images,', len(sources), 'reference files,', dict(Counter(e['kind'] for e in entries)))
    if a.json:
        json.dump({'entries': entries, 'sources': sources}, open(a.json, 'w'), indent=1)
    if not (a.install or a.catalogue_only):
        return
    cat = os.path.join(a.repo, CATALOGUE)
    kept = other_entries(a.repo)
    with open(cat, 'w', encoding='utf-8', newline='\n') as f:
        f.write(catalogue_source(entries + kept))
    write_if_absent(cat + '.meta', ('fileFormatVersion: 2\nguid: ' + guid_for(CATALOGUE) + '\n').encode())
    if a.catalogue_only:
        print('catalogue written')
        return
    made = 0
    zips = {}
    for e in entries:
        zf = zips.setdefault(e['zip'], zipfile.ZipFile(os.path.join(a.downloads, e['zip'])))
        target = os.path.join(a.repo, e['path'])
        folder_meta(a.repo, os.path.dirname(e['path']).replace('\\', '/'))
        if write_if_absent(target, zf.read(e['member'])):
            made += 1
        write_if_absent(target + '.meta', ('fileFormatVersion: 2\nguid: ' + guid_for(e['path']) + '\n').encode())
    for zname, member, target in sources:
        zf = zips.setdefault(zname, zipfile.ZipFile(os.path.join(a.downloads, zname)))
        write_if_absent(os.path.join(a.repo, target), zf.read(member))
    guide = os.path.join(a.downloads, GUIDE)
    if os.path.exists(guide):
        write_if_absent(os.path.join(a.repo, SOURCE_ROOT, GUIDE), open(guide, 'rb').read())
    print('installed', made, 'new images')


if __name__ == '__main__':
    main()
