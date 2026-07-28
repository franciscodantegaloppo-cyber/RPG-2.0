"""Create clean, opaque MU UI frames from the existing cropped assets."""
from pathlib import Path
from collections import deque
from PIL import Image, ImageEnhance, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
UI = ROOT / "Assets" / "Resources" / "UI"


def enhance(image):
    image = ImageEnhance.Color(image).enhance(1.35)
    image = ImageEnhance.Contrast(image).enhance(1.22)
    return ImageEnhance.Brightness(image).enhance(1.18)


def contour_alpha(image):
    """Keep the ornate closed frame and its interior; remove reachable exterior black."""
    rgb = image.convert("RGB")
    brightness = rgb.convert("L")
    barrier = brightness.point(lambda value: 255 if value >= 28 else 0).filter(ImageFilter.MaxFilter(5))
    blocked = barrier.load()
    width, height = image.size
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
    return alpha


panel = Image.open(UI / "PanelFrame_Clean.png").convert("RGBA")
pixels = panel.load()
# Remove the baked horizontal mirror seam.
for y in range(214, 220):
    amount = (y - 213) / 7
    for x in range(panel.width):
        upper = pixels[x, 213]
        lower = pixels[x, 220]
        pixels[x, y] = tuple(round(upper[i] * (1 - amount) + lower[i] * amount) for i in range(4))
panel = panel.crop((4, 27, 332, 410))
panel = enhance(panel)
panel.putalpha(contour_alpha(panel))
panel.save(UI / "PanelFrame_MU.png", optimize=True)


banner = Image.open(UI / "PanelFrame_Banner_Clean.png").convert("RGBA")
pixels = banner.load()
# Remove the baked vertical mirror seam.
for x in range(190, 196):
    amount = (x - 189) / 7
    for y in range(banner.height):
        left = pixels[189, y]
        right = pixels[196, y]
        pixels[x, y] = tuple(round(left[i] * (1 - amount) + right[i] * amount) for i in range(4))
banner = banner.crop((15, 0, 370, 78))
banner = enhance(banner)
banner.putalpha(contour_alpha(banner))
banner.save(UI / "PanelFrame_Banner_MU.png", optimize=True)

print("Created PanelFrame_MU.png and PanelFrame_Banner_MU.png")
