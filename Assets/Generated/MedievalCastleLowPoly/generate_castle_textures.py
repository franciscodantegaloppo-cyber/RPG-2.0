import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


OUT_DIR = Path(__file__).resolve().parent / "Textures"
OUT_DIR.mkdir(parents=True, exist_ok=True)
SIZE = 512


def clamp(value):
    return max(0, min(255, int(value)))


def jitter(color, amount):
    return tuple(clamp(c + random.randint(-amount, amount)) for c in color)


def save(img, name):
    img.save(OUT_DIR / f"{name}.png")


def stone(name, base=(95, 95, 88), mortar=(45, 45, 42), block_w=92, block_h=56):
    random.seed(name)
    img = Image.new("RGB", (SIZE, SIZE), mortar)
    draw = ImageDraw.Draw(img)
    y = 0
    row = 0
    while y < SIZE:
        offset = 0 if row % 2 == 0 else -block_w // 2
        x = offset
        while x < SIZE:
            bw = block_w + random.randint(-18, 18)
            bh = block_h + random.randint(-10, 10)
            rect = (x + 2, y + 2, x + bw - 2, y + bh - 2)
            draw.rounded_rectangle(rect, radius=3, fill=jitter(base, 24))
            for _ in range(5):
                px0 = max(0, x + 6)
                px1 = min(SIZE - 1, x + max(7, bw - 6))
                py0 = max(0, y + 6)
                py1 = min(SIZE - 1, y + max(7, bh - 6))
                if px0 <= px1 and py0 <= py1:
                    px = random.randint(px0, px1)
                    py = random.randint(py0, py1)
                    draw.point((px, py), fill=jitter((60, 60, 58), 18))
            x += bw
        y += block_h
        row += 1
    img = img.filter(ImageFilter.GaussianBlur(0.25))
    save(img, name)


def cobble():
    random.seed("cobble")
    img = Image.new("RGB", (SIZE, SIZE), (42, 43, 40))
    draw = ImageDraw.Draw(img)
    for _ in range(260):
        x = random.randint(-20, SIZE)
        y = random.randint(-20, SIZE)
        rx = random.randint(12, 28)
        ry = random.randint(9, 24)
        draw.ellipse((x - rx, y - ry, x + rx, y + ry), fill=jitter((78, 78, 72), 28), outline=(35, 35, 32))
    save(img.filter(ImageFilter.GaussianBlur(0.2)), "Cobble_Texture")


def wood(name, base=(105, 62, 31), plank_w=64):
    random.seed(name)
    img = Image.new("RGB", (SIZE, SIZE), base)
    draw = ImageDraw.Draw(img)
    x = 0
    while x < SIZE:
        w = plank_w + random.randint(-12, 12)
        color = jitter(base, 25)
        draw.rectangle((x, 0, x + w - 3, SIZE), fill=color)
        draw.line((x + w - 2, 0, x + w - 2, SIZE), fill=(48, 28, 15), width=3)
        for yy in range(12, SIZE, 34):
            wave = random.randint(-8, 8)
            draw.arc((x + 8, yy + wave, x + w - 12, yy + 28 + wave), 0, 180, fill=jitter((57, 32, 17), 16), width=1)
        x += w
    save(img.filter(ImageFilter.GaussianBlur(0.2)), name)


def iron():
    random.seed("iron")
    img = Image.new("RGB", (SIZE, SIZE), (32, 33, 34))
    draw = ImageDraw.Draw(img)
    for y in range(0, SIZE, 64):
        draw.rectangle((0, y, SIZE, y + 60), fill=jitter((38, 39, 40), 10))
        draw.line((0, y + 60, SIZE, y + 60), fill=(15, 15, 16), width=4)
    for _ in range(130):
        x = random.randint(0, SIZE - 1)
        y = random.randint(0, SIZE - 1)
        r = random.randint(2, 6)
        draw.ellipse((x - r, y - r, x + r, y + r), fill=jitter((78, 74, 68), 22))
    save(img, "Iron_Texture")


def roof():
    random.seed("roof")
    img = Image.new("RGB", (SIZE, SIZE), (33, 39, 46))
    draw = ImageDraw.Draw(img)
    for y in range(-20, SIZE, 34):
        for x in range(-30, SIZE, 58):
            draw.polygon(
                [(x, y + 28), (x + 28, y), (x + 58, y + 28), (x + 45, y + 42), (x + 12, y + 42)],
                fill=jitter((42, 51, 60), 16),
                outline=(18, 22, 27),
            )
    save(img, "RoofSlate_Texture")


