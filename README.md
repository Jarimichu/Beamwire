# Jarimichus Laser Cannon

*Part of the Jarimichus mod series, with [Jarimichus Beamwire](https://github.com/jarimichu/beamwire) and [Jarimichus Mining Laser](https://github.com/jarimichu/mining-laser).*

A three-barrelled defensive laser turret for RimWorld 1.4, 1.5 and 1.6.

- **Targeting:** it picks the nearest hostile pawn it can see within 26 tiles, along the same straight-line rules as the mining laser. Hostile animals are targeted too, unless you turn that off in the settings.
- **Burning:** three continuous red beams pulse independently. Each pulse (every 30 ticks) deals 9 damage per kW, with armour penetration growing with power and a 6% chance to set the target alight.
- **Friendly fire:** it **holds fire** while any non-hostile pawn (colonist, ally, guest, prisoner, tame animal) is on the line of fire.
- **Manning:** right-click with a colonist to **man** it, for +6 range and ×1.5 damage. Violent work must be enabled. It also fires on its own.
- **Controls:** force target, clear target and hold fire. Selecting it shows its range ring.
- **Power:** it draws up to **2,000 W** from its grid only while it has a target (50 W idle).
- **Research:** **Laser cannon** (Gun turrets + Microelectronics, hi-tech bench) on the vanilla Main tab, placed at load time beside its prerequisites.

## With other mods

- **Jarimichus Beamwire** (optional): power beams feed it first and the grid tops up. A closed safety shutter on its line holds its fire.
- **Odyssey** (optional): ×1.5 damage when the cannon or its target is in vacuum.

## Building

```bash
dotnet test Source/JarimichusLaserCannon.Tests
dotnet build Source/JarimichusLaserCannon/JarimichusLaserCannon.csproj -c Release -p:RimWorldVersion=1.6
```

## License

MIT.
