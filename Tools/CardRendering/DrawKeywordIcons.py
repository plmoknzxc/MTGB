"""Rasterize the battlefield pictograms into the keyword atlas (resvg-py, Pillow)."""
import io
import json
from pathlib import Path

import resvg_py
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2] / "Assets/Resources/UI/KeywordIcons"
PAPER = "#fff3d6"
INK = "#171d29"
ICE = "#a6e1e7"
CORAL = "#efa58d"
JADE = "#a6d7ad"
LILAC = "#c7b3ec"


def path(shape, fill=PAPER):
    return f'<path fill="{fill}" fill-rule="evenodd" d="{shape}"/>'


# Each mark has a distinct outer contour. Counters and open cuts must survive at 14 px.
SWORD = "M4 27l5-7-3-3 2-2 3 2L23 5l6-2-2 7-12 11 2 3-2 2-3-3-6 6z"
MARKS = {
    "FLYING": path("M3 27C9 13 18 9 30 3c-1 7-7 12-15 16l9-2c-3 5-9 7-15 7l6 1c-4 3-8 4-12 2z")
              + path("M4 27c6-8 11-13 19-17L8 25z", ICE),
    "VIGILANCE": path("M1 16Q16-3 31 16Q16 35 1 16zm5 0q10 12 20 0Q16 4 6 16z")
                 + '<circle cx="16" cy="16" r="4.6" fill="#a6e1e7"/>',
    "DEATHTOUCH": path("M5 14C4 0 28 0 27 14l-2 7-5 1-1 5h-7l-1-5-5-2zm3-1 1 5 5-2v-3zm10 0v3l5 2 1-5zm-2 4-2 4h4z"),
    "LIFELINK": path("M16 28C10 24 2 18 2 11 2 3 12 1 16 9c5-8 15-6 15 2 0 7-9 14-15 17z", CORAL)
                + path("M18 10c0 5-7 7-7 11 0 3 4 5 7 2 3-3 2-8 0-13z", INK)
                + path("M17 17c0 3-1 6-3 6-2-1 1-4 3-6z"),
    "HASTE": path("M16 3h14L19 14h8L7 30l6-14H5z", CORAL)
             + path("M2 7h10l-2 3H1zm0 7h6l-2 3H1z"),
    "FLASH": path("M17 1l3 10 9-3-6 8 7 4-10 1-4 10-3-10-11 2 7-8-6-5 10 1z")
             + path("M17 8l1 7 4 2-6 2-2 5 1-8-4-1 5-2z", ICE),
    "FIRST_STRIKE": path(SWORD) + path("M3 7h9l-3 3H2zm-2 6h6l-2 2H1z", CORAL),
    "DOUBLE_STRIKE": path("M1 28l3-6-2-2 2-2 2 1L21 3l5-2-2 6L9 22l1 2-2 2-2-2z")
                    + path("M10 30l3-6-2-2 2-2 2 1L26 9l5-2-1 6-12 12 1 2-2 2-2-2z", ICE),
    "REACH": path("M5 4c12 1 20 9 22 23L4 5zm4 4 14 14C21 16 17 11 9 8z")
             + path("M4 25 21 8l-3-1 11-5-4 12-2-4L7 28l-6 3z", ICE),
    "TRAMPLE": path("M2 24V14C2 7 8 4 15 5c6-3 12 2 13 8l1 8q1 3 2-1c1 7-3 10-6 7-2-2-3-6-4-10l-4 2-1 7h-5v-7H8l-1 7H3z")
               + path("M10 9q3-4 8-2l2 5q-2 5-7 7l-1-4 4 1 2-5-3-2z", INK)
               + path("M19 17c5 2 10 0 12-4-1 7-7 10-12 7z", CORAL),
    "DEFENDER": path("M1 5h5v5h4V5h5v5h3V5h5v5h4V5h4v12l-3 2v9H4v-9l-3-2z", ICE)
                + path("M4 13h24v3H4zm6 16v-6q0-5 6-8 6 3 6 8v6z", INK)
                + path("M1 25h7v4H1zm23 0h7v4h-7z"),
    "INDESTRUCTIBLE": path("M2 11l6-7h16l6 7-14 18zm5 1 7 10-3-10zm7 0 2 11 3-11zm7 0-2 9 7-9z", ICE),
    "MENACE": path("M1 8l13 3 2 8-6 9-6-5zm3 5 2 5 5 1-1-3z")
              + path("M16 5l15-2-3 15-7 7-4-5 2-4-2-7zm3 7 5 1 4-5-7 1z", CORAL),
    "WARD": path("M4 7l12-5 12 5v9c-1 6-7 11-12 14C9 26 4 21 4 16zm4 2v7c0 4 4 8 8 11 5-4 8-8 8-12V9l-8-4z", ICE)
            + path("M16 8l5 7-3 3 1 4h-6l1-4-3-3z"),
    "HEXPROOF": path("M3 7c9 1 14-5 14-5s5 5 12 5l-3 9-7-1-4 7 6-3 4 1-9 10C8 23 4 16 3 7z", JADE)
                + path("M18 5l-8 11 5-2-3 7 10-12-5 2z", INK),
    "SHROUD": path("M4 25c2-6 3-14 7-20q5-5 10 1c4 6 5 14 8 19l-9-3-4 7-4-7zm6-7 6 3 6-4-5-9z", LILAC),
    "SHADOW": path("M24 2C12 7 11 20 24 24l5 1C19 35 3 26 3 17 3 8 13 2 24 2z", LILAC),
    "EXALTED": path("M4 19l9-8h6l9 8-10-4v10l-2 5-2-5V15z")
               + path("M16 1l5 5-5 5-5-5z", CORAL),
    "FEAR": path("M3 7C9 1 23 1 29 7l-3 14-10 9L6 21zm4 4 2 6 5 1-1-4zm11 3-1 4 6-1 2-6zm-6 10 4-4 4 4-4 2z", LILAC),
    "INTIMIDATE": path("M2 4l9 6 5-2 5 2 9-6-3 14-6 7-5 4-5-4-6-7zm5 9 2 5 5-1-2-3zm13 1-2 3 5 1 2-5zm-8 7 4 5 4-5z", CORAL),
    "HORSEMANSHIP": path("M6 28l5-9 7-5-8 3-6-3 9-10 1-4 4 4 5-3v6c5 5 6 13 4 21zm7-19 2 1 2-2-2-1z"),
    "LANDWALK": path("M1 25 12 9l5 7 4-10 10 19zm5-3h6l-1-6zm14-5 4-2-3-4z", JADE)
                + path("M6 29c4-6 14-2 14-7l5 1c-1 6-12 4-13 7z"),
    "TOXIC": path("M16 1C13 9 5 15 5 21a11 10 0 0 0 22 0C27 15 20 7 16 1z", JADE)
             + path("M12 14h8v3h-2v3l4 5H10l4-5v-3h-2z", INK),
    "WITHER": path("M4 25C5 9 14 5 29 3l-2 6-8 1 6 3-4 5-7-1 3 4-8 4-5 5-2-2 16-16z", JADE),
    "ANNIHILATOR": path("M16 1l4 9 9-4-5 9 7 4-10 2-3 10-5-9-9 3 5-9-8-5 11-1zm0 10-5 5 5 5 5-5z", CORAL),
    "COMMANDER": path("M2 6l8 7 6-11 6 11 8-7-5 16H7zm5 18h18v4H7z"),
    "TOKEN": path("M16 2l13 7v14l-13 7-13-7V9zm0 4L8 10l8 4 8-4zm-9 7v8l7 4v-9zm12 3v9l7-4v-8z", ICE),
}

