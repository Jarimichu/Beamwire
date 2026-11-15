#!/usr/bin/env python3
"""Generates Beamwire's placeholder art and sound.

Run from the repo root:  python3 Tools/generate_art.py
Needs Pillow (pip install pillow). Overwrites files under Common/Textures, Common/Sounds and About/.
Replace the outputs with hand-drawn art whenever you like; the paths are what the Defs reference.
"""
import math
import os
import struct
import wave

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEX = os.path.join(ROOT, "Common", "Textures", "Beamwire")
CYAN = (90, 215, 255)
STEEL = (110, 116, 124)
STEEL_DARK = (62, 66, 72)
S = 128  # sprite resolution (drawn at 1x1 tile)


def save(img, *parts):
    path = os.path.join(TEX, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)


def blank(size=S):
    return Image.new("RGBA", (size, size), (0, 0, 0, 0))


def glow(img, radius=6):
    """Adds a soft halo behind the bright parts of img."""
    halo = img.filter(ImageFilter.GaussianBlur(radius))
    out = blank(img.size[0])
    out.alpha_composite(halo)
    out.alpha_composite(img)
    return out


# ---------------------------------------------------------------- base plates
def base_plate(accent):
    img = blank()
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([10, 10, S - 10, S - 10], radius=14, fill=STEEL_DARK + (255,), outline=(30, 32, 36, 255), width=4)
    d.rounded_rectangle([20, 20, S - 20, S - 20], radius=10, outline=STEEL + (255,), width=3)
    for x, y in [(22, 22), (S - 22, 22), (22, S - 22), (S - 22, S - 22)]:
        d.ellipse([x - 5, y - 5, x + 5, y + 5], fill=(150, 156, 164, 255))
    d.rectangle([S // 2 - 22, S - 18, S // 2 + 22, S - 13], fill=accent + (255,))
    return img


# ---------------------------------------------------------------- rotating tops (drawn pointing North)
def top_emitter():
    img = blank()
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([42, 34, 86, 100], radius=10, fill=(150, 156, 164, 255), outline=(40, 42, 46, 255), width=4)
    d.rectangle([52, 14, 76, 40], fill=(90, 96, 104, 255), outline=(40, 42, 46, 255), width=3)
    lens = blank()
    ImageDraw.Draw(lens).ellipse([54, 8, 74, 26], fill=CYAN + (255,))
    img = glow(img)
    img.alpha_composite(glow(lens, 4))
    return img


def top_receiver():
    img = blank()
    d = ImageDraw.Draw(img)
    d.ellipse([22, 22, S - 22, S - 22], fill=(170, 176, 184, 255), outline=(40, 42, 46, 255), width=4)
    d.ellipse([36, 36, S - 36, S - 36], fill=(120, 126, 134, 255))
    core = blank()
    ImageDraw.Draw(core).ellipse([52, 52, S - 52, S - 52], fill=CYAN + (255,))
    img.alpha_composite(glow(core, 5))
    return img


def top_mirror():
    # A flat pane lying East-West. Its reflective face (the normal) points North, toward the top of the sprite.
    img = blank()
    d = ImageDraw.Draw(img)
    d.rectangle([14, 62, S - 14, 76], fill=(40, 42, 46, 255))          # dark backing (south side)
    d.rectangle([14, 52, S - 14, 63], fill=(225, 235, 245, 255))       # silvered face (north side)
    d.line([18, 54, S - 18, 54], fill=(255, 255, 255, 255), width=2)
    d.rectangle([56, 76, 72, 96], fill=(90, 96, 104, 255))             # mount
    return img


def top_splitter():
    img = blank()
    d = ImageDraw.Draw(img)
    pane = blank()
    ImageDraw.Draw(pane).rectangle([14, 56, S - 14, 72], fill=CYAN + (150,))
    img.alpha_composite(glow(pane, 4))
    d.line([14, 56, S - 14, 56], fill=(235, 250, 255, 255), width=3)
    d.line([14, 72, S - 14, 72], fill=(235, 250, 255, 255), width=3)
    d.rectangle([10, 52, 18, 76], fill=(90, 96, 104, 255))
    d.rectangle([S - 18, 52, S - 10, 76], fill=(90, 96, 104, 255))
    return img


def top_relay():
    img = blank()
    d = ImageDraw.Draw(img)
    d.polygon([(64, 18), (104, 64), (64, 110), (24, 64)], fill=(150, 156, 164, 255), outline=(40, 42, 46, 255))
    d.polygon([(64, 30), (92, 64), (64, 98), (36, 64)], fill=(100, 106, 114, 255))
    lens = blank()
    ld = ImageDraw.Draw(lens)
    ld.ellipse([56, 10, 72, 26], fill=CYAN + (255,))
    ld.ellipse([58, 56, 70, 68], fill=CYAN + (200,))
    img.alpha_composite(glow(lens, 4))
    return img


# ---------------------------------------------------------------- beam textures
def beam_core():
    """Thin, Portal-style laser: white-hot centre with cyan-tinted soft edges. Drawn very narrow in game."""
    w, h = 32, 64
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for x in range(w):
        t = abs((x + 0.5) / w - 0.5) * 2  # 0 at centre, 1 at edge
        a = max(0.0, 1 - t ** 2.2)
        mix = min(1.0, t * 1.6)
        r = int(255 * (1 - mix) + CYAN[0] * mix)
        g = int(255 * (1 - mix) + CYAN[1] * mix)
        b = int(255 * (1 - mix) + CYAN[2] * mix)
        for y in range(h):
            px[x, y] = (r, g, b, int(255 * a))
    return img


def beam_glow():
    """Wide, faint additive halo; tinted cyan by the material colour at runtime."""
    w, h = 64, 64
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for x in range(w):
        t = ((x + 0.5) / w - 0.5) * 2
        v = math.exp(-(t * 2.2) ** 2)
        for y in range(h):
            px[x, y] = (int(255 * v), int(255 * v), int(255 * v), int(255 * v))
    return img


# ---------------------------------------------------------------- UI icons
def icon_show_beams():
    img = blank()
    beam = blank()
    bd = ImageDraw.Draw(beam)
    bd.line([16, 100, 64, 52], fill=CYAN + (255,), width=6)
    bd.line([64, 52, 112, 100], fill=CYAN + (255,), width=6)
    img.alpha_composite(glow(beam, 6))
    d = ImageDraw.Draw(img)
    d.line([16, 100, 64, 52], fill=(255, 255, 255, 255), width=2)
    d.line([64, 52, 112, 100], fill=(255, 255, 255, 255), width=2)
    d.polygon([(44, 40), (84, 40), (64, 60)], fill=(220, 230, 240, 255))
    return img


def icon_throughput():
    img = blank()
    d = ImageDraw.Draw(img)
    for i, hgt in enumerate([24, 44, 64, 88]):
        x = 18 + i * 26
        d.rectangle([x, 108 - hgt, x + 18, 108], fill=CYAN + (255,) if i < 3 else (220, 230, 240, 255))
    return glow(img, 3)


# ---------------------------------------------------------------- preview image
def preview():
    w, h = 640, 360
    img = Image.new("RGBA", (w, h), (34, 30, 26, 255))
    d = ImageDraw.Draw(img)
    for x in range(0, w, 40):
        d.line([x, 0, x, h], fill=(44, 40, 36, 255))
    for y in range(0, h, 40):
        d.line([0, y, w, y], fill=(44, 40, 36, 255))
    beams = blank(w)
    beams = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    bd = ImageDraw.Draw(beams)
    path = [(100, 260), (300, 260), (420, 140), (580, 140)]
    branch = [(300, 260), (300, 60)]
    for seg in (path, branch):
        bd.line(seg, fill=CYAN + (255,), width=8)
    img.alpha_composite(beams.filter(ImageFilter.GaussianBlur(7)))
    for seg in (path, branch):
        d.line(seg, fill=(255, 255, 255, 255), width=2)
    sprite = lambda top, angle, pos: img.alpha_composite(
        Image.alpha_composite(base_plate(CYAN), top.rotate(-angle)).resize((64, 64)), (pos[0] - 32, pos[1] - 32))
    sprite(top_emitter(), 90, (100, 260))
    sprite(top_splitter(), 135, (300, 260))
    sprite(top_mirror(), 292.5, (420, 260 - 120))
    sprite(top_receiver(), 0, (580, 140))
    sprite(top_receiver(), 0, (300, 60))
    d.text((24, 24), "BEAMWIRE", fill=(235, 245, 255, 255))
    d.text((24, 40), "wireless laser power", fill=CYAN + (255,))
    return img.convert("RGB")


# ---------------------------------------------------------------- sound
def hum(path, seconds=2.0, rate=22050):
    """A soft electric hum that loops seamlessly (every frequency completes whole cycles in the loop)."""
    n = int(seconds * rate)
    partials = [(60.0, 0.5), (120.0, 0.3), (180.0, 0.12), (240.0, 0.06), (1500.0, 0.015)]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(rate)
        frames = bytearray()
        for i in range(n):
            t = i / rate
            wobble = 1 + 0.15 * math.sin(2 * math.pi * 1.5 * t)
            v = sum(a * math.sin(2 * math.pi * f * t) for f, a in partials) * wobble
            frames += struct.pack("<h", int(max(-1, min(1, v * 0.6)) * 32767))
        wf.writeframes(bytes(frames))


def main():
    accents = {
        "Emitter": (255, 170, 60),
        "Receiver": (120, 220, 120),
        "Mirror": (200, 200, 210),
        "Splitter": CYAN,
        "Relay": (200, 140, 255),
    }
    tops = {
        "Emitter": top_emitter,
        "Receiver": top_receiver,
        "Mirror": top_mirror,
        "Splitter": top_splitter,
        "Relay": top_relay,
    }
    for name, accent in accents.items():
        save(base_plate(accent), "Things", f"{name}_Base.png")
        save(tops[name](), "Things", f"{name}_Top.png")
        # Combined icon for the architect menu and the minified item.
        icon = base_plate(accent)
        icon.alpha_composite(tops[name]())
        save(icon, "Things", f"{name}_Icon.png")
    save(beam_core(), "Effects", "BeamCore.png")
    save(beam_glow(), "Effects", "BeamGlow.png")
    save(icon_show_beams(), "UI", "ShowBeams.png")
    save(icon_throughput(), "UI", "Throughput.png")
    preview().save(os.path.join(ROOT, "About", "Preview.png"))
    hum(os.path.join(ROOT, "Common", "Sounds", "Beamwire", "EmitterHum.wav"))
    print("Art and sound generated.")


if __name__ == "__main__":
    main()
