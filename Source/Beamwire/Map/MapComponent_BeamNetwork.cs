using System.Collections.Generic;
using Beamwire.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace Beamwire
{
    /// <summary>
    /// Owns every beam node on a map. It re-traces and re-solves the beams when something changes
    /// (and every 250 ticks for weather and power demand), applies the results, burns pawns standing
    /// in powered beams, and draws the beams.
    /// </summary>
    public class MapComponent_BeamNetwork : MapComponent
    {
        private const int SolveInterval = 250;
        private const int DamageInterval = 60;
        private const int SparkInterval = 20;
        private const float DamageMinWatts = 25f;

        private readonly List<CompBeamNode> nodes = new List<CompBeamNode>();
        private readonly Dictionary<int, CompBeamNode> byId = new Dictionary<int, CompBeamNode>();
        private readonly HashSet<IntVec3> watchedCells = new HashSet<IntVec3>();
        private readonly List<Pawn> tmpPawns = new List<Pawn>();

        private BeamGraph graph;
        private bool dirty = true;

        public MapComponent_BeamNetwork(Map map) : base(map)
        {
        }

        public BeamGraph Graph => graph;

        public static void DirtyAllMaps()
        {
            if (Current.Game?.Maps == null) return;
            foreach (Map m in Current.Game.Maps)
            {
                m.GetComponent<MapComponent_BeamNetwork>()?.MarkDirty();
            }
        }

        public void MarkDirty() => dirty = true;

        public void Register(CompBeamNode node)
        {
            if (!nodes.Contains(node))
            {
                nodes.Add(node);
                byId[node.parent.thingIDNumber] = node;
            }
            dirty = true;
        }

        public void Deregister(CompBeamNode node)
        {
            nodes.Remove(node);
            byId.Remove(node.parent.thingIDNumber);
            dirty = true;
        }

        /// <summary>Called by the Harmony patches when a building appears or disappears.</summary>
        public void NotifyBuildingChanged(IntVec3 cell)
        {
            if (watchedCells.Contains(cell))
            {
                dirty = true;
            }
        }

        public IEnumerable<Thing> DustyNodes()
        {
            foreach (CompBeamNode c in nodes)
            {
                CompOpticalDust dust = c.parent.GetComp<CompOpticalDust>();
                if (dust != null && dust.NeedsCleaning)
                {
                    yield return c.parent;
                }
            }
        }

        public bool AnyDustyNodes()
        {
            foreach (CompBeamNode c in nodes)
            {
                CompOpticalDust dust = c.parent.GetComp<CompOpticalDust>();
                if (dust != null && dust.NeedsCleaning) return true;
            }
            return false;
        }

        public CompBeamNode NodeFor(BeamNode n) => n != null && byId.TryGetValue(n.Id, out var c) ? c : null;

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            int ticks = Find.TickManager.TicksGame;

            if (dirty || ticks % SolveInterval == 0)
            {
                Recompute();
            }
            if (graph == null)
            {
                return;
            }
            if (ticks % DamageInterval == 0 && BeamwireMod.Settings.beamDamage)
            {
                BurnPawnsInBeams();
            }
            if (ticks % SparkInterval == 0)
            {
                ThrowImpactSparks();
            }
        }

        public void Recompute()
        {
            dirty = false;
            BeamwireSettings settings = BeamwireMod.Settings;
            BeamSettings beamSettings = settings.ToBeamSettings();

            var snapshot = new List<BeamNode>(nodes.Count);
            foreach (CompBeamNode c in nodes)
            {
                if (c.parent.Destroyed || !c.parent.Spawned) continue;

                var n = new BeamNode(c.parent.thingIDNumber, c.Kind, MapBeamGrid.ToGrid(c.parent.Position), c.Orientation)
                {
                    Operational = c.Operational,
                    Efficiency = c.Efficiency,
                    MaxThroughput = c.MaxThroughput,
                    Upkeep = c.Props.upkeep,
                };
                if (c.Kind == NodeKind.Receiver)
                {
                    n.Demand = ReceiverDemand(c);
                    c.LastDemand = n.Demand;
                }
                snapshot.Add(n);
            }

            graph = BeamTracer.Trace(new MapBeamGrid(map, settings), snapshot, beamSettings);
            PowerSolver.Solve(graph, beamSettings);

            foreach (BeamNode n in graph.Nodes)
            {
                NodeFor(n)?.ApplySolution(n);
            }

            // Remember which cells matter, so a wall built or removed there triggers an immediate re-trace.
            watchedCells.Clear();
            foreach (BeamSegment s in graph.Segments)
            {
                foreach (GridPos p in s.Cells)
                {
                    watchedCells.Add(MapBeamGrid.ToCell(p));
                }
                if (s.End == SegmentEnd.Blocked)
                {
                    watchedCells.Add(MapBeamGrid.ToCell(s.EndPos.Step(s.Dir)));
                }
                if (s.End == SegmentEnd.Node && s.Cells.Count == 0)
                {
                    watchedCells.Add(MapBeamGrid.ToCell(s.From.Pos.Step(s.Dir)));
                }
            }
        }

        /// <summary>
        /// How many watts a receiver should ask for. That is the shortfall on its power net, shared between
        /// all beam receivers on that net, plus its full intake while any battery there can still charge.
        /// </summary>
        private static float ReceiverDemand(CompBeamNode node)
        {
            CompBeamReceiver rc = node.Receiver;
            PowerNet net = rc?.PowerNet;
            if (net == null)
            {
                return 0f;
            }

            float consumption = 0f;
            float generation = 0f;
            int receivers = 0;
            foreach (CompPowerTrader comp in net.powerComps)
            {
                if (comp is CompBeamReceiver)
                {
                    receivers++;
                    continue; // other receivers are served by the same beam network
                }
                if (comp is CompPowerTrader_BeamEmitter)
                {
                    continue; // feeding our own emitters from our own receivers would just loop
                }
                float output = comp.PowerOutput;
                if (output < 0f)
                {
                    consumption -= output;
                }
                else if (comp.PowerOn)
                {
                    generation += output;
                }
            }

            float shortfall = Mathf.Max(0f, consumption - generation) / Mathf.Max(1, receivers);
            float demand = shortfall > 0f ? shortfall * 1.05f + 5f : 0f;

            foreach (CompPowerBattery battery in net.batteryComps)
            {
                if (battery.AmountCanAccept > 1f)
                {
                    demand = node.Props.maxIntake;
                    break;
                }
            }
            return Mathf.Min(demand, node.Props.maxIntake);
        }

        private void BurnPawnsInBeams()
        {
            float mult = BeamwireMod.Settings.damageMultiplier;
            DamageDef def = BeamwireDefOf.Beamwire_BeamBurn;

            foreach (BeamSegment s in graph.Segments)
            {
                if (s.Watts < DamageMinWatts) continue;

                float amount = Mathf.Clamp(s.Watts / 250f * mult, 1f, 40f);
                float pen = def.defaultArmorPenetration * (1f + s.Watts / 2000f);

                foreach (GridPos p in s.Cells)
                {
                    IntVec3 c = MapBeamGrid.ToCell(p);
                    tmpPawns.Clear();
                    foreach (Thing t in c.GetThingList(map))
                    {
                        if (t is Pawn pawn && !pawn.Dead)
                        {
                            tmpPawns.Add(pawn);
                        }
                    }
                    foreach (Pawn pawn in tmpPawns)
                    {
                        pawn.TakeDamage(new DamageInfo(def, amount, pen, -1f, null));
                        FleckMaker.ThrowMicroSparks(pawn.DrawPos, map);
                    }
                }
            }
            tmpPawns.Clear();
        }

        /// <summary>Sparks where powered beams land: on receivers, and on walls that block beams carrying power.</summary>
        private void ThrowImpactSparks()
        {
            if (map != Find.CurrentMap)
            {
                return;
            }
            foreach (BeamSegment s in graph.Segments)
            {
                if (s.Watts < 1f) continue;
                if (s.End == SegmentEnd.Blocked || (s.To != null && s.To.Kind == NodeKind.Receiver && Rand.Chance(0.25f)))
                {
                    Vector3 hit = BeamRenderer.EndPoint(s);
                    FleckMaker.ThrowMicroSparks(hit, map);
                }
            }
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            if (graph == null || map != Find.CurrentMap)
            {
                return;
            }
            if (BeamwireMod.Settings.showBeams || AnyNodeSelected())
            {
                BeamRenderer.Draw(graph);
            }
        }

        private static bool AnyNodeSelected()
        {
            foreach (object o in Find.Selector.SelectedObjectsListForReading)
            {
                if (o is ThingWithComps t && t.GetComp<CompBeamNode>() != null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
