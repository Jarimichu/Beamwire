# Jarimichus Lasers

Lasers for RimWorld 1.4, 1.5 and 1.6: wireless power over laser beams, a mining laser, a laser cannon, and a control console to crew them. Every part can be switched off in the mod settings.

| Part | What it is |
|---|---|
| **Beamwire** | Wireless power. Emitters fire power across the map to receivers, bent by mirrors and split by splitters and prisms. |
| **Mining laser** | A three-barrelled turret that cuts rock marked for mining, for full yield. |
| **Laser cannon** | A twin-barrelled defence turret that burns hostiles. |
| **Laser control console** | A military fire-control station that lets one colonist man up to three lasers, e.g. from inside a gravship. |

Beamwire beams can power the lasers: beams first, and the grid tops up.

## Beamwire

Put a **beam emitter** on one power grid and a **beam receiver** on another, and power flows between them through the air with no conduit in between. **Mirrors** bend beams, **splitters** divide them by demand, and **relays** carry them across the map.

### How beams work

| Node | Turns in | What it does |
|---|---|---|
| Emitter | 45° | Draws from its grid and fires a beam. It only draws what receivers downstream need, up to its throughput limit (250 W – 4 kW gizmo). |
| Receiver | – | Catches beams from any side. It asks for its grid's shortfall, or its full intake (4 kW) while a battery can charge. |
| Mirror | 45° | One-sided pane. A beam striking it at an angle turns 90°; head-on it bounces straight back. The back absorbs; a beam running along the pane passes by. |
| Splitter | 45° | Glass beam-splitter cube: every beam both passes through and turns 90°. Power is shared between the branches by downstream demand. |
| Relay | 45° | Re-fires every incoming beam as one, with full range. Uses 30 W of the beam's power. |
| Prism | – | Every beam goes straight on and turns 90° to both sides; power shared by demand. Feeds receivers on both sides of a beam. |
| Safety shutter | – | Lossless inline gate. On Auto it closes while a non-hostile pawn is in the beam beyond it. Can be set to always open or always closed. |
| Beam capacitor | 45° | Stores 600 Wd. Fires one beam out of its facing (range restored), charges from spare power at up to 1 kW, and covers shortfalls from storage. |
| Beam window | – | A wall (seals rooms, holds the roof) that beams pass through. Built from metal or stone. |

- **Auto-aim:** every node except the receiver has an Auto/Manual toggle. Auto nodes are aimed by the network so beams reach as many receivers as possible, using the fewest bounces and then the shortest paths. They re-aim whenever the layout changes: nodes built or removed, walls in a possible beam path, switches or breakdowns. Rotating a node by hand locks it to Manual, and auto-aim routes around it. A receiver that no emitter can reach says so in its inspect pane. Auto-aim can be turned off for the whole game in the mod settings.
- Several emitters can feed one receiver: spare auto emitters are aimed at receivers they can reach, and the load is shared. If one emitter is capped, the others cover the shortfall.
- Beams and nodes point in **8 directions** (45° steps). Diagonal steps count as √2 tiles, and a diagonal beam can't squeeze between two walls that touch at their corners.
- **Losses:** 5% per mirror, splitter or relay, and 1% per 10 tiles. Fog, rain and smoke dim outdoor beams. Range is 60 tiles from an emitter or relay. All of this can be changed in the mod settings.
- Walls, rock and other impassable buildings block beams. Roofs can block them too if you turn that setting on.
- Powered beams **burn** anyone standing in them. You can turn this off or scale it in the settings.
- Optics gather **dust** (faster outdoors), and colonists clean them under the Cleaning work type. Explosions can knock nodes **out of alignment**, and emitters and receivers can **break down**.
- Research has its own tab. Each project also needs the vanilla research its tech builds on:

  | Project | Unlocks | Requires | Bench |
  |---|---|---|---|
  | Beam optics | Emitter, receiver, mirror, beam window | Electricity, Solar panels | any |
  | Beam safety | Safety shutter | Beam optics, Autodoors | any |
  | Beam splitting | Splitter, prism | Beam optics, Microelectronics | hi-tech |
  | Coherent relays | Relay | Beam splitting, Fabrication | hi-tech |
  | Beam capacitors | Capacitor | Coherent relays, Batteries | hi-tech |

