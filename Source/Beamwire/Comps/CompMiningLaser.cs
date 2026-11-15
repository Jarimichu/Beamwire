using System.Collections.Generic;
using Beamwire.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Beamwire
{
    public class CompProperties_MiningLaser : CompProperties
    {
        /// <summary>Watts it asks for while it has rock to cut.</summary>
        public float maxIntake = 2000f;

        /// <summary>How far it reaches, in tiles.</summary>
        public float range = 30f;

        /// <summary>Rock hit points cut per second for each watt it receives (1 kW ≈ 60 HP/s).</summary>
        public float hpPerWattSecond = 0.06f;

        /// <summary>Cutting speed multiplier while the laser stands in vacuum (Odyssey).</summary>
        public float vacuumBonus = 1.5f;

        public CompProperties_MiningLaser()
        {
            compClass = typeof(CompMiningLaser);
        }
    }

    /// <summary>
    /// A beam-fed mining laser. It powers itself from beams, like a receiver, and spends that power cutting the
    /// nearest rock or ore marked for mining that lies on one of its 8 lines. It yields the same as a colonist
    /// mining. While it has nothing to cut it asks for no power, so auto-aim sends beams elsewhere.
    /// </summary>
    public class CompMiningLaser : ThingComp
    {
        private const int SearchInterval = 250;
        private const int CutInterval = 60;

        private IntVec3 target = IntVec3.Invalid;
        private float headAngle;
        private float delivered;
        private CompPowerTrader_MiningLaser power;
        private bool cutting;
        private Sustainer sound;
        private readonly List<IntVec3> lineCells = new List<IntVec3>();

        public CompProperties_MiningLaser Props => (CompProperties_MiningLaser)props;

        public bool HasWork => target.IsValid;

        public float WantedWatts => HasWork ? Props.maxIntake : 0f;

        /// <summary>Watts arriving by beam.</summary>
        public float Delivered => delivered;

        /// <summary>Watts arriving from the laser's own power grid, topping up what beams don't cover.</summary>
        public float GridWatts => power != null ? power.GridSupply : 0f;

        /// <summary>All the power it is cutting with: beams first, then the grid.</summary>
        public float TotalWatts => delivered + GridWatts;

        /// <summary>True while it is actually cutting: it has a target, power, and no closed shutter in the way.</summary>
        public bool Cutting => cutting;

        public IntVec3 Target => target;

        /// <summary>Cells on the straight line between the laser and its target (not including either).</summary>
        public List<IntVec3> LineCells => lineCells;

        /// <summary>Degrees clockwise from North toward the target, at any angle.</summary>
        public float HeadAngle => headAngle;

        private MapComponent_BeamNetwork Network => parent.Map?.GetComponent<MapComponent_BeamNetwork>();

        public void SetDelivered(float watts)
        {
            delivered = watts;
            UpdateGridDraw();
        }

        /// <summary>Asks the grid for whatever the beams don't cover, while there is rock to cut.</summary>
        private void UpdateGridDraw()
        {
            power?.SetGridDraw(HasWork ? Mathf.Max(0f, Props.maxIntake - delivered) : 0f);
        }

        public bool InVacuum
        {
            get
            {
#if RW16
                return parent.Spawned && ModsConfig.OdysseyActive && VacuumUtility.GetVacuum(parent.Position, parent.Map) >= 0.5f;
#else
                return false;
#endif
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            power = parent.GetComp<CompPowerTrader_MiningLaser>();
            if (!respawningAfterLoad || !StillValid(target)) FindTarget();
            else BuildLine();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref target, "beamwireLaserTarget", IntVec3.Invalid);
            Scribe_Values.Look(ref headAngle, "beamwireLaserAngle", 0f);
            Scribe_Values.Look(ref delivered, "beamwireLaserWatts", 0f);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned) return;

            if (parent.IsHashIntervalTick(SearchInterval) || (target.IsValid && !StillValid(target)))
            {
                FindTarget();
            }

            cutting = target.IsValid && TotalWatts > 1f && LineOpen();
            if (cutting)
            {
                if (sound == null || sound.Ended)
                {
                    SoundDef def = DefDatabase<SoundDef>.GetNamedSilentFail("Recipe_Smelt");
                    sound = def?.TrySpawnSustainer(SoundInfo.InMap(parent, MaintenanceType.PerTick));
                }
                sound?.Maintain();
                if (parent.IsHashIntervalTick(20))
                {
                    FleckMaker.ThrowMicroSparks(target.ToVector3Shifted(), parent.Map);
                }
                if (parent.IsHashIntervalTick(CutInterval))
                {
                    float hp = TotalWatts * Props.hpPerWattSecond * (InVacuum ? Props.vacuumBonus : 1f);
                    Cut(Mathf.Max(1, Mathf.RoundToInt(hp)));
                }
            }
            else if (sound != null)
            {
                if (!sound.Ended) sound.End();
                sound = null;
            }
        }

        /// <summary>
        /// Picks the nearest marked rock (by true distance, within range) that the laser can see along a straight
        /// grid line, at any angle.
        /// </summary>
        private void FindTarget()
        {
            bool hadWork = HasWork;
            Map map = parent.Map;
            IntVec3 from = parent.Position;
            float rangeSq = Props.range * Props.range;

            var candidates = new List<IntVec3>();
            AddMarked(map, DesignationDefOf.Mine, from, rangeSq, candidates);
#if RW16
            AddMarked(map, DesignationDefOf.MineVein, from, rangeSq, candidates);
#endif
            candidates.Sort((a, b) => (a - from).LengthHorizontalSquared.CompareTo((b - from).LengthHorizontalSquared));

            IntVec3 best = IntVec3.Invalid;
            foreach (IntVec3 c in candidates)
            {
                if (IsMarkedRock(c, map) && LineClear(from, c, map))
                {
                    best = c;
                    break;
                }
            }

            target = best;
            BuildLine();
            if (hadWork != HasWork)
            {
                UpdateGridDraw();
                Network?.MarkPlanDirty();
            }
        }

        private static void AddMarked(Map map, DesignationDef def, IntVec3 from, float rangeSq, List<IntVec3> into)
        {
            foreach (Designation d in map.designationManager.SpawnedDesignationsOfDef(def))
            {
                IntVec3 c = d.target.Cell;
                if (c != from && (c - from).LengthHorizontalSquared <= rangeSq && !into.Contains(c)) into.Add(c);
            }
        }

        private void BuildLine()
        {
            lineCells.Clear();
            if (!target.IsValid) return;
            CellsBetween(parent.Position, target, lineCells);
            IntVec3 d = target - parent.Position;
            headAngle = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        private static readonly List<IntVec3> tmpLine = new List<IntVec3>();

        /// <summary>
        /// True when nothing solid sits between the two cells. A diagonal step can't squeeze between two
        /// corner-touching blockers, the same rule as power beams.
        /// </summary>
        private static bool LineClear(IntVec3 a, IntVec3 b, Map map)
        {
            CellsBetween(a, b, tmpLine);
            IntVec3 prev = a;
            tmpLine.Add(b);
            foreach (IntVec3 c in tmpLine)
            {
                if (c.x != prev.x && c.z != prev.z &&
                    BlocksCut(new IntVec3(c.x, 0, prev.z), map) && BlocksCut(new IntVec3(prev.x, 0, c.z), map))
                {
                    return false;
                }
                if (c != b && BlocksCut(c, map)) return false;
                prev = c;
            }
            return true;
        }

        /// <summary>Bresenham line: the cells strictly between <paramref name="a"/> and <paramref name="b"/>.</summary>
        public static void CellsBetween(IntVec3 a, IntVec3 b, List<IntVec3> into)
        {
            into.Clear();
            int x = a.x, z = a.z;
            int dx = Mathf.Abs(b.x - a.x), dz = Mathf.Abs(b.z - a.z);
            int sx = a.x < b.x ? 1 : -1, sz = a.z < b.z ? 1 : -1;
            int err = dx - dz;
            while (true)
            {
                int e2 = 2 * err;
                if (e2 > -dz) { err -= dz; x += sx; }
                if (e2 < dx) { err += dx; z += sz; }
                if (x == b.x && z == b.z) break;
                into.Add(new IntVec3(x, 0, z));
            }
        }

        private bool StillValid(IntVec3 c) => c.IsValid && parent.Spawned && IsMarkedRock(c, parent.Map);

        private static bool IsMarkedRock(IntVec3 c, Map map)
        {
            Building edifice = c.GetEdifice(map);
            if (edifice == null || !edifice.def.mineable) return false;
            if (map.designationManager.DesignationAt(c, DesignationDefOf.Mine) != null) return true;
#if RW16
            if (map.designationManager.DesignationAt(c, DesignationDefOf.MineVein) != null) return true;
#endif
            return false;
        }

        /// <summary>Walls, rock and beam nodes stop the cutting beam; beam windows and shutters don't.</summary>
        private static bool BlocksCut(IntVec3 c, Map map)
        {
            Building edifice = c.GetEdifice(map);
            if (edifice is Building_BeamNode node)
            {
                return node.Node == null || node.Node.Kind != NodeKind.Shutter;
            }
            if (c.GetFirstThing<Building_BeamNode>(map) is Building_BeamNode other)
            {
                return other.Node == null || other.Node.Kind != NodeKind.Shutter;
            }
            if (edifice != null && edifice.def.passability == Traversability.Impassable &&
                !edifice.def.HasModExtension<BeamTransparentExtension>())
            {
                return true;
            }
            return false;
        }

        /// <summary>False while a closed safety shutter sits on the cutting line.</summary>
        private bool LineOpen()
        {
            foreach (IntVec3 c in lineCells)
            {
                if (c.GetFirstThing<Building_BeamNode>(parent.Map) is Building_BeamNode node && node.Node != null && node.Node.ShutterClosed)
                {
                    return false;
                }
            }
            return true;
        }

        private void Cut(int amount)
        {
            Map map = parent.Map;
            Building rock = target.GetEdifice(map);
            if (rock == null || !rock.def.mineable)
            {
                FindTarget();
                return;
            }

            Vector3 hit = target.ToVector3Shifted();
            FleckMaker.ThrowMicroSparks(hit, map);
            if (Rand.Chance(0.5f)) FleckMaker.ThrowDustPuff(target, map, 1f);

            if (rock.HitPoints > amount)
            {
                rock.TakeDamage(new DamageInfo(DamageDefOf.Mining, amount));
                return;
            }

            // Final cut: full yield, as if a colonist had mined it.
            BuildingProperties b = rock.def.building;
            ThingDef yieldDef = b.mineableThing;
            int count = 0;
            if (yieldDef != null)
            {
                if (b.isResourceRock) count = Mathf.Max(1, b.mineableYield);
                else if (Rand.Chance(b.mineableDropChance)) count = Mathf.Max(1, b.mineableYield);
            }
            IntVec3 cell = target;
            Designation des = map.designationManager.DesignationAt(cell, DesignationDefOf.Mine);
            if (des != null) map.designationManager.RemoveDesignation(des);
#if RW16
            Designation vein = map.designationManager.DesignationAt(cell, DesignationDefOf.MineVein);
            if (vein != null) map.designationManager.RemoveDesignation(vein);
#endif
            rock.Destroy(DestroyMode.Vanish);
            if (count > 0)
            {
                Thing thing = ThingMaker.MakeThing(yieldDef);
                thing.stackCount = count;
                GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
            }
            FindTarget();
        }

        public override string CompInspectStringExtra()
        {
            if (!HasWork)
            {
                return "Beamwire_LaserIdle".Translate(Props.range.ToString("0"));
            }
            Building rock = target.GetEdifice(parent.Map);
            string what = rock != null ? rock.LabelShort : "?";
            string line = cutting
                ? "Beamwire_LaserCutting".Translate(what, target.x, target.z, delivered.ToString("0"), GridWatts.ToString("0"))
                : LineOpen() ? "Beamwire_LaserNeedsPower".Translate(what, target.x, target.z)
                : "Beamwire_LaserShuttered".Translate(what);
            if (InVacuum)
            {
                line += " " + "Beamwire_LaserVacuum".Translate(Props.vacuumBonus.ToString("0.#"));
            }
            return line;
        }
    }
}
