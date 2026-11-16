#!/usr/bin/env python3
"""Builds About/Preview.png (the Workshop preview) from the mod's own textures: a small Beamwire network and the
mining laser, laser cannon and laser control console, each with its beams.
Run from the repo root after the art generators:  python3 Tools/make_preview.py   (needs Pillow)
"""
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEX = os.path.join(ROOT, "Common", "Textures")
W, H = 640, 360
BG, FG, SUB = (24, 24, 26), (240, 240, 235), (175, 180, 185)


def font(size, bold=False):
    for name in (("DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"), "Arial.ttf"):
        for folder in ("/usr/share/fonts/truetype/dejavu", "/Library/Fonts", "C:/Windows/Fonts"):
            path = os.path.join(folder, name)
            if os.path.exists(path):
                return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def tex(*parts):
    return Image.open(os.path.join(TEX, *parts) + ".png").convert("RGBA")


def turret(name, angle, size):
    """A laser building: base plate with its head turned by angle (degrees clockwise)."""
    img = tex("JarimichusLasers", "Styles", "Standard", "Things", f"{name}_Base").resize((size, size), Image.LANCZOS)
    top = tex("JarimichusLasers", "Styles", "Standard", "Things", f"{name}_Top").resize((size, size), Image.LANCZOS)
    img.alpha_composite(top.rotate(-angle, resample=Image.BICUBIC))
    return img


def node(name, size, angle=0):
    img = tex("Beamwire", "Styles", "Standard", "Things", f"{name}_Base").resize((size, size), Image.LANCZOS)
    top = tex("Beamwire", "Styles", "Standard", "Things", f"{name}_Top").resize((size, size), Image.LANCZOS)
    img.alpha_composite(top.rotate(-angle, resample=Image.BICUBIC))
    return img


def beam(layer, a, b, colour, width=3, glow=7):
    g = Image.new("RGBA", layer.size, (0, 0, 0, 0)); gd = ImageDraw.Draw(g)
    gd.line([a, b], fill=colour + (200,), width=glow)
    layer.alpha_composite(g.filter(ImageFilter.GaussianBlur(4)))
    ImageDraw.Draw(layer).line([a, b], fill=tuple(min(255, c + 90) for c in colour) + (255,), width=width)


def panel(img, box, title, caption):
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(box, radius=10, fill=(36, 37, 40), outline=(58, 60, 64), width=2)
    x0, y0, x1, y1 = box
    d.text((x0 + 10, y1 - 38), title, font=font(13, True), fill=FG)
    d.text((x0 + 10, y1 - 20), caption, font=font(10), fill=SUB)


def main():
    img = Image.new("RGBA", (W, H), BG)
    d = ImageDraw.Draw(img)
    d.text((20, 16), "JARIMICHUS LASERS", font=font(26, True), fill=FG)
    d.text((22, 50), "Wireless laser power, a mining laser, a laser cannon and a console to crew them",
           font=font(12), fill=SUB)

    # Beamwire: emitter -> mirror -> receiver, with a splitter branch
    panel(img, (16, 80, 316, 344), "Beamwire", "wireless power over laser beams")
    fx = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    s = 56
    e, m, sp, r1, r2 = (40, 230), (130, 230), (220, 230), (220, 110), (130, 110)
    yellow = (255, 200, 90)
    for a, b in ((e, m), (m, sp), (sp, r1)):
        beam(fx, (a[0] + s // 2, a[1] + s // 2), (b[0] + s // 2, b[1] + s // 2), yellow)
    img.alpha_composite(fx)
    img.alpha_composite(node("Emitter", s, 90), e)
    img.alpha_composite(node("Splitter", s, 0), m)
    img.alpha_composite(node("Mirror", s, 315), sp)
    img.alpha_composite(node("Receiver", s), r1)
    beam(img, (m[0] + s // 2, m[1] + s // 2), (r2[0] + s // 2, r2[1] + s // 2), yellow)
    img.alpha_composite(node("Receiver", s), r2)

    # The three laser buildings
    tiles = [
        ("MiningLaser", "Mining laser", "cuts marked rock, full yield", (255, 110, 10), -14),
        ("LaserCannon", "Laser cannon", "burns hostiles, 18-27 DPS", (255, 30, 25), 16),
        ("LaserConsole", "Console", "one colonist, 3 lasers", None, 0),
    ]
    for i, (name, title, caption, colour, angle) in enumerate(tiles):
        x0 = 328 + i * 100
        panel(img, (x0, 80, x0 + 92, 344), "", "")
        cx, cy = x0 + 46, 240
        size = 80
        if colour:
            fx = Image.new("RGBA", (W, H), (0, 0, 0, 0))
            import math
            rad = math.radians(angle)
            for off in ((-5, 0, 5) if name == "MiningLaser" else (-4, 4)):
                sx = cx + off * math.cos(rad)
                sy = cy + off * math.sin(rad)
                beam(fx, (sx, sy), (sx + 150 * math.sin(rad), sy - 150 * math.cos(rad)), colour, width=2, glow=5)
            clip = Image.new("L", (W, H), 0)
            ImageDraw.Draw(clip).rounded_rectangle((x0 + 2, 82, x0 + 90, 342), radius=9, fill=255)
            fx.putalpha(Image.composite(fx.getchannel("A"), Image.new("L", (W, H), 0), clip))
            img.alpha_composite(fx)
            sprite = turret(name, angle, size)
        else:
            sprite = tex("JarimichusLasers", "Styles", "Standard", "Things", "LaserConsole_Base").resize((size, size), Image.LANCZOS)
        img.alpha_composite(sprite, (cx - size // 2, cy - size // 2))
        d = ImageDraw.Draw(img)
        d.text((x0 + 8, 298), title, font=font(11, True), fill=FG)
        y = 314
        for line in caption.replace(", ", ",\n").split("\n"):
            d.text((x0 + 8, y), line, font=font(9), fill=SUB); y += 12
    img.convert("RGB").save(os.path.join(ROOT, "About", "Preview.png"))
    print("About/Preview.png written.")


if __name__ == "__main__":
    main()
