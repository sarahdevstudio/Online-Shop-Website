#!/usr/bin/env python3
"""
Subsets the bundled Vazirmatn (variable, weight axis narrowed to 400-700) to the characters the shop actually writes, from the
@fontsource-variable/vazirmatn files in node_modules:

  pip install fonttools brotli
  python3 scripts/perf/subset-vazirmatn.py

The unicode-ranges in src/lib/theme-css.ts (VAZIRMATN_*_RANGE) list exactly these code points, so a character outside them
falls back to the next family of the font stack instead of showing an empty box. Shaping (GSUB/GPOS) is kept whole.
"""
import sys
from pathlib import Path

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "node_modules/@fontsource-variable/vazirmatn/files"
TARGET = ROOT / "public/fonts/vazirmatn"

# Persian + the Arabic letters Persian text uses: punctuation, letters (incl. پ چ ژ ک گ ی), harakat, Persian and Arabic-Indic digits,
# Arabic decimal/thousands separators (what Intl fa-IR writes), ZWNJ/ZWJ/LRM/RLM and the rial sign.
ARABIC = (
    [0x060C, 0x061B, 0x061F, 0x0640, 0x066A, 0x066B, 0x066C, 0x066D, 0x0670, 0x06D4, 0xFDFC]
    + list(range(0x0621, 0x063B))   # hamza .. ghain
    + list(range(0x0641, 0x0653))   # feh .. sukun (letters and harakat)
    + [0x0654, 0x0655, 0x0653]
    + [0x067E, 0x0686, 0x0698, 0x06A9, 0x06AF, 0x06CC, 0x06C0, 0x06BE, 0x06D5]   # pe che zhe keheh gaf farsi-yeh heh-yeh heh-doachashmee ae
    + list(range(0x0660, 0x066A))   # Arabic-Indic digits
    + list(range(0x06F0, 0x06FA))   # Persian digits
    + [0x200C, 0x200D, 0x200E, 0x200F]
)
# Latin: printable ASCII (letters, Western digits, punctuation), no-break space, the copyright sign, guillemets, dashes and quotes.
LATIN = (
    list(range(0x0020, 0x007F))
    + [0x00A0, 0x00A9, 0x00AB, 0x00BB, 0x00D7]
    + [0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2026, 0x2212]
)


def ranges(points):
    """Code points as a CSS unicode-range value (contiguous runs collapsed)."""
    points = sorted(set(points))
    runs, start = [], points[0]
    for previous, current in zip(points, points[1:] + [None]):
        if current is None or current != previous + 1:
            runs.append((start, previous))
            start = current
    return ",".join(f"U+{a:04X}" if a == b else f"U+{a:04X}-{b:04X}" for a, b in runs)


def run(name, points):
    options = subset.Options()
    options.flavor = "woff2"
    options.layout_features = ["*"]      # keep every shaping feature (joining forms, marks)
    options.notdef_outline = True
    options.name_IDs = ["*"]
    options.drop_tables += []
    font = subset.load_font(str(SOURCE / name), options)
    subsetter = subset.Subsetter(options)
    subsetter.populate(unicodes=points)
    subsetter.subset(font)
    out = TARGET / name
    subset.save_font(font, str(out), options)
    # The shop uses 400, 500 and 700 only: narrowing the weight axis to 400-700 drops the masters nobody asks for. (After the
    # subset: instancing the full font trips over unused glyph names.)
    narrowed = instancer.instantiateVariableFont(TTFont(str(out)), {"wght": (400, 700)})
    narrowed.flavor = "woff2"
    narrowed.save(str(out))
    print(f"{name}: {(SOURCE / name).stat().st_size} -> {out.stat().st_size} bytes")


if __name__ == "__main__":
    run("vazirmatn-arabic-wght-normal.woff2", ARABIC)
    run("vazirmatn-latin-wght-normal.woff2", LATIN)
    print("ARABIC_RANGE:", ranges(ARABIC))
    print("LATIN_RANGE:", ranges(LATIN))
    sys.exit(0)
