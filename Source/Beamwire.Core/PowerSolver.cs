using System;
using System.Collections.Generic;

namespace Beamwire.Core
{
    /// <summary>
    /// Works out how many watts flow through each beam.
    /// <para>
    /// 1. <b>Demand pass</b> (receivers back to emitters): each node works out the input it needs to meet
    ///    downstream demand, allowing for losses on the way.
    /// </para>
    /// <para>
    /// 2. <b>Supply pass</b> (emitters to receivers): each emitter draws what is needed, up to its
    ///    throughput limit. Splitters share power between their outputs in proportion to each output's demand.
    /// </para>
    /// </summary>
    public static class PowerSolver
    {
        private const float Epsilon = 1e-4f;

        public static void Solve(BeamGraph graph, BeamSettings settings)
        {
            foreach (BeamNode n in graph.Nodes)
            {
                n.ResetOutputs();
            }
            foreach (BeamSegment s in graph.Segments)
            {
                s.Watts = 0f;
                s.RequiredWatts = 0f;
            }

            List<BeamNode> order = TopologicalOrder(graph);

            // ---- Demand pass: sinks first ----
            for (int i = order.Count - 1; i >= 0; i--)
            {
                BeamNode n = order[i];
                float required = RequiredInputFor(graph, n, settings);
                n.RequiredInput = required;

                if (n.Kind == NodeKind.Emitter)
                {
                    float eff = Math.Max(n.Efficiency, Epsilon);
                    n.DrawWatts = n.Operational ? Math.Min(n.MaxThroughput, SumRequired(graph, n) / eff) : 0f;
                    continue;
                }

                // Share the requirement between the beams feeding this node.
                IReadOnlyList<BeamSegment> incoming = graph.IncomingOf(n);
                int feeders = 0;
                foreach (BeamSegment s in incoming)
                {
                    if (!s.IsCycle) feeders++;
                }
                if (feeders == 0 || required <= 0f)
                {
                    continue;
                }
                float perFeeder = required / feeders;
                foreach (BeamSegment s in incoming)
                {
                    if (s.IsCycle) continue;
                    s.RequiredWatts = perFeeder / Math.Max(s.Transmittance, Epsilon);
                }
            }

            // ---- Supply pass: emitters first ----
            foreach (BeamNode n in order)
            {
                float available = AvailableOutput(n, settings);
                if (n.Kind == NodeKind.Receiver)
                {
                    n.DeliveredWatts = n.Operational ? n.InputWatts * n.Efficiency : 0f;
                    continue;
                }
                Distribute(graph, n, available);
            }
        }

        private static float SumRequired(BeamGraph graph, BeamNode n)
        {
            float sum = 0f;
            foreach (BeamSegment s in graph.OutgoingOf(n))
            {
                if (!s.IsCycle) sum += s.RequiredWatts;
            }
            return sum;
        }

        private static float RequiredInputFor(BeamGraph graph, BeamNode n, BeamSettings settings)
        {
            if (!n.Operational)
            {
                return 0f;
            }
            float eff = Math.Max(n.Efficiency, Epsilon);
            switch (n.Kind)
            {
                case NodeKind.Receiver:
                    return Math.Max(0f, n.Demand) / eff;

                case NodeKind.Mirror:
                case NodeKind.Splitter:
                {
                    float need = SumRequired(graph, n);
                    return need / (eff * (1f - settings.NodeLoss));
                }

                case NodeKind.Relay:
                {
                    float need = SumRequired(graph, n);
                    if (need <= 0f) return 0f;
                    return (need + n.Upkeep) / (eff * (1f - settings.NodeLoss));
                }

                case NodeKind.Emitter:
                default:
                    return 0f; // emitters absorb beams that hit them
            }
        }

        private static float AvailableOutput(BeamNode n, BeamSettings settings)
        {
            if (!n.Operational)
            {
                return 0f;
            }
            switch (n.Kind)
            {
                case NodeKind.Emitter:
                    return n.DrawWatts * n.Efficiency;
                case NodeKind.Mirror:
                case NodeKind.Splitter:
                    return n.InputWatts * n.Efficiency * (1f - settings.NodeLoss);
                case NodeKind.Relay:
                    return Math.Max(0f, n.InputWatts * n.Efficiency * (1f - settings.NodeLoss) - n.Upkeep);
                default:
                    return 0f;
            }
        }

        private static void Distribute(BeamGraph graph, BeamNode n, float available)
        {
            IReadOnlyList<BeamSegment> outs = graph.OutgoingOf(n);
            if (outs.Count == 0 || available <= 0f)
            {
                return;
            }

            float totalRequired = 0f;
            int live = 0;
            foreach (BeamSegment s in outs)
            {
                if (s.IsCycle) continue;
                totalRequired += s.RequiredWatts;
                live++;
            }
            if (live == 0)
            {
                return;
            }

            foreach (BeamSegment s in outs)
            {
                if (s.IsCycle) continue;
                float share = totalRequired > Epsilon ? s.RequiredWatts / totalRequired : 1f / live;
                s.Watts = available * share;
                if (s.To != null)
                {
                    s.To.InputWatts += s.Watts * s.Transmittance;
                }
            }
        }

        /// <summary>Kahn's algorithm over the non-cycle segments. Nodes come out in Id order where there is a choice.</summary>
        private static List<BeamNode> TopologicalOrder(BeamGraph graph)
        {
            var inDegree = new Dictionary<BeamNode, int>();
            foreach (BeamNode n in graph.Nodes)
            {
                inDegree[n] = 0;
            }
            foreach (BeamSegment s in graph.Segments)
            {
                if (s.To != null && !s.IsCycle)
                {
                    inDegree[s.To]++;
                }
            }

            var ready = new SortedSet<BeamNode>(Comparer<BeamNode>.Create((a, b) => a.Id.CompareTo(b.Id)));
            foreach (var kv in inDegree)
            {
                if (kv.Value == 0) ready.Add(kv.Key);
            }

            var order = new List<BeamNode>(graph.Nodes.Count);
            while (ready.Count > 0)
            {
                BeamNode n = ready.Min;
                ready.Remove(n);
                order.Add(n);
                foreach (BeamSegment s in graph.OutgoingOf(n))
                {
                    if (s.To == null || s.IsCycle) continue;
                    if (--inDegree[s.To] == 0)
                    {
                        ready.Add(s.To);
                    }
                }
            }
            return order;
        }
    }
}
