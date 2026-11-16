# Jarimichus Beamwire

*Part of the Jarimichus mod series, with [Jarimichus Mining Laser](https://github.com/jarimichu/mining-laser) and [Jarimichus Laser Cannon](https://github.com/jarimichu/laser-cannon).*

Wireless power over laser beams for RimWorld 1.4, 1.5 and 1.6.

Put a **beam emitter** on one power grid and a **beam receiver** on another, and power flows between them through the air with no conduit in between. **Mirrors** bend beams, **splitters** divide them by demand, and **relays** carry them across the map.

## How beams work

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

## Odyssey

With Odyssey active (RimWorld 1.6), Beamwire nodes standing in vacuum never gather dust. Nothing else needs Odyssey.

## Beam-powered devices API

Other mods can let Beamwire beams power their buildings, with no compile-time reference either way. The Jarimichus Mining Laser and Laser Cannon use this.

1. Give one of your building's comps these public members (found by name):
   - `float BeamWantedWatts { get; }`: watts wanted from beams right now (0 = nothing to do).
   - `void SetBeamWatts(float watts)`: called after every solve with the watts beams deliver.
   - optional `IEnumerable<IntVec3> BeamGuardLine { get; }`: cells of a dangerous line your device fires along; safety shutters on it guard the stretch beyond them.
2. Ship a patch that loads only when Beamwire is active (`LoadFolders.xml`: `<li IfModActive="jarimichu.beamwire">Mods/Beamwire</li>`) and adds two comps to your building:

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

## Art styles

Players pick one in the mod settings, and it switches live: node art, architect icons, build ghosts and beam colour.

| Style | Look | Beam |
|---|---|---|
| Rim Standard (default) | Close to vanilla: muted flat colours, thick outlines | Warm yellow |
| Hazard Industrial | Hazard stripes, rivets, amber lamps | Orange |
| Glitterworld Spacer | White ceramic, teal light strips | Teal |

`python3 Tools/generate_art.py` regenerates every style. Each style is a folder under `Common/Textures/Beamwire/Styles/`. To add one, add it to `STYLES` in the script and to `BeamwireStyles.All` in `Source/Beamwire/BeamwireStyles.cs`.

## Repository layout

```
About/            mod metadata + preview
Common/           Defs, Patches, Languages, Textures, Sounds (shared by all versions)
1.4/ 1.5/ 1.6/    Assemblies (build output; not committed)
Source/
  Beamwire.Core/  pure beam tracing + power solver, no RimWorld dependency
  Beamwire.Tests/ xUnit tests for the core
  Beamwire/       RimWorld assembly (comps, map component, rendering, Harmony patches)
Tools/generate_art.py   regenerates the placeholder art and hum sound
```

## Building

You need the .NET 8 SDK. No RimWorld install is needed to compile, because the build uses the `Krafs.Rimworld.Ref` reference assemblies.

```bash
dotnet test Source/Beamwire.Tests
dotnet build Source/Beamwire/Beamwire.csproj -c Release -p:RimWorldVersion=1.6
dotnet build Source/Beamwire/Beamwire.csproj -c Release -p:RimWorldVersion=1.5
dotnet build Source/Beamwire/Beamwire.csproj -c Release -p:RimWorldVersion=1.4
```

Each build writes `Beamwire.dll` to `<version>/Assemblies/`. To test in game, copy or symlink the repo folder into `RimWorld/Mods/`. Harmony must be installed as a mod.

## Compatibility

- **Vanilla Expanded power mods:** emitters and receivers are ordinary vanilla power-net comps, so VE batteries, generators and conduits work with them unchanged. No patch is needed.
- **Combat Extended:** a patch adds `Bulk` to the nodes. Beam burns are Heat damage. This still needs testing in game under CE.

## Credits

The beam rendering technique (a stretched plane mesh with a hot-core texture) follows Mlie's MIT-licensed [MiningCo. DrillTurret](https://github.com/emipa606/MiningCoDrillTurret). No code or assets are copied from it.

## License

MIT, see [LICENSE](LICENSE).
