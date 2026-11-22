using System.Collections.Generic;
using System.Linq;
using Beamwire.Core;
using Xunit;

namespace Beamwire.Tests
{
    public class AutoAimTests
    {
        private static readonly BeamSettings Lossless = new BeamSettings { NodeLoss = 0f, LossPerTile = 0f, MaxRange = 60f };

        private static BeamNode Node(int id, NodeKind kind, int x, int z, Dir8 facing = Dir8.North, bool auto = true) =>
            new BeamNode(id, kind, new GridPos(x, z), facing)
            {
                AutoAim = auto,
                MaxThroughput = 10000f,
                Demand = kind == NodeKind.Receiver ? 100f : 0f,
                Upkeep = kind == NodeKind.Relay ? 10f : 0f,
            };

        /// <summary>Plans, applies the plan, then traces and solves the result. This is exactly what the game does.</summary>
        private static (AimPlan plan, List<BeamNode> applied) PlanAndApply(TestGrid grid, BeamSettings s, params BeamNode[] nodes)
        {
            AimPlan plan = AutoAimPlanner.Plan(grid, nodes, s);
            var applied = nodes.Select(n =>
            {
                Dir8 f = plan.Facings.TryGetValue(n.Id, out Dir8 pf) ? pf : n.Facing;
                return new BeamNode(n.Id, n.Kind, n.Pos, f)
                {
                    AutoAim = n.AutoAim,
                    Operational = n.Operational,
                    MaxThroughput = n.MaxThroughput,
                    Demand = n.Demand,
                    Upkeep = n.Upkeep,
                };
            }).ToList();
            BeamGraph g = BeamTracer.Trace(grid, applied, s);
            PowerSolver.Solve(g, s);
            return (plan, applied);
        }

        private static BeamNode Get(List<BeamNode> nodes, int id) => nodes.First(n => n.Id == id);

        [Fact]
        public void Emitter_TurnsToFaceReceiver()
        {
            var (plan, applied) = PlanAndApply(new TestGrid(), Lossless,
                Node(1, NodeKind.Emitter, 0, 5, Dir8.North),
                Node(2, NodeKind.Receiver, 9, 5));

            Assert.Equal(Dir8.East, plan.Facings[1]);
            Assert.Contains(2, plan.ReachedReceivers);
            Assert.Equal(100f, Get(applied, 2).DeliveredWatts, 2);
        }

        [Fact]
        public void Emitter_AimsDiagonally()
        {
            var (plan, applied) = PlanAndApply(new TestGrid(), Lossless,
                Node(1, NodeKind.Emitter, 2, 2),
                Node(2, NodeKind.Receiver, 7, 7));
            Assert.Equal(Dir8.NorthEast, plan.Facings[1]);
            Assert.Equal(100f, Get(applied, 2).DeliveredWatts, 2);
        }

        [Fact]
        public void Mirror_IsAimedToTurnTheCorner()
        {
            // The receiver isn't in line with the emitter; the mirror at the corner must be aimed.
            var (plan, applied) = PlanAndApply(new TestGrid(), Lossless,
                Node(1, NodeKind.Emitter, 0, 0),
                Node(2, NodeKind.Mirror, 6, 0),
                Node(3, NodeKind.Receiver, 6, 9));

            Assert.Equal(Dir8.East, plan.Facings[1]);
            Assert.Contains(3, plan.ReachedReceivers);
            Assert.Equal(100f, Get(applied, 3).DeliveredWatts, 2);
        }

        [Fact]
        public void Splitter_ServesTwoReceivers()
        {
            var (plan, applied) = PlanAndApply(new TestGrid(), Lossless,
                Node(1, NodeKind.Emitter, 0, 5),
                Node(2, NodeKind.Splitter, 5, 5),
                Node(3, NodeKind.Receiver, 10, 5),
                Node(4, NodeKind.Receiver, 5, 10));

            Assert.Contains(3, plan.ReachedReceivers);
            Assert.Contains(4, plan.ReachedReceivers);
            Assert.Equal(100f, Get(applied, 3).DeliveredWatts, 2);
            Assert.Equal(100f, Get(applied, 4).DeliveredWatts, 2);
        }

