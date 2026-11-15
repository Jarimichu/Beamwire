using System.Collections.Generic;
using System.Linq;
using Beamwire.Core;
using Xunit;

namespace Beamwire.Tests
{
    internal sealed class TestGrid : IBeamGrid
    {
        public int Width = 50;
        public int Height = 50;
        public HashSet<GridPos> Walls = new HashSet<GridPos>();
        public Dictionary<GridPos, float> Fog = new Dictionary<GridPos, float>();

        public bool InBounds(GridPos p) => p.X >= 0 && p.Z >= 0 && p.X < Width && p.Z < Height;

        public bool IsBlocked(GridPos p) => Walls.Contains(p);

        public float CellTransmittance(GridPos p) => Fog.TryGetValue(p, out float f) ? f : 1f;
    }

    public class OpticsTests
    {
        private static List<Dir8> Out(NodeKind kind, Dir8 facing, Dir8 travel)
        {
            var result = new List<Dir8>();
            BeamOptics.Outputs(new BeamNode(1, kind, new GridPos(0, 0), facing), travel, result);
            return result;
        }

        [Fact]
        public void Mirror_TurnsStraightBeam90()
        {
            // A mirror whose face points NE works like a "\" pane seen from the north-east.
            Assert.Equal(new[] { Dir8.East }, Out(NodeKind.Mirror, Dir8.NorthEast, Dir8.South));
            Assert.Equal(new[] { Dir8.North }, Out(NodeKind.Mirror, Dir8.NorthEast, Dir8.West));
        }

        [Fact]
        public void Mirror_FacingBeam_ReflectsStraightBack()
        {
            Assert.Equal(new[] { Dir8.West }, Out(NodeKind.Mirror, Dir8.West, Dir8.East));
        }

        [Fact]
        public void Mirror_DiagonalBeam_Turns90()
        {
            // A NE-bound beam off a south-facing mirror (horizontal pane) leaves heading SE.
            Assert.Equal(new[] { Dir8.SouthEast }, Out(NodeKind.Mirror, Dir8.South, Dir8.NorthEast));
        }

        [Fact]
        public void Mirror_AbsorbsOnBack()
        {
            Assert.Empty(Out(NodeKind.Mirror, Dir8.NorthEast, Dir8.North));
            Assert.Empty(Out(NodeKind.Mirror, Dir8.NorthEast, Dir8.East));
        }

        [Fact]
        public void Mirror_GrazingBeam_PassesBy()
        {
            // The pane lies along the N-S line (face points E); a northbound beam runs along it.
            Assert.Equal(new[] { Dir8.North }, Out(NodeKind.Mirror, Dir8.East, Dir8.North));
        }

        [Fact]
        public void Splitter_PassesThroughAndReflects()
        {
            var a = Out(NodeKind.Splitter, Dir8.NorthEast, Dir8.South);
            Assert.Contains(Dir8.South, a);
            Assert.Contains(Dir8.East, a);

            // Its back is also half-silvered.
            var b = Out(NodeKind.Splitter, Dir8.NorthEast, Dir8.North);
            Assert.Contains(Dir8.North, b);
            Assert.Contains(Dir8.West, b);
        }

        [Fact]
        public void Relay_RedirectsToFacing_ButNotFromItsOutput()
        {
            Assert.Equal(new[] { Dir8.East }, Out(NodeKind.Relay, Dir8.East, Dir8.North));
            Assert.Equal(new[] { Dir8.SouthEast }, Out(NodeKind.Relay, Dir8.SouthEast, Dir8.East));
            Assert.Empty(Out(NodeKind.Relay, Dir8.East, Dir8.West));
        }

        [Fact]
        public void BrokenNode_Absorbs()
        {
            var result = new List<Dir8>();
            var n = new BeamNode(1, NodeKind.Splitter, new GridPos(0, 0), Dir8.NorthEast) { Operational = false };
            BeamOptics.Outputs(n, Dir8.South, result);
            Assert.Empty(result);
        }

        [Fact]
        public void EightWayMirror_OnlyTurns90_BouncesBack_OrPassesBy()
        {
            var result = new List<Dir8>();
            for (int f = 0; f < 8; f++)
            {
                for (int d = 0; d < 8; d++)
                {
                    BeamOptics.Outputs(new BeamNode(1, NodeKind.Mirror, new GridPos(0, 0), (Dir8)f), (Dir8)d, result);
                    foreach (Dir8 o in result)
                    {
                        int turn = Dir8Ext.Mod((int)o - d, 8);
                        Assert.Contains(turn, new[] { 0, 2, 4, 6 }); // straight on, 90° either way, or back
                    }
                }
            }
        }

