"""Prepare PeriTune's official menu loops from the unmodified Oggs in Masters/.

Run e.g. python Tools/Audio/menu_scores.py moon. Only a constant gain is
applied. Preserve the original tempo, sample rate, length and loop boundaries.
"""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
TRACKS = json.loads((HERE / "menu_themes.json").read_text(encoding="utf-8"))
LICENSE = "https://creativecommons.org/licenses/by/4.0/"


def prepare(theme):
    track = TRACKS[theme]
    source = HERE / "Masters" / track["source_file"]
    destination = ROOT / track["game_file"]
    with tempfile.TemporaryDirectory() as work:
        encoded = Path(work) / "music.ogg"
        subprocess.run([
            "ffmpeg", "-v", "error", "-i", str(source), "-map", "0:a:0",
            "-af", f"asetpts=N/SR/TB,volume={track['gain_db']}dB",
            "-c:a", "libvorbis", "-q:a", "6",
            "-fflags", "+bitexact", "-metadata", "artist=PeriTune (Sei Mutsuki)",
            "-metadata", "title=" + track["title"], "-metadata", "license=" + LICENSE,
            "-metadata", "comment=" + track["page"] + " | Official loop, volume adjusted by MTGB.",
            str(encoded)
        ], check=True)
        contents = encoded.read_bytes()
    if not destination.exists() or destination.read_bytes() != contents:
        destination.write_bytes(contents)


def moon_menu():
    prepare("moon")


def tide_menu():
    prepare("tide")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("theme", choices=TRACKS)
    prepare(parser.parse_args().theme)
