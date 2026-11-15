namespace Beamwire.Core
{
    /// <summary>What the tracer needs to know about the map.</summary>
    public interface IBeamGrid
    {
        bool InBounds(GridPos pos);

        /// <summary>True when something in the cell stops beams (walls, rock, impassable buildings).</summary>
        bool IsBlocked(GridPos pos);

        /// <summary>Fraction of beam power that survives crossing this cell, from weather and smoke (1 = clear).</summary>
        float CellTransmittance(GridPos pos);
    }
}