        [Fact]
        public void Mirror_TurnsDiagonalBeamOntoOtherDiagonal()
        {
            // A NE-bound beam off a west-facing mirror (vertical pane) leaves heading NW.
            Assert.Equal(new[] { Dir8.NorthWest }, Out(NodeKind.Mirror, Dir8.West, Dir8.NorthEast));
        }
    }

    public class TracerAndSolverTests
    {
        private static readonly BeamSettings Lossless = new BeamSettings { NodeLoss = 0f, LossPerTile = 0f, MaxRange = 60 };

        private static BeamNode Emitter(int id, int x, int z, Dir8 f, float max = 10000f) =>
            new BeamNode(id, NodeKind.Emitter, new GridPos(x, z), f) { MaxThroughput = max };

        private static BeamNode Receiver(int id, int x, int z, float demand) =>
            new BeamNode(id, NodeKind.Receiver, new GridPos(x, z), Dir8.North) { Demand = demand };

        private static BeamGraph Run(TestGrid grid, BeamSettings s, params BeamNode[] nodes)
        {
            BeamGraph g = BeamTracer.Trace(grid, nodes, s);
            PowerSolver.Solve(g, s);
            return g;
        }

        [Fact]
        public void StraightLine_DeliversDemand()
        {
            var e = Emitter(1, 0, 5, Dir8.East);
            var r = Receiver(2, 10, 5, 500f);
            var g = Run(new TestGrid(), Lossless, e, r);

            Assert.Single(g.Segments);
            Assert.Same(r, g.Segments[0].To);
            Assert.Equal(9, g.Segments[0].Cells.Count);
            Assert.Equal(500f, e.DrawWatts, 2);
            Assert.Equal(500f, r.DeliveredWatts, 2);
        }

        [Fact]
        public void DistanceLoss_IsCompensatedByDrawingMore()
        {
            var s = new BeamSettings { NodeLoss = 0f, LossPerTile = 0.001f, MaxRange = 60 };
            var e = Emitter(1, 0, 5, Dir8.East);
            var r = Receiver(2, 10, 5, 1000f);
            Run(new TestGrid(), s, e, r);

            float expectedTransmit = (float)System.Math.Pow(0.999, 10);
            Assert.Equal(1000f / expectedTransmit, e.DrawWatts, 1);
            Assert.Equal(1000f, r.DeliveredWatts, 1);
        }

        [Fact]
        public void ThroughputCap_LimitsDelivery()
        {
            var e = Emitter(1, 0, 5, Dir8.East, max: 300f);
            var r = Receiver(2, 5, 5, 1000f);
            Run(new TestGrid(), Lossless, e, r);
            Assert.Equal(300f, e.DrawWatts, 2);
            Assert.Equal(300f, r.DeliveredWatts, 2);
        }

        [Fact]
        public void Wall_BlocksBeam()
        {
            var grid = new TestGrid();
            grid.Walls.Add(new GridPos(5, 5));
            var e = Emitter(1, 0, 5, Dir8.East);
            var r = Receiver(2, 10, 5, 500f);
            var g = Run(grid, Lossless, e, r);

            Assert.Equal(SegmentEnd.Blocked, g.Segments[0].End);
            Assert.Equal(new GridPos(4, 5), g.Segments[0].EndPos);
            Assert.Equal(4, g.Segments[0].Cells.Count);
            Assert.Equal(0f, e.DrawWatts);
            Assert.Equal(0f, r.DeliveredWatts);
        }

        [Fact]
        public void Range_LimitsBeam()
        {
            var s = new BeamSettings { NodeLoss = 0f, LossPerTile = 0f, MaxRange = 5f };
            var e = Emitter(1, 0, 5, Dir8.East);
            var r = Receiver(2, 10, 5, 500f);
            var g = Run(new TestGrid(), s, e, r);
            Assert.Equal(SegmentEnd.OutOfRange, g.Segments[0].End);
            Assert.Equal(5, g.Segments[0].Cells.Count);
            Assert.Equal(0f, r.DeliveredWatts);
        }

        [Fact]
        public void MapEdge_StopsBeam()
        {
            var g = Run(new TestGrid { Width = 8 }, Lossless, Emitter(1, 0, 5, Dir8.East));
            Assert.Equal(SegmentEnd.MapEdge, g.Segments[0].End);
            Assert.Equal(7, g.Segments[0].Cells.Count);
        }

        [Fact]
        public void Mirror_BendsBeamAndCostsNodeLoss()
        {
            var s = new BeamSettings { NodeLoss = 0.05f, LossPerTile = 0f, MaxRange = 60 };
            // The emitter fires East into a mirror at (5,5). The beam enters the mirror's West side.
            // A mirror whose face points SW sends it South.
            var e = Emitter(1, 0, 5, Dir8.East);
            var m = new BeamNode(2, NodeKind.Mirror, new GridPos(5, 5), Dir8.SouthWest);
            var r = Receiver(3, 5, 0, 950f);
            var g = Run(new TestGrid(), s, e, m, r);

            Assert.Equal(2, g.Segments.Count);
            Assert.Same(r, g.Segments[1].To);
            Assert.Equal(1000f, e.DrawWatts, 1);
            Assert.Equal(950f, r.DeliveredWatts, 1);
        }

