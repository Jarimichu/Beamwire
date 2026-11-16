# Jarimichus Mining Laser

*Part of the Jarimichus mod series, with [Jarimichus Beamwire](https://github.com/jarimichu/beamwire) and [Jarimichus Laser Cannon](https://github.com/jarimichu/laser-cannon).*

A three-barrelled mining laser for RimWorld 1.4, 1.5 and 1.6.

- It cuts the nearest rock or ore **marked for mining** that it can see in a straight line, at any angle, within 30 tiles, for the same yield a colonist would get. Walls block its line; Beamwire's beam windows don't.
- It cuts about 60 HP per second per kW, and draws up to **2,000 W** from its power grid only while it has work (20 W idle).
- Three thin orange industrial-laser beams pulse independently. The head turns smoothly onto its target and back to rest (the direction it was placed facing) when idle.
- Its cutting beam burns anyone on the line (mod setting).
- Research **Mining laser** (Electricity + Deep drilling) on the vanilla Main tab. Its position is worked out at load time, one column right of Deep drilling.

## With other mods

- **Jarimichus Beamwire** (optional): `Mods/Beamwire` loads automatically and makes the laser a beam-powered device. Power beams feed it first and the grid tops up. Auto-aim routes beams to it while it has work, and a closed safety shutter on its line pauses it.
- **Odyssey** (optional): built on a gravship's substructure it switches to a flush hull-mounted look that blends into the deck (same stats); and in vacuum it cuts ×1.5 faster.

## Building

```bash
dotnet test Source/JarimichusMiningLaser.Tests
dotnet build Source/JarimichusMiningLaser/JarimichusMiningLaser.csproj -c Release -p:RimWorldVersion=1.6
```

Use `RimWorldVersion` 1.4, 1.5 or 1.6. `python3 Tools/generate_art.py` regenerates the art, and `python3 Tools/check_defs.py` checks the XML.

## License

MIT.
