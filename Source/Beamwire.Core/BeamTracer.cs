using System.Collections.Generic;

namespace Beamwire.Core
{
    /// <summary>Follows every beam from every emitter and builds the beam graph.</summary>
    public static class BeamTracer
    {
        private struct Launch
        {
            public BeamNode Node;
            public Dir8 Dir;
            public float RangeLeft;
        }

        public static BeamGraph Trace(IBeamGrid grid, IEnumerable<BeamNode> nodes, BeamSettings settings)
        {
            var graph = new BeamGraph();
            var byPos = new Dictionary<GridPos, BeamNode>();
            foreach (var n in nodes)
            {
                if (byPos.ContainsKey(n.Pos))
                {
                    continue; // two nodes cannot share a cell; keep the first
                }
                byPos[n.Pos] = n;
                graph.Nodes.Add(n);
            }
            // Deterministic order, so the same map always gives the same graph.
            graph.Nodes.Sort((a, b) => a.Id.CompareTo(b.Id));

            var queue = new Queue<Launch>();
            var launched = new HashSet<long>();
            var outputs = new List<Dir8>(3);

            foreach (var n in graph.Nodes)
            {
                // Emitters always fire. Charged capacitors fire too, so stored power flows even with no input.
                bool source = n.Kind == NodeKind.Emitter || (n.Kind == NodeKind.Capacitor && n.HasCharge);
                if (source && n.Operational)
                {
                    Enqueue(queue, launched, n, n.Facing, settings.MaxRange);
                }
            }

            while (queue.Count > 0)
            {
                Launch l = queue.Dequeue();
                BeamSegment seg = TraceSegment(grid, byPos, l, settings);
                graph.Add(seg);

                if (seg.To == null)
                {
                    continue;
                }

                BeamOptics.Outputs(seg.To, seg.Dir, outputs);
                float rangeLeft = seg.To.ResetsRange ? settings.MaxRange : l.RangeLeft - seg.Length;
                if (rangeLeft <= 0)
                {
                    continue;
                }
                foreach (Dir8 d in outputs)
                {
                    Enqueue(queue, launched, seg.To, d, rangeLeft);
                }
            }

            MarkCycles(graph);
            return graph;
        }

        private static void Enqueue(Queue<Launch> queue, HashSet<long> launched, BeamNode node, Dir8 dir, float range)
        {
            long key = ((long)node.Id << 3) | (long)dir;
            if (launched.Add(key))
            {
                queue.Enqueue(new Launch { Node = node, Dir = dir, RangeLeft = range });
            }
        }

        private static BeamSegment TraceSegment(
            IBeamGrid grid,
            Dictionary<GridPos, BeamNode> byPos,
            Launch l,
            BeamSettings settings)
        {
            var seg = new BeamSegment(l.Node, l.Dir);
            float step = l.Dir.StepLength();
            bool diagonal = l.Dir.IsDiagonal();
            float keepPerStep = (float)System.Math.Pow(1f - settings.LossPerTile, step);
            GridPos offset = l.Dir.Offset();
            float transmittance = 1f;
            float travelled = 0f;
            GridPos pos = l.Node.Pos;

            while (true)
            {
                GridPos prev = pos;
                pos = pos.Step(l.Dir);

                if (travelled + step > l.RangeLeft + 1e-3f)
                {
                    seg.End = SegmentEnd.OutOfRange;
                    break;
                }
                if (!grid.InBounds(pos))
                {
                    seg.End = SegmentEnd.MapEdge;
                    break;
                }
                // A diagonal beam can't squeeze between two walls that touch at their corners.
                if (diagonal &&
                    grid.IsBlocked(new GridPos(prev.X + offset.X, prev.Z)) &&
                    grid.IsBlocked(new GridPos(prev.X, prev.Z + offset.Z)))
                {
                    seg.End = SegmentEnd.Blocked;
                    break;
                }

                travelled += step;
                transmittance *= keepPerStep;

                if (byPos.TryGetValue(pos, out BeamNode hit))
                {
                    seg.To = hit;
                    seg.End = SegmentEnd.Node;
                    break;
                }
                if (grid.IsBlocked(pos))
                {
                    seg.End = SegmentEnd.Blocked;
                    break;
                }

                float cell = grid.CellTransmittance(pos);
                transmittance *= diagonal ? (float)System.Math.Pow(cell, step) : cell;
                seg.Cells.Add(pos);
            }

            seg.Length = travelled;
            seg.Transmittance = transmittance;
            return seg;
        }

        /// <summary>Depth-first search that flags segments closing a loop (back edges).</summary>
        private static void MarkCycles(BeamGraph graph)
        {
            // 0 = unvisited, 1 = on the current DFS path, 2 = finished
            var state = new Dictionary<BeamNode, int>();
            var stack = new Stack<KeyValuePair<BeamNode, int>>();

            foreach (BeamNode root in graph.Nodes)
            {
                if (state.TryGetValue(root, out int s) && s != 0)
                {
                    continue;
                }
                state[root] = 1;
                stack.Push(new KeyValuePair<BeamNode, int>(root, 0));

                while (stack.Count > 0)
                {
                    var top = stack.Pop();
                    BeamNode node = top.Key;
                    int index = top.Value;
                    IReadOnlyList<BeamSegment> outs = graph.OutgoingOf(node);

                    if (index >= outs.Count)
                    {
                        state[node] = 2;
                        continue;
                    }

                    stack.Push(new KeyValuePair<BeamNode, int>(node, index + 1));
                    BeamSegment seg = outs[index];
                    if (seg.To == null)
                    {
                        continue;
                    }

                    state.TryGetValue(seg.To, out int targetState);
                    if (targetState == 1)
                    {
                        seg.IsCycle = true;
                    }
                    else if (targetState == 0)
                    {
                        state[seg.To] = 1;
                        stack.Push(new KeyValuePair<BeamNode, int>(seg.To, 0));
                    }
                }
            }
        }
    }
}
