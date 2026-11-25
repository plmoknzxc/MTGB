"""Draw the permanent-counter enamel plates and pictograms for both world and UI cards."""
import io
import json
from pathlib import Path

from PIL import Image
import resvg_py

ROOT = Path(__file__).resolve().parents[2] / "Assets/Resources/UI/PermanentCounters"


def path(d, fill):
    return f'<path d="{d}" fill="{fill}" fill-rule="evenodd"/>'


def mark(d, tint, cut=""):
    return path(d, tint) + (path(cut, "#252739") if cut else "")


MINT, ROSE, LILAC, TEAL, GOLD = "#a7efe1", "#ffada1", "#dfc5ff", "#a4dae9", "#f2d39d"
MARKS = {
    "P1P1": mark("M13 5h6v8h8v6h-8v8h-6v-8H5v-6h8z", MINT),
    "M1M1": mark("M4 12h24v7H4z", ROSE),
    "SHIELD": mark("M16 2q6 4 12 4v9c0 6-5 11-12 15C9 26 4 21 4 15V6q6 0 12-4z", TEAL,
                   "M16 7v17c4-3 7-6 7-10v-4z"),
    "STUN": mark("M22 1l2 6 7 2-7 3-2 6-3-6-6-3 6-2zM3 18c3-6 8-9 13-10l-3 4q-7 3-8 7c0 4 10 2 17-2l2-3 2 2 4-2c-1 6-11 12-20 13-9 1-11-3-7-9z", LILAC),
    "CHARGE": mark("M17 1h11L19 13h8L9 31l4-13H4z", GOLD),
    "OIL": mark("M17 1c-2 8-12 13-12 20a11 10 0 0 0 22-1C27 13 19 6 17 1z", "#a8b9de",
                "M9 19c1 5 4 7 8 7-6 3-9 0-8-7z"),
    "LORE": mark("M2 6q7-2 13 3v19q-6-5-13-4zm15 3q7-5 13-3v18q-7-1-13 4z", GOLD,
                 "M5 10l7 2v2l-7-2zm15 1 7-2v2l-7 2z"),
    "TIME": mark("M4 3h24v4h-3c0 4-3 7-6 9 3 2 6 5 6 9h3v4H4v-4h3c0-4 3-7 6-9-3-2-6-5-6-9H4zm7 4c0 3 3 6 5 7 3-2 5-4 5-7zm5 13-5 5h10z", GOLD),
    "AGE": mark("M16 2 29 9l-1 13-12 8-13-8L2 10zm0 5-9 5 1 8 8 5 8-5 1-8zm0 5 5 3-1 5-4 2-4-3-1-4z", "#e8be98"),
    "QUEST": mark("M5 3h4v2c6-4 12 4 21-1L25 12l4 5c-8 4-14-3-20 0v12H5z", MINT),
    "VERSE": mark("M10 7 27 3v19c0 6-10 8-11 3-1-4 4-7 7-6V9l-9 2v14c0 6-10 8-11 3-1-4 4-7 7-6z", LILAC),
    "POISON": mark("M18 2c6 0 10 5 9 11-1 7-10 7-15 9-3 2-2 5 4 6-8 3-15-2-12-8 3-5 12-5 16-9l-6 2-6-5 5-5z", "#c3db91",
                   "M17 6h5l-2 3z"),
    "FINALITY": mark("M6 3h20v5l-6 7v4l6 6v4H6v-4l6-6v-4L6 8zm6 4 4 5 4-5z", LILAC),
    "ICE": mark("M14 2h4v10l8-5 2 4-8 5 8 5-2 4-8-5v10h-4V20l-8 5-2-4 8-5-8-5 2-4 8 5z", TEAL),
    "LOYALTY": mark("M2 8l7 5 2-10 5 9 5-9 2 10 7-5-4 16H6zm5 18h18v3H7z", LILAC),
    "DEFENSE": mark("M2 7h7v5h4V7h6v5h4V7h7v10l-3 2v9H5v-9l-3-2zm11 20h6v-6a3 3 0 0 0-6 0z", GOLD),
    "MORE": ''.join(f'<circle cx="{x}" cy="16" r="3.2" fill="{LILAC}"/>' for x in (6,16,26)),
}

