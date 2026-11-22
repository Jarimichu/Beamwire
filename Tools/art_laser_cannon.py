#!/usr/bin/env python3
"""Generates the laser cannon's, the laser control console's and the laser fence post's art in the mod's high-tech look (the same as Beamwire's), plus the cannon's buzz.

Run from the repo root:  python3 Tools/art_laser_cannon.py   (needs Pillow)
"""
import math
import os
import struct
import wave

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEX = os.path.join(ROOT, "Common", "Textures", "JarimichusLasers")
S = 256          # working resolution
FINAL = 128      # saved resolution
NODE = "LaserCannon"
NODES = [NODE]

# The mod's one look: high tech (white ceramic, teal light strips). The drawing code still has branches for
# other decorations ("vents" is the gravship look; the rest are unused).
STYLES = {
  "Spacer": dict(
    beam=(90, 240, 220), outline=(70, 86, 96), ow=5, radius=40,
    plate=(222, 228, 232), plate_hi=(246, 249, 250), plate_lo=(184, 193, 200),
    metal=(236, 240, 243), metal_dark=(150, 162, 172), glass=(90, 240, 220), mirror=(200, 255, 250),
    accents={k: (90, 240, 220) for k in NODES},
    deco="strips", ground="tile"),
}

STYLE = STYLES["Spacer"]


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
    """Heavy twin-barrelled turret head: two long barrels with red lenses, an armoured shroud and a targeting sensor."""
    img = blank(); d = ImageDraw.Draw(img); o = rgba(st["outline"]); ow = max(3, st["ow"] - 2)
    sharp = st["deco"] in ("vents", "engrave", "hazard")
    r = 6 if sharp else 20
    hot, hot_core = (255, 40, 30, 255), (255, 215, 205, 255)
    # armoured shroud
    d.polygon([(60, 230), (196, 230), (214, 150), (176, 104), (80, 104), (42, 150)], fill=rgba(st["metal"]), outline=o)
    d.line([(60, 230), (196, 230), (214, 150), (176, 104), (80, 104), (42, 150), (60, 230)], fill=o, width=ow)
    d.rounded_rectangle([86, 140, 170, 210], radius=r, fill=rgba(st["metal_dark"]), outline=o, width=3)
    for y in range(152, 206, 12):
        d.line([(96, y), (160, y)], fill=rgba(st["plate_lo"]), width=4)
    # twin long barrels, a little thicker than the mining laser's
    for bx in (104, 152):
        d.rectangle([bx - 13, 8, bx + 13, 112], fill=rgba(st["metal"]), outline=o, width=max(3, ow - 3))
        for y in (34, 62, 88):
            d.rectangle([bx - 16, y, bx + 16, y + 8], fill=rgba(st["metal_dark"]), outline=o, width=2)
        heat = blank(); ImageDraw.Draw(heat).ellipse([bx - 13, -5, bx + 13, 21], fill=hot)
        img.alpha_composite(glow_layer(heat, 6))
        d.ellipse([bx - 6, 2, bx + 6, 14], fill=hot_core)
    # targeting sensor
    sensor = blank(); ImageDraw.Draw(sensor).ellipse([196, 118, 222, 144], fill=hot)
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
    for dx in (-6, 6):
        bd.line([(130 + dx, 180), (230 + dx, 50)], fill=(255, 40, 30, 255), width=4)
    img.alpha_composite(beams.filter(ImageFilter.GaussianBlur(4)))
    d.text((20, 20), "JARIMICHUS LASER CANNON", fill=(240, 240, 235, 255))
    d.text((20, 38), "auto or manned - burns hostiles, never fires through friendlies", fill=(255, 120, 100, 255))
    return img.convert("RGB")



# ---------------------------------------------------------------- gravship hull-mount variant (Odyssey)
# Used instead of the normal look on gravship substructure: gunmetal hull plating like gravship substructure, so the turret blends into the deck.
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
_RED = (255, 60, 45, 255); _TEAL = (90, 240, 220, 255); _AMBER = (255, 170, 40, 255)
_PHOS = (110, 255, 140)


def _console_pal(st):
    return dict(o=rgba(st["outline"]), ow=max(3, st["ow"] - 2), metal=rgba(st["metal"]), dark=rgba(st["metal_dark"]),
                lo=rgba(st["plate_lo"]), hi=rgba(st["plate_hi"]), glass=rgba(st["glass"]), ship=st["deco"] == "vents")


def _console_glow(img, draw_fn, radius=4):
    layer = blank(); draw_fn(ImageDraw.Draw(layer)); img.alpha_composite(glow_layer(layer, radius))


