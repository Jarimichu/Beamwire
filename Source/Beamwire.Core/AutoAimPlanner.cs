using System.Collections.Generic;
using System.Linq;

namespace Beamwire.Core
{
    /// <summary>The planner's answer: a facing for every auto-aim node, and which receivers it could reach.</summary>
    public sealed class AimPlan
    {
        /// <summary>The chosen facing for every auto-aim node (unchanged ones included), keyed by node Id.</summary>
        public Dictionary<int, Dir8> Facings { get; } = new Dictionary<int, Dir8>();

        public HashSet<int> ReachedReceivers { get; } = new HashSet<int>();
        public HashSet<int> UnreachedReceivers { get; } = new HashSet<int>();

        /// <summary>
        /// Every cell a possible beam could cross or be stopped by. If something is built or removed in one of
        /// these cells, the plan may change.
        /// </summary>
        public HashSet<GridPos> WatchedCells { get; } = new HashSet<GridPos>();
    }

    /// <summary>
    /// Aims auto-aim nodes so that beams reach as many receivers as possible, using as few bounces as possible
    /// and then the shortest paths.
    /// <para>
    /// The planner works greedily, one receiver at a time. Each round runs a Dijkstra search from every emitter
    /// over "a beam arrives at node X travelling in direction d" states. Manual nodes redirect beams exactly as
    /// their facing dictates. Auto nodes may take any of their 8 facings, as long as every promise made by earlier
    /// rounds still holds: each committed route records "a beam arriving in direction a must leave in direction b"
    /// at every node it passes. So a splitter aimed for one receiver can still be re-aimed to also feed a second
    /// one. The cheapest reachable receiver is connected by recording its route's promises, then the next round
    /// starts. Earlier routes never break: optics treat every beam independently, so extra beams only add power.
    /// </para>
    /// <para>
    /// Choosing a node's current facing costs slightly less, so re-planning an unchanged network leaves every
    /// node where it is.
    /// </para>
    /// </summary>
    public static class AutoAimPlanner
    {
        private const float NodeCost = 1000f;     // one bounce or relay outweighs any path length
        private const float KeepBonus = 0.5f;     // prefer a node's current facing on ties

        private sealed class Ray
        {
            public BeamNode To;
            public float Length;
        }

        private sealed class Arrival
        {
            public BeamNode Node;
            public Dir8 Travel;
            public float RangeLeft;
            public float Cost;
            public Arrival Parent;
            public BeamNode ChoiceNode; // the auto node whose facing this step chose (null for manual nodes)
            public Dir8 ChoiceFacing;
            public bool HasEntry;       // false for an emitter's own beam
            public Dir8 ChoiceEntry;    // the direction the beam arrived at ChoiceNode
        }

        /// <summary>What earlier rounds promised about an auto node.</summary>
        private sealed class Commitment
        {
            public Dir8 Facing;
            public readonly List<KeyValuePair<Dir8, Dir8>> Routes = new List<KeyValuePair<Dir8, Dir8>>(); // entry -> exit
            public Dir8? EmitterOut;
        }

        /// <summary>How many extra attempts the planner makes when its first plan leaves receivers dark.</summary>
        private const int MaxRetries = 6;

        public static AimPlan Plan(IBeamGrid grid, IEnumerable<BeamNode> nodeSource, BeamSettings settings)
        {
            var nodes = new List<BeamNode>();
            var byPos = new Dictionary<GridPos, BeamNode>();
            foreach (BeamNode n in nodeSource)
            {
                if (byPos.ContainsKey(n.Pos)) continue;
                byPos[n.Pos] = n;
                nodes.Add(n);
            }
            nodes.Sort((a, b) => a.Id.CompareTo(b.Id));

            var watched = new HashSet<GridPos>();
            Dictionary<BeamNode, Ray[]> rays = CastAllRays(grid, nodes, byPos, settings, watched);

            // Greedy planning can paint itself into a corner (for example by spending an emitter's only beam on
            // the nearest receiver). If receivers are left dark, retry with those receivers served first, and
            // keep the attempt that reaches the most receivers, then the one with the lowest total cost.
            AimPlan best = Attempt(nodes, rays, settings, new List<int>(), out float bestCost);
            var tried = new HashSet<string>();
            for (int i = 0; i < MaxRetries && best.UnreachedReceivers.Count > 0; i++)
            {
                var priority = new List<int>(best.UnreachedReceivers);
                priority.Sort();
                if (i > 0 && priority.Count > 1)
                {
                    // Rotate the order on later attempts, so different receivers get first pick.
                    int shift = i % priority.Count;
                    priority = priority.GetRange(shift, priority.Count - shift).Concat(priority.GetRange(0, shift)).ToList();
                }
                if (!tried.Add(string.Join(",", priority))) break;

                AimPlan candidate = Attempt(nodes, rays, settings, priority, out float cost);
                if (candidate.ReachedReceivers.Count > best.ReachedReceivers.Count ||
                    (candidate.ReachedReceivers.Count == best.ReachedReceivers.Count && cost < bestCost - 1e-3f))
                {
                    best = candidate;
                    bestCost = cost;
                }
                else
                {
                    break;
                }
            }

            foreach (GridPos p in watched) best.WatchedCells.Add(p);
            return best;
        }

