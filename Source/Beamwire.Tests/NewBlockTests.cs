using System.Collections.Generic;
using System.Linq;
using Beamwire.Core;
using Xunit;

namespace Beamwire.Tests
{
    public class NewBlockTests
    {
        private static readonly BeamSettings Lossless = new BeamSettings { NodeLoss = 0f, LossPerTile = 0f, MaxRange = 60f };

        private static BeamNode N(int id, NodeKind kind, int x, int z, Dir8 facing = Dir8.North, float demand = 0f) =>
            new BeamNode(id, kind, new GridPos(x, z), facing) { MaxThroughput = 10000f, Demand = demand };

        private static BeamNode Capacitor(int id, int x, int z, Dir8 facing, float stored) =>
            new BeamNode(id, NodeKind.Capacitor, new GridPos(x, z), facing)
            {
                Stored = stored, Capacity = 600f, MaxCharge = 1000f, MaxDischarge = 1000f, ChargeEfficiency = 0.85f,
            };

        private static BeamGraph Run(BeamSettings s, params BeamNode[] nodes)
        {
            BeamGraph g = BeamTracer.Trace(new TestGrid(), nodes, s);
            PowerSolver.Solve(g, s);
            return g;
        }

        // ---------------- prism

        [Fact]
        public void Prism_SendsStraightAndBothSides()
        {
            var r = new List<Dir8>();
            BeamOptics.Outputs(N(1, NodeKind.Prism, 0, 0), Dir8.East, r);
            Assert.Equal(new[] { Dir8.East, Dir8.South, Dir8.North }.OrderBy(d => d), r.OrderBy(d => d));

            BeamOptics.Outputs(N(1, NodeKind.Prism, 0, 0), Dir8.NorthEast, r);
            Assert.Equal(new[] { Dir8.NorthEast, Dir8.SouthEast, Dir8.NorthWest }.OrderBy(d => d), r.OrderBy(d => d));
        }

        [Fact]
        public void Prism_SharesThreeWaysByDemand()
        {
            var e = N(1, NodeKind.Emitter, 0, 5, Dir8.East);
            var p = N(2, NodeKind.Prism, 5, 5);
            var east = N(3, NodeKind.Receiver, 9, 5, demand: 300f);
            var north = N(4, NodeKind.Receiver, 5, 9, demand: 200f);
            var south = N(5, NodeKind.Receiver, 5, 1, demand: 100f);
            Run(Lossless, e, p, east, north, south);

            Assert.Equal(600f, e.DrawWatts, 2);
            Assert.Equal(300f, east.DeliveredWatts, 2);
            Assert.Equal(200f, north.DeliveredWatts, 2);
            Assert.Equal(100f, south.DeliveredWatts, 2);
        }

        [Fact]
        public void Prism_FeedsBothSides_WhereASplitterCannot()
        {
            // The playground's "S3 can't reach R1" case: receivers on both sides of the beam.
            var nodes = new[]
            {
                N(1, NodeKind.Emitter, 0, 5),
                N(2, NodeKind.Prism, 5, 5),
                N(3, NodeKind.Receiver, 5, 9, demand: 100f),
                N(4, NodeKind.Receiver, 5, 1, demand: 100f),
            };
            foreach (var n in nodes) n.AutoAim = true;
            AimPlan plan = AutoAimPlanner.Plan(new TestGrid(), nodes, Lossless);
            Assert.Contains(3, plan.ReachedReceivers);
            Assert.Contains(4, plan.ReachedReceivers);
            Assert.False(plan.Facings.ContainsKey(2)); // prisms are never turned

            var splitterNodes = new[]
            {
                N(1, NodeKind.Emitter, 0, 5),
                N(2, NodeKind.Splitter, 5, 5),
                N(3, NodeKind.Receiver, 5, 9, demand: 100f),
                N(4, NodeKind.Receiver, 5, 1, demand: 100f),
            };
            foreach (var n in splitterNodes) n.AutoAim = true;
            AimPlan splitterPlan = AutoAimPlanner.Plan(new TestGrid(), splitterNodes, Lossless);
            Assert.Single(splitterPlan.ReachedReceivers);
        }

        // ---------------- shutter

        [Fact]
        public void OpenShutter_IsLossless_ClosedShutterAbsorbs()
        {
            var s = new BeamSettings { NodeLoss = 0.05f, LossPerTile = 0f, MaxRange = 60f };
            var e = N(1, NodeKind.Emitter, 0, 5, Dir8.East);
            var sh = N(2, NodeKind.Shutter, 3, 5);
            var r = N(3, NodeKind.Receiver, 9, 5, demand: 500f);
            Run(s, e, sh, r);
            Assert.Equal(500f, e.DrawWatts, 2);
            Assert.Equal(500f, r.DeliveredWatts, 2);

            sh.Closed = true;
            Run(s, e, sh, r);
            Assert.Equal(0f, e.DrawWatts);
            Assert.Equal(0f, r.DeliveredWatts);
        }