def _console_joystick(img, p, jx, jy):
    d = ImageDraw.Draw(img); o = p["o"]
    d.ellipse([jx - 18, jy - 11, jx + 18, jy + 11], fill=(30, 30, 32, 255), outline=o, width=3)
    d.line([(jx, jy), (jx + 2, jy - 18)], fill=(55, 55, 58, 255), width=8)
    d.rounded_rectangle([jx - 10, jy - 40, jx + 12, jy - 12], radius=7, fill=(36, 38, 36, 255), outline=o, width=3)
    d.rectangle([jx - 4, jy - 18, jx + 6, jy - 13], fill=(70, 70, 70, 255))
    _console_glow(img, lambda g: g.ellipse([jx - 3, jy - 36, jx + 5, jy - 28], fill=_RED), 3)


def _console_fire_button(img, p, fx, fy, size=20):
    d = ImageDraw.Draw(img); o = p["o"]
    d.rectangle([fx - size, fy - size + 2, fx + size, fy + size - 2], fill=(230, 180, 30, 255), outline=o, width=3)
    for i in range(-size, size + 1, 10):
        d.line([(fx + i - 6, fy + size - 2), (fx + i + 6, fy - size + 2)], fill=(30, 30, 30, 255), width=3)
    d.rectangle([fx - size + 7, fy - size + 9, fx + size - 7, fy + size - 9], fill=(30, 30, 34, 255), outline=o, width=2)
    _console_glow(img, lambda g: g.ellipse([fx - 9, fy - 9, fx + 9, fy + 9], fill=_RED), 3)


def _console_toggles(img, p, xs, y):
    d = ImageDraw.Draw(img)
    for x in xs:
        d.rectangle([x - 5, y - 13, x + 5, y + 13], fill=(30, 30, 34, 255), outline=p["o"], width=2)
        d.rectangle([x - 2, y - 11, x + 2, y + 1], fill=(200, 200, 205, 255))


def _console_radar(img, c, r, contacts=((0.55, -0.4), (-0.45, 0.5)), colour=_PHOS, rings=2):
    def fn(g):
        for k in range(1, rings + 1):
            rr = r * k / rings
            g.ellipse([c[0] - rr, c[1] - rr, c[0] + rr, c[1] + rr], outline=colour + (150,), width=2)
        g.line([(c[0] - r, c[1]), (c[0] + r, c[1])], fill=colour + (110,), width=2)
        g.line([(c[0], c[1] - r), (c[0], c[1] + r)], fill=colour + (110,), width=2)
        g.pieslice([c[0] - r, c[1] - r, c[0] + r, c[1] + r], -60, -20, fill=colour + (90,))
        for ux, uy in contacts:
            bx, by = c[0] + ux * r, c[1] + uy * r
            g.line([(bx - 6, by - 6), (bx + 6, by + 6)], fill=_RED, width=3)
            g.line([(bx - 6, by + 6), (bx + 6, by - 6)], fill=_RED, width=3)
    _console_glow(img, fn, 4)


def _console_deck(img, p, box, bolts=True):
    d = ImageDraw.Draw(img)
    d.rectangle(box, fill=p["lo"], outline=p["o"], width=p["ow"])
    d.line([(box[0] + 6, box[1] + 8), (box[2] - 6, box[1] + 8)], fill=p["hi"], width=3)
    if bolts:
        for x, y in ((box[0] + 8, box[1] + 16), (box[2] - 8, box[1] + 16), (box[0] + 8, box[3] - 8), (box[2] - 8, box[3] - 8)):
            d.ellipse([x - 3, y - 3, x + 3, y + 3], fill=p["hi"], outline=p["o"], width=1)


def _console_strip(img, p, box):
    if not p["ship"]:
        _console_glow(img, lambda g: g.rectangle(box, fill=_TEAL), 5)


