from collections import deque
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path.home() / "Downloads"
OUTPUT = ROOT / "Assets" / "Resources" / "UI" / "CardFrames"

FRAME_SOURCES = {
    "Creature_W.png": "creature white.png",
    "Creature_U.png": "creature blue.png",
    "Creature_B.png": "creature black.png",
    "Creature_R.png": "creature red.png",
    "Creature_G.png": "creature green.png",
    "Creature_C.png": "C ARTIFACT.png",
    "Land_W.png": "land white.png",
    "Land_U.png": "land blue.png",
    "Land_B.png": "land black.png",
    "Land_R.png": "land red.png",
    "Land_G.png": "land green.png",
    "Land_C.png": "C land.png",
}


def clear_connected_black_background(image):
    image = image.convert("RGBA")
    pixels = image.load()
    width, height = image.size
    visited = bytearray(width * height)
    queue = deque()

    def is_background(x, y):
        red, green, blue, _ = pixels[x, y]
        return red <= 10 and green <= 10 and blue <= 10

    def enqueue(x, y):
        index = y * width + x
        if visited[index] or not is_background(x, y):
            return
        visited[index] = 1
        queue.append((x, y))

    for x in range(width):
        enqueue(x, 0)
        enqueue(x, height - 1)
    for y in range(height):
        enqueue(0, y)
        enqueue(width - 1, y)

    while queue:
        x, y = queue.popleft()
        if x > 0:
            enqueue(x - 1, y)
        if x + 1 < width:
            enqueue(x + 1, y)
        if y > 0:
            enqueue(x, y - 1)
        if y + 1 < height:
            enqueue(x, y + 1)

    for y in range(height):
        for x in range(width):
            if visited[y * width + x]:
                red, green, blue, _ = pixels[x, y]
                pixels[x, y] = (red, green, blue, 0)

    # The supplied PNGs contain an opaque black illustration placeholder.
    # Clear only its interior, keeping the surrounding black frame line.
    for y in range(92, 465):
        for x in range(45, 550):
            red, green, blue, _ = pixels[x, y]
            if red <= 10 and green <= 10 and blue <= 10:
                pixels[x, y] = (red, green, blue, 0)

    return image


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for output_name, source_name in FRAME_SOURCES.items():
        source_path = SOURCE / source_name
        if not source_path.exists():
            raise FileNotFoundError(source_path)
        image = clear_connected_black_background(Image.open(source_path))
        image.save(OUTPUT / output_name)
        print(f"Imported {source_name} -> {output_name}")


if __name__ == "__main__":
    main()