        [Fact]
        public void ClosedShutter_IsStillPlannedAsOpen()
        {
            var e = N(1, NodeKind.Emitter, 0, 5);
            e.AutoAim = true;
            var sh = N(2, NodeKind.Shutter, 3, 5);
            sh.Closed = true;
            var r = N(3, NodeKind.Receiver, 9, 5, demand: 100f);
            AimPlan plan = AutoAimPlanner.Plan(new TestGrid(), new[] { e, sh, r }, Lossless);
            Assert.Equal(Dir8.East, plan.Facings[1]);
            Assert.Contains(3, plan.ReachedReceivers);
        }

        // ---------------- capacitor

        [Fact]
        public void Capacitor_PassesPowerAndChargesFromSurplus()
        {
            var e = N(1, NodeKind.Emitter, 0, 5, Dir8.East);
            var c = Capacitor(2, 4, 5, Dir8.East, stored: 0f);
            var r = N(3, NodeKind.Receiver, 9, 5, demand: 300f);
            Run(Lossless, e, c, r);

            // It asks for the receiver's 300 W plus enough to charge at its full 1,000 W.
            Assert.Equal(300f + 1000f / 0.85f, e.DrawWatts, 1);
            Assert.Equal(300f, r.DeliveredWatts, 1);
            Assert.Equal(1000f, c.StoredDeltaWatts, 1);
        }

        [Fact]
        public void Capacitor_WhenFull_OnlyPassesThrough()
        {
            var e = N(1, NodeKind.Emitter, 0, 5, Dir8.East);
            var c = Capacitor(2, 4, 5, Dir8.East, stored: 600f);
            var r = N(3, NodeKind.Receiver, 9, 5, demand: 300f);
            Run(Lossless, e, c, r);
            Assert.Equal(300f, e.DrawWatts, 1);
            Assert.Equal(0f, c.StoredDeltaWatts, 1);
        }

        [Fact]
        public void Capacitor_CoversShortfallFromStorage()
        {
            var e = N(1, NodeKind.Emitter, 0, 5, Dir8.East);
            e.MaxThroughput = 200f;
            var c = Capacitor(2, 4, 5, Dir8.East, stored: 300f);
            var r = N(3, NodeKind.Receiver, 9, 5, demand: 500f);
            Run(Lossless, e, c, r);
            Assert.Equal(500f, r.DeliveredWatts, 1);
            Assert.Equal(-300f, c.StoredDeltaWatts, 1);
        }

        [Fact]
        public void Capacitor_PowersReceiverWithNoInput_UpToItsRate()
        {
            var c = Capacitor(1, 0, 5, Dir8.East, stored: 300f);
            var r = N(2, NodeKind.Receiver, 9, 5, demand: 1500f);
            Run(Lossless, c, r);
            Assert.Equal(1000f, r.DeliveredWatts, 1);
            Assert.Equal(-1000f, c.StoredDeltaWatts, 1);
        }

        [Fact]
        public void EmptyCapacitor_WithNoInput_FiresNothing()
        {
            var c = Capacitor(1, 0, 5, Dir8.East, stored: 0f);
            var r = N(2, NodeKind.Receiver, 9, 5, demand: 100f);
            BeamGraph g = Run(Lossless, c, r);
            Assert.Empty(g.Segments);
            Assert.Equal(0f, r.DeliveredWatts);
        }

        [Fact]
        public void Capacitor_ResetsRange()
        {
            var s = new BeamSettings { NodeLoss = 0f, LossPerTile = 0f, MaxRange = 6f };
            var e = N(1, NodeKind.Emitter, 0, 5, Dir8.East);
            var c = Capacitor(2, 5, 5, Dir8.East, stored: 600f);
            var r = N(3, NodeKind.Receiver, 10, 5, demand: 100f);
            Run(s, e, c, r);
            Assert.Equal(100f, r.DeliveredWatts, 1);
        }

        [Fact]
        public void Planner_AimsAChargedCapacitorAtAReceiver()
        {
            var c = Capacitor(1, 0, 0, Dir8.North, stored: 300f);
            c.AutoAim = true;
            var r = N(2, NodeKind.Receiver, 6, 6, demand: 100f);
            AimPlan plan = AutoAimPlanner.Plan(new TestGrid(), new[] { c, r }, Lossless);
            Assert.Equal(Dir8.NorthEast, plan.Facings[1]);
            Assert.Contains(2, plan.ReachedReceivers);
        }

