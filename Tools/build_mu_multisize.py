"""Extract the native pixel-size alphabets from Abecedario mu onine 2.0."""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "_Recovery" / "Abecedario mu onine 2.0.png"
OUTPUT = ROOT / "Assets" / "_RPG" / "Resources" / "UI" / "MuFont"

# size: (panel left, panel right, [(top, bottom) for A-M, N-Z, 0-9])
PANELS = {
    64: (29, 681, [(79, 140), (146, 211), (217, 276)]),
    48: (710, 1192, [(80, 139), (143, 199), (201, 247)]),
    36: (31, 306, [(352, 380), (381, 408), (409, 434)]),
    32: (366, 628, [(361, 383), (384, 408), (409, 434)]),
    28: (671, 908, [(361, 383), (384, 408), (409, 434)]),
    24: (950, 1190, [(361, 383), (384, 408), (409, 434)]),
    20: (32, 255, [(500, 522), (522, 541), (541, 562)]),
    18: (365, 600, [(500, 522), (522, 541), (541, 562)]),
    16: (671, 910, [(500, 522), (522, 541), (541, 562)]),
    14: (951, 1191, [(500, 522), (522, 541), (541, 562)]),
    12: (43, 270, [(599, 618), (618, 635), (635, 652)]),
    10: (375, 610, [(599, 618), (618, 635), (635, 652)]),
    8:  (671, 903, [(599, 618), (618, 635), (635, 652)]),
    6:  (953, 1187, [(599, 618), (618, 635), (635, 652)]),
    4:  (48, 245, [(679, 695), (695, 710), (710, 726)]),
    3:  (378, 590, [(679, 695), (695, 710), (710, 726)]),
    2:  (673, 881, [(679, 695), (695, 710), (710, 726)]),
    1:  (958, 1160, [(679, 695), (695, 710), (710, 726)]),
}

# Exact authored horizontal bounds in the clean 64 px master. The source does
# not use a uniform grid after G, so equal slicing clipped H and shifted I-M.
GLYPH_BOUNDS_64 = [
    [(35, 75), (89, 120), (138, 171), (191, 228), (248, 278), (302, 329), (349, 385),
     (404, 441), (459, 474), (491, 506), (523, 560), (576, 604), (620, 662)],
    [(36, 75), (92, 130), (148, 178), (195, 236), (250, 287), (302, 328), (343, 375),
     (390, 428), (441, 481), (490, 548), (557, 595), (603, 640), (649, 679)],
    [(37, 66), (92, 108), (135, 162), (189, 214), (240, 269), (295, 320), (346, 374),
     (402, 427), (454, 480), (512, 539)],
]

source = Image.open(SOURCE).convert("RGB")
OUTPUT.mkdir(parents=True, exist_ok=True)

for size, (left, right, rows) in PANELS.items():
    cell_width = 58 if size == 64 else (right - left) / 13.0
    row_height = size
    atlas = Image.new("RGBA", (round(cell_width * 13), row_height * 3), (0, 0, 0, 0))

    for row_index, (top, bottom) in enumerate(rows):
        # The numeric row has 0..9 in its first ten cells; the final combined
        # "10" sample is intentionally not needed.
        count = 13 if row_index < 2 else 10
        for column in range(count):
            if size == 64:
                x0, x1 = GLYPH_BOUNDS_64[row_index][column]
            else:
                x0 = round(left + column * cell_width)
                x1 = round(left + (column + 1) * cell_width)
            crop = source.crop((x0, top, x1, bottom))
            if crop.height != row_height:
                crop = crop.resize((crop.width, row_height), Image.Resampling.LANCZOS)
            rgba = Image.new("RGBA", crop.size)
            src, dst = crop.load(), rgba.load()
            for y in range(crop.height):
                for x in range(crop.width):
                    r, g, b = src[x, y]
                    # Remove near-black PNG background while retaining the gold
                    # antialiasing authored separately at every native size.
                    brightness = max(r, g, b)
                    # The supplied small samples are already faint. A soft matte
                    # makes them disappear in-game, so every authored gold pixel
                    # is kept fully opaque while the dark panel stays transparent.
                    is_gold = brightness >= 28 and (r - b) >= 7 and r >= g
                    alpha = 255 if is_gold else 0
                    dst[x, y] = (r, g, b, alpha)
            # Panel separators sit on cell edges; they are not part of glyphs.
            edge = min(2, max(0, min(rgba.width, rgba.height) // 4))
            for y in range(rgba.height):
                for x in range(rgba.width):
                    if x < edge or x >= rgba.width - edge or y < edge or y >= rgba.height - edge:
                        r, g, b, _ = dst[x, y]
                        dst[x, y] = (r, g, b, 0)
            alpha_box = rgba.getchannel("A").getbbox()
            if alpha_box:
                if size == 64:
                    # The master row was authored at one consistent cap height.
                    # Never normalize its glyphs independently: doing so made
                    # narrow / multipart characters appear at different sizes.
                    destination_width = round((column + 1) * cell_width) - round(column * cell_width)
                    paste_x = round(column * cell_width) + (destination_width - rgba.width) // 2
                    atlas.alpha_composite(rgba, (paste_x, row_index * row_height))
                    continue
                glyph = rgba.crop(alpha_box)
                destination_width = round((column + 1) * cell_width) - round(column * cell_width)
                target_height = max(1, round(size * 0.84))
                scale = min(target_height / glyph.height, max(1, destination_width - 2) / glyph.width)
                new_size = (max(1, round(glyph.width * scale)), max(1, round(glyph.height * scale)))
                glyph = glyph.resize(new_size, Image.Resampling.LANCZOS)
                # Resampling is used only for shape quality; restore fully opaque
                # authored pixels afterwards as requested.
                alpha = glyph.getchannel("A").point(lambda value: 255 if value >= 48 else 0)
                glyph.putalpha(alpha)
                paste_x = round(column * cell_width) + (destination_width - glyph.width) // 2
                paste_y = row_index * row_height + (row_height - glyph.height) // 2
                atlas.alpha_composite(glyph, (paste_x, paste_y))

    atlas.save(OUTPUT / f"MuFont{size}.png", optimize=True)
    print(size, atlas.size)