        [Fact]
        public void Splitter_SharesByDemand()
        {
            // The emitter fires East into a splitter at (5,5) whose pane faces NE/SW.
            // The beam passes through East and reflects South.
            var e = Emitter(1, 0, 5, Dir8.East);
            var sp = new BeamNode(2, NodeKind.Splitter, new GridPos(5, 5), Dir8.NorthEast);
            var east = Receiver(3, 10, 5, 300f);
            var south = Receiver(4, 5, 0, 100f);
            Run(new TestGrid(), Lossless, e, sp, east, south);

            Assert.Equal(400f, e.DrawWatts, 2);
            Assert.Equal(300f, east.DeliveredWatts, 2);
            Assert.Equal(100f, south.DeliveredWatts, 2);
        }

        [Fact]
        public void Splitter_WhenCapped_SharesProportionally()
        {
            var e = Emitter(1, 0, 5, Dir8.East, max: 200f);
            var sp = new BeamNode(2, NodeKind.Splitter, new GridPos(5, 5), Dir8.NorthEast);
            var east = Receiver(3, 10, 5, 300f);
            var south = Receiver(4, 5, 0, 100f);
            Run(new TestGrid(), Lossless, e, sp, east, south);

            Assert.Equal(150f, east.DeliveredWatts, 2);
            Assert.Equal(50f, south.DeliveredWatts, 2);
        }

        [Fact]
        public void Relay_ResetsRange_AndConsumesUpkeep()
        {
            var s = new BeamSettings { NodeLoss = 0f, LossPerTile = 0f, MaxRange = 6f };
            var e = Emitter(1, 0, 5, Dir8.East);
            var relay = new BeamNode(2, NodeKind.Relay, new GridPos(5, 5), Dir8.East) { Upkeep = 30f };
            var r = Receiver(3, 10, 5, 500f);
            var g = Run(new TestGrid(), s, e, relay, r);

            Assert.Equal(2, g.Segments.Count);
            Assert.Equal(530f, e.DrawWatts, 2);
            Assert.Equal(500f, r.DeliveredWatts, 2);
        }

        [Fact]
        public void Relay_WithNoDemand_DrawsNothing()
        {
            var e = Emitter(1, 0, 5, Dir8.East);
            var relay = new BeamNode(2, NodeKind.Relay, new GridPos(5, 5), Dir8.East) { Upkeep = 30f };
            Run(new TestGrid(), Lossless, e, relay);
            Assert.Equal(0f, e.DrawWatts);
        }

        [Fact]
        public void MirrorLoop_TerminatesAndDoesNotCrashSolver()
        {
            // Four splitters in a square, so beams can go round and round.
            var e = Emitter(1, 0, 5, Dir8.East);
            var a = new BeamNode(2, NodeKind.Splitter, new GridPos(5, 5), Dir8.NorthEast);
            var b = new BeamNode(3, NodeKind.Splitter, new GridPos(10, 5), Dir8.NorthEast);
            var c = new BeamNode(4, NodeKind.Splitter, new GridPos(10, 0), Dir8.NorthEast);
            var d = new BeamNode(5, NodeKind.Splitter, new GridPos(5, 0), Dir8.NorthEast);
            var r = Receiver(6, 15, 5, 200f);
            var g = Run(new TestGrid(), Lossless, e, a, b, c, d, r);

            Assert.True(g.Segments.Count < 40);
            Assert.True(r.DeliveredWatts > 0f);
            Assert.True(r.DeliveredWatts <= 200f + 0.01f);
        }

        [Fact]
        public void Fog_ReducesTransmittance()
        {
            var grid = new TestGrid();
            for (int x = 1; x < 10; x++) grid.Fog[new GridPos(x, 5)] = 0.99f;
            var e = Emitter(1, 0, 5, Dir8.East);
            var r = Receiver(2, 10, 5, 100f);
            var g = Run(grid, Lossless, e, r);

            Assert.Equal((float)System.Math.Pow(0.99, 9), g.Segments[0].Transmittance, 4);
            Assert.True(e.DrawWatts > 100f);
            Assert.Equal(100f, r.DeliveredWatts, 2);
        }