        [Fact]
        public void Planner_PrefersEmitterOverCapacitor()
        {
            // Both could reach the receiver directly; the emitter's route should be the one used.
            var e = N(1, NodeKind.Emitter, 0, 5);
            var c = Capacitor(2, 5, 0, Dir8.South, stored: 300f);
            var r = N(3, NodeKind.Receiver, 5, 5, demand: 100f);
            e.AutoAim = c.AutoAim = true;
            AimPlan plan = AutoAimPlanner.Plan(new TestGrid(), new[] { e, c, r }, Lossless);
            Assert.Equal(Dir8.East, plan.Facings[1]);
            Assert.Equal(Dir8.South, plan.Facings[2]); // left where it was
        }
    }
}

namespace Beamwire.Tests
{
    public class SharedReceiverTests
    {
        private static readonly BeamSettings Lossless = new BeamSettings { NodeLoss = 0f, LossPerTile = 0f, MaxRange = 60f };

        private static BeamNode N(int id, NodeKind kind, int x, int z, Dir8 facing = Dir8.North, float demand = 0f, float max = 10000f) =>
            new BeamNode(id, kind, new GridPos(x, z), facing) { MaxThroughput = max, Demand = demand, AutoAim = true };

        private static (AimPlan, BeamGraph) PlanAndSolve(params BeamNode[] nodes)
        {
            AimPlan plan = AutoAimPlanner.Plan(new TestGrid(), nodes, Lossless);
            var applied = new System.Collections.Generic.List<BeamNode>();
            foreach (BeamNode n in nodes)
            {
                Dir8 f = plan.Facings.TryGetValue(n.Id, out Dir8 pf) ? pf : n.Facing;
                applied.Add(new BeamNode(n.Id, n.Kind, n.Pos, f) { MaxThroughput = n.MaxThroughput, Demand = n.Demand });
            }
            BeamGraph g = BeamTracer.Trace(new TestGrid(), applied, Lossless);
            PowerSolver.Solve(g, Lossless);
            return (plan, g);
        }

        private static BeamNode Get(BeamGraph g, int id) => System.Linq.Enumerable.First(g.Nodes, n => n.Id == id);

        [Fact]
        public void TwoEmitters_BothAimAtTheOnlyReceiver()
        {
            var (plan, g) = PlanAndSolve(
                N(1, NodeKind.Emitter, 0, 5),
                N(2, NodeKind.Emitter, 5, 0),
                N(3, NodeKind.Receiver, 5, 5, demand: 800f));

            Assert.Equal(Dir8.East, plan.Facings[1]);
            Assert.Equal(Dir8.North, plan.Facings[2]);
            Assert.Equal(2, g.IncomingOf(Get(g, 3)).Count);
            Assert.Equal(800f, Get(g, 3).DeliveredWatts, 1);
            Assert.Equal(400f, Get(g, 1).DrawWatts, 1);
            Assert.Equal(400f, Get(g, 2).DrawWatts, 1);
        }

        [Fact]
        public void CappedEmitter_TheOtherCoversTheShortfall()
        {
            var (_, g) = PlanAndSolve(
                N(1, NodeKind.Emitter, 0, 5, max: 300f),
                N(2, NodeKind.Emitter, 5, 0, max: 1000f),
                N(3, NodeKind.Receiver, 5, 5, demand: 800f));

            Assert.Equal(800f, Get(g, 3).DeliveredWatts, 1);
            Assert.Equal(300f, Get(g, 1).DrawWatts, 1);
            Assert.Equal(500f, Get(g, 2).DrawWatts, 1);
        }

        [Fact]
        public void BothCapped_DeliverTheirSum()
        {
            var (_, g) = PlanAndSolve(
                N(1, NodeKind.Emitter, 0, 5, max: 300f),
                N(2, NodeKind.Emitter, 5, 0, max: 250f),
                N(3, NodeKind.Receiver, 5, 5, demand: 800f));
            Assert.Equal(550f, Get(g, 3).DeliveredWatts, 1);
        }

        [Fact]
        public void SpareEmitter_JoinsTheCheapestReceiver_EvenWhenEveryReceiverIsAlreadyReached()
        {
            var (plan, g) = PlanAndSolve(
                N(1, NodeKind.Emitter, 0, 0),
                N(2, NodeKind.Receiver, 6, 0, demand: 100f),
                N(3, NodeKind.Emitter, 6, 9));
            Assert.Equal(Dir8.South, plan.Facings[3]);
            Assert.Equal(2, g.IncomingOf(Get(g, 2)).Count);
        }
    }
}