COLORS = {"W":"#fff1be", "U":"#77cafa", "B":"#bc9acc", "R":"#f48770", "G":"#87cda0", "C":"#d6dae4"}
SHIELD = "M4 5Q11 6 16 2q6 4 12 3v10q-1 9-12 15Q5 23 4 15z"
INNER = "M8 9q5 0 8-3 4 3 8 3v6q-1 6-8 11-7-5-8-11z"


def shield(kind):
    hexproof = kind.startswith("HEXPROOF_")
    flags = kind.rsplit("_", 1)[-1]
    hues = ([COLORS[x] for x in flags] if all(x in COLORS for x in flags)
            else list(COLORS.values())[:5] if flags in ("ALL","COLOREDSPELLS") else [ICE])
    stripes = "".join(f'<rect x="{7+i*18/len(hues):.2f}" y="5" width="{18/len(hues)+.1:.2f}" height="23" fill="{h}"/>'
                      for i,h in enumerate(hues))
    body = path(SHIELD) + f'<defs><clipPath id="field">{path(INNER)}</clipPath></defs><g clip-path="url(#field)">{stripes}</g>'
    if hexproof:
        return body + path("M20 7l-10 11 6-1-3 7 10-13-6 2z", INK)
    if flags == "ALL":
        return body + path("M16 8l2 5 5 2-5 2-2 6-2-6-5-2 5-2z", INK)
    if flags == "COLOREDSPELLS":
        return body + path("M11 10h8l3 3-3 9H9zm2 3-1 3h6l1-3z", INK)
    return body + path("M16 9l5 6-5 7-5-7z", INK)


def draw(kind, size):
    drawing = MARKS[kind] if kind in MARKS else shield(kind)
    # Keep a transparent guard band on every side for oblique mipmap samples.
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="-1 -1 34 34">{drawing}</svg>'
    pixels = size * 3
    raw = Image.open(io.BytesIO(resvg_py.svg_to_bytes(svg_string=svg, width=pixels, height=pixels))).convert("RGBA")
    keyline = Image.new("RGBA", raw.size, INK)
    keyline.putalpha(raw.getchannel("A").filter(ImageFilter.MaxFilter(11)))
    keyline.alpha_composite(raw)
    return keyline.resize((size, size), Image.Resampling.LANCZOS)


if __name__ == "__main__":
    entries = json.loads((ROOT / "Index.json").read_text(encoding="utf-8"))["entries"]
    atlas = Image.new("RGBA", (494, 740))
    for e in entries:
        kind = e["kind"]
        if kind not in MARKS and not kind.startswith(("HEXPROOF_", "PROTECT_")) and kind != "PROTECTION":
            raise ValueError(f"Missing drawing for {kind}")
        atlas.paste(draw(kind,e["width"]), (e["x"], e["y"]))
    atlas.save(ROOT / "ForgeAbilities.png", optimize=True)
