#!/usr/bin/env python3
"""Generates JarimichusLaserCannon's art in the three Jarimichus styles (shared look with Jarimichus Beamwire).

Run from the repo root:  python3 Tools/generate_art.py   (needs Pillow)
"""
import math
import os
import struct
import wave

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEX = os.path.join(ROOT, "Common", "Textures", "JarimichusLaserCannon")
S = 256          # working resolution
FINAL = 128      # saved resolution
NODE = "LaserCannon"
NODES = [NODE]

# Folder name -> style. The folder names must match BeamwireStyles.All in Source/Beamwire/BeamwireStyles.cs.
STYLES = {
  "Standard": dict(
    beam=(255, 214, 120), outline=(28, 26, 24), ow=10, radius=18,
    plate=(118, 112, 104), plate_hi=(146, 140, 131), plate_lo=(92, 87, 81),
    metal=(160, 156, 148), metal_dark=(84, 80, 75), glass=(255, 214, 120), mirror=(214, 222, 228),
    accents={k: (200, 90, 60) for k in NODES},
    deco="bolts", ground="dirt"),
  "Industrial": dict(
    beam=(255, 150, 40), outline=(22, 20, 18), ow=8, radius=6,
    plate=(84, 86, 84), plate_hi=(112, 114, 110), plate_lo=(60, 61, 60),
    metal=(140, 138, 128), metal_dark=(70, 68, 62), glass=(255, 160, 50), mirror=(200, 206, 210),
    accents={k: (236, 186, 30) for k in NODES},
    deco="hazard", ground="concrete"),
  "Spacer": dict(
    beam=(90, 240, 220), outline=(70, 86, 96), ow=5, radius=40,
    plate=(222, 228, 232), plate_hi=(246, 249, 250), plate_lo=(184, 193, 200),
    metal=(236, 240, 243), metal_dark=(150, 162, 172), glass=(90, 240, 220), mirror=(200, 255, 250),
    accents={k: (90, 240, 220) for k in NODES},
    deco="strips", ground="tile"),
}


def save(img, *parts):
    path = os.path.join(TEX, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)


def rgba(c, a=255): return tuple(c[:3]) + (a,)
def blank(sz=S): return Image.new("RGBA", (sz, sz), (0, 0, 0, 0))
def down(img): return img.resize((FINAL, FINAL), Image.LANCZOS)

def glow_layer(img, radius):
    out = blank(img.size[0]); out.alpha_composite(img.filter(ImageFilter.GaussianBlur(radius))); out.alpha_composite(img); return out

def shape(d, box, st, fill, outline=True, chamfer=False):
    x0, y0, x1, y1 = box
    ow = st["ow"] if outline else 0
    if chamfer or st["radius"] == 0:
        c = 22
        pts = [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c), (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]
        d.polygon(pts, fill=rgba(fill), outline=rgba(st["outline"]) if ow else None, width=ow)
    else:
        d.rounded_rectangle(box, radius=st["radius"], fill=rgba(fill), outline=rgba(st["outline"]) if ow else None, width=ow)

