"""Bake the journal's approved vector artwork. Requires Pillow and resvg-py."""
from io import BytesIO
from pathlib import Path

from PIL import Image
from resvg_py import svg_to_bytes


def lantern(active):
    edge = "#ffb5a6" if active else "#9e8996"
    fill = "#eab4a0" if active else "#34313c"
    ribs = "#985c61" if active else edge
    return f'''<g stroke="{edge}" stroke-linejoin="round" stroke-width=".95">
        <path d="M6 0v2 m-3 1h6 m-6 11h6 M6 14.5v3" fill="none"/>
        <path d="M3 4h6l1.4 2.8v3.7L9 13H3l-1.4-2.5V6.8z" fill="{fill}"/>
        <path d="M2 7h8M2 10h8" stroke="{ribs}" stroke-width=".85"/></g>'''


def tide(centres, selected):
    def y(x):
        return 34 + 6 * (1 - ((x - 217) / 205) ** 2)

    art = '<path d="M12 34 Q217 46 422 34" stroke="#846671" stroke-width=".9" fill="none"/>'
    for i, x in enumerate(centres):
        art += f'<g transform="translate({x-5.2} {y(x)-6:.1f}) scale(.88)">{lantern(i==selected)}</g>'
    x = centres[selected]
    art += f'''<path d="M{x-27} {y(x-27):.1f} Q{x} {y(x):.1f} {x-8} {y(x-8):.1f}
        M{x+8} {y(x+8):.1f} Q{x+19} {y(x+19):.1f} {x+27} {y(x+27):.1f}"
        stroke="#edaba0" stroke-width="1.6" fill="none"/>'''
    return art


def sanctum(centres, selected):
    peaks = "".join(f"H{x-12}l12 -6 12 6" for x in centres)
    art = f'<path d="M12 42{peaks}H422" stroke="#718695" stroke-width=".9" fill="none"/>'
    for i, x in enumerate(centres):
        edge, fill = ("#c1d8f0", "#bbcee5") if i == selected else ("#8599a9", "#20303d")
        art += f'''<path transform="translate({x-4} 33) scale(.67)"
            d="M6 0l1.7 5.9L12 8l-4.3 2.1L6 16l-1.7-5.9L0 8l4.3-2.1z" stroke="{edge}" fill="{fill}" stroke-width=".9"/>'''
    x = centres[selected]
    return art + f'<path d="M{x-33} 42h21l6 -3 M{x+6} 39l6 3h21" stroke="#c2d7ed" stroke-width="1.8" fill="none"/>'


def export(path, artwork, top, bottom, opacity):
    # Five rows, in page order: three-page layout, then two-page layout.
    width, height = 868, 108
    sheet = Image.new("RGB", (width, 5 * height))
    row = 0
    for count in (3, 2):
        centres = [5.5 + (i + .5) * 423 / count for i in range(count)]
        for selected in range(count):
            svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 434 54">
                <defs><linearGradient id="bg" x2="0" y2="1"><stop stop-color="{top}"/>
                <stop offset="1" stop-color="{bottom}"/></linearGradient></defs>
                <rect width="434" height="54" fill="url(#bg)"/><g opacity="{opacity}">{artwork(centres, selected)}</g></svg>'''
            sheet.paste(Image.open(BytesIO(svg_to_bytes(svg_string=svg))), (0, row * height))
            row += 1
    sheet.save(path, optimize=True)


if __name__ == "__main__":
    menu = Path(__file__).resolve().parents[2] / "Assets/Resources/Menu"
    export(menu.parents[1] / "ThemeSources/Tidelights/JournalTabs.png", tide, "#2a2732", "#21212b", .84)
    export(menu / "JournalTabs.png", sanctum, "#172934", "#121e29", 1)
