#!/usr/bin/env python3
"""Checks the mod's research wiring (Beamwire and the lasers). Run from the repo root:  python3 Tools/check_research.py

- Every XML file under Common/ and About/ parses.
- Every building's researchPrerequisites names one of the mod's projects or a known vanilla project.
- Every project's prerequisites are the mod's own projects or known vanilla projects.
- requiredResearchBuilding (if set) is a known vanilla bench.
- The prerequisite graph has no cycles.
Exits non-zero on any problem.
"""
import glob
import os
import sys
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Vanilla Core research defNames the mod depends on. MicroelectronicsBasics is referenced by the game's own
# ResearchProjectDefOf; the rest are used as research prerequisites by many published mods for 1.4-1.6.
VANILLA_RESEARCH = {"Electricity", "SolarPanels", "Autodoors", "MicroelectronicsBasics", "Fabrication", "Batteries",
                    "DeepDrilling", "GunTurrets"}
VANILLA_BENCHES = {"HiTechResearchBench", "SimpleResearchBench"}

problems = []
projects = {}   # defName -> [prerequisites]
buildings = {}  # defName -> [researchPrerequisites]
benches = {}

for path in sorted(glob.glob(os.path.join(ROOT, "Common", "**", "*.xml"), recursive=True) +
                   glob.glob(os.path.join(ROOT, "About", "*.xml"))):
    try:
        tree = ET.parse(path)
    except ET.ParseError as e:
        problems.append(f"{os.path.relpath(path, ROOT)}: XML does not parse: {e}")
        continue
    for proj in tree.getroot().iter("ResearchProjectDef"):
        name = proj.findtext("defName")
        projects[name] = [li.text for li in proj.findall("prerequisites/li")]
        benches[name] = proj.findtext("requiredResearchBuilding")
    for thing in tree.getroot().iter("ThingDef"):
        name = thing.findtext("defName")
        reqs = [li.text for li in thing.findall("researchPrerequisites/li")]
        if name and reqs:
            buildings[name] = reqs

for b, reqs in buildings.items():
    for r in reqs:
        if r not in projects and r not in VANILLA_RESEARCH:
            problems.append(f"building {b} needs unknown research project {r}")
for p, reqs in projects.items():
    for r in reqs:
        if r not in projects and r not in VANILLA_RESEARCH:
            problems.append(f"project {p} needs {r}, which is neither one of the mod's projects nor a known vanilla project")
    bench = benches[p]
    if bench and bench not in VANILLA_BENCHES:
        problems.append(f"project {p} needs unknown research bench {bench}")

# cycle check
state = {}
def visit(p, path):
    if state.get(p) == 1:
        problems.append("prerequisite cycle: " + " -> ".join(path + [p]))
        return
    if state.get(p) == 2 or p not in projects:
        return
    state[p] = 1
    for r in projects[p]:
        visit(r, path + [p])
    state[p] = 2
for p in projects:
    visit(p, [])

for p in sorted(projects, key=lambda n: list(projects).index(n)):
    vanilla = [r for r in projects[p] if r in VANILLA_RESEARCH]
    own = [r for r in projects[p] if r in projects]
    unlocks = sorted(b for b, reqs in buildings.items() if p in reqs)
    print(f"{p:26} needs {', '.join(own + vanilla):48} bench: {benches[p] or 'any':20} unlocks: {', '.join(unlocks)}")

if problems:
    print("\nPROBLEMS:")
    for pr in problems:
        print("  -", pr)
    sys.exit(1)
print("\nResearch wiring OK.")
