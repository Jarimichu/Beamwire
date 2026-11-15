namespace Beamwire.Core
{
    public enum NodeKind
    {
        /// <summary>Draws power from its own power net and fires a beam in <see cref="BeamNode.Facing"/>.</summary>
        Emitter,

        /// <summary>Absorbs beams from any side and feeds the power into its power net.</summary>
        Receiver,

        /// <summary>
        /// One-sided flat mirror that turns in 22.5° steps. Its <see cref="BeamNode.Orientation"/> is the direction
        /// its reflective face points. Beams bounce off it like light off a real mirror, so the turn is 45°,
        /// 90°, 135° or 180° depending on the angle. The back absorbs beams; a beam grazing the edge passes by.
        /// </summary>
        Mirror,

        /// <summary>
        /// Half-silvered pane (both sides reflective). Every beam passes straight through and also reflects,
        /// so one beam becomes two.
        /// </summary>
        Splitter,

        /// <summary>
        /// Collects beams from any side except its output and re-emits them in <see cref="BeamNode.Facing"/>.
        /// Resets the beam's range and consumes a small upkeep from the beam.
        /// </summary>
        Relay,
    }

    /// <summary>A snapshot of one optical node, built fresh before each trace.</summary>
    public sealed class BeamNode
    {
        /// <param name="orientation">Direction in 22.5° steps (0 = North, clockwise, 0..15).</param>
        public BeamNode(int id, NodeKind kind, GridPos pos, int orientation)
        {
            Id = id;
            Kind = kind;
            Pos = pos;
            Orientation = Dir8Ext.Mod(orientation, 16);
        }

        public BeamNode(int id, NodeKind kind, GridPos pos, Dir8 facing)
            : this(id, kind, pos, facing.To16())
        {
        }

        public int Id { get; }
        public NodeKind Kind { get; }
        public GridPos Pos { get; }

        /// <summary>
        /// The node's angle in 22.5° steps (0..15, 0 = North, clockwise). Mirrors and splitters use the full
        /// precision: it is the direction of the reflective face's normal. Emitters and relays aim in
        /// <see cref="Facing"/>, which rounds down to a 45° step.
        /// </summary>
        public int Orientation { get; }

        /// <summary>The beam direction for emitters and relays.</summary>
        public Dir8 Facing => (Dir8)(Orientation / 2);

        /// <summary>When false, the node absorbs incoming beams and emits nothing (broken, unpowered, switched off).</summary>
        public bool Operational { get; set; } = true;

        /// <summary>Optical efficiency multiplier, 0..1 (dust, damage).</summary>
        public float Efficiency { get; set; } = 1f;

        /// <summary>Emitter only: the most watts it can put into the beam.</summary>
        public float MaxThroughput { get; set; }

        /// <summary>Receiver only: the watts it wants delivered.</summary>
        public float Demand { get; set; }

        /// <summary>Relay only: watts it consumes from the beam while passing power on.</summary>
        public float Upkeep { get; set; }

        // ---- Solver outputs ----

        /// <summary>Emitter: watts drawn from its power net to feed the beam.</summary>
        public float DrawWatts { get; internal set; }

        /// <summary>Total watts arriving at this node from beams.</summary>
        public float InputWatts { get; internal set; }

        /// <summary>Receiver: watts it delivers to its power net.</summary>
        public float DeliveredWatts { get; internal set; }

        /// <summary>Watts this node needs at its input to meet downstream demand (computed by the solver).</summary>
        public float RequiredInput { get; internal set; }

        internal void ResetOutputs()
        {
            DrawWatts = 0f;
            InputWatts = 0f;
            DeliveredWatts = 0f;
            RequiredInput = 0f;
        }

        public override string ToString() => $"{Kind}#{Id}@{Pos} facing {Facing}";
    }
}
