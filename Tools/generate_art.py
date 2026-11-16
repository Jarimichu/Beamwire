#!/usr/bin/env python3
"""Generates JarimichusMiningLaser's art in the three Jarimichus styles (shared look with Jarimichus Beamwire).

Run from the repo root:  python3 Tools/generate_art.py   (needs Pillow)
"""
import math
import os
import struct
import wave

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEX = os.path.join(ROOT, "Common", "Textures", "JarimichusMiningLaser")
S = 256          # working resolution
FINAL = 128      # saved resolution
NODE = "MiningLaser"
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
    """Trigun turret head: three short barrels with orange-hot lenses on a finned heat-sink block, pointing North."""
    img = blank(); d = ImageDraw.Draw(img); o = rgba(st["outline"]); ow = max(3, st["ow"] - 2)
    sharp = st["deco"] in ("vents", "engrave", "hazard")
    r = 8 if sharp else 22
    for y in range(150, 214, 14):
        d.rounded_rectangle([54, y, 202, y + 8], radius=3, fill=rgba(st["metal_dark"]), outline=o, width=2)
    d.rounded_rectangle([66, 112, 190, 220], radius=r, fill=rgba(st["metal"]), outline=o, width=ow)
    d.rounded_rectangle([58, 92, 198, 128], radius=min(r, 10), fill=rgba(st["metal_dark"]), outline=o, width=ow)
    for bx in (84, 128, 172):
        d.rectangle([bx - 11, 26, bx + 11, 100], fill=rgba(st["metal"]), outline=o, width=max(3, ow - 3))
        d.rectangle([bx - 14, 54, bx + 14, 64], fill=rgba(st["metal_dark"]), outline=o, width=2)
        heat = blank(); ImageDraw.Draw(heat).ellipse([bx - 12, 10, bx + 12, 34], fill=(255, 130, 30, 255))
        img.alpha_composite(glow_layer(heat, 6))
        d.ellipse([bx - 6, 16, bx + 6, 28], fill=(255, 232, 200, 255))
    if st["deco"] == "hazard":
        d.rectangle([66, 170, 190, 184], fill=rgba(st["accents"][NODE]), outline=o, width=3)
    elif st["deco"] == "strips":
        strip = blank(); ImageDraw.Draw(strip).rounded_rectangle([96, 176, 160, 184], radius=3, fill=rgba(st["glass"]))
        img.alpha_composite(glow_layer(strip, 5))
    elif st["deco"] == "bolts":
        for x in (80, 176):
            d.ellipse([x - 6, 196, x + 6, 208], fill=rgba(st["metal"]), outline=o, width=2)
    return img


def preview():
    w, h = 640, 360
    img = Image.new("RGBA", (w, h), (34, 30, 26, 255))
    d = ImageDraw.Draw(img)
    for i, (name, st) in enumerate(STYLES.items()):
        t = base(st, NODE); t.alpha_composite(top(st).rotate(-30, resample=Image.BICUBIC))
        img.alpha_composite(t.resize((150, 150), Image.LANCZOS), (40 + i * 200, 120))
    beams = Image.new("RGBA", (w, h), (0, 0, 0, 0)); bd = ImageDraw.Draw(beams)
    for k, dx in enumerate((-8, 0, 8)):
        bd.line([(150 + dx, 150), (300 + dx * 2, 40)], fill=(255, 130, 30, 255), width=4)
    img.alpha_composite(beams.filter(ImageFilter.GaussianBlur(4)))
    d.text((20, 20), "JARIMICHUS MINING LASER", fill=(240, 240, 235, 255))
    d.text((20, 38), "cuts marked rock at any angle - grid or beam powered", fill=(255, 170, 90, 255))
    return img.convert("RGB")



# ---------------------------------------------------------------- gravship hull-mount variant (Odyssey)
# One look in every art style: gunmetal hull plating like gravship substructure, so the turret blends into the deck.
SHIP = dict(
    beam=(255, 214, 120), outline=(18, 20, 24), ow=6, radius=4,
    plate=(72, 78, 86), plate_hi=(96, 102, 110), plate_lo=(52, 56, 64),
    metal=(108, 114, 124), metal_dark=(44, 48, 56), glass=(120, 170, 220), mirror=(200, 210, 220),
    accents={k: (120, 140, 170) for k in NODES}, deco="vents", ground="metal")