def thatch():
    random.seed("thatch")
    img = Image.new("RGB", (SIZE, SIZE), (145, 112, 43))
    draw = ImageDraw.Draw(img)
    for _ in range(1600):
        x = random.randint(0, SIZE)
        y = random.randint(0, SIZE)
        length = random.randint(18, 54)
        color = jitter((169, 135, 57), 35)
        draw.line((x, y, x + random.randint(-5, 5), y + length), fill=color, width=1)
    save(img.filter(ImageFilter.GaussianBlur(0.15)), "Thatch_Texture")


def water():
    random.seed("water")
    img = Image.new("RGB", (SIZE, SIZE), (26, 75, 104))
    draw = ImageDraw.Draw(img)
    for y in range(0, SIZE, 22):
        for x in range(-30, SIZE, 90):
            amp = random.randint(5, 13)
            draw.arc((x, y - amp, x + 80, y + amp), 0, 180, fill=jitter((55, 125, 150), 30), width=2)
    save(img.filter(ImageFilter.GaussianBlur(0.35)), "Water_Texture")


def fire():
    img = Image.new("RGB", (SIZE, SIZE), (92, 20, 6))
    draw = ImageDraw.Draw(img)
    for i in range(16):
        color = (255, clamp(70 + i * 9), 10)
        left = 40 + i * 13
        right = SIZE - 40 - i * 13
        top = 35 + i * 17
        bottom = SIZE - 15
        draw.polygon(((SIZE // 2, top), (right, bottom), (SIZE // 2, bottom - 80), (left, bottom)), fill=color)
    save(img.filter(ImageFilter.GaussianBlur(4)), "Fire_Texture")


def cloth(name, base):
    random.seed(name)
    img = Image.new("RGB", (SIZE, SIZE), base)
    draw = ImageDraw.Draw(img)
    for y in range(0, SIZE, 12):
        draw.line((0, y, SIZE, y + random.randint(-2, 2)), fill=jitter(base, 22), width=2)
    for x in range(0, SIZE, 18):
        draw.line((x, 0, x + random.randint(-2, 2), SIZE), fill=jitter(base, 16), width=1)
    save(img.filter(ImageFilter.GaussianBlur(0.2)), name)


def grass():
    random.seed("grass")
    img = Image.new("RGB", (SIZE, SIZE), (42, 82, 38))
    draw = ImageDraw.Draw(img)
    for _ in range(2400):
        x = random.randint(0, SIZE)
        y = random.randint(0, SIZE)
        draw.line((x, y, x + random.randint(-3, 3), y - random.randint(2, 8)), fill=jitter((57, 110, 49), 35))
    save(img, "Grass_Texture")


def dirt():
    random.seed("dirt")
    img = Image.new("RGB", (SIZE, SIZE), (82, 58, 34))
    draw = ImageDraw.Draw(img)
    for _ in range(1800):
        x = random.randint(0, SIZE)
        y = random.randint(0, SIZE)
        r = random.randint(1, 4)
        draw.ellipse((x - r, y - r, x + r, y + r), fill=jitter((91, 65, 39), 35))
    save(img.filter(ImageFilter.GaussianBlur(0.15)), "Dirt_Texture")


if __name__ == "__main__":
    stone("Stone_Texture", (96, 96, 90), (42, 42, 39), 90, 55)
    stone("StoneTrim_Texture", (128, 126, 114), (55, 53, 49), 82, 50)
    stone("StoneInterior_Texture", (78, 78, 75), (36, 36, 35), 76, 48)
    stone("StoneFloor_Texture", (83, 81, 74), (38, 37, 35), 66, 42)
    cobble()
    wood("Wood_Texture", (111, 69, 34), 72)
    wood("WoodDark_Texture", (69, 36, 17), 58)
    iron()
    roof()
    thatch()
    water()
    fire()
    cloth("ClothRed_Texture", (116, 16, 16))
    cloth("ClothTan_Texture", (140, 116, 78))
    grass()
    dirt()
    print(f"Generated textures in {OUT_DIR}")
