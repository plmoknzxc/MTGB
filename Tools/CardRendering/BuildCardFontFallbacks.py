"""Rebuild the two small, renamed card fallback fonts from pinned open sources.

Requires fonttools==4.66.1. Run from the repository root. Sources stay in .tools;
the public manifest pins original and derived bytes, licenses and modifications.
"""
from pathlib import Path
import argparse
import hashlib
import io
import json
import urllib.request
import zipfile

from fontTools import subset, version
from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parents[2]
FONTS = ROOT / "Assets/Resources/UI/CardTemplates/Fonts"


def checked(data, expected):
    if hashlib.sha256(data).hexdigest() != expected:
        raise ValueError("Font source/output SHA256 mismatch")
    return data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=Path, default=ROOT / ".tools/open-font-preview-1014/downloads")
    parser.add_argument("--output", type=Path, default=ROOT / ".tools/open-font-preview-1014/rebuilt")
    args = parser.parse_args()
    args.source_dir.mkdir(parents=True, exist_ok=True)
    args.output.mkdir(parents=True, exist_ok=True)
    for row in json.loads((FONTS / "open-fonts.json").read_text(encoding="utf8"))["files"]:
        if not row.get("derived"):
            continue
        if version != row["fonttoolsVersion"]:
            raise ValueError("Use fonttools==" + row["fonttoolsVersion"])
        origin = row["source"]
        source_name = origin.get("archiveMember") or Path(origin["sourcePath"]).name
        path = args.source_dir / source_name
        if path.exists():
            raw = checked(path.read_bytes(), row["sourceFileSha256"])
        else:
            raw = urllib.request.urlopen(origin["url"], timeout=60).read()
            if "archiveMember" in origin:
                checked(raw, origin["archiveSha256"])
                raw = zipfile.ZipFile(io.BytesIO(raw)).read(origin["archiveMember"])
            checked(raw, row["sourceFileSha256"])
            path.write_bytes(raw)
        font = TTFont(io.BytesIO(raw), recalcTimestamp=False)
        options = subset.Options()
        options.name_IDs = ["*"]
        options.name_languages = ["*"]
        options.recalc_timestamp = False
        reducer = subset.Subsetter(options=options)
        reducer.populate(unicodes=row["unicodeSubset"])
        reducer.subset(font)
        family = row["family"]
        notice = "MTGB contributors: subsetted and renamed on 2026-10-14; glyph outlines unchanged. " + row["license"] + "."
        values = {1: family, 2: "Regular", 3: family + "-Regular-20261014", 4: family,
                  6: family.replace(" ", "") + "-Regular", 16: family, 17: "Regular", 10: notice}
        for record in font["name"].names:
            if record.nameID in values:
                record.string = values[record.nameID].encode(record.getEncoding())
        font["name"].setName(notice, 10, 3, 1, 0x409)
        output = args.output / row["file"]
        font.save(output)
        font.close()
        checked(output.read_bytes(), row["sha256"])
        print(row["file"] + " reproduced exactly")


if __name__ == "__main__":
    main()
