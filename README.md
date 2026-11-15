# Beamwire

Wireless power over laser beams for RimWorld 1.4, 1.5 and 1.6.

Put a **beam emitter** on one power grid and a **beam receiver** on another, and power flows between them through the air with no conduit in between. **Mirrors** bend beams, **splitters** divide them by demand, and **relays** carry them across the map.

## How beams work

| Node | Turns in | What it does |
|---|---|---|
| Emitter | 45° | Draws from its grid and fires a beam. It only draws what receivers downstream need, up to its throughput limit (250 W – 4 kW gizmo). |
| Receiver | – | Catches beams from any side. It asks for its grid's shortfall, or its full intake (4 kW) while a battery can charge. |
| Mirror | 22.5° | One-sided pane that reflects like a real mirror: 45°, 90°, 135° or straight back. The back absorbs; a beam grazing the edge passes by. |
| Splitter | 22.5° | Half-silvered: transmits and reflects. Power is shared between the branches by downstream demand. |
| Relay | 45° | Re-fires every incoming beam as one, with full range. Uses 30 W of the beam's power. |

- Beams travel in **8 directions**. Diagonal steps count as √2 tiles, and a diagonal beam can't squeeze between two walls that touch at their corners.
- **Losses:** 5% per mirror, splitter or relay, and 1% per 10 tiles. Fog, rain and smoke dim outdoor beams. Range is 60 tiles from an emitter or relay. All of this can be changed in the mod settings.
- Walls, rock and other impassable buildings block beams. Roofs can block them too if you turn that setting on.
- Powered beams **burn** anyone standing in them. You can turn this off or scale it in the settings.
- Optics gather **dust** (faster outdoors), and colonists clean them under the Cleaning work type. Explosions can knock nodes **out of alignment**, and emitters and receivers can **break down**.
- Research has its own tab: *Beam optics* → *Beam splitting* → *Coherent relays*.

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