        private static AimPlan Attempt(
            List<BeamNode> nodes,
            Dictionary<BeamNode, Ray[]> rays,
            BeamSettings settings,
            List<int> priority,
            out float totalCost)
        {
            var plan = new AimPlan();
            totalCost = 0f;

            // Manual (and broken) nodes never turn. Auto nodes collect promises as routes are committed.
            var fixedFacing = new Dictionary<BeamNode, Dir8>();
            foreach (BeamNode n in nodes)
            {
                if (!IsAuto(n)) fixedFacing[n] = n.Facing;
            }
            var commits = new Dictionary<BeamNode, Commitment>();

            var targets = new HashSet<BeamNode>();
            var byId = new Dictionary<int, BeamNode>();
            foreach (BeamNode n in nodes)
            {
                byId[n.Id] = n;
                if (n.Kind == NodeKind.Receiver && n.Operational) targets.Add(n);
            }

            // Priority receivers are connected one at a time, before the rest.
            var queue = new Queue<int>(priority);
            var single = new HashSet<BeamNode>();
            while (targets.Count > 0)
            {
                HashSet<BeamNode> searchFor = targets;
                while (queue.Count > 0)
                {
                    int id = queue.Dequeue();
                    if (byId.TryGetValue(id, out BeamNode r) && targets.Contains(r))
                    {
                        single.Clear();
                        single.Add(r);
                        searchFor = single;
                        break;
                    }
                }

                Arrival found = Search(nodes, rays, fixedFacing, commits, searchFor, settings);
                if (found == null)
                {
                    if (searchFor == targets) break;
                    foreach (BeamNode r in searchFor)
                    {
                        targets.Remove(r);
                        plan.UnreachedReceivers.Add(r.Id);
                    }
                    continue;
                }
                targets.Remove(found.Node);

                if (TryCommit(found, commits))
                {
                    plan.ReachedReceivers.Add(found.Node.Id);
                    totalCost += found.Cost;
                }
                else
                {
                    plan.UnreachedReceivers.Add(found.Node.Id);
                }
            }

            foreach (BeamNode t in targets) plan.UnreachedReceivers.Add(t.Id);

            // Every auto emitter that no route used joins in: it is aimed at the cheapest receiver it can reach,
            // so several emitters can share one receiver's load instead of sitting idle.
            var allReceivers = new HashSet<BeamNode>();
            foreach (BeamNode n in nodes)
            {
                if (n.Kind == NodeKind.Receiver && n.Operational) allReceivers.Add(n);
            }
            foreach (BeamNode e in nodes)
            {
                if (e.Kind != NodeKind.Emitter || !IsAuto(e) || commits.ContainsKey(e)) continue;
                Arrival found = Search(nodes, rays, fixedFacing, commits, allReceivers, settings, e);
                if (found != null && TryCommit(found, commits))
                {
                    totalCost += found.Cost;
                }
            }

            foreach (BeamNode n in nodes)
            {
                if (IsAuto(n))
                {
                    plan.Facings[n.Id] = commits.TryGetValue(n, out Commitment c) ? c.Facing : n.Facing;
                }
            }
            return plan;
        }

        /// <summary>Nodes the planner may turn. Receivers, prisms and shutters work the same at any facing.</summary>
        private static bool IsAuto(BeamNode n) =>
            n.AutoAim && n.Operational &&
            n.Kind != NodeKind.Receiver && n.Kind != NodeKind.Prism && n.Kind != NodeKind.Shutter;

