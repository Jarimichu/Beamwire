namespace Beamwire.Core
{
    /// <summary>Tuning values for tracing and power loss.</summary>
    public sealed class BeamSettings
    {
        /// <summary>Fraction lost at each mirror, splitter or relay the beam passes through.</summary>
        public float NodeLoss = 0.05f;

        /// <summary>Fraction lost per tile travelled (0.001 = 1% per 10 tiles).</summary>
        public float LossPerTile = 0.001f;

        /// <summary>Maximum tiles a beam can travel from an emitter or relay before it dissipates.</summary>
        public float MaxRange = 60f;
    }
}