        [Fact]
        public void TwoEmitters_FeedOneReceiver()
        {
            var e1 = Emitter(1, 0, 5, Dir8.East, max: 100f);
            var e2 = Emitter(2, 5, 0, Dir8.North, max: 1000f);
            // The splitter at (5,5) is fed from the West and from the South.
            var sp = new BeamNode(3, NodeKind.Splitter, new GridPos(5, 5), Dir8.NorthEast);
            var r = Receiver(4, 10, 5, 400f);
            Run(new TestGrid(), Lossless, e1, e2, sp, r);

            Assert.True(r.DeliveredWatts > 0f);
            Assert.True(e1.DrawWatts <= 100f);
        }

        [Fact]
        public void DustyReceiver_NeedsMoreInput()
        {
            var e = Emitter(1, 0, 5, Dir8.East);
            var r = Receiver(2, 10, 5, 500f);
            r.Efficiency = 0.5f;
            Run(new TestGrid(), Lossless, e, r);
            Assert.Equal(1000f, e.DrawWatts, 2);
            Assert.Equal(500f, r.DeliveredWatts, 2);
        }

        [Fact]
        public void DiagonalBeam_ReachesReceiver_AndCountsSqrt2PerStep()
        {
            var s = new BeamSettings { NodeLoss = 0f, LossPerTile = 0.001f, MaxRange = 60f };
            var e = Emitter(1, 0, 0, Dir8.NorthEast);
            var r = Receiver(2, 5, 5, 100f);
            var g = Run(new TestGrid(), s, e, r);

            Assert.Same(r, g.Segments[0].To);
            Assert.Equal(4, g.Segments[0].Cells.Count);
            Assert.Equal(5f * Dir8Ext.Sqrt2, g.Segments[0].Length, 3);
            Assert.Equal((float)System.Math.Pow(0.999, 5 * 1.41421356), g.Segments[0].Transmittance, 4);
            Assert.Equal(100f, r.DeliveredWatts, 2);
        }

        [Fact]
        public void DiagonalBeam_RangeUsesTrueDistance()
        {
            // 10 tiles of range covers only 7 diagonal steps (7 x 1.414 = 9.9).
            var s = new BeamSettings { NodeLoss = 0f, LossPerTile = 0f, MaxRange = 10f };
            var g = Run(new TestGrid(), s, Emitter(1, 0, 0, Dir8.NorthEast));
            Assert.Equal(SegmentEnd.OutOfRange, g.Segments[0].End);
            Assert.Equal(7, g.Segments[0].Cells.Count);
        }

        [Fact]
        public void DiagonalBeam_CannotSqueezeBetweenCornerWalls()
        {
            var grid = new TestGrid();
            grid.Walls.Add(new GridPos(3, 2));
            grid.Walls.Add(new GridPos(2, 3));
            var g = Run(grid, Lossless, Emitter(1, 0, 0, Dir8.NorthEast));
            Assert.Equal(SegmentEnd.Blocked, g.Segments[0].End);
            Assert.Equal(new GridPos(2, 2), g.Segments[0].EndPos);
        }

        [Fact]
        public void DiagonalBeam_PassesOneCornerWall()
        {
            var grid = new TestGrid();
            grid.Walls.Add(new GridPos(3, 2));
            var g = Run(grid, Lossless, Emitter(1, 0, 0, Dir8.NorthEast), Receiver(2, 6, 6, 10f));
            Assert.Equal(SegmentEnd.Node, g.Segments[0].End);
        }

        [Fact]
        public void Mirror_DiagonalTurn_EndToEnd()
        {
            // NE from (0,0) into a south-facing mirror (horizontal pane) at (4,4): it turns SE, down to (8,0).
            var e = Emitter(1, 0, 0, Dir8.NorthEast);
            var m = new BeamNode(2, NodeKind.Mirror, new GridPos(4, 4), Dir8.South);
            var r = Receiver(3, 8, 0, 200f);
            var g = Run(new TestGrid(), Lossless, e, m, r);

            Assert.Equal(Dir8.SouthEast, g.Segments[1].Dir);
            Assert.Same(r, g.Segments[1].To);
            Assert.Equal(200f, r.DeliveredWatts, 2);
        }

        [Fact]
        public void RetroReflection_EndsAtEmitter()
        {
            var e = Emitter(1, 0, 5, Dir8.East);
            var m = new BeamNode(2, NodeKind.Mirror, new GridPos(5, 5), Dir8.West);
            var g = Run(new TestGrid(), Lossless, e, m);
            Assert.Same(e, g.Segments[1].To);
            Assert.Equal(0f, e.DrawWatts);
        }

        [Fact]
        public void SegmentCells_ExcludeNodes()
        {
            var g = Run(new TestGrid(), Lossless, Emitter(1, 0, 5, Dir8.East), Receiver(2, 3, 5, 10f));
            Assert.Equal(new[] { new GridPos(1, 5), new GridPos(2, 5) }, g.Segments[0].Cells.ToArray());
        }
    }
}
