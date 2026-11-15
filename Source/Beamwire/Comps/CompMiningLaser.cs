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
        private Dir8 targetDir;
        private float delivered;
        private bool cutting;
        private Sustainer sound;
        private readonly List<IntVec3> lineCells = new List<IntVec3>();

        public CompProperties_MiningLaser Props => (CompProperties_MiningLaser)props;

        public bool HasWork => target.IsValid;

        public float WantedWatts => HasWork ? Props.maxIntake : 0f;

        public float Delivered => delivered;

        /// <summary>True while it is actually cutting: it has a target, power, and no closed shutter in the way.</summary>
        public bool Cutting => cutting;

        public IntVec3 Target => target;

        /// <summary>Cells between the laser and its target (not including either).</summary>
        public List<IntVec3> LineCells => lineCells;

        public float HeadAngle => (int)targetDir * 45f;

        private MapComponent_BeamNetwork Network => parent.Map?.GetComponent<MapComponent_BeamNetwork>();

        public void SetDelivered(float watts) => delivered = watts;

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
            if (!respawningAfterLoad || !StillValid(target)) FindTarget();
            else BuildLine();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref target, "beamwireLaserTarget", IntVec3.Invalid);
            Scribe_Values.Look(ref targetDir, "beamwireLaserDir", Dir8.North);
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

            cutting = target.IsValid && delivered > 1f && LineOpen();
            if (cutting)
            {
                if (sound == null || sound.Ended)
                {
                    SoundDef def = DefDatabase<SoundDef>.GetNamedSilentFail("Recipe_Smelt");
                    sound = def?.TrySpawnSustainer(SoundInfo.InMap(parent, MaintenanceType.PerTick));
                }
                sound?.Maintain();
                if (parent.IsHashIntervalTick(CutInterval))
                {
                    float hp = delivered * Props.hpPerWattSecond * (InVacuum ? Props.vacuumBonus : 1f);
                    Cut(Mathf.Max(1, Mathf.RoundToInt(hp)));
                }
            }
            else if (sound != null)
            {
                if (!sound.Ended) sound.End();
                sound = null;
            }
        }

        /// <summary>Picks the nearest marked rock on one of the 8 lines with nothing solid in between.</summary>
        private void FindTarget()
        {
            bool hadWork = HasWork;
            Map map = parent.Map;
            IntVec3 best = IntVec3.Invalid;
            Dir8 bestDir = targetDir;
            float bestDist = float.MaxValue;

            for (int d = 0; d < 8; d++)
            {
                var dir = (Dir8)d;
                GridPos off = dir.Offset();
                float step = dir.StepLength();
                IntVec3 c = parent.Position;
                for (float dist = step; dist <= Props.range && dist < bestDist; dist += step)
                {
                    c = new IntVec3(c.x + off.X, 0, c.z + off.Z);
                    if (!c.InBounds(map)) break;
                    if (IsMarkedRock(c, map))
                    {
                        best = c;
                        bestDir = dir;
                        bestDist = dist;
                        break;
                    }
                    if (BlocksCut(c, map)) break;
                }
            }

            target = best;
            targetDir = bestDir;
            BuildLine();
            if (hadWork != HasWork)
            {
                Network?.MarkPlanDirty();
            }
        }

        private void BuildLine()
        {
            lineCells.Clear();
            if (!target.IsValid) return;
            GridPos off = targetDir.Offset();
            IntVec3 c = parent.Position;
            for (int i = 0; i < 200; i++)
            {
                c = new IntVec3(c.x + off.X, 0, c.z + off.Z);
                if (c == target) break;
                lineCells.Add(c);
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
                ? "Beamwire_LaserCutting".Translate(what, target.x, target.z, delivered.ToString("0"))
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
