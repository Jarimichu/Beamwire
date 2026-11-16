using System.Collections.Generic;
using System.Linq;
using JarimichusLaserCannon.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace JarimichusLaserCannon
{
    public class CompProperties_LaserCannon : CompProperties
    {
        public float maxIntake = 2000f;
        public float range = 26f;
        public float mannedRangeBonus = 6f;

        /// <summary>Burn damage per pulse (every <see cref="pulseInterval"/> ticks) per kW.</summary>
        public float damagePerKw = 9f;

        public int pulseInterval = 30;
        public float mannedDamageMultiplier = 1.5f;
        public float vacuumBonus = 1.5f;
        public float igniteChance = 0.06f;

        public CompProperties_LaserCannon()
        {
            compClass = typeof(CompLaserCannon);
        }
    }

    /// <summary>
    /// The laser cannon: an automatic turret that locks two continuous green beams onto the nearest hostile it can
    /// see, burning it. It holds fire while anyone non-hostile stands on the line of fire. A colonist manning it
    /// adds range and damage. It runs on its power grid; with Jarimichus Beamwire installed, beams power it first.
    /// </summary>
    public class CompLaserCannon : ThingComp
    {
        private const int TargetInterval = 15;
        private const float TurnDegreesPerTick = 6f;

        private Pawn target;
        private Pawn forcedTarget;
        private bool holdFire;
        private float headAngle = float.NaN;
        private float targetAngle;
        private float beamWatts;
        private bool inUse;
        private bool friendlyInLine;
        private int lastMannedTick = -9999;
        private Pawn lastManningPawn;
        private CompPowerTrader_Laser power;
        private Sustainer sound;
        private readonly List<IntVec3> lineCells = new List<IntVec3>();

        public CompProperties_LaserCannon Props => (CompProperties_LaserCannon)props;

        public Pawn Target => target;

        public float GridWatts => power != null ? power.GridSupply : 0f;

        public float TotalWatts => beamWatts + GridWatts;

        public bool MannedNow => Find.TickManager.TicksGame - lastMannedTick <= 1 && lastManningPawn != null && lastManningPawn.Spawned;

        public float Range => Props.range + (MannedNow ? Props.mannedRangeBonus : 0f);

        public float HeadAngle => float.IsNaN(headAngle) ? RestAngle : headAngle;

        public float RestAngle => parent.Rotation.AsAngle;

        public bool OnTarget => Mathf.Abs(LaserMath.DeltaAngle(HeadAngle, targetAngle)) < 3f;

        /// <summary>Beams are on: a target, power, nothing friendly or shuttered in the way, and the head is on target.</summary>
        public bool Firing => inUse && OnTarget;

        // ---- Beam-powered devices API (used by Jarimichus Beamwire, found by name) ----

        public float BeamWantedWatts => target != null && !holdFire && Switchable ? Props.maxIntake : 0f;

        public void SetBeamWatts(float watts)
        {
            beamWatts = watts;
            UpdateGridDraw();
        }

        public IEnumerable<IntVec3> BeamGuardLine => target != null ? (IEnumerable<IntVec3>)lineCells : Enumerable.Empty<IntVec3>();

        // ----

        public void ManForATick(Pawn pawn)
        {
            lastMannedTick = Find.TickManager.TicksGame;
            lastManningPawn = pawn;
        }

        private bool Switchable
        {
            get
            {
                CompFlickable flick = parent.GetComp<CompFlickable>();
                CompBreakdownable breakdown = parent.GetComp<CompBreakdownable>();
                return (flick == null || flick.SwitchIsOn) && (breakdown == null || !breakdown.BrokenDown);
            }
        }

        private void UpdateGridDraw()
        {
            power?.SetGridDraw(target != null && !holdFire && Switchable ? Mathf.Max(0f, Props.maxIntake - beamWatts) : 0f);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            power = parent.GetComp<CompPowerTrader_Laser>();
            UpdateGridDraw();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref target, "jlcTarget");
            Scribe_References.Look(ref forcedTarget, "jlcForcedTarget");
            Scribe_Values.Look(ref holdFire, "jlcHoldFire", false);
            Scribe_Values.Look(ref headAngle, "jlcHeadAngle", float.NaN);
            Scribe_Values.Look(ref beamWatts, "jlcBeamWatts", 0f);
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

            if (parent.IsHashIntervalTick(TargetInterval) || (target != null && (target.Destroyed || target.Dead || !target.Spawned)))
            {
                RefreshTarget();
            }
            if (target != null)
            {
                Vector3 d = target.DrawPos - parent.DrawPos;
                targetAngle = LaserMath.AngleTo(d.x, d.z);
            }

            inUse = target != null && !holdFire && TotalWatts > 1f && !friendlyInLine && !LaserLine.AnyClosedShutter(lineCells, parent.Map);
            bool tracking = target != null && !holdFire;
            headAngle = LaserMath.TurnTowards(HeadAngle, tracking ? targetAngle : RestAngle, TurnDegreesPerTick);

            if (Firing)
            {
                if (sound == null || sound.Ended)
                {
                    sound = LaserCannonDefOf.JLC_LaserBuzz.TrySpawnSustainer(SoundInfo.InMap(parent, MaintenanceType.PerTick));
                }
                sound?.Maintain();
                if (parent.IsHashIntervalTick(Props.pulseInterval))
                {
                    Burn();
                }
            }
            else if (sound != null)
            {
                if (!sound.Ended) sound.End();
                sound = null;
            }
        }

        private void RefreshTarget()
        {
            Pawn before = target;
            if (forcedTarget != null && !Valid(forcedTarget, true)) forcedTarget = null;
            if (forcedTarget != null) target = forcedTarget;
            else if (target == null || !Valid(target, false)) target = FindNearest();
            BuildLine();
            friendlyInLine = target != null && FriendlyOnLine();
            if ((before == null) != (target == null)) UpdateGridDraw();
        }

        private bool IsEnemy(Pawn p) =>
            p.HostileTo(Faction.OfPlayer) && (!p.RaceProps.Animal || LaserCannonMod.Settings.attackAnimals);

        private bool Valid(Pawn p, bool forced)
        {
            if (p == null || p.Destroyed || p.Dead || !p.Spawned || p.Map != parent.Map || p.Downed) return false;
            if (!forced && !IsEnemy(p)) return false;
            if ((p.Position - parent.Position).LengthHorizontalSquared > Range * Range) return false;
            return LaserLine.LineClear(parent.Position, p.Position, parent.Map);
        }

        private Pawn FindNearest()
        {
            Pawn best = null;
            float bestDist = float.MaxValue;
            foreach (Pawn p in parent.Map.mapPawns.AllPawnsSpawned)
            {
                if (p.Dead || p.Downed || !IsEnemy(p)) continue;
                float dist = (p.Position - parent.Position).LengthHorizontalSquared;
                if (dist >= bestDist || dist > Range * Range) continue;
                if (!LaserLine.LineClear(parent.Position, p.Position, parent.Map)) continue;
                best = p;
                bestDist = dist;
            }
            return best;
        }

        private void BuildLine()
        {
            lineCells.Clear();
            if (target != null) LaserLine.CellsBetween(parent.Position, target.Position, lineCells);
        }

        /// <summary>True when a non-hostile pawn (colonist, ally, guest, prisoner, tame animal) stands on the line of fire.</summary>
        private bool FriendlyOnLine()
        {
            Map map = parent.Map;
            return LaserMath.FriendlyOnLine(parent.Position.x, parent.Position.z, target.Position.x, target.Position.z, (x, z) =>
            {
                foreach (Thing t in new IntVec3(x, 0, z).GetThingList(map))
                {
                    if (t is Pawn p && !p.Dead && p != target && !p.HostileTo(Faction.OfPlayer)) return true;
                }
                return false;
            });
        }

        private bool InVacuum
        {
            get
            {
#if RW16
                if (!ModsConfig.OdysseyActive) return false;
                return VacuumUtility.GetVacuum(parent.Position, parent.Map) >= 0.5f ||
                       (target != null && VacuumUtility.GetVacuum(target.Position, parent.Map) >= 0.5f);
#else
                return false;
#endif
            }
        }

        private void Burn()
        {
            if (target == null) return;
            DamageDef def = LaserCannonDefOf.JLC_LaserBurn;
            float amount = TotalWatts / 1000f * Props.damagePerKw * LaserCannonMod.Settings.damageMultiplier;
            if (MannedNow) amount *= Props.mannedDamageMultiplier;
            if (InVacuum) amount *= Props.vacuumBonus;
            if (amount < 0.5f) return;
            float pen = def.defaultArmorPenetration * (1f + TotalWatts / 2000f);
            target.TakeDamage(new DamageInfo(def, amount, pen, targetAngle, parent, null, parent.def));
            FleckMaker.ThrowMicroSparks(target.DrawPos, parent.Map);
            if (target != null && !target.Destroyed && Rand.Chance(Props.igniteChance))
            {
#if RW14
                FireUtility.TryAttachFire(target, 0.3f);
#else
                FireUtility.TryAttachFire(target, 0.3f, parent);
#endif
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;
            if (parent.Faction != Faction.OfPlayer) yield break;

            yield return new Command_Target
            {
                defaultLabel = "JLC_ForceTarget".Translate(),
                defaultDesc = "JLC_ForceTargetDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", false) ?? BaseContent.BadTex,
                targetingParams = new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetBuildings = false,
                    canTargetItems = false,
                    validator = t => t.HasThing && t.Thing is Pawn p && !p.Dead && p.Spawned,
                },
                action = t =>
                {
                    if (t.Thing is Pawn p && Valid(p, true))
                    {
                        forcedTarget = p;
                        RefreshTarget();
                    }
                    else
                    {
                        Messages.Message("JLC_CannotTarget".Translate(), parent, MessageTypeDefOf.RejectInput, false);
                    }
                },
            };
            if (forcedTarget != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "JLC_ClearTarget".Translate(),
                    defaultDesc = "JLC_ClearTargetDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", false) ?? BaseContent.BadTex,
                    action = () => { forcedTarget = null; RefreshTarget(); },
                };
            }
            yield return new Command_Toggle
            {
                defaultLabel = "JLC_HoldFire".Translate(),
                defaultDesc = "JLC_HoldFireDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/HoldFire", false) ?? BaseContent.BadTex,
                isActive = () => holdFire,
                toggleAction = () => { holdFire = !holdFire; UpdateGridDraw(); },
            };
        }

        public override string CompInspectStringExtra()
        {
            var lines = new List<string>();
            if (holdFire) lines.Add("JLC_InspectHoldFire".Translate());
            else if (target == null) lines.Add("JLC_InspectNoTarget".Translate(Range.ToString("0")));
            else if (friendlyInLine) lines.Add("JLC_InspectFriendly".Translate(target.LabelShort));
            else if (LaserLine.AnyClosedShutter(lineCells, parent.Map)) lines.Add("JLC_InspectShuttered".Translate(target.LabelShort));
            else if (inUse) lines.Add("JLC_InspectFiring".Translate(target.LabelShort, beamWatts.ToString("0"), GridWatts.ToString("0")));
            else lines.Add("JLC_InspectNeedsPower".Translate(target.LabelShort));
            if (forcedTarget != null) lines.Add("JLC_InspectForced".Translate());
            if (MannedNow) lines.Add("JLC_InspectManned".Translate(lastManningPawn.LabelShort, Props.mannedRangeBonus.ToString("0"), Props.mannedDamageMultiplier.ToString("0.#")));
            if (InVacuum) lines.Add("JLC_InspectVacuum".Translate(Props.vacuumBonus.ToString("0.#")));
            return string.Join("\n", lines);
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            GenDraw.DrawRadiusRing(parent.Position, Range);
        }
    }
}
