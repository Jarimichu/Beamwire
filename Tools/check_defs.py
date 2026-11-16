#!/usr/bin/env python3
"""Checks the mod's XML against its code and textures. Run from the repo root:  python3 Tools/check_defs.py
- every XML file parses;
- every Class="..." / thingClass / compClass / driverClass / workerClass naming one of the mod's namespaces
  (Beamwire, JarimichusLasers) exists in Source/;
- every texPath, uiIconPath and clipPath in the defs exists under Common/Textures or Common/Sounds;
- the part toggles name only known parts, and every def they remove exists;
- the patches that add Beamwire comps to the lasers are guarded by both parts.
"""
import glob, os, re, sys, xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PARTS = {"Beamwire", "MiningLaser", "LaserCannon"}
NAMESPACES = ("Beamwire.", "JarimichusLasers.")
problems = []

def rel(p): return os.path.relpath(p, ROOT)

# classes declared in the source, as Namespace.Name
declared = set()
for path in glob.glob(os.path.join(ROOT, "Source", "JarimichusLasers*", "**", "*.cs"), recursive=True):
    if "/obj/" in path or "/bin/" in path: continue
    text = open(path, encoding="utf-8").read()
    ns = re.search(r"^namespace\s+([\w.]+)", text, re.M)
    if not ns: continue
    for name in re.findall(r"\b(?:class|struct|enum)\s+(\w+)", text):
        declared.add(f"{ns.group(1)}.{name}")

xml_files = [p for p in glob.glob(os.path.join(ROOT, "**", "*.xml"), recursive=True)
             if "/Source/" not in p and "/1.4/" not in p and "/1.5/" not in p and "/1.6/" not in p]
trees = {}
for path in xml_files:
    try:
        trees[path] = ET.parse(path)
    except ET.ParseError as e:
        problems.append(f"{rel(path)}: does not parse: {e}")

def class_refs(root):
    for el in root.iter():
        if el.get("Class"): yield el.get("Class")
        if el.tag in ("thingClass", "compClass", "driverClass", "workerClass", "giverClass", "placeWorkers") and el.text:
            yield el.text.strip()
        if el.tag == "li" and el.text and el.text.strip().startswith(NAMESPACES):
            yield el.text.strip()

for path, tree in trees.items():
    for cls in class_refs(tree.getroot()):
        if cls.startswith(NAMESPACES) and cls not in declared:
            problems.append(f"{rel(path)}: class {cls} not found in Source/")

# texture and sound paths
tex_root = os.path.join(ROOT, "Common", "Textures")
snd_root = os.path.join(ROOT, "Common", "Sounds")
defs_files = [p for p in trees if "/Common/Defs/" in p]
def_names = {}
for path in defs_files:
    for d in trees[path].getroot():
        name = d.findtext("defName")
        if name: def_names.setdefault(d.tag, set()).add(name)
        for tag in ("texPath", "uiIconPath"):
            for el in d.iter(tag):
                t = el.text.strip()
                if t.startswith(("Beamwire/", "JarimichusLasers/")) and not os.path.exists(os.path.join(tex_root, t + ".png")):
                    problems.append(f"{rel(path)}: {d.findtext('defName')} {tag} {t} has no texture")
        for el in d.iter("clipPath"):
            t = el.text.strip()
            if t.startswith("JarimichusLasers/") and not glob.glob(os.path.join(snd_root, t + ".*")):
                problems.append(f"{rel(path)}: sound {t} not found")

# part toggles
def defs_matching(xpath):
    """Def names a toggle xpath removes, for the simple forms used in PartToggles.xml."""
    found = set()
    types = re.findall(r"self::(\w+)", xpath) or [re.match(r"Defs/(\w+)", xpath).group(1)]
    for t in types:
        for n in def_names.get(t, ()):
            exact = re.findall(r'defName="([^"]+)"', xpath)
            prefixes = re.findall(r'starts-with\(defName, "([^"]+)"\)', xpath)
            if n in exact or any(n.startswith(p) for p in prefixes): found.add((t, n))
    return found

for path, tree in trees.items():
    if "/Patches/" not in path: continue
    for op in tree.getroot().iter("Operation"):
        if op.get("Class") != "JarimichusLasers.PatchOperationIfParts": continue
        parts = [li.text for li in op.findall("enabled/li")] + [li.text for li in op.findall("disabled/li")]
        for p in parts:
            if p not in PARTS: problems.append(f"{rel(path)}: unknown part {p}")
        for xp in op.iter("xpath"):
            text = xp.text.strip()
            if "PartToggles" in path:
                for name in re.findall(r'defName="([^"]+)"', text):
                    if not any(name in s for s in def_names.values()):
                        problems.append(f"{rel(path)}: removes {name}, which no def defines")
                if not defs_matching(text):
                    problems.append(f"{rel(path)}: {text} matches no def")
            if "LaserBeamPower" in path:
                need = {"Beamwire", "MiningLaser" if "JML_" in text else "LaserCannon"}
                if set(li.text for li in op.findall("enabled/li")) != need:
                    problems.append(f"{rel(path)}: patch on {text} should need exactly {sorted(need)}")

if problems:
    print("PROBLEMS:"); [print(" -", p) for p in problems]; sys.exit(1)
print(f"Defs OK: {len(trees)} XML files, {sum(len(v) for v in def_names.values())} defs, {len(declared)} classes.")