        private static Arrival Search(
            List<BeamNode> nodes,
            Dictionary<BeamNode, Ray[]> rays,
            Dictionary<BeamNode, Dir8> fixedFacing,
            Dictionary<BeamNode, Commitment> commits,
            HashSet<BeamNode> targets,
            BeamSettings settings,
            BeamNode onlySource = null)
        {
            var heap = new MinHeap<Arrival>();
            var settled = new HashSet<long>();
            var outputs = new List<Dir8>(2);

            // Seeds: every working emitter, plus charged capacitors (which cost a bounce, so emitter routes win).
            // Manual or already-committed sources fire one way; free auto sources may fire any way.
            foreach (BeamNode e in nodes)
            {
                bool emitter = e.Kind == NodeKind.Emitter;
                bool charged = e.Kind == NodeKind.Capacitor && e.HasCharge;
                if (!(emitter || charged) || !e.Operational) continue;
                if (onlySource != null && e != onlySource) continue;
                float baseCost = emitter ? 0f : NodeCost;
                if (fixedFacing.TryGetValue(e, out Dir8 f))
                {
                    Launch(heap, rays, null, e, f, settings.MaxRange, baseCost, null, default, false, default);
                }
                else if (commits.TryGetValue(e, out Commitment c))
                {
                    // Already aimed by an earlier route: it fires that way.
                    Launch(heap, rays, null, e, c.Facing, settings.MaxRange, baseCost, e, c.Facing, false, default);
                }
                else
                {
                    for (int d = 0; d < 8; d++)
                    {
                        float bonus = (Dir8)d == e.Facing ? -KeepBonus : 0f;
                        Launch(heap, rays, null, e, (Dir8)d, settings.MaxRange, baseCost + bonus, e, (Dir8)d, false, default);
                    }
                }
            }

            while (heap.Count > 0)
            {
                Arrival a = heap.Pop();
                long key = ((long)a.Node.Id << 3) | (long)a.Travel;
                if (!settled.Add(key)) continue;

                BeamNode n = a.Node;
                if (n.Kind == NodeKind.Receiver)
                {
                    if (targets.Contains(n)) return a;
                    continue;
                }
                if (!n.Operational || n.Kind == NodeKind.Emitter)
                {
                    continue; // absorbed
                }

                float range = n.ResetsRange ? settings.MaxRange : a.RangeLeft;

                if (fixedFacing.TryGetValue(n, out Dir8 fixedF))
                {
                    BeamOptics.Outputs(WithFacing(n, fixedF), a.Travel, outputs);
                    foreach (Dir8 o in outputs)
                    {
                        Launch(heap, rays, a, n, o, range, a.Cost + NodeCost, null, default, false, default);
                    }
                    continue;
                }

                // An auto mirror, splitter or relay: try every facing that keeps earlier promises.
                commits.TryGetValue(n, out Commitment promised);
                Dir8 preferred = promised != null ? promised.Facing : n.Facing;
                for (int fi = 0; fi < 8; fi++)
                {
                    var f = (Dir8)fi;
                    if (promised != null && !Keeps(n, f, promised)) continue;
                    BeamOptics.Outputs(WithFacing(n, f), a.Travel, outputs);
                    float bonus = f == preferred ? -KeepBonus : 0f;
                    foreach (Dir8 o in outputs)
                    {
                        Launch(heap, rays, a, n, o, range, a.Cost + NodeCost + bonus, n, f, true, a.Travel);
                    }
                }
            }
            return null;
        }

        private static void Launch(
            MinHeap<Arrival> heap,
            Dictionary<BeamNode, Ray[]> rays,
            Arrival parent,
            BeamNode from,
            Dir8 dir,
            float rangeLeft,
            float cost,
            BeamNode choiceNode,
            Dir8 choiceFacing,
            bool hasEntry,
            Dir8 entry)
        {
            Ray r = rays[from][(int)dir];
            if (r.To == null || r.Length > rangeLeft + 1e-3f)
            {
                return;
            }
            var next = new Arrival
            {
                Node = r.To,
                Travel = dir,
                RangeLeft = rangeLeft - r.Length,
                Cost = cost + r.Length,
                Parent = parent,
                ChoiceNode = choiceNode,
                ChoiceFacing = choiceFacing,
                HasEntry = hasEntry,
                ChoiceEntry = entry,
            };
            heap.Push(next, next.Cost);
        }

        /// <summary>True when facing <paramref name="f"/> still sends every promised beam where it was promised to go.</summary>
        private static bool Keeps(BeamNode n, Dir8 f, Commitment c)
        {
            var outs = new List<Dir8>(2);
            BeamNode probe = WithFacing(n, f);
            foreach (var route in c.Routes)
            {
                BeamOptics.Outputs(probe, route.Key, outs);
                if (!outs.Contains(route.Value)) return false;
            }
            return true;
        }

