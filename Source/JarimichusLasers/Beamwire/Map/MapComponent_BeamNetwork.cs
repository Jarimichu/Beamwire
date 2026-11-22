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

        private readonly HashSet<IntVec3> planCells = new HashSet<IntVec3>();

        private BeamGraph graph;
        private bool dirty = true;
        private bool planDirty = true;
        private int lastSolveTick = -1;

        public MapComponent_BeamNetwork(Map map) : base(map)
        {
        }

        public BeamGraph Graph => graph;

        public static void DirtyAllMaps()
        {
            if (Current.Game?.Maps == null) return;
            foreach (Map m in Current.Game.Maps)
            {
                m.GetComponent<MapComponent_BeamNetwork>()?.MarkPlanDirty();
            }
        }

        public void MarkDirty() => dirty = true;

        /// <summary>Something changed that may call for different aims: re-plan, then re-trace.</summary>
        public void MarkPlanDirty()
        {
            planDirty = true;
            dirty = true;
        }

        public void Register(CompBeamNode node)
        {
            if (!nodes.Contains(node))
            {
                nodes.Add(node);
                byId[node.parent.thingIDNumber] = node;
            }
            MarkPlanDirty();
        }

        public void Deregister(CompBeamNode node)
        {
            nodes.Remove(node);
            byId.Remove(node.parent.thingIDNumber);
            MarkPlanDirty();
        }

        /// <summary>Called by the Harmony patches when a building appears or disappears.</summary>
        public void NotifyBuildingChanged(IntVec3 cell)
        {
            if (watchedCells.Contains(cell))
            {
                dirty = true;
            }
            if (planCells.Contains(cell))
            {
                MarkPlanDirty();
            }
        }

        private readonly HashSet<IntVec3> tmpGuarded = new HashSet<IntVec3>();

        /// <summary>
        /// True when a non-hostile pawn stands in, or is stepping into, the path a shutter's beam takes once it
        /// passes through. The path is worked out from the beams arriving at the shutter, so it's known whether the
        /// shutter is open or closed.
        /// </summary>
        public bool FriendlyInPath(CompBeamNode shutter)
        {
            if (graph == null) return false;
            tmpGuarded.Clear();
            var grid = new MapBeamGrid(map, BeamwireMod.Settings);
            float range = BeamwireMod.Settings.maxRange;
            int id = shutter.parent.thingIDNumber;
            foreach (BeamSegment s in graph.Segments)
            {
                if (s.To == null || s.To.Id != id) continue;
                GridPos p = s.To.Pos;
                float travelled = 0f;
                while (true)
                {
                    p = p.Step(s.Dir);
                    travelled += s.Dir.StepLength();
                    if (travelled > range || !grid.InBounds(p) || grid.IsBlocked(p)) break;
                    IntVec3 c = MapBeamGrid.ToCell(p);
                    if (c.GetFirstThing<Building_BeamNode>(map) != null) break;
                    tmpGuarded.Add(c);
                }
            }
            // A beam-powered device's firing line (e.g. another mod's laser) through this shutter: guard beyond it.
            foreach (CompBeamNode n in nodes)
            {
                CompBeamPowered device = n.BeamPowered;
                if (device == null) continue;
                bool past = false;
                foreach (IntVec3 c in device.GuardLine)
                {
                    if (past) tmpGuarded.Add(c);
                    else if (c == shutter.parent.Position) past = true;
                }
            }

            if (tmpGuarded.Count == 0) return false;

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Dead || pawn.HostileTo(Faction.OfPlayer)) continue;
                if (tmpGuarded.Contains(pawn.Position)) return true;
                if (pawn.pather != null && pawn.pather.Moving && tmpGuarded.Contains(pawn.pather.nextCell)) return true;
            }
            return false;
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

            if (planDirty)
            {
                Replan();
            }
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

        /// <summary>
        /// Aims every auto-aim node so beams reach as many receivers as possible. This only runs when the
        /// network's layout changes: nodes built or removed, walls in possible beam paths, switches,
        /// breakdowns, or a player turning a node or switching it between Auto and Manual.
        /// </summary>
        public void Replan()
        {
            planDirty = false;
            planCells.Clear();
            if (!BeamwireMod.Settings.autoAim)
            {
                foreach (CompBeamNode c in nodes) c.NoRoute = false;
                return;
            }

            var snapshot = new List<BeamNode>(nodes.Count);
            foreach (CompBeamNode c in nodes)
            {
                if (c.parent.Destroyed || !c.parent.Spawned) continue;
                snapshot.Add(new BeamNode(c.parent.thingIDNumber, c.Kind, MapBeamGrid.ToGrid(c.parent.Position), c.Facing)
                {
                    // Plan with what the player controls: switched on and not broken down. Emitters that
                    // have merely lost power stay in the plan, so a brownout doesn't re-aim everything.
                    // Beam-powered devices only want a route while they want power.
                    Operational = c.PlannableNow && (c.BeamPowered == null || c.BeamPowered.WantedWatts > 0f),
                    AutoAim = c.AutoAimActive,
                });
            }

            AimPlan plan = AutoAimPlanner.Plan(new MapBeamGrid(map, BeamwireMod.Settings), snapshot, BeamwireMod.Settings.ToBeamSettings());

            foreach (var kv in plan.Facings)
            {
                if (byId.TryGetValue(kv.Key, out CompBeamNode c))
                {
                    c.ApplyPlannedFacing(kv.Value);
                }
            }
            foreach (CompBeamNode c in nodes)
            {
                c.NoRoute = c.Kind == NodeKind.Receiver && plan.UnreachedReceivers.Contains(c.parent.thingIDNumber);
            }
            foreach (GridPos p in plan.WatchedCells)
            {
                planCells.Add(MapBeamGrid.ToCell(p));
            }
            dirty = true;
        }

        public void Recompute()
        {
            dirty = false;
            BeamwireSettings settings = BeamwireMod.Settings;
            BeamSettings beamSettings = settings.ToBeamSettings();

            // Capacitors charge or drain at the last solve's rate for the time since then.
            int now = Find.TickManager.TicksGame;
            int elapsed = lastSolveTick < 0 ? 0 : now - lastSolveTick;
            lastSolveTick = now;
            foreach (CompBeamNode c in nodes)
            {
                c.IntegrateStorage(elapsed);
            }

            var snapshot = new List<BeamNode>(nodes.Count);
            foreach (CompBeamNode c in nodes)
            {
                if (c.parent.Destroyed || !c.parent.Spawned) continue;

                var n = new BeamNode(c.parent.thingIDNumber, c.Kind, MapBeamGrid.ToGrid(c.parent.Position), c.Facing)
                {
                    Operational = c.Operational,
                    Efficiency = c.Efficiency,
                    MaxThroughput = c.MaxThroughput,
                    Upkeep = c.Props.upkeep,
                    Closed = c.ShutterClosed,
                    Stored = c.Stored,
                    Capacity = c.Props.capacity,
                    MaxCharge = c.Props.maxCharge,
                    MaxDischarge = c.Props.maxDischarge,
                    ChargeEfficiency = c.Props.chargeEfficiency,
                };
                if (c.Kind == NodeKind.Receiver)
                {
                    n.Demand = c.BeamPowered != null ? c.BeamPowered.WantedWatts : ReceiverDemand(c);
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
                    BurnPawnsAt(MapBeamGrid.ToCell(p), def, amount, pen);
                }
            }
            tmpPawns.Clear();
        }

        private void BurnPawnsAt(IntVec3 c, DamageDef def, float amount, float pen)
        {
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
                BeamRenderer.Draw(graph, map);
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
