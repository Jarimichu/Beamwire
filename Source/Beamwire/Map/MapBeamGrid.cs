using Beamwire.Core;
using RimWorld;
using Verse;

namespace Beamwire
{
    /// <summary>Lets the tracer read a RimWorld map: what blocks beams, and how much weather and smoke dim them.</summary>
    public class MapBeamGrid : IBeamGrid
    {
        private const float GasTransmittance = 0.97f;

        private readonly Map map;
        private readonly bool roofsBlock;
        private readonly float weatherPerTile;

        public MapBeamGrid(Map map, BeamwireSettings settings)
        {
            this.map = map;
            roofsBlock = settings.roofsBlock;

            // Weather's accuracy multiplier is 1 in clear weather, about 0.8 in rain and about 0.5 in fog.
            // Fog gives roughly 1% loss per tile in the open.
            float accuracy = map.weatherManager?.curWeather?.accuracyMultiplier ?? 1f;
            weatherPerTile = settings.weatherAffects ? 1f - (1f - accuracy) * 0.02f : 1f;
        }

        public bool InBounds(GridPos pos) => ToCell(pos).InBounds(map);

        public bool IsBlocked(GridPos pos)
        {
            IntVec3 c = ToCell(pos);
            Building edifice = c.GetEdifice(map);
            if (edifice != null && edifice.def.passability == Traversability.Impassable &&
                !edifice.def.HasModExtension<BeamTransparentExtension>())
            {
                return true;
            }
            return roofsBlock && c.Roofed(map);
        }

        public float CellTransmittance(GridPos pos)
        {
            IntVec3 c = ToCell(pos);
            float t = c.Roofed(map) ? 1f : weatherPerTile;
            if (map.gasGrid != null && map.gasGrid.AnyGasAt(c))
            {
                t *= GasTransmittance;
            }
            return t;
        }

        public static IntVec3 ToCell(GridPos p) => new IntVec3(p.X, 0, p.Z);

        public static GridPos ToGrid(IntVec3 c) => new GridPos(c.x, c.z);
    }
}