PLATES = {
    "NEUTRAL": ("#607785", "#37434d", "#202830"),
    "P1P1": ("#72afa3", "#33544d", "#1a3532"),
    "M1M1": ("#b27e7c", "#604044", "#3c272f"),
    "LORE": ("#b9a17a", "#554c40", "#302c2a"),
    "MORE": ("#84778d", "#474052", "#2c2736"),
}


def plate(edge, light, dark):
    return (f'<defs><linearGradient id="field" x2="0" y2="1" gradientUnits="objectBoundingBox">'
            f'<stop stop-color="{light}"/><stop offset="1" stop-color="{dark}"/></linearGradient></defs>'
            + path("M13 1h49l3 4v19l-4 5H13C-2 29-2 1 13 1z", "#121821")
            + path("M13 2h48l3 3v18l-4 5H13C-1 28-1 2 13 2z", edge)
            + path("M13 4h47l2 2v16l-3 4H13C2 26 2 4 13 4z", "url(#field)")
            + '<path d="M27 7v16" fill="none" stroke="#e7e7ed" stroke-opacity=".22" stroke-width="1.4"/>')


def seal(index):
    # Custom names keep 64 stable silhouettes/engravings, large enough to read at 14 px.
    silhouettes = [
        "M16 1l12 7v16l-12 7-12-7V8z", "M16 1l14 15-14 15L2 16z",
        "M7 3h18l5 9-4 15-10 4L6 27 2 12z", "M16 1l5 6 9 2-1 15-13 7-13-7L2 9l9-2z",
        "M3 10l8-8 5 3 5-3 8 8-2 14-11 7L5 24z", "M7 2h18l5 20-14 9L2 22z",
        "M16 1l11 6 3 10-8 12H10L2 17 5 7z", "M2 8l8-6 6 4 6-4 8 6-4 17-10 6-10-6z",
    ]
    engravings = [
        "M16 6l6 10-6 9-6-9z", "M8 11h16v4h-6v9h-4v-9H8z",
        "M7 17l9-10 9 10-3 3-6-7-6 7z", "M9 9h5v9h8v5H9z",
        "M9 10l7 4 7-4v5l-5 3v6h-4v-6l-5-3z", "M8 11h16v4H8zm3 8h10v4H11z",
        "M11 8l5 6 5-6 3 3-5 6 4 5-3 3-4-5-4 5-3-3 4-5-5-6z", "M16 7l8 8-8 10-8-10zm0 6-3 3 3 3 3-3z",
    ]
    return mark(silhouettes[index//8],"#c4cfd4",engravings[index%8])


def build():
    ROOT.mkdir(parents=True, exist_ok=True)
    atlas = Image.new("RGBA", (512,512))
    entries = []

    def place(kind, art, x, y, width, height, view):
        svg = f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{view}">{art}</svg>'
        data = resvg_py.svg_to_bytes(svg_string=svg, width=width*3, height=height*3)
        image = Image.open(io.BytesIO(data)).convert("RGBA").resize((width,height),Image.Resampling.LANCZOS)
        atlas.paste(image,(x,y))
        entries.append(dict(kind=kind,x=x,y=y,width=width,height=height))

    for i,(kind,art) in enumerate(MARKS.items()):
        place(kind,art,4+(i%7)*72,4+(i//7)*72,64,64,"-1 -1 34 34")
    for i,(kind,tints) in enumerate(PLATES.items()):
        place("PLATE_"+kind,plate(*tints),4+(i%3)*160,236+(i//3)*80,144,64,"0 0 66 30")
    for i in range(64):
        place("SEAL_"+str(i),seal(i),2+(i%16)*32,384+(i//16)*32,28,28,"-1 -1 34 34")
    atlas.save(ROOT/"Artwork.png",optimize=True)
    (ROOT/"Index.json").write_text(json.dumps({"entries":entries},indent=2)+"\n",encoding="utf-8")


if __name__ == "__main__":
    build()