# ---------------------------------------------------------------- base plates
def base(st, kind):
    img = blank(); d = ImageDraw.Draw(img)
    chamfer = st["deco"] == "vents"
    shape(d, (14, 14, S - 14, S - 14), st, st["plate"], chamfer=chamfer)
    # soft top highlight / bottom shade bands
    if st["deco"] == "strips":
        grad = blank(); gd = ImageDraw.Draw(grad)
        for i in range(80):
            gd.line([(30, 24 + i), (S - 30, 24 + i)], fill=rgba(st["plate_hi"], int(160 * (1 - i / 80))))
        mask = blank(); ImageDraw.Draw(mask).rounded_rectangle((20, 20, S - 20, S - 20), radius=st["radius"] - 6, fill=(255, 255, 255, 255))
        img.alpha_composite(Image.composite(grad, blank(), mask.split()[3]))
    else:
        d.line([(34, 30), (S - 34, 30)], fill=rgba(st["plate_hi"]), width=8)
        d.line([(34, S - 30), (S - 34, S - 30)], fill=rgba(st["plate_lo"]), width=8)
    acc = st["accents"][kind]
    deco = st["deco"]
    if deco == "bolts":
        for x, y in [(40, 40), (S - 40, 40), (40, S - 40), (S - 40, S - 40)]:
            d.ellipse([x - 9, y - 9, x + 9, y + 9], fill=rgba(st["metal"]), outline=rgba(st["outline"]), width=4)
        d.rectangle([S // 2 - 40, S - 46, S // 2 + 40, S - 34], fill=rgba(acc), outline=rgba(st["outline"]), width=3)
    elif deco == "hazard":
        stripe = blank(); sd = ImageDraw.Draw(stripe)
        for i in range(-S, S * 2, 36):
            sd.polygon([(i, 0), (i + 18, 0), (i + 18 - S, S), (i - S, S)], fill=rgba(acc))
        band = blank(); bd = ImageDraw.Draw(band)
        bd.rectangle([22, 22, S - 22, 46], fill=(255, 255, 255, 255)); bd.rectangle([22, S - 46, S - 22, S - 22], fill=(255, 255, 255, 255))
        dark = blank(); ImageDraw.Draw(dark).rectangle([22, 22, S - 22, S - 22], fill=rgba((30, 28, 24)))
        img.alpha_composite(Image.composite(dark, blank(), band.split()[3]))
        img.alpha_composite(Image.composite(stripe, blank(), band.split()[3]))
        for x in range(36, S - 30, 46):
            for y in (60, S - 60):
                d.ellipse([x - 6, y - 6, x + 6, y + 6], fill=rgba(st["metal"]), outline=rgba(st["outline"]), width=2)
        lamp = blank(); ImageDraw.Draw(lamp).ellipse([S - 58, 54, S - 38, 74], fill=rgba(st["glass"]))
        img.alpha_composite(glow_layer(lamp, 6))
    elif deco == "strips":
        strip = blank(); ImageDraw.Draw(strip).rounded_rectangle([S // 2 - 50, S - 42, S // 2 + 50, S - 34], radius=4, fill=rgba(acc))
        img.alpha_composite(glow_layer(strip, 8))
    elif deco == "engrave":
        d.rectangle([28, 28, S - 28, S - 28], outline=rgba(st["metal_dark"]), width=4)
        d.rectangle([38, 38, S - 38, S - 38], outline=rgba(st["metal"]), width=3)
        for x, y in [(28, 28), (S - 28, 28), (28, S - 28), (S - 28, S - 28)]:
            d.polygon([(x, y - 12), (x + 12, y), (x, y + 12), (x - 12, y)], fill=rgba(st["glass"]), outline=rgba(st["outline"]))
        # simple glyph band
        for i, x in enumerate(range(60, S - 50, 28)):
            g = i % 3
            if g == 0: d.ellipse([x, S - 52, x + 14, S - 38], outline=rgba(st["plate_lo"]), width=3)
            elif g == 1: d.line([(x + 7, S - 54), (x + 7, S - 36)], fill=rgba(st["plate_lo"]), width=3); d.line([(x, S - 48), (x + 14, S - 48)], fill=rgba(st["plate_lo"]), width=3)
            else: d.polygon([(x, S - 38), (x + 7, S - 54), (x + 14, S - 38)], outline=rgba(st["plate_lo"]))
    elif deco == "vents":
        for y in range(40, 72, 10):
            d.line([(40, y), (90, y)], fill=rgba(st["plate_lo"]), width=4)
            d.line([(S - 90, y), (S - 40, y)], fill=rgba(st["plate_lo"]), width=4)
        d.line([(30, S // 2), (60, S // 2)], fill=rgba(st["metal_dark"]), width=5)
        d.line([(S - 60, S // 2), (S - 30, S // 2)], fill=rgba(st["metal_dark"]), width=5)
        eye = blank(); ImageDraw.Draw(eye).rectangle([S // 2 - 30, S - 44, S // 2 + 30, S - 36], fill=rgba(acc))
        img.alpha_composite(glow_layer(eye, 6))
    return img

# ---------------------------------------------------------------- beam textures (neutral; tinted per style in game)
def beam_core():
    """Thin, Portal-style laser: opaque white centre with soft edges. Drawn very narrow in game."""
    w, h = 32, 64
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for x in range(w):
        t = abs((x + 0.5) / w - 0.5) * 2
        a = max(0.0, 1 - t ** 2.2)
        for y in range(h):
            px[x, y] = (255, 255, 255, int(255 * a))
    return img


def beam_glow():
    """Wide, faint additive halo; tinted with the style's beam colour in game."""
    w, h = 64, 64
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for x in range(w):
        t = ((x + 0.5) / w - 0.5) * 2
        v = math.exp(-(t * 2.2) ** 2)
        for y in range(h):
            px[x, y] = (int(255 * v),) * 4
    return img



def top(st):
    """Heavy trigun turret head: three long barrels with red lenses, an armoured shroud and a targeting sensor."""
    img = blank(); d = ImageDraw.Draw(img); o = rgba(st["outline"]); ow = max(3, st["ow"] - 2)
    sharp = st["deco"] in ("vents", "engrave", "hazard")
    r = 6 if sharp else 20
    # armoured shroud
    d.polygon([(60, 230), (196, 230), (214, 150), (176, 104), (80, 104), (42, 150)], fill=rgba(st["metal"]), outline=o)
    d.line([(60, 230), (196, 230), (214, 150), (176, 104), (80, 104), (42, 150), (60, 230)], fill=o, width=ow)
    d.rounded_rectangle([86, 140, 170, 210], radius=r, fill=rgba(st["metal_dark"]), outline=o, width=3)
    for y in range(152, 206, 12):
        d.line([(96, y), (160, y)], fill=rgba(st["plate_lo"]), width=4)
    # long barrels
    for bx in (92, 128, 164):
        d.rectangle([bx - 10, 8, bx + 10, 112], fill=rgba(st["metal"]), outline=o, width=max(3, ow - 3))
        for y in (36, 64):
            d.rectangle([bx - 13, y, bx + 13, y + 8], fill=rgba(st["metal_dark"]), outline=o, width=2)
        heat = blank(); ImageDraw.Draw(heat).ellipse([bx - 11, -4, bx + 11, 18], fill=(255, 60, 40, 255))
        img.alpha_composite(glow_layer(heat, 6))
        d.ellipse([bx - 5, 2, bx + 5, 12], fill=(255, 225, 215, 255))
    # targeting sensor
    sensor = blank(); ImageDraw.Draw(sensor).ellipse([196, 118, 222, 144], fill=(255, 80, 60, 255))
    img.alpha_composite(glow_layer(sensor, 5))
    d.ellipse([194, 116, 224, 146], outline=o, width=3)
    if st["deco"] == "hazard":
        d.rectangle([60, 214, 196, 226], fill=rgba(st["accents"][NODE]), outline=o, width=2)
    elif st["deco"] == "strips":
        strip = blank(); ImageDraw.Draw(strip).rounded_rectangle([98, 216, 158, 224], radius=3, fill=rgba(st["glass"]))
        img.alpha_composite(glow_layer(strip, 5))
    return img


def buzz(path, seconds=2.0, rate=22050):
    """A crackling energy buzz that loops seamlessly: harmonics of 50 Hz with a slow beat."""
    n = int(seconds * rate)
    partials = [(100.0, 0.35), (150.0, 0.25), (300.0, 0.12), (650.0, 0.06), (1250.0, 0.03)]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as wf:
        wf.setnchannels(1); wf.setsampwidth(2); wf.setframerate(rate)
        frames = bytearray()
        for i in range(n):
            t = i / rate
            beat = 1 + 0.25 * math.sin(2 * math.pi * 6.0 * t)
            v = sum(a * math.sin(2 * math.pi * f * t) for f, a in partials) * beat
            frames += struct.pack("<h", int(max(-1, min(1, v * 0.7)) * 32767))
        wf.writeframes(bytes(frames))


def preview():
    w, h = 640, 360
    img = Image.new("RGBA", (w, h), (30, 28, 26, 255))
    d = ImageDraw.Draw(img)
    for i, (name, st) in enumerate(STYLES.items()):
        t = base(st, NODE); t.alpha_composite(top(st).rotate(-20, resample=Image.BICUBIC))
        img.alpha_composite(t.resize((150, 150), Image.LANCZOS), (40 + i * 200, 150))
    beams = Image.new("RGBA", (w, h), (0, 0, 0, 0)); bd = ImageDraw.Draw(beams)
    for dx in (-8, 0, 8):
        bd.line([(130 + dx, 180), (230 + dx, 50)], fill=(255, 60, 40, 255), width=4)
    img.alpha_composite(beams.filter(ImageFilter.GaussianBlur(4)))
    d.text((20, 20), "JARIMICHUS LASER CANNON", fill=(240, 240, 235, 255))
    d.text((20, 38), "auto or manned - burns hostiles, never fires through friendlies", fill=(255, 120, 100, 255))
    return img.convert("RGB")


def main():
    for folder, st in STYLES.items():
        save(down(base(st, NODE)), "Styles", folder, "Things", f"{NODE}_Base.png")
        save(down(top(st)), "Styles", folder, "Things", f"{NODE}_Top.png")
        i = base(st, NODE); i.alpha_composite(top(st))
        save(down(i), "Styles", folder, "Things", f"{NODE}_Icon.png")
    save(beam_core(), "Effects", "BeamCore.png")
    save(beam_glow(), "Effects", "BeamGlow.png")
    preview().save(os.path.join(ROOT, "About", "Preview.png"))
    buzz(os.path.join(ROOT, "Common", "Sounds", "JarimichusLaserCannon", "LaserBuzz.wav"))
    print("Art and sound generated.")


if __name__ == "__main__":
    main()
