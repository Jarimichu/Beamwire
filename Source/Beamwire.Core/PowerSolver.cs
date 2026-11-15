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

        /// <summary>Re-solves needed at most, when some beams feeding a shared node turn out to be capped.</summary>
        private const int MaxPasses = 4;

        public static void Solve(BeamGraph graph, BeamSettings settings)
        {
            List<BeamNode> order = TopologicalOrder(graph);

            // When several beams feed one node, its demand is shared between them. If one of them can't deliver
            // its share (its emitter is capped, or it shares a splitter with other demand), the next pass caps it
            // at what it actually delivered and asks the others for the rest.
            var caps = new Dictionary<BeamSegment, float>();
            for (int pass = 0; pass < MaxPasses; pass++)
            {
                RunPasses(graph, settings, order, caps);
                if (!UpdateCaps(graph, caps))
                {
                    break;
                }
            }
        }

        private static void RunPasses(BeamGraph graph, BeamSettings settings, List<BeamNode> order, Dictionary<BeamSegment, float> caps)
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

            // ---- Demand pass: sinks first ----
            var feeders = new List<BeamSegment>();
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

                feeders.Clear();
                foreach (BeamSegment s in graph.IncomingOf(n))
                {
                    if (!s.IsCycle) feeders.Add(s);
                }
                if (feeders.Count == 0 || required <= 0f)
                {
                    continue;
                }
                ShareRequirement(feeders, required, caps);
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
                if (n.Kind == NodeKind.Capacitor)
                {
                    available = CapacitorOutput(graph, n, settings);
                }
                Distribute(graph, n, available);
            }
        }

        /// <summary>
        /// Splits what a node needs between the beams feeding it: equal shares, except that a beam known to
        /// deliver less gets only what it can, and the rest goes to the others ("water filling").
        /// </summary>
        private static void ShareRequirement(List<BeamSegment> feeders, float required, Dictionary<BeamSegment, float> caps)
        {
            var open = new List<BeamSegment>(feeders);
            float remaining = required;
            bool changed = true;
            while (changed && open.Count > 0)
            {
                changed = false;
                float share = remaining / open.Count;
                for (int i = open.Count - 1; i >= 0; i--)
                {
                    BeamSegment s = open[i];
                    if (caps.TryGetValue(s, out float cap) && cap < share)
                    {
                        s.RequiredWatts = cap / Math.Max(s.Transmittance, Epsilon);
                        remaining -= cap;
                        open.RemoveAt(i);
                        changed = true;
                    }
                }
            }
            if (open.Count == 0) return;
            float each = Math.Max(0f, remaining) / open.Count;
            foreach (BeamSegment s in open)
            {
                s.RequiredWatts = each / Math.Max(s.Transmittance, Epsilon);
            }
        }

        /// <summary>
        /// Caps every feeding beam that delivered less than it was asked for, at what it delivered. Only matters for
        /// nodes with more than one feeder. Returns true if any cap is new, so another pass is worthwhile.
        /// </summary>
        private static bool UpdateCaps(BeamGraph graph, Dictionary<BeamSegment, float> caps)
        {
            bool any = false;
            foreach (BeamNode n in graph.Nodes)
            {
                IReadOnlyList<BeamSegment> incoming = graph.IncomingOf(n);
                if (incoming.Count < 2) continue;
                foreach (BeamSegment s in incoming)
                {
                    if (s.IsCycle) continue;
                    float asked = s.RequiredWatts * s.Transmittance;
                    float arrived = s.Watts * s.Transmittance;
                    if (arrived < asked - 0.01f && (!caps.TryGetValue(s, out float old) || arrived < old - 0.01f))
                    {
                        caps[s] = arrived;
                        any = true;
                    }
                }
            }
            return any;
        }

        /// <summary>
        /// A capacitor sends downstream what is needed, using its input first and storage for any shortfall.
        /// Whatever input is left over charges it. Sets <see cref="BeamNode.StoredDeltaWatts"/>.
        /// </summary>
        private static float CapacitorOutput(BeamGraph graph, BeamNode n, BeamSettings settings)
        {
            if (!n.Operational)
            {
                return 0f;
            }
            float inflow = n.InputWatts * n.Efficiency * (1f - settings.NodeLoss);
            float need = SumRequired(graph, n);
            float discharge = n.HasCharge ? n.MaxDischarge : 0f;
            float output = Math.Min(need, inflow + discharge);
            float surplus = inflow - output;

            if (surplus >= 0f)
            {
                n.StoredDeltaWatts = n.HasRoom ? Math.Min(surplus * n.ChargeEfficiency, n.MaxCharge) : 0f;
            }
            else
            {
                n.StoredDeltaWatts = surplus; // drawing on storage
            }
            return output;
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
                case NodeKind.Prism:
                {
                    float need = SumRequired(graph, n);
                    return need / (eff * (1f - settings.NodeLoss));
                }

                case NodeKind.Shutter:
                    return n.Closed ? 0f : SumRequired(graph, n) / eff;

                case NodeKind.Capacitor:
                {
                    // Ask for what downstream needs plus enough to charge at full rate while there is room.
                    float need = SumRequired(graph, n);
                    float charge = n.HasRoom ? n.MaxCharge / Math.Max(n.ChargeEfficiency, Epsilon) : 0f;
                    return (need + charge) / (eff * (1f - settings.NodeLoss));
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
                case NodeKind.Prism:
                    return n.InputWatts * n.Efficiency * (1f - settings.NodeLoss);
                case NodeKind.Shutter:
                    return n.Closed ? 0f : n.InputWatts * n.Efficiency;
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