        [Fact]
        public void TwoEmitters_EachTakeAReceiver()
        {
            var (plan, applied) = PlanAndApply(new TestGrid(), Lossless,
                Node(1, NodeKind.Emitter, 0, 0),
                Node(2, NodeKind.Emitter, 0, 10),
                Node(3, NodeKind.Receiver, 0, 5),
                Node(4, NodeKind.Receiver, 8, 10));

            Assert.Contains(3, plan.ReachedReceivers);
            Assert.Contains(4, plan.ReachedReceivers);
            Assert.Equal(100f, Get(applied, 3).DeliveredWatts, 2);
            Assert.Equal(100f, Get(applied, 4).DeliveredWatts, 2);
        }

        [Fact]
        public void PrefersDirectShotOverBounce()
        {
            // Both work: straight East, or North to a mirror and back down. Fewer bounces wins.
            var (plan, _) = PlanAndApply(new TestGrid(), Lossless,
                Node(1, NodeKind.Emitter, 0, 0),
                Node(2, NodeKind.Mirror, 0, 4),
                Node(3, NodeKind.Receiver, 4, 0));
            Assert.Equal(Dir8.East, plan.Facings[1]);
        }

        [Fact]
        public void ManualNode_IsNeverTurned()
        {
            var (plan, applied) = PlanAndApply(new TestGrid(), Lossless,
                Node(1, NodeKind.Emitter, 0, 0),
                Node(2, NodeKind.Mirror, 6, 0, Dir8.South, auto: false), // its back faces the incoming beam
                Node(3, NodeKind.Receiver, 6, 9));

            Assert.False(plan.Facings.ContainsKey(2));
            Assert.Contains(3, plan.UnreachedReceivers);
            Assert.Equal(0f, Get(applied, 3).DeliveredWatts);
        }

        [Fact]
        public void ManualNode_IsUsedWhenItAlreadyPointsTheRightWay()
        {
            // A manual mirror whose face points NW: an eastbound beam turns north.
            var (plan, applied) = PlanAndApply(new TestGrid(), Lossless,
                Node(1, NodeKind.Emitter, 0, 0),
                Node(2, NodeKind.Mirror, 6, 0, Dir8.NorthWest, auto: false),
                Node(3, NodeKind.Receiver, 6, 9));
            Assert.Contains(3, plan.ReachedReceivers);
            Assert.Equal(100f, Get(applied, 3).DeliveredWatts, 2);
        }

        [Fact]
        public void Wall_LeavesReceiverUnreached()
        {
            var grid = new TestGrid();
            grid.Walls.Add(new GridPos(4, 5));
            var (plan, _) = PlanAndApply(grid, Lossless,
                Node(1, NodeKind.Emitter, 0, 5),
                Node(2, NodeKind.Receiver, 9, 5));
            Assert.Contains(2, plan.UnreachedReceivers);
            Assert.Contains(new GridPos(4, 5), plan.WatchedCells);
        }

        [Fact]
        public void Wall_IsRoutedAroundWithMirrors()
        {
            var grid = new TestGrid();
            grid.Walls.Add(new GridPos(5, 5));
            var (plan, applied) = PlanAndApply(grid, Lossless,
                Node(1, NodeKind.Emitter, 0, 5),
                Node(2, NodeKind.Mirror, 2, 5),
                Node(3, NodeKind.Mirror, 2, 8),
                Node(4, NodeKind.Mirror, 8, 8),
                Node(5, NodeKind.Mirror, 8, 5),
                Node(6, NodeKind.Receiver, 10, 5));
            Assert.Contains(6, plan.ReachedReceivers);
            Assert.Equal(100f, Get(applied, 6).DeliveredWatts, 2);
        }

        [Fact]
        public void Range_UsesRelayWhenTooFar()
        {
            var s = new BeamSettings { NodeLoss = 0f, LossPerTile = 0f, MaxRange = 10f };
            var (plan, applied) = PlanAndApply(new TestGrid(), s,
                Node(1, NodeKind.Emitter, 0, 5),
                Node(2, NodeKind.Relay, 8, 5),
                Node(3, NodeKind.Receiver, 16, 5));
            Assert.Equal(Dir8.East, plan.Facings[2]);
            Assert.Contains(3, plan.ReachedReceivers);
            Assert.True(Get(applied, 3).DeliveredWatts > 99f);
        }