def ship_base():
    """A flush deck panel: full-bleed plating with seams and rivets, and a recessed turret well in the middle."""
    img = Image.new("RGBA", (S, S), rgba(SHIP["plate"]))
    d = ImageDraw.Draw(img)
    # panel seams at the tile edge so neighbouring deck tiles read as one surface
    d.rectangle([0, 0, S - 1, S - 1], outline=rgba(SHIP["plate_lo"]), width=6)
    d.line([(6, 8), (S - 7, 8)], fill=rgba(SHIP["plate_hi"]), width=3)
    for x, y in [(18, 18), (S - 18, 18), (18, S - 18), (S - 18, S - 18), (S // 2, 14), (S // 2, S - 14), (14, S // 2), (S - 14, S // 2)]:
        d.ellipse([x - 5, y - 5, x + 5, y + 5], fill=rgba(SHIP["metal"]), outline=rgba(SHIP["outline"]), width=2)
    # recessed well
    c = S // 2
    d.ellipse([c - 98, c - 98, c + 98, c + 98], fill=rgba(SHIP["plate_lo"]), outline=rgba(SHIP["outline"]), width=4)
    d.ellipse([c - 86, c - 86, c + 86, c + 86], fill=rgba(SHIP["metal_dark"]))
    for a in range(0, 360, 30):
        x1 = c + 90 * math.cos(math.radians(a)); y1 = c + 90 * math.sin(math.radians(a))
        x2 = c + 97 * math.cos(math.radians(a)); y2 = c + 97 * math.sin(math.radians(a))
        d.line([(x1, y1), (x2, y2)], fill=rgba(SHIP["plate_hi"]), width=3)
    return img


def ship_top():
    """The turret head sitting low in its well: a dark armoured collar with the head scaled down inside it."""
    img = blank(); d = ImageDraw.Draw(img); c = S // 2
    d.ellipse([c - 84, c - 84, c + 84, c + 84], fill=rgba(SHIP["metal_dark"]), outline=rgba(SHIP["outline"]), width=6)
    d.ellipse([c - 70, c - 70, c + 70, c + 70], outline=rgba(SHIP["plate_lo"]), width=4)
    head = top(SHIP)
    small = head.resize((int(S * 0.86), int(S * 0.86)), Image.LANCZOS)
    off = (S - small.size[0]) // 2
    img.alpha_composite(small, (off, off))
    return img


def ship_art():
    save(down(ship_base()), "Ship", f"{NODE}_ShipBase.png")
    save(down(ship_top()), "Ship", f"{NODE}_ShipTop.png")



# ---------------------------------------------------------------- laser control console
def console_art(st):
    """A military fire-control station built like an arcade cabinet, seen from above and in front: an armoured
    hood with warning lamps at the back (north), a green radar scope with red target marks, and an armoured
    control deck toward the operator (south) with a flight-stick grip, a guarded fire button and toggle switches."""
    img = blank()
    d = ImageDraw.Draw(img); o = rgba(st["outline"]); ow = max(3, st["ow"] - 2)
    r = 4 if st["deco"] in ("vents", "engrave", "hazard") else 10
    red = (255, 60, 45, 255); phosphor = (110, 255, 140, 255)
    metal, dark, lo, hi = rgba(st["metal"]), rgba(st["metal_dark"]), rgba(st["plate_lo"]), rgba(st["plate_hi"])
    # armoured cabinet with chamfered top corners and bolted side plates
    d.polygon([(58, 14), (S - 58, 14), (S - 44, 28), (S - 44, S - 14), (44, S - 14), (44, 28)], fill=metal, outline=o)
    d.line([(58, 14), (S - 58, 14), (S - 44, 28), (S - 44, S - 14), (44, S - 14), (44, 28), (58, 14)], fill=o, width=ow, joint="curve")
    for x0 in (50, S - 60):
        d.rectangle([x0, 64, x0 + 10, 152], fill=dark, outline=o, width=2)
        for y in (70, 108, 146):
            d.ellipse([x0 + 3, y - 2, x0 + 7, y + 2], fill=hi)
    # hood: an armoured visor over the scope, with amber and red status lamps instead of a lit marquee
    d.rectangle([58, 22, S - 58, 50], fill=dark, outline=o, width=3)
    for x in range(66, S - 66, 12):
        d.line([(x, 26), (x, 46)], fill=lo, width=3)  # vent slots
    lamps = blank(); ld = ImageDraw.Draw(lamps)
    for x, col in ((78, (255, 170, 40, 255)), (S - 78, red)):
        ld.ellipse([x - 7, 29, x + 7, 43], fill=col)
    img.alpha_composite(glow_layer(lamps, 5))
    d = ImageDraw.Draw(img)
    # radar scope: green phosphor, range rings, a sweep and red target marks
    d.rectangle([62, 58, S - 62, 152], fill=dark, outline=o, width=ow)
    d.rectangle([72, 66, S - 72, 144], fill=(10, 26, 16, 255))
    scr = blank(); sd = ImageDraw.Draw(scr); c = (S // 2, 105)
    for rr in (14, 28):
        sd.ellipse([c[0] - rr, c[1] - rr, c[0] + rr, c[1] + rr], outline=phosphor[:3] + (150,), width=2)
    sd.line([(c[0] - 36, c[1]), (c[0] + 36, c[1])], fill=phosphor[:3] + (120,), width=2)
    sd.line([(c[0], c[1] - 36), (c[0], c[1] + 36)], fill=phosphor[:3] + (120,), width=2)
    sd.polygon([c, (c[0] + 30, c[1] - 26), (c[0] + 36, c[1] - 12)], fill=phosphor[:3] + (110,))  # sweep
    for bx, by in ((150, 84), (98, 124)):
        sd.line([(bx - 6, by - 6), (bx + 6, by + 6)], fill=red, width=3)
        sd.line([(bx - 6, by + 6), (bx + 6, by - 6)], fill=red, width=3)
    img.alpha_composite(glow_layer(scr, 4))
    d = ImageDraw.Draw(img)
    # armoured control deck, bolted at the corners
    d.rectangle([30, 160, S - 30, 222], fill=lo, outline=o, width=ow)
    d.line([(36, 168), (S - 36, 168)], fill=hi, width=3)
    for x, y in ((38, 176), (S - 38, 176), (38, 214), (S - 38, 214)):
        d.ellipse([x - 3, y - 3, x + 3, y + 3], fill=hi, outline=o, width=1)
    # flight-stick grip: boot, shaft, black grip with a red thumb button and a trigger
    jx, jy = 80, 196
    d.ellipse([jx - 18, jy - 11, jx + 18, jy + 11], fill=(30, 30, 32, 255), outline=o, width=3)
    d.line([(jx, jy), (jx + 2, jy - 18)], fill=(55, 55, 58, 255), width=8)
    d.rounded_rectangle([jx - 10, jy - 40, jx + 12, jy - 12], radius=7, fill=(36, 38, 36, 255), outline=o, width=3)
    d.rectangle([jx - 4, jy - 18, jx + 6, jy - 13], fill=(70, 70, 70, 255))  # trigger
    thumb = blank(); ImageDraw.Draw(thumb).ellipse([jx - 3, jy - 36, jx + 5, jy - 28], fill=red)
    img.alpha_composite(glow_layer(thumb, 3))
    d = ImageDraw.Draw(img)
    # guarded fire button: red, under a flip-up cover in yellow/black
    fx, fy = 182, 194
    d.rectangle([fx - 20, fy - 18, fx + 20, fy + 18], fill=(230, 180, 30, 255), outline=o, width=3)
    for i in range(-20, 21, 10):
        d.line([(fx + i - 6, fy + 18), (fx + i + 6, fy - 18)], fill=(30, 30, 30, 255), width=3)
    d.rectangle([fx - 13, fy - 11, fx + 13, fy + 11], fill=(30, 30, 34, 255), outline=o, width=2)
    btn = blank(); ImageDraw.Draw(btn).ellipse([fx - 9, fy - 9, fx + 9, fy + 9], fill=red)
    img.alpha_composite(glow_layer(btn, 3))
    d = ImageDraw.Draw(img)
    # arming toggle switches
    for x in (122, 140):
        d.rectangle([x - 5, 182, x + 5, 208], fill=(30, 30, 34, 255), outline=o, width=2)
        d.rectangle([x - 2, 184, x + 2, 196], fill=(200, 200, 205, 255))
    # stencilled unit plate on the front instead of coin slots
    d.rectangle([S // 2 - 34, 228, S // 2 + 34, 240], fill=dark, outline=o, width=2)
    for i, x in enumerate(range(S // 2 - 28, S // 2 + 28, 10)):
        d.rectangle([x, 231, x + 6, 237], fill=hi if i % 3 else (230, 180, 30, 255))
    if st["deco"] == "hazard":
        for i in range(44, S - 44, 16):
            d.line([(i, S - 16), (i + 8, S - 24)], fill=rgba(st["accents"][NODE]), width=4)
    elif st["deco"] == "strips":
        strip = blank(); ImageDraw.Draw(strip).rectangle([60, 154, S - 60, 157], fill=rgba(st["glass"]))
        img.alpha_composite(glow_layer(strip, 5))
    return img


def console_files():
    for folder, st in STYLES.items():
        c = console_art(st)
        save(down(c), "Styles", folder, "Things", "LaserConsole_Base.png")
        save(down(c), "Styles", folder, "Things", "LaserConsole_Icon.png")
    save(down(console_art(SHIP)), "Ship", "LaserConsole_ShipBase.png")


def main():
    for folder, st in STYLES.items():
        save(down(base(st, NODE)), "Styles", folder, "Things", f"{NODE}_Base.png")
        save(down(top(st)), "Styles", folder, "Things", f"{NODE}_Top.png")
        i = base(st, NODE); i.alpha_composite(top(st))
        save(down(i), "Styles", folder, "Things", f"{NODE}_Icon.png")
    save(beam_core(), "Effects", "BeamCore.png")
    save(beam_glow(), "Effects", "BeamGlow.png")
    ship_art()
    console_files()
    preview().save(os.path.join(ROOT, "About", "Preview.png"))
    print("Art generated.")


if __name__ == "__main__":
    main()