## Mining laser

- It cuts the nearest rock or ore **marked for mining** that it can see in a straight line, at any angle, within 30 tiles, for the same yield a colonist would get. Walls block its line; beam windows don't.
- It cuts about **50 HP per second at its full 1,500 W**, roughly a good miner: granite in ~18 s, steel ore in ~30 s, plasteel in ~2.7 min. Less power means slower cutting. It draws power only while it has work (20 W idle).
- **Balance:** 200 steel, 25 plasteel, 5 components and 8,000 work; 250 HP. About a deep drill's cost, paid for working unattended, at range, for full yield.
- Three thin orange industrial-laser beams pulse independently. The head turns onto its target and back to rest (the direction it was placed facing) when idle.
- Its cutting beam burns anyone on the line (setting). A closed Beamwire safety shutter on its line pauses it.
- Research **Mining laser** (Electricity + Deep drilling) on the vanilla Main tab, placed at load time one column right of Deep drilling.

## Laser cannon

- **Targeting:** the nearest hostile pawn it can see within 28 tiles (34 manned), along the same straight-line rules as the mining laser. Hostile animals too, unless you turn that off.
- **Burning:** two continuous red beams pulse independently. Each pulse (every 30 ticks) deals 9 damage per kW, with armour penetration growing with power and a 6% chance to set the target alight.
- **Friendly fire:** it **holds fire** while any non-hostile pawn (colonist, ally, guest, prisoner, tame animal) is on the line of fire, or a Beamwire safety shutter on the line is closed.
- **Manning:** right-click with a colonist to **man** it from the tile in front, for +6 range and ×1.5 damage. Violent work must be enabled. It also fires on its own.
- **Controls:** force target, clear target and hold fire. Selecting it shows its range ring.
- **Power:** up to **1,000 W** only while it has a target (50 W idle). Damage scales with the power it gets.
- **Balance:** 18 DPS unmanned (27 manned) with perfect accuracy, between the vanilla mini-turret and autocannon turret. 200 steel, 40 plasteel, 5 components and 8,000 work; 300 HP.
- **Research:** **Laser cannon** (Gun turrets + Microelectronics, hi-tech bench) on the vanilla Main tab, placed at load time beside its prerequisites.

## Laser control console

A 1x1 fire-control station built like an arcade cabinet: armoured hood, radar scope, guarded fire button, arming switches and a flight-stick on the right (Security tab; research Microelectronics basics; 80 steel, 3 components; 100 W).

- Use **Link laser** to connect up to **3** laser cannons or mining lasers within **20 tiles**. Selecting it shows its reach and its links.
- While a colonist mans it, every linked laser counts as manned: cannons get +6 range and ×1.5 damage; mining lasers cut ×1.5 faster, and their **Choose rock** button picks which marked rock they cut next.
- Without power, the linked lasers aren't manned.

## Odyssey (optional)

- Beamwire optics standing in vacuum never gather dust.
- Built on a gravship's substructure, the lasers and the console switch to a flush hull-mounted look that blends into the deck (same stats).
- In vacuum the mining laser cuts ×1.5 faster, the laser cannon does ×1.5 damage (when it or its target is in vacuum), and both reach ×1.25 further.

## Mod settings

- **Parts:** switch Beamwire, the mining laser or the laser cannon off. Their buildings and research are removed while the game loads (`Common/Patches/PartToggles.xml`), so this needs a restart, and their buildings disappear from existing saves. The console stays while either laser is on. Patches that join two parts (beam power for the lasers) only run when both are on.
- **Beamwire:** auto-aim, beam burns, losses, range, weather, roofs, dust, beam visibility.
- **Mining laser:** burns on the cutting line, and their strength.
- **Laser cannon:** damage multiplier, and whether it attacks hostile animals.

## Look

Everything has a high-tech look: white ceramic casings with teal light strips. Power beams are teal, the mining laser's beams orange and the laser cannon's red. On Odyssey gravship substructure the lasers and the console switch to a gunmetal hull-mounted look that blends into the deck.

