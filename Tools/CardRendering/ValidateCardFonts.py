"""Reject retired font binaries/families and verify reviewed fonts and notices.

Requires fonttools (also used by BuildCardFontFallbacks.py).
"""
import argparse
import hashlib
import json
from pathlib import Path
from fontTools.ttLib import TTFont, TTCollection

ROOT = Path(__file__).resolve().parents[2]
FONT_ROOT = Path("Assets/Resources/UI/CardTemplates/Fonts")
EXTENSIONS = {".otf", ".ttf", ".ttc", ".woff", ".woff2"}


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def validate_file(path, policy):
    errors = []
    sha = digest(path)
    for item in policy:
        if sha == item["sha256"]:
            errors.append(f"Banned font binary: {path} ({item['family']})")
    container = TTCollection(path) if path.suffix.lower() == ".ttc" else TTFont(path)
    try:
        fonts = container.fonts if isinstance(container, TTCollection) else [container]
        for font in fonts:
            names = {n.toUnicode().casefold() for n in font["name"].names if n.nameID in (1, 4, 6, 16)}
            for item in policy:
                if any(alias.casefold() in name for alias in item["aliases"] for name in names):
                    errors.append(f"Banned font family: {path} ({item['family']})")
    finally:
        container.close()
    return errors


def validate(root, extra_files=()):
    policy = json.loads((root / "Tools/CardRendering/banned-fonts.json").read_text(encoding="utf-8"))["fonts"]
    if len(policy) != 5 or any(not f.get("sha256") or not f.get("aliases") for f in policy):
        raise ValueError("Incomplete banned-font policy")
    font_root = root / FONT_ROOT
    manifest = json.loads((font_root / "open-fonts.json").read_text(encoding="utf-8"))["files"]
    errors = []
    approved = {item["file"]: item["sha256"] for item in manifest}
    if not approved: raise ValueError("Empty approved-font manifest")
    for name, sha in approved.items():
        if Path(name).name != name: raise ValueError("Invalid font-manifest path")
        p = font_root / name
        if not p.is_file() or digest(p) != sha:
            errors.append(f"Reviewed file missing or changed: {p}")
    fonts = [p for p in (root / "Assets").rglob("*") if p.is_file() and p.suffix.lower() in EXTENSIONS]
    for p in fonts + list(map(Path, extra_files)):
        errors.extend(validate_file(p, policy))
        if p.is_relative_to(font_root) and p.name not in approved:
            errors.append(f"Unreviewed template font: {p}")
    return dict(status="FAIL" if errors else "PASS", fontsChecked=len(fonts), approvedFiles=len(approved), errors=errors)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--check-file", action="append", default=[])
    args = parser.parse_args()
    report = validate(args.root.resolve(), args.check_file)
    print(json.dumps(report, ensure_ascii=False, indent=2))
    raise SystemExit(1 if report["errors"] else 0)
