namespace Beamwire.Core
{
    public enum NodeKind
    {
        /// <summary>Draws power from its own power net and fires a beam in <see cref="BeamNode.Facing"/>.</summary>
        Emitter,

        /// <summary>Absorbs beams from any side and feeds the power into its power net.</summary>
        Receiver,

        /// <summary>
        /// One-sided flat mirror. <see cref="BeamNode.Facing"/> is the direction its reflective face points.
        /// A beam hitting the face at 45° turns 90°; a beam hitting it head-on goes straight back. The back
        /// absorbs beams, and a beam running along the pane passes by.
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
        public BeamNode(int id, NodeKind kind, GridPos pos, Dir8 facing)
        {
            Id = id;
            Kind = kind;
            Pos = pos;
            Facing = facing;
        }

        public int Id { get; }
        public NodeKind Kind { get; }
        public GridPos Pos { get; }

        /// <summary>
        /// Which way the node points: the beam direction for emitters and relays, and the direction the
        /// reflective face points for mirrors and splitters.
        /// </summary>
        public Dir8 Facing { get; }

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