`Tools/art_beamwire.py`, `Tools/art_mining_laser.py` and `Tools/art_laser_cannon.py` regenerate the art (the last also makes the console art and the cannon's buzz), and `Tools/make_preview.py` the Workshop preview. Beamwire's textures are in `Common/Textures/Beamwire/Things/`, the lasers' in `Common/Textures/JarimichusLasers/Things/` (gravship looks in `.../Ship/`).

## Beam-powered devices API (for other modders)

Other mods can let Beamwire beams power their buildings, with no compile-time reference either way. The mod's own mining laser and laser cannon use it (see `Common/Patches/LaserBeamPower.xml`).

1. Give one of your building's comps these public members (found by name):
   - `float BeamWantedWatts { get; }`: watts wanted from beams right now (0 = nothing to do).
   - `void SetBeamWatts(float watts)`: called after every solve with the watts beams deliver.
   - optional `IEnumerable<IntVec3> BeamGuardLine { get; }`: cells of a dangerous line your device fires along; safety shutters on it guard the stretch beyond them.
2. Ship a patch that loads only when Jarimichus Lasers is active (`LoadFolders.xml`: `<li IfModActive="jarimichu.lasers">Mods/JarimichusLasers</li>`) and adds two comps to your building:

```xml
<Operation Class="PatchOperationAdd">
  <xpath>Defs/ThingDef[defName="YourBuilding"]/comps</xpath>
  <value>
    <li Class="Beamwire.CompProperties_BeamNode">
      <kind>Receiver</kind>
      <rotatable>false</rotatable>
    </li>
    <li Class="Beamwire.CompProperties_BeamPowered" />
  </value>
</Operation>
```

Auto-aim then routes beams to your building whenever it wants power. To respect Beamwire from your side:
- a closed safety shutter has a comp whose `bool BlocksLasers` is true;
- beam windows' defs carry the mod extension `Beamwire.BeamTransparentExtension`.

## Repository layout

```
About/                     mod metadata + preview
Common/                    Defs (one folder per part), Patches, Languages, Textures, Sounds
1.4/ 1.5/ 1.6/             Assemblies (build output; not committed)
Source/
  JarimichusLasers.Core/   pure logic, no RimWorld dependency: Beamwire tracing, solver and auto-aim; laser maths
  JarimichusLasers.Tests/  xUnit tests for the core
  JarimichusLasers/        the RimWorld assembly
    Beamwire/              power network (namespace Beamwire)
    Lasers/                mining laser, laser cannon, console and their shared code (namespace JarimichusLasers)
    LasersMod.cs           settings window, part toggles
Tools/                     art generators and def checks
```

## Building

You need the .NET 8 SDK. No RimWorld install is needed to compile, because the build uses the `Krafs.Rimworld.Ref` reference assemblies.

```bash
dotnet test Source/JarimichusLasers.Tests
dotnet build Source/JarimichusLasers/JarimichusLasers.csproj -c Release -p:RimWorldVersion=1.6
dotnet build Source/JarimichusLasers/JarimichusLasers.csproj -c Release -p:RimWorldVersion=1.5
dotnet build Source/JarimichusLasers/JarimichusLasers.csproj -c Release -p:RimWorldVersion=1.4
python3 Tools/check_research.py
python3 Tools/check_lasers.py
```

Each build writes `JarimichusLasers.dll` to `<version>/Assemblies/`. To test in game, copy or symlink the repo folder into `RimWorld/Mods/`. Harmony must be installed as a mod.

## Compatibility

- **Vanilla Expanded power mods:** emitters and receivers are ordinary vanilla power-net comps, so VE batteries, generators and conduits work with them unchanged.
- **Combat Extended:** a patch adds `Bulk` to the Beamwire nodes. Beam burns are Heat damage. This still needs testing in game under CE.

## Credits

The beam rendering technique (a stretched plane mesh with a hot-core texture) follows Mlie's MIT-licensed [MiningCo. DrillTurret](https://github.com/emipa606/MiningCoDrillTurret). No code or assets are copied from it.

## License

MIT, see [LICENSE](LICENSE).
