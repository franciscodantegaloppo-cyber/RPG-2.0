"""Remove only the exterior black surrounding the crafting-table frame."""
from collections import deque
from pathlib import Path
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "Bitszer" / "Crafting table grid.png"
OUTPUT = ROOT / "Assets" / "Resources" / "UI" / "Crafting table grid.png"

image = Image.open(SOURCE).convert("RGBA")
width, height = image.size

# Bright frame pixels form a closed barrier. Dilating it closes tiny decorative
# gaps, then a flood fill from the canvas edges identifies only exterior black.
barrier = image.convert("RGB").convert("L").point(
    lambda value: 255 if value >= 27 else 0
).filter(ImageFilter.MaxFilter(7))
blocked = barrier.load()
outside = bytearray(width * height)
queue = deque()


def add(x, y):
    index = y * width + x
    if not outside[index] and blocked[x, y] == 0:
        outside[index] = 1
        queue.append((x, y))


for x in range(width):
    add(x, 0)
    add(x, height - 1)
for y in range(height):
    add(0, y)
    add(width - 1, y)

while queue:
    x, y = queue.popleft()
    if x > 0: add(x - 1, y)
    if x + 1 < width: add(x + 1, y)
    if y > 0: add(x, y - 1)
    if y + 1 < height: add(x, y + 1)

alpha = Image.new("L", image.size, 255)
alpha_pixels = alpha.load()
for y in range(height):
    for x in range(width):
        if outside[y * width + x]:
            alpha_pixels[x, y] = 0

image.putalpha(alpha)
image.save(OUTPUT, optimize=True)
print(f"Saved {OUTPUT} ({sum(outside)} exterior pixels made transparent)")