        /// <summary>
        /// Walks a found route back to its emitter and records its promises. Fails without changing anything if
        /// the route needs one node to face two ways.
        /// </summary>
        private static bool TryCommit(Arrival end, Dictionary<BeamNode, Commitment> commits)
        {
            var steps = new List<Arrival>();
            var facing = new Dictionary<BeamNode, Dir8>();
            for (Arrival a = end; a != null; a = a.Parent)
            {
                if (a.ChoiceNode == null) continue;
                if (facing.TryGetValue(a.ChoiceNode, out Dir8 f) && f != a.ChoiceFacing) return false;
                facing[a.ChoiceNode] = a.ChoiceFacing;
                steps.Add(a);
            }

            foreach (Arrival a in steps)
            {
                if (!commits.TryGetValue(a.ChoiceNode, out Commitment c))
                {
                    commits[a.ChoiceNode] = c = new Commitment();
                }
                c.Facing = a.ChoiceFacing;
                if (a.HasEntry)
                {
                    c.Routes.Add(new KeyValuePair<Dir8, Dir8>(a.ChoiceEntry, a.Travel));
                }
                else
                {
                    c.EmitterOut = a.Travel;
                }
            }
            return true;
        }

        // Shutters are always planned as open, so a shutter closing for a passing colonist doesn't re-aim anything.
        private static BeamNode WithFacing(BeamNode n, Dir8 f) =>
            new BeamNode(n.Id, n.Kind, n.Pos, f) { Operational = n.Operational };

        /// <summary>For every node and direction, the first node a beam would hit and how far away it is.</summary>
        private static Dictionary<BeamNode, Ray[]> CastAllRays(
            IBeamGrid grid,
            List<BeamNode> nodes,
            Dictionary<GridPos, BeamNode> byPos,
            BeamSettings settings,
            HashSet<GridPos> watched)
        {
            var result = new Dictionary<BeamNode, Ray[]>();
            foreach (BeamNode n in nodes)
            {
                var arr = new Ray[8];
                for (int d = 0; d < 8; d++)
                {
                    arr[d] = Cast(grid, byPos, n, (Dir8)d, settings.MaxRange, watched);
                }
                result[n] = arr;
            }
            return result;
        }

        private static Ray Cast(IBeamGrid grid, Dictionary<GridPos, BeamNode> byPos, BeamNode from, Dir8 dir, float maxRange, HashSet<GridPos> watched)
        {
            var ray = new Ray();
            float step = dir.StepLength();
            bool diagonal = dir.IsDiagonal();
            GridPos off = dir.Offset();
            GridPos pos = from.Pos;
            float travelled = 0f;

            while (travelled + step <= maxRange + 1e-3f)
            {
                GridPos prev = pos;
                pos = pos.Step(dir);
                if (!grid.InBounds(pos)) break;

                if (diagonal)
                {
                    var sideA = new GridPos(prev.X + off.X, prev.Z);
                    var sideB = new GridPos(prev.X, prev.Z + off.Z);
                    watched.Add(sideA);
                    watched.Add(sideB);
                    if (grid.IsBlocked(sideA) && grid.IsBlocked(sideB)) break;
                }

                travelled += step;
                watched.Add(pos);
                if (byPos.TryGetValue(pos, out BeamNode hit))
                {
                    ray.To = hit;
                    ray.Length = travelled;
                    return ray;
                }
                if (grid.IsBlocked(pos)) break;
            }
            return ray;
        }
    }

    /// <summary>Small binary min-heap (netstandard2.0 has no PriorityQueue). Ties pop in insertion order.</summary>
    internal sealed class MinHeap<T>
    {
        private readonly List<(T item, float key, long seq)> items = new List<(T, float, long)>();
        private long seq;

        public int Count => items.Count;

        public void Push(T item, float key)
        {
            items.Add((item, key, seq++));
            int i = items.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (!Less(i, p)) break;
                Swap(i, p);
                i = p;
            }
        }

        public T Pop()
        {
            T top = items[0].item;
            int last = items.Count - 1;
            items[0] = items[last];
            items.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < items.Count && Less(l, m)) m = l;
                if (r < items.Count && Less(r, m)) m = r;
                if (m == i) break;
                Swap(i, m);
                i = m;
            }
            return top;
        }

        private bool Less(int a, int b) =>
            items[a].key < items[b].key || (items[a].key == items[b].key && items[a].seq < items[b].seq);

        private void Swap(int a, int b)
        {
            var t = items[a];
            items[a] = items[b];
            items[b] = t;
        }
    }
}
