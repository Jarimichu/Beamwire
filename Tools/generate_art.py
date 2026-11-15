#!/usr/bin/env python3
"""Generates Beamwire's art in every selectable style, plus the beam textures, gizmo icons,
the Workshop preview and the emitter hum.

Run from the repo root:  python3 Tools/generate_art.py
Needs Pillow (pip install pillow). Overwrites the files it writes under Common/ and About/.

Styles are chosen in the mod settings. Each style lives in Common/Textures/Beamwire/Styles/<Folder>/Things/
as <Node>_Base.png (fixed plate), <Node>_Top.png (rotating part, drawn pointing North) and <Node>_Icon.png.
To add a style: add an entry to STYLES here and to BeamwireStyles.All in the C# source.
"""
import math
import os
import struct
import wave

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEX = os.path.join(ROOT, "Common", "Textures", "Beamwire")
S = 256          # working resolution
FINAL = 128      # saved resolution
NODES = ["Emitter", "Receiver", "Mirror", "Splitter", "Relay", "Prism", "Shutter", "Capacitor", "MiningLaser"]

# Folder name -> style. The folder names must match BeamwireStyles.All in Source/Beamwire/BeamwireStyles.cs.
STYLES = {
  "Standard": dict(
    beam=(255, 214, 120), outline=(28, 26, 24), ow=10, radius=18,
    plate=(118, 112, 104), plate_hi=(146, 140, 131), plate_lo=(92, 87, 81),
    metal=(160, 156, 148), metal_dark=(84, 80, 75), glass=(255, 214, 120), mirror=(214, 222, 228),
    accents={"Emitter": (196, 120, 52), "Receiver": (110, 150, 92), "Mirror": (150, 150, 150), "Splitter": (190, 170, 90), "Relay": (130, 110, 150), "Prism": (110, 160, 190), "Shutter": (196, 72, 60), "Capacitor": (96, 160, 110), "MiningLaser": (214, 120, 40)},
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

# ---------------------------------------------------------------- tops (pointing north)
def lens(st, box, r=8):
    l = blank(); ImageDraw.Draw(l).ellipse(box, fill=rgba(st["glass"])); return glow_layer(l, r)

def top(st, kind):
    img = blank(); d = ImageDraw.Draw(img); o = rgba(st["outline"]); ow = max(3, st["ow"] - 2)
    sharp = st["deco"] in ("vents", "engrave", "hazard")
    if kind == "Emitter":
        if st["deco"] == "engrave":   # obelisk
            d.polygon([(98, 200), (158, 200), (146, 60), (128, 36), (110, 60)], fill=rgba(st["plate_hi"]), outline=o, width=ow)
            d.polygon([(110, 60), (128, 36), (146, 60)], fill=rgba(st["metal"]), outline=o)
            img.alpha_composite(lens(st, [116, 16, 140, 40]))
        elif st["deco"] == "vents":   # angular sensor head
            d.polygon([(84, 196), (172, 196), (160, 70), (128, 40), (96, 70)], fill=rgba(st["metal"]), outline=o, width=ow)
            d.polygon([(104, 90), (152, 90), (146, 150), (110, 150)], fill=rgba(st["metal_dark"]))
            img.alpha_composite(lens(st, [114, 26, 142, 54], 10))
        else:
            r = 10 if sharp else 22
            d.rounded_rectangle([84, 70, 172, 200], radius=r, fill=rgba(st["metal"]), outline=o, width=ow)
            d.rounded_rectangle([104, 30, 152, 84], radius=r // 2, fill=rgba(st["metal_dark"]), outline=o, width=ow)
            if st["deco"] == "hazard":
                d.rectangle([84, 150, 172, 168], fill=rgba(st["accents"]["Emitter"]), outline=o, width=3)
            if st["deco"] == "strips":
                s = blank(); ImageDraw.Draw(s).line([(128, 100), (128, 180)], fill=rgba(st["glass"]), width=6); img.alpha_composite(glow_layer(s, 6))
            img.alpha_composite(lens(st, [110, 14, 146, 50]))
    elif kind == "Receiver":
        if st["deco"] == "engrave":   # sun disc
            for a in range(0, 360, 30):
                x = 128 + 92 * math.cos(math.radians(a)); y = 128 + 92 * math.sin(math.radians(a))
                d.line([(128, 128), (x, y)], fill=rgba(st["metal_dark"]), width=8)
            d.ellipse([56, 56, 200, 200], fill=rgba(st["metal"]), outline=o, width=ow)
            d.ellipse([84, 84, 172, 172], fill=rgba(st["metal_dark"]))
            img.alpha_composite(lens(st, [104, 104, 152, 152], 10))
        elif st["deco"] == "vents":
            d.regular_polygon((128, 128, 90), 6, fill=rgba(st["metal"]), outline=o, width=ow)
            d.regular_polygon((128, 128, 60), 6, fill=rgba(st["metal_dark"]))
            img.alpha_composite(lens(st, [108, 108, 148, 148], 12))
        else:
            d.ellipse([40, 40, S - 40, S - 40], fill=rgba(st["metal"]), outline=o, width=ow)
            d.ellipse([66, 66, S - 66, S - 66], fill=rgba(st["metal_dark"]), outline=o, width=3)
            for a in range(0, 360, 90):
                x = 128 + 62 * math.cos(math.radians(a + 45)); y = 128 + 62 * math.sin(math.radians(a + 45))
                d.line([(128, 128), (x, y)], fill=rgba(st["metal"]), width=5)
            img.alpha_composite(lens(st, [102, 102, 154, 154], 10))
    elif kind == "Mirror":
        # pane lies E-W, reflective face to the north
        y0 = 100
        back = st["metal_dark"]
        if st["deco"] == "engrave":
            d.rectangle([24, y0 + 22, S - 24, y0 + 44], fill=rgba(st["plate_lo"]), outline=o, width=ow)
            d.rectangle([24, y0, S - 24, y0 + 24], fill=rgba(st["mirror"]), outline=o, width=ow)
            d.line([(34, y0 + 6), (S - 34, y0 + 6)], fill=(255, 250, 220, 255), width=4)
        else:
            if st["radius"] > 20:
                d.rounded_rectangle([22, y0 + 14, S - 22, y0 + 46], radius=14, fill=rgba(back), outline=o, width=ow)
                d.rounded_rectangle([22, y0, S - 22, y0 + 24], radius=12, fill=rgba(st["mirror"]), outline=o, width=ow)
            else:
                d.rectangle([22, y0 + 20, S - 22, y0 + 46], fill=rgba(back), outline=o, width=ow)
                d.rectangle([22, y0, S - 22, y0 + 24], fill=rgba(st["mirror"]), outline=o, width=ow)
            d.line([(36, y0 + 6), (S - 36, y0 + 6)], fill=(255, 255, 255, 255), width=4)
            if st["deco"] == "hazard":
                for x in range(30, S - 30, 28):
                    d.polygon([(x, y0 + 46), (x + 14, y0 + 46), (x + 24, y0 + 22), (x + 10, y0 + 22)], fill=rgba(st["accents"]["Mirror"]))
        d.rectangle([110, y0 + 46, 146, y0 + 92], fill=rgba(st["metal"]), outline=o, width=ow)
    elif kind == "Splitter":
        # A beam-splitter cube seen corner-on: a glass diamond whose horizontal diagonal is the
        # half-silvered pane (lying E-W, like the mirror, so the facing maths matches).
        c, r = 128, 92
        diamond = [(c, c - r), (c + r, c), (c, c + r), (c - r, c)]
        glass = blank(); gd = ImageDraw.Draw(glass)
        gd.polygon(diamond, fill=rgba(st["beam"], 95))
        img.alpha_composite(glow_layer(glass, 10))
        # upper and lower prism faces, slightly different tints
        d.polygon([(c, c - r), (c + r, c), (c - r, c)], fill=rgba(st["beam"], 70))
        d.polygon([(c - r, c), (c + r, c), (c, c + r)], fill=rgba(st["plate_lo"], 90))
        frame_w = max(4, st["ow"] - 2)
        d.line(diamond + [diamond[0]], fill=rgba(st["metal_dark"]), width=frame_w + 4, joint="curve")
        d.line(diamond + [diamond[0]], fill=rgba(st["metal"]), width=frame_w, joint="curve")
        # the pane: a bright line across the diagonal
        pane = blank(); ImageDraw.Draw(pane).line([(c - r + 8, c), (c + r - 8, c)], fill=(255, 255, 255, 255), width=6)
        img.alpha_composite(glow_layer(pane, 6))
        # clamps at the two corners where the pane meets the frame
        clamp = st["accents"]["Splitter"] if st["deco"] == "hazard" else st["metal_dark"]
        for x in (c - r, c + r):
            d.rounded_rectangle([x - 14, c - 18, x + 14, c + 18], radius=0 if sharp else 6, fill=rgba(clamp), outline=o, width=3)
        # small arrows on the glass showing "through" and "reflected"
        d.polygon([(c - 6, c - r + 30), (c + 6, c - r + 30), (c, c - r + 20)], fill=rgba(st["outline"], 160))
    elif kind == "Relay":
        if st["radius"] > 20:
            d.ellipse([52, 52, 204, 204], fill=rgba(st["metal"]), outline=o, width=ow)
            d.ellipse([84, 84, 172, 172], fill=rgba(st["metal_dark"]))
            d.polygon([(110, 60), (146, 60), (128, 28)], fill=rgba(st["metal"]), outline=o)
        else:
            d.polygon([(128, 34), (210, 128), (128, 222), (46, 128)], fill=rgba(st["metal"]), outline=o, width=ow)
            d.polygon([(128, 64), (182, 128), (128, 192), (74, 128)], fill=rgba(st["metal_dark"]))
        if st["deco"] == "hazard":
            d.polygon([(128, 100), (152, 140), (104, 140)], fill=rgba(st["accents"]["Relay"]), outline=o, width=3)
        img.alpha_composite(lens(st, [112, 16, 144, 48]))
        img.alpha_composite(lens(st, [116, 116, 140, 140], 6))
    elif kind == "Prism":
        # A triangular crystal with exit marks on three sides (straight on, left, right).
        c = 128
        tri = [(c, 42), (c + 86, c + 66), (c - 86, c + 66)]
        glass = blank(); gd = ImageDraw.Draw(glass)
        gd.polygon(tri, fill=rgba(st["beam"], 120))
        img.alpha_composite(glow_layer(glass, 10))
        d.polygon([(c, 42), (c, c + 66), (c - 86, c + 66)], fill=rgba(st["mirror"], 90))
        d.line(tri + [tri[0]], fill=rgba(st["metal_dark"]), width=max(6, st["ow"]), joint="curve")
        d.line(tri + [tri[0]], fill=rgba(st["metal"]), width=max(3, st["ow"] - 4), joint="curve")
        core = blank(); ImageDraw.Draw(core).ellipse([c - 14, c + 4, c + 14, c + 32], fill=(255, 255, 255, 255))
        img.alpha_composite(glow_layer(core, 8))
        for (x, y) in [(c, 26), (30, c + 22), (S - 30, c + 22)]:
            d.ellipse([x - 9, y - 9, x + 9, y + 9], fill=rgba(st["glass"]), outline=o, width=3)
    elif kind == "Shutter":
        draw_shutter(d, img, st, o, ow, closed=False)
    elif kind == "Capacitor":
        # A drum of stacked cells with charge lights and an output lens.
        r = 12 if sharp else 30
        d.rounded_rectangle([58, 50, 198, 214], radius=r, fill=rgba(st["metal"]), outline=o, width=ow)
        for i, y in enumerate(range(76, 200, 30)):
            d.rounded_rectangle([74, y, 182, y + 18], radius=min(r, 8), fill=rgba(st["metal_dark"]))
            cell = blank(); ImageDraw.Draw(cell).rounded_rectangle([82, y + 5, 82 + 22 + i * 18, y + 13], radius=3, fill=rgba(st["glass"]))
            img.alpha_composite(glow_layer(cell, 4))
        d.rectangle([108, 22, 148, 56], fill=rgba(st["metal_dark"]), outline=o, width=ow)
        img.alpha_composite(lens(st, [114, 10, 142, 38]))
    elif kind == "MiningLaser":
        # A trigun turret head: three short parallel barrels with hot red lenses, on a finned heat-sink block,
        # pointing North.
        r = 8 if sharp else 22
        for i, y in enumerate(range(150, 214, 14)):
            d.rounded_rectangle([54, y, 202, y + 8], radius=3, fill=rgba(st["metal_dark"]), outline=o, width=2)
        d.rounded_rectangle([66, 112, 190, 220], radius=r, fill=rgba(st["metal"]), outline=o, width=ow)
        d.rounded_rectangle([58, 92, 198, 128], radius=min(r, 10), fill=rgba(st["metal_dark"]), outline=o, width=ow)
        for bx in (84, 128, 172):
            d.rectangle([bx - 11, 26, bx + 11, 100], fill=rgba(st["metal"]), outline=o, width=max(3, ow - 3))
            d.rectangle([bx - 14, 54, bx + 14, 64], fill=rgba(st["metal_dark"]), outline=o, width=2)
            heat = blank(); ImageDraw.Draw(heat).ellipse([bx - 12, 10, bx + 12, 34], fill=(255, 70, 50, 255))
            img.alpha_composite(glow_layer(heat, 6))
            d.ellipse([bx - 6, 16, bx + 6, 28], fill=(255, 220, 210, 255))
        if st["deco"] == "hazard":
            d.rectangle([66, 170, 190, 184], fill=rgba(st["accents"]["MiningLaser"]), outline=o, width=3)
        elif st["deco"] == "strips":
            strip = blank(); ImageDraw.Draw(strip).rounded_rectangle([96, 176, 160, 184], radius=3, fill=rgba(st["glass"]))
            img.alpha_composite(glow_layer(strip, 5))
        elif st["deco"] == "bolts":
            for x in (80, 176):
                d.ellipse([x - 6, 196, x + 6, 208], fill=rgba(st["metal"]), outline=o, width=2)
    return img


def draw_shutter(d, img, st, o, ow, closed):
    """Sliding blast gates in a frame. Open: gates pulled aside with a lit gap. Closed: gates meet, warning stripes."""
    r = 6 if st["radius"] < 20 else 18
    d.rounded_rectangle([20, 58, S - 20, S - 58], radius=r, fill=rgba(st["metal"]), outline=o, width=ow)
    d.rectangle([42, 84, S - 42, S - 84], fill=rgba(st["metal_dark"]))
    warn = st["accents"]["Shutter"] if st["deco"] == "hazard" else (214, 64, 52)
    if closed:
        gates = [(42, 84, 128, S - 84), (128, 84, S - 42, S - 84)]
        for g in gates:
            d.rectangle(g, fill=rgba(st["plate_hi"]), outline=o, width=3)
        stripes = blank(); sd = ImageDraw.Draw(stripes)
        for x in range(-S, S * 2, 28):
            sd.polygon([(x, 84), (x + 14, 84), (x + 14 - 88, S - 84), (x - 88, S - 84)], fill=rgba(warn))
        mask = blank(); ImageDraw.Draw(mask).rectangle([46, 92, S - 46, S - 92], fill=(255, 255, 255, 255))
        img.alpha_composite(Image.composite(stripes, blank(), mask.split()[3]))
        d.line([(128, 84), (128, S - 84)], fill=o, width=4)
        lamp_col = warn
    else:
        gap = blank(); ImageDraw.Draw(gap).rectangle([80, 90, S - 80, S - 90], fill=rgba(st["glass"], 170))
        img.alpha_composite(glow_layer(gap, 8))
        for g in [(42, 84, 74, S - 84), (S - 74, 84, S - 42, S - 84)]:
            d.rectangle(g, fill=rgba(st["plate_hi"]), outline=o, width=3)
        lamp_col = (90, 220, 110)
    lamp = blank(); ImageDraw.Draw(lamp).ellipse([118, 34, 138, 54], fill=rgba(lamp_col))
    img.alpha_composite(glow_layer(lamp, 6))
    d.ellipse([118, 34, 138, 54], outline=o, width=2)


def top_closed(st, kind):
    img = blank(); d = ImageDraw.Draw(img)
    draw_shutter(d, img, st, rgba(st["outline"]), max(3, st["ow"] - 2), closed=True)
    return img


def window(st):
    """A wall block with a glass slit; beams pass through."""
    img = blank(); d = ImageDraw.Draw(img)
    d.rectangle([0, 0, S, S], fill=rgba(st["plate_lo"]), outline=rgba(st["outline"]), width=max(4, st["ow"] - 2))
    d.rectangle([0, 0, S, 22], fill=rgba(st["plate_hi"]))
    if st["deco"] == "hazard":
        for x in range(-S, S * 2, 36):
            d.polygon([(x, S - 26), (x + 18, S - 26), (x + 8, S), (x - 10, S)], fill=rgba(st["accents"]["Emitter"]))
    slit = blank(); sd = ImageDraw.Draw(slit)
    sd.rounded_rectangle([20, 104, S - 20, 152], radius=8, fill=rgba(st["beam"], 120))
    img.alpha_composite(glow_layer(slit, 6))
    d.rounded_rectangle([20, 104, S - 20, 152], radius=8, outline=rgba(st["metal_dark"]), width=6)
    d.line([(32, 114), (S - 60, 114)], fill=(255, 255, 255, 200), width=4)
    return img


def icon(st, kind):
    i = base(st, kind); i.alpha_composite(top(st, kind)); return i


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


# ---------------------------------------------------------------- UI icons (style-neutral)
UI_BLUE = (120, 200, 255)


def icon_show_beams():
    img = blank(128)
    beam = blank(128)
    bd = ImageDraw.Draw(beam)
    bd.line([16, 100, 64, 52], fill=rgba(UI_BLUE), width=6)
    bd.line([64, 52, 112, 100], fill=rgba(UI_BLUE), width=6)
    img.alpha_composite(glow_layer(beam, 6))
    d = ImageDraw.Draw(img)
    d.line([16, 100, 64, 52], fill=(255, 255, 255, 255), width=2)
    d.line([64, 52, 112, 100], fill=(255, 255, 255, 255), width=2)
    d.polygon([(44, 40), (84, 40), (64, 60)], fill=(220, 230, 240, 255))
    return img


def icon_throughput():
    img = blank(128)
    d = ImageDraw.Draw(img)
    for i, hgt in enumerate([24, 44, 64, 88]):
        x = 18 + i * 26
        d.rectangle([x, 108 - hgt, x + 18, 108], fill=rgba(UI_BLUE) if i < 3 else (220, 230, 240, 255))
    return glow_layer(img, 3)


def icon_auto_aim():
    """Crosshair with a small spark: 'aims itself'."""
    img = blank(128)
    ring = blank(128)
    rd = ImageDraw.Draw(ring)
    rd.ellipse([24, 24, 104, 104], outline=rgba(UI_BLUE), width=8)
    for a, b in [((64, 8), (64, 40)), ((64, 88), (64, 120)), ((8, 64), (40, 64)), ((88, 64), (120, 64))]:
        rd.line([a, b], fill=rgba(UI_BLUE), width=8)
    img.alpha_composite(glow_layer(ring, 4))
    d = ImageDraw.Draw(img)
    d.ellipse([54, 54, 74, 74], fill=(255, 255, 255, 255))
    return img


# ---------------------------------------------------------------- Workshop preview: the three styles side by side
def mini_scene(st, w, h, t):
    import random
    col = {"dirt": (92, 74, 56), "concrete": (96, 96, 92), "tile": (178, 186, 192)}[st["ground"]]
    img = Image.new("RGBA", (w, h), rgba(col))
    d = ImageDraw.Draw(img)
    rnd = random.Random(7)
    if st["ground"] == "dirt":
        for _ in range(900):
            x, y = rnd.randrange(w), rnd.randrange(h)
            dv = rnd.randint(-14, 14)
            d.point((x, y), fill=rgba(tuple(max(0, min(255, c + dv)) for c in col)))
    else:
        for x in range(0, w, t):
            d.line([(x, 0), (x, h)], fill=rgba(tuple(c - 18 for c in col)), width=1)
        for y in range(0, h, t):
            d.line([(0, y), (w, y)], fill=rgba(tuple(c - 18 for c in col)), width=1)
    cell = lambda cx, cy: (cx * t + t // 2, cy * t + t // 2)
    # The emitter fires east into a splitter, which reflects north to a receiver and passes east to a mirror.
    # The mirror turns the beam 90° north into a relay, which re-fires it diagonally north-east.
    segs = [[cell(0, 3), cell(2, 3)], [cell(2, 3), cell(2, 0)], [cell(2, 3), cell(4, 3)],
            [cell(4, 3), cell(4, 1)], [cell(4, 1), (6 * t, 0)]]
    beams = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    bd = ImageDraw.Draw(beams)
    for sgm in segs:
        bd.line(sgm, fill=rgba(st["beam"]), width=8)
    img.alpha_composite(beams.filter(ImageFilter.GaussianBlur(6)))
    for sgm in segs:
        d.line(sgm, fill=(255, 255, 255, 235), width=2)

    def place(kind, c, deg):
        b = base(st, kind)
        b.alpha_composite(top(st, kind).rotate(-deg, resample=Image.BICUBIC))
        b = b.resize((t, t), Image.LANCZOS)
        x, y = cell(*c)
        img.alpha_composite(b, (x - t // 2, y - t // 2))
    place("Emitter", (0, 3), 90)
    place("Splitter", (2, 3), 315)
    place("Receiver", (2, 0), 0)
    place("Mirror", (4, 3), 315)
    place("Relay", (4, 1), 45)
    return img


def preview():
    w, h = 640, 360
    img = Image.new("RGBA", (w, h), (24, 22, 20, 255))
    t = 34
    for i, st in enumerate(STYLES.values()):
        sc = mini_scene(st, 6 * t, 4 * t, t)
        img.alpha_composite(sc, (14 + i * (6 * t + 4), 90))
    d = ImageDraw.Draw(img)
    d.text((16, 24), "BEAMWIRE", fill=(240, 240, 235, 255))
    d.text((16, 42), "wireless laser power  -  3 art styles", fill=(255, 214, 120, 255))
    for i, name in enumerate(["Rim Standard", "Hazard Industrial", "Glitterworld Spacer"]):
        d.text((16 + i * (6 * t + 4), 90 + 4 * t + 10), name, fill=(200, 200, 195, 255))
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
            tt = i / rate
            wobble = 1 + 0.15 * math.sin(2 * math.pi * 1.5 * tt)
            v = sum(a * math.sin(2 * math.pi * f * tt) for f, a in partials) * wobble
            frames += struct.pack("<h", int(max(-1, min(1, v * 0.6)) * 32767))
        wf.writeframes(bytes(frames))


def main():
    for folder, st in STYLES.items():
        for k in NODES:
            save(down(base(st, k)), "Styles", folder, "Things", f"{k}_Base.png")
            save(down(top(st, k)), "Styles", folder, "Things", f"{k}_Top.png")
            save(down(icon(st, k)), "Styles", folder, "Things", f"{k}_Icon.png")
        closed_icon = base(st, "Shutter")
        closed_icon.alpha_composite(top_closed(st, "Shutter"))
        save(down(top_closed(st, "Shutter")), "Styles", folder, "Things", "Shutter_TopClosed.png")
        save(down(window(st)), "Styles", folder, "Things", "Window_Base.png")
        save(down(window(st)), "Styles", folder, "Things", "Window_Icon.png")
        if folder == "Standard":
            save(down(icon(st, "Shutter")), "UI", "ShutterOpen.png")
            save(down(closed_icon), "UI", "ShutterClosed.png")
    save(beam_core(), "Effects", "BeamCore.png")
    save(beam_glow(), "Effects", "BeamGlow.png")
    save(icon_show_beams(), "UI", "ShowBeams.png")
    save(icon_throughput(), "UI", "Throughput.png")
    save(icon_auto_aim(), "UI", "AutoAim.png")
    preview().save(os.path.join(ROOT, "About", "Preview.png"))
    hum(os.path.join(ROOT, "Common", "Sounds", "Beamwire", "EmitterHum.wav"))
    print("Art and sound generated for styles:", ", ".join(STYLES))


if __name__ == "__main__":
    main()