        [Fact]
        public void Replanning_IsStable()
        {
            var grid = new TestGrid();
            var nodes = new[]
            {
                Node(1, NodeKind.Emitter, 0, 5),
                Node(2, NodeKind.Splitter, 5, 5),
                Node(3, NodeKind.Receiver, 10, 5),
                Node(4, NodeKind.Receiver, 5, 10),
                Node(5, NodeKind.Mirror, 20, 20),
            };
            var (first, applied) = PlanAndApply(grid, Lossless, nodes);
            foreach (BeamNode n in applied) n.AutoAim = nodes.First(x => x.Id == n.Id).AutoAim;
            AimPlan second = AutoAimPlanner.Plan(grid, applied, Lossless);
            Assert.Equal(first.Facings.OrderBy(k => k.Key), second.Facings.OrderBy(k => k.Key));
        }

        [Fact]
        public void UnusedAutoNode_KeepsItsFacing()
        {
            var (plan, _) = PlanAndApply(new TestGrid(), Lossless,
                Node(1, NodeKind.Emitter, 0, 5, Dir8.East),
                Node(2, NodeKind.Receiver, 9, 5),
                Node(3, NodeKind.Mirror, 30, 30, Dir8.SouthWest));
            Assert.Equal(Dir8.SouthWest, plan.Facings[3]);
        }

        [Fact]
        public void BrokenNode_IsNotPlannedThrough()
        {
            var mirror = Node(2, NodeKind.Mirror, 6, 0);
            mirror.Operational = false;
            var (plan, _) = PlanAndApply(new TestGrid(), Lossless,
                Node(1, NodeKind.Emitter, 0, 0),
                mirror,
                Node(3, NodeKind.Receiver, 6, 9));
            Assert.Contains(3, plan.UnreachedReceivers);
        }

        [Fact]
        public void BigGrid_PlansQuickly()
        {
            // 10 emitters, 30 mirrors and 20 receivers scattered on a 120x120 map.
            var grid = new TestGrid { Width = 120, Height = 120 };
            var rnd = new System.Random(3);
            var used = new HashSet<GridPos>();
            var nodes = new List<BeamNode>();
            int id = 1;
            void Add(NodeKind k)
            {
                GridPos p;
                do { p = new GridPos(rnd.Next(120), rnd.Next(120)); } while (!used.Add(p));
                nodes.Add(Node(id++, k, p.X, p.Z));
            }
            for (int i = 0; i < 10; i++) Add(NodeKind.Emitter);
            for (int i = 0; i < 30; i++) Add(NodeKind.Mirror);
            for (int i = 0; i < 10; i++) Add(NodeKind.Splitter);
            for (int i = 0; i < 20; i++) Add(NodeKind.Receiver);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var (plan, applied) = PlanAndApply(grid, Lossless, nodes.ToArray());
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 500, $"planning took {sw.ElapsedMilliseconds} ms");
            // Every receiver the plan claims to reach really receives power.
            foreach (int r in plan.ReachedReceivers)
            {
                Assert.True(Get(applied, r).InputWatts > 0f, $"receiver {r} was planned but gets no beam");
            }
        }
    
        /// <summary>
        /// A capacitor's charge must not change where auto-aim points things. It used to seed routes when charged,
        /// so a capacitor charging and draining flipped the emitters' aim back and forth. This board did exactly that.
        /// </summary>
        [Fact]
        public void CapacitorCharge_DoesNotChangeThePlan()
        {
            AimPlan PlanWith(float stored)
            {
                var grid = new TestGrid { Width = 10, Height = 10 };
                var nodes = new[]
                {
                    Node(1, NodeKind.Emitter, 7, 3),
                    Node(2, NodeKind.Emitter, 0, 6),
                    new BeamNode(3, NodeKind.Capacitor, new GridPos(7, 6), Dir8.North)
                    {
                        AutoAim = true, Capacity = 60f, Stored = stored, MaxCharge = 1000f, MaxDischarge = 1000f, ChargeEfficiency = 0.85f,
                    },
                    Node(4, NodeKind.Receiver, 2, 8),
                    Node(5, NodeKind.Receiver, 9, 8),
                    Node(6, NodeKind.Mirror, 1, 2),
                };
                return AutoAimPlanner.Plan(grid, nodes, Lossless);
            }

            AimPlan empty = PlanWith(0f), half = PlanWith(30f), full = PlanWith(60f);
            foreach (AimPlan other in new[] { half, full })
            {
                Assert.Equal(empty.Facings.Count, other.Facings.Count);
                foreach (var kv in empty.Facings)
                {
                    Assert.True(other.Facings.TryGetValue(kv.Key, out Dir8 f) && f == kv.Value, $"node {kv.Key} aims differently");
                }
            }
        }
}
}
