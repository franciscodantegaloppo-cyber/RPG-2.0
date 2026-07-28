"""Build the transparent MU alphabet atlas consumed by MuOnlineAlphabet."""
from pathlib import Path
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "AI Toolkit" / "Abecedario mu onine.jpg"
OUTPUT = ROOT / "Assets" / "_RPG" / "Resources" / "UI" / "MuAlphabet.png"
SMALL_OUTPUT = ROOT / "Assets" / "_RPG" / "Resources" / "UI" / "MuAlphabetSmall.png"

image = Image.open(SOURCE).convert("RGB")
pixels = image.load()
result = Image.new("RGBA", image.size)
out = result.load()

# JPEG black is not exactly (0, 0, 0). A soft luminance matte removes its blocks
# while retaining the dim golden glow and anti-aliased bevels.
for y in range(image.height):
    for x in range(image.width):
        r, g, b = pixels[x, y]
        brightness = max(r, g, b)
        alpha = max(0, min(255, round((brightness - 5) * 255 / 30)))
        out[x, y] = (r, g, b, alpha)

OUTPUT.parent.mkdir(parents=True, exist_ok=True)
result.save(OUTPUT, optimize=True)
small = Image.merge("RGBA", tuple(channel.filter(ImageFilter.MaxFilter(5)) for channel in result.split()))
small.save(SMALL_OUTPUT, optimize=True)
print(OUTPUT)
print(SMALL_OUTPUT)
