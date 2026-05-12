"""
Strip accented characters from translations, producing an ASCII-only version.

Usage:
    python make_ascii_translations.py <input.json> <output.json>

Some UE5 games use custom bitmap fonts with ASCII-only glyph sets.
Accented characters (e, a, o, etc.) will appear as boxes or crash
the game if the font or string table has no entry for those code points.
This script is a safe fallback: strip diacritics via NFD decomposition.

Output JSON has the same structure as input but all Translated strings
contain only ASCII characters (0x00–0x7F).
"""

import json
import sys
import unicodedata


def to_ascii(s: str) -> str:
    return unicodedata.normalize("NFD", s).encode("ascii", "ignore").decode("ascii")


def main():
    if len(sys.argv) < 3:
        print("Usage: python make_ascii_translations.py <input.json> <output.json>")
        sys.exit(1)

    input_path  = sys.argv[1]
    output_path = sys.argv[2]

    with open(input_path, encoding="utf-8") as f:
        entries = json.load(f)

    changed = 0
    for entry in entries:
        t = entry.get("Translated", "")
        if t:
            ascii_t = to_ascii(t)
            if ascii_t != t:
                entry["Translated"] = ascii_t
                changed += 1

    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(entries, f, ensure_ascii=False, indent=2)

    print(f"Converted {changed} strings with accented chars to ASCII")
    print(f"Saved to: {output_path}")


if __name__ == "__main__":
    main()