def console_art(st):
    """A cockpit-style fire-control station seen from above and in front: an angled radar scope between two
    angled status screens at the back (north), then a deck toward the operator (south) with a throttle lever on
    the left, a guarded fire button and arming switches in the middle, and a flight stick on the right."""
    p = _console_pal(st); img = blank(); d = ImageDraw.Draw(img); o = p["o"]
    d.polygon([(40, 18), (S - 40, 18), (S - 16, 60), (S - 16, S - 22), (16, S - 22), (16, 60)], fill=p["metal"])
    d.line([(40, 18), (S - 40, 18), (S - 16, 60), (S - 16, S - 22), (16, S - 22), (16, 60), (40, 18)], fill=o, width=p["ow"], joint="curve")
    # side screens, angled in
    for side in (-1, 1):
        x0 = 28 if side < 0 else S - 72
        pts = [(x0, 64), (x0 + 44, 52), (x0 + 44, 140), (x0, 136)] if side < 0 else [(x0, 52), (x0 + 44, 64), (x0 + 44, 136), (x0, 140)]
        d.polygon(pts, fill=p["dark"], outline=o)
        inner = [(x + (6 if i in (0, 3) else -6) * (1 if side < 0 else 1), y + (6 if i < 2 else -6)) for i, (x, y) in enumerate(pts)]
        d.polygon(inner, fill=(10, 26, 16, 255))
        def bars(g, x0=x0):
            for k, w in enumerate((26, 18, 30, 12)):
                g.rectangle([x0 + 9, 74 + k * 14, x0 + 9 + w, 80 + k * 14], fill=_PHOS + (200,))
        _console_glow(img, bars, 2)
    d = ImageDraw.Draw(img)
    d.rectangle([76, 40, S - 76, 146], fill=p["dark"], outline=o, width=p["ow"])
    d.rectangle([84, 48, S - 84, 138], fill=(10, 26, 16, 255))
    _console_radar(img, (S // 2, 93), 36)
    _console_deck(img, p, [28, 156, S - 28, 222])
    # throttle lever on the left
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([56, 172, 76, 214], radius=6, fill=(30, 30, 34, 255), outline=o, width=2)
    d.rounded_rectangle([50, 178, 82, 194], radius=6, fill=(60, 62, 66, 255), outline=o, width=3)
    _console_fire_button(img, p, 118, 192, size=16)
    _console_toggles(img, p, (150, 164), 192)
    _console_joystick(img, p, S - 64, 200)
    _console_strip(img, p, [40, 150, S - 40, 153])
    return img


def console_files():
    for folder, st in STYLES.items():
        c = console_art(st)
        save(down(c), "Things", "LaserConsole_Base.png")
        save(down(c), "Things", "LaserConsole_Icon.png")
    save(down(console_art(SHIP)), "Ship", "LaserConsole_ShipBase.png")



# ---------------------------------------------------------------- laser fence post
def gate_post_art(st):
    """A laser fence post seen from above: a square footing, a rounded pillar, and a red emitter lens on each of its
    four sides, since a post links in every direction."""
    img = blank(); d = ImageDraw.Draw(img)
    o = rgba(st["outline"]); ow = max(3, st["ow"] - 1)
    d.rounded_rectangle([16, 16, S - 16, S - 16], radius=30, fill=rgba(st["plate_lo"]), outline=o, width=ow)
    for x, y in ((36, 36), (S - 36, 36), (36, S - 36), (S - 36, S - 36)):
        d.ellipse([x - 6, y - 6, x + 6, y + 6], fill=rgba(st["plate_hi"]), outline=o, width=2)
    # emitter housings on all four sides
    c = S // 2
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        hx, hy = c + dx * 84, c + dy * 84
        w, h = (40, 24) if dy else (24, 40)
        d.rounded_rectangle([hx - w, hy - h, hx + w, hy + h], radius=8, fill=rgba(st["metal_dark"]), outline=o, width=3)
        lens = blank(); ImageDraw.Draw(lens).ellipse([hx + dx * 10 - 13, hy + dy * 10 - 13, hx + dx * 10 + 13, hy + dy * 10 + 13], fill=(255, 50, 40, 255))
        img.alpha_composite(glow_layer(lens, 5))
        d = ImageDraw.Draw(img)
    # the pillar
    d.ellipse([c - 58, c - 58, c + 58, c + 58], fill=rgba(st["metal"]), outline=o, width=ow)
    d.ellipse([c - 38, c - 38, c + 38, c + 38], fill=rgba(st["plate_hi"]), outline=rgba(st["metal_dark"]), width=4)
    if st["deco"] == "strips":
        ring = blank(); ImageDraw.Draw(ring).ellipse([c - 48, c - 48, c + 48, c + 48], outline=rgba(st["glass"]), width=4)
        img.alpha_composite(glow_layer(ring, 4))
    core = blank(); ImageDraw.Draw(core).ellipse([c - 13, c - 13, c + 13, c + 13], fill=(255, 90, 70, 255))
    img.alpha_composite(glow_layer(core, 4))
    return img


def gate_post_files():
    a = gate_post_art(STYLE)
    save(down(a), "Things", "LaserGatePost_Base.png")
    save(down(a), "Things", "LaserGatePost_Icon.png")

def main():
    for folder, st in STYLES.items():
        save(down(base(st, NODE)), "Things", f"{NODE}_Base.png")
        save(down(top(st)), "Things", f"{NODE}_Top.png")
        i = base(st, NODE); i.alpha_composite(top(st))
        save(down(i), "Things", f"{NODE}_Icon.png")
    save(beam_core(), "Effects", "BeamCore.png")
    save(beam_glow(), "Effects", "BeamGlow.png")
    ship_art()
    console_files()
    gate_post_files()
    os.makedirs(os.path.join(ROOT, "Tools", "previews"), exist_ok=True)
    preview().save(os.path.join(ROOT, "Tools", "previews", NODE + ".png"))
    buzz(os.path.join(ROOT, "Common", "Sounds", "JarimichusLasers", "LaserBuzz.wav"))
    print("Art and sound generated.")


if __name__ == "__main__":
    main()
