using System.Collections.Generic;

namespace Beamwire.Core
{
    /// <summary>Why a beam segment stopped.</summary>
    public enum SegmentEnd
    {
        /// <summary>It reached another node (see <see cref="BeamSegment.To"/>).</summary>
        Node,

        /// <summary>A wall or other impassable building stopped it.</summary>
        Blocked,

        /// <summary>It ran past the beam's maximum range.</summary>
        OutOfRange,

        /// <summary>It left the map.</summary>
        MapEdge,
    }

    /// <summary>One straight run of beam between two nodes, or from a node to whatever stopped it.</summary>
    public sealed class BeamSegment
    {
        internal BeamSegment(BeamNode from, Dir8 dir)
        {
            From = from;
            Dir = dir;
        }

        public BeamNode From { get; }
        public Dir8 Dir { get; }
        public BeamNode To { get; internal set; }
        public SegmentEnd End { get; internal set; }

        /// <summary>The cells the beam crosses, not including the source node's cell or the target node's cell.</summary>
        public List<GridPos> Cells { get; } = new List<GridPos>();

        /// <summary>Distance travelled in tiles (diagonal steps count √2).</summary>
        public float Length { get; internal set; }

        /// <summary>Fraction of power that survives the trip (distance, weather, smoke), 0..1.</summary>
        public float Transmittance { get; internal set; } = 1f;

        /// <summary>
        /// True when this segment would close a loop. The solver ignores it so power can't circulate forever.
        /// </summary>
        public bool IsCycle { get; internal set; }

        /// <summary>Solver output: watts entering this segment at its source end.</summary>
        public float Watts { get; internal set; }

        /// <summary>Solver output: watts that need to enter this segment to meet downstream demand.</summary>
        public float RequiredWatts { get; internal set; }

        /// <summary>The cell where the beam visibly ends: the target node, or the last crossed cell.</summary>
        public GridPos EndPos => To != null ? To.Pos : (Cells.Count > 0 ? Cells[Cells.Count - 1] : From.Pos);

        public override string ToString() => $"{From} -> {Dir} -> {(To != null ? To.ToString() : End.ToString())} ({Cells.Count} cells)";
    }

    public sealed class BeamGraph
    {
        public List<BeamNode> Nodes { get; } = new List<BeamNode>();
        public List<BeamSegment> Segments { get; } = new List<BeamSegment>();

        internal Dictionary<BeamNode, List<BeamSegment>> Outgoing { get; } = new Dictionary<BeamNode, List<BeamSegment>>();
        internal Dictionary<BeamNode, List<BeamSegment>> Incoming { get; } = new Dictionary<BeamNode, List<BeamSegment>>();

        public IReadOnlyList<BeamSegment> OutgoingOf(BeamNode n) =>
            Outgoing.TryGetValue(n, out var list) ? list : (IReadOnlyList<BeamSegment>)System.Array.Empty<BeamSegment>();

        public IReadOnlyList<BeamSegment> IncomingOf(BeamNode n) =>
            Incoming.TryGetValue(n, out var list) ? list : (IReadOnlyList<BeamSegment>)System.Array.Empty<BeamSegment>();

        internal void Add(BeamSegment s)
        {
            Segments.Add(s);
            if (!Outgoing.TryGetValue(s.From, out var outList))
            {
                Outgoing[s.From] = outList = new List<BeamSegment>();
            }
            outList.Add(s);
            if (s.To != null)
            {
                if (!Incoming.TryGetValue(s.To, out var inList))
                {
                    Incoming[s.To] = inList = new List<BeamSegment>();
                }
                inList.Add(s);
            }
        }
    }
}
