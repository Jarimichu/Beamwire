using System.Collections.Generic;
using System.Linq;
using JarimichusMiningLaser.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace JarimichusMiningLaser
{
    public class CompProperties_MiningLaser : CompProperties
    {
        /// <summary>Watts it uses while it has rock to cut (beams first, then its grid).</summary>
        public float maxIntake = 2000f;

        /// <summary>How far it reaches, in tiles.</summary>
        public float range = 30f;

        /// <summary>Rock hit points cut per second for each watt (1 kW ≈ 60 HP/s).</summary>
        public float hpPerWattSecond = 0.06f;

        /// <summary>Cutting speed multiplier while the laser stands in vacuum (Odyssey).</summary>
        public float vacuumBonus = 1.5f;

        public CompProperties_MiningLaser()
        {
            compClass = typeof(CompMiningLaser);
        }
    }

    /// <summary>
    /// A three-barrelled mining laser. It cuts the nearest rock or ore marked for mining that it can see in a
    /// straight line, at any angle, for full yield. It runs on its power grid; with Jarimichus Beamwire installed,
    /// beams power it first and the grid tops up (see the Beam-powered devices API members below).
    /// </summary>
    public class CompMiningLaser : ThingComp
    {
        private const int SearchInterval = 250;
        private const int CutInterval = 60;
        private const float TurnDegreesPerTick = 6f;

        private IntVec3 target = IntVec3.Invalid;
        private float targetAngle;
        private float headAngle = float.NaN;
        private float beamWatts;
        private bool inUse;
        private CompPowerTrader_Laser power;
        private Sustainer sound;
        private readonly List<IntVec3> lineCells = new List<IntVec3>();
        private readonly List<Pawn> tmpPawns = new List<Pawn>();

        public CompProperties_MiningLaser Props => (CompProperties_MiningLaser)props;

        public bool HasWork => target.IsValid;

        public IntVec3 Target => target;

        /// <summary>Watts arriving from its grid, topping up what beams don't cover.</summary>
        public float GridWatts => power != null ? power.GridSupply : 0f;

        /// <summary>All the power it is cutting with: beams first, then the grid.</summary>
        public float TotalWatts => beamWatts + GridWatts;

        /// <summary>Firing: it has a target, power, no closed shutter in the way, and its head is on target.</summary>
        public bool Cutting => inUse && OnTarget;

        public float HeadAngle => float.IsNaN(headAngle) ? RestAngle : headAngle;

        public bool OnTarget => Mathf.Abs(LaserMath.DeltaAngle(HeadAngle, targetAngle)) < 3f;

        /// <summary>Where the head points when not in use: the rotation it was placed with.</summary>
        public float RestAngle => parent.Rotation.AsAngle;

        // ---- Beam-powered devices API (used by Jarimichus Beamwire, found by name) ----

        public float BeamWantedWatts => HasWork && Switchable ? Props.maxIntake : 0f;

        public void SetBeamWatts(float watts)
        {
            beamWatts = watts;
            UpdateGridDraw();
        }

        public IEnumerable<IntVec3> BeamGuardLine => HasWork ? (IEnumerable<IntVec3>)lineCells : Enumerable.Empty<IntVec3>();

        // ----

        private bool Switchable
        {
            get
            {
                CompFlickable flick = parent.GetComp<CompFlickable>();
                CompBreakdownable breakdown = parent.GetComp<CompBreakdownable>();
                return (flick == null || flick.SwitchIsOn) && (breakdown == null || !breakdown.BrokenDown);
            }
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

        private void UpdateGridDraw()
        {
            power?.SetGridDraw(HasWork && Switchable ? Mathf.Max(0f, Props.maxIntake - beamWatts) : 0f);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            power = parent.GetComp<CompPowerTrader_Laser>();
            if (!respawningAfterLoad || !StillValid(target)) FindTarget();
            else BuildLine();
            UpdateGridDraw();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref target, "jmlTarget", IntVec3.Invalid);
            Scribe_Values.Look(ref headAngle, "jmlHeadAngle", float.NaN);
            Scribe_Values.Look(ref beamWatts, "jmlBeamWatts", 0f);
        }

        public override void ReceiveCompSignal(string signal)
        {
            base.ReceiveCompSignal(signal);
            UpdateGridDraw();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned) return;

            if (parent.IsHashIntervalTick(SearchInterval) || (target.IsValid && !StillValid(target)))
            {
                FindTarget();
            }

            inUse = target.IsValid && TotalWatts > 1f && !LaserLine.AnyClosedShutter(lineCells, parent.Map);
            headAngle = LaserMath.TurnTowards(HeadAngle, inUse ? targetAngle : RestAngle, TurnDegreesPerTick);

            if (Cutting)
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
                    BurnPawnsOnLine();
                    Cut(Mathf.Max(1, Mathf.RoundToInt(hp)));
                }
            }
            else if (sound != null)
            {
                if (!sound.Ended) sound.End();
                sound = null;
            }
        }

        /// <summary>Nearest marked rock (true distance, within range) that it can see in a straight line.</summary>
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
                if (IsMarkedRock(c, map) && LaserLine.LineClear(from, c, map))
                {
                    best = c;
                    break;
                }
            }
            target = best;
            BuildLine();
            if (hadWork != HasWork) UpdateGridDraw();
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
            LaserLine.CellsBetween(parent.Position, target, lineCells);
            IntVec3 d = target - parent.Position;
            targetAngle = LaserMath.AngleTo(d.x, d.z);
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

        private void BurnPawnsOnLine()
        {
            if (!MiningLaserMod.Settings.burn || TotalWatts < 25f) return;
            DamageDef def = MiningLaserDefOf.JML_LaserBurn;
            float amount = Mathf.Clamp(TotalWatts / 250f * MiningLaserMod.Settings.burnMultiplier, 1f, 40f);
            float pen = def.defaultArmorPenetration * (1f + TotalWatts / 2000f);
            foreach (IntVec3 c in lineCells)
            {
                tmpPawns.Clear();
                foreach (Thing t in c.GetThingList(parent.Map))
                {
                    if (t is Pawn p && !p.Dead) tmpPawns.Add(p);
                }
                foreach (Pawn p in tmpPawns)
                {
                    p.TakeDamage(new DamageInfo(def, amount, pen, -1f, parent));
                    FleckMaker.ThrowMicroSparks(p.DrawPos, parent.Map);
                }
            }
            tmpPawns.Clear();
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
            FleckMaker.ThrowMicroSparks(target.ToVector3Shifted(), map);
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
            if (!HasWork) return "JML_Idle".Translate(Props.range.ToString("0"));
            Building rock = target.GetEdifice(parent.Map);
            string what = rock != null ? rock.LabelShort : "?";
            string line = inUse
                ? "JML_Cutting".Translate(what, target.x, target.z, beamWatts.ToString("0"), GridWatts.ToString("0"))
                : LaserLine.AnyClosedShutter(lineCells, parent.Map) ? "JML_Shuttered".Translate(what)
                : "JML_NeedsPower".Translate(what, target.x, target.z);
            if (InVacuum) line += " " + "JML_Vacuum".Translate(Props.vacuumBonus.ToString("0.#"));
            return line;
        }
    }
}
