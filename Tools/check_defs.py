#!/usr/bin/env python3
"""Checks the mod's XML. Run from the repo root:  python3 Tools/check_defs.py
- every XML file parses;
- research prerequisites are known vanilla projects (names used by many published mods);
- the Beamwire integration patch only targets this mod's own ThingDef and only Beamwire classes that exist.
"""
import glob, os, sys, xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VANILLA_RESEARCH = {"Electricity", "DeepDrilling"}
OWN_DEF = "JML_MiningLaser"
# Beamwire classes the integration patch may use (part of Beamwire's beam-powered devices API).
BEAMWIRE_API = {"Beamwire.CompProperties_BeamNode", "Beamwire.CompProperties_BeamPowered"}

problems = []
for path in sorted(glob.glob(os.path.join(ROOT, "**", "*.xml"), recursive=True)):
    if "/Source/" in path or "/1." in path: continue
    try:
        ET.parse(path)
    except ET.ParseError as e:
        problems.append(f"{os.path.relpath(path, ROOT)}: does not parse: {e}")

projects = {}
for path in glob.glob(os.path.join(ROOT, "Common", "**", "*.xml"), recursive=True):
    for p in ET.parse(path).getroot().iter("ResearchProjectDef"):
        projects[p.findtext("defName")] = [li.text for li in p.findall("prerequisites/li")]
        bench = p.findtext("requiredResearchBuilding")
        if bench and bench not in ("HiTechResearchBench", "SimpleResearchBench"):
            problems.append(f"unknown research bench {bench}")
for name, reqs in projects.items():
    for r in reqs:
        if r not in VANILLA_RESEARCH and r not in projects:
            problems.append(f"research {name} needs unknown project {r}")
    print(f"research {name} needs {', '.join(reqs)}")

for path in glob.glob(os.path.join(ROOT, "Mods", "Beamwire", "**", "*.xml"), recursive=True):
    text = open(path).read()
    if f'defName="{OWN_DEF}"' not in text:
        problems.append(f"{os.path.relpath(path, ROOT)} does not target {OWN_DEF}")
    for el in ET.parse(path).getroot().iter("li"):
        cls = el.get("Class")
        if cls and cls.startswith("Beamwire.") and cls not in BEAMWIRE_API:
            problems.append(f"{os.path.relpath(path, ROOT)} uses {cls}, which is not part of Beamwire's API")

if problems:
    print("PROBLEMS:"); [print(" -", p) for p in problems]; sys.exit(1)
print("Defs OK.")
