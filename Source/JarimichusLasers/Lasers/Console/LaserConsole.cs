using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace JarimichusLasers
{
    public class CompProperties_LaserConsole : CompProperties
    {
        /// <summary>How many lasers one console can run.</summary>
        public int maxLinks = 3;

        /// <summary>How far away a linked laser may be, in tiles.</summary>
        public float linkRange = 20f;

        public CompProperties_LaserConsole()
        {
            compClass = typeof(CompLaserConsole);
        }
    }

    /// <summary>
    /// A laser control console: a colonist at the console mans every linked Jarimichus laser at once, for example
    /// turrets on a gravship's hull operated from inside the ship. Works with the laser cannon and the mining laser
    /// and with any other mod's laser that has a comp with ManForATick(Pawn) and BeamWantedWatts (found by name).
    /// </summary>
    public class CompLaserConsole : ThingComp
    {
        private static readonly Dictionary<Type, MethodInfo> ManMethods = new Dictionary<Type, MethodInfo>();

        private List<Thing> links = new List<Thing>();
        private int lastMannedTick = -9999;
        private Pawn operatorPawn;
        private CompPowerTrader power;

        public CompProperties_LaserConsole Props => (CompProperties_LaserConsole)props;

        public List<Thing> Links => links;

        public bool Powered => power == null || power.PowerOn;

        public bool MannedNow => Find.TickManager.TicksGame - lastMannedTick <= 1 && operatorPawn != null && operatorPawn.Spawned;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            power = parent.GetComp<CompPowerTrader>();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref links, "jlxLinks", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (links == null) links = new List<Thing>();
                links.RemoveAll(t => t == null);
            }
        }

        /// <summary>Finds the laser comp on a thing: any comp with ManForATick(Pawn) and BeamWantedWatts.</summary>
        public static bool TryGetLaser(Thing t, out ThingComp comp, out MethodInfo man)
        {
            comp = null;
            man = null;
            if (!(t is ThingWithComps twc)) return false;
            foreach (ThingComp c in twc.AllComps)
            {
                Type type = c.GetType();
                if (!ManMethods.TryGetValue(type, out MethodInfo m))
                {
                    m = type.GetMethod("ManForATick", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Pawn) }, null);
                    if (m != null && type.GetProperty("BeamWantedWatts", BindingFlags.Public | BindingFlags.Instance) == null) m = null;
                    ManMethods[type] = m;
                }
                if (m != null)
                {
                    comp = c;
                    man = m;
                    return true;
                }
            }
            return false;
        }

        private bool InReach(Thing t) =>
            t.Spawned && t.Map == parent.Map && (t.Position - parent.Position).LengthHorizontalSquared <= Props.linkRange * Props.linkRange;

        public AcceptanceReport CanLink(Thing t)
        {
            if (t == null || t == parent || !TryGetLaser(t, out _, out _)) return "JLX_NotALaser".Translate();
            if (links.Contains(t)) return "JLX_AlreadyLinked".Translate();
            if (!InReach(t)) return "JLX_TooFar".Translate(Props.linkRange.ToString("0"));
            if (links.Count >= Props.maxLinks) return "JLX_TooMany".Translate(Props.maxLinks);
            return true;
        }

        /// <summary>Called every tick by the operator's job: mans every linked laser.</summary>
        public void ManForATick(Pawn pawn)
        {
            if (!Powered) return;
            lastMannedTick = Find.TickManager.TicksGame;
            operatorPawn = pawn;
            links.RemoveAll(t => t == null || t.Destroyed || !InReach(t));
            foreach (Thing t in links)
            {
                if (TryGetLaser(t, out ThingComp comp, out MethodInfo man))
                {
                    man.Invoke(comp, new object[] { pawn });
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;
            if (parent.Faction != Faction.OfPlayer) yield break;

            var link = new Command_Target
            {
                defaultLabel = "JLX_Link".Translate(),
                defaultDesc = "JLX_LinkDesc".Translate(Props.maxLinks, Props.linkRange.ToString("0")),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/LinkStorageSettings", false) ?? ContentFinder<Texture2D>.Get("UI/Commands/Attack", false) ?? BaseContent.BadTex,
                targetingParams = new TargetingParameters
                {
                    canTargetBuildings = true,
                    canTargetPawns = false,
                    canTargetItems = false,
                    validator = t => t.HasThing && CanLink(t.Thing).Accepted,
                },
                action = t =>
                {
                    AcceptanceReport ok = CanLink(t.Thing);
                    if (ok.Accepted) links.Add(t.Thing);
                    else Messages.Message(ok.Reason, parent, MessageTypeDefOf.RejectInput, false);
                },
            };
            if (links.Count >= Props.maxLinks) link.Disable("JLX_TooMany".Translate(Props.maxLinks));
            yield return link;

            if (links.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "JLX_Unlink".Translate(),
                    defaultDesc = "JLX_UnlinkDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", false) ?? BaseContent.BadTex,
                    action = () => links.Clear(),
                };
            }
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            GenDraw.DrawRadiusRing(parent.Position, Props.linkRange);
            foreach (Thing t in links)
            {
                if (t != null && t.Spawned && t.Map == parent.Map)
                {
                    GenDraw.DrawLineBetween(parent.TrueCenter(), t.TrueCenter());
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            var lines = new List<string> { "JLX_InspectLinks".Translate(links.Count, Props.maxLinks) };
            if (!Powered) lines.Add("JLX_InspectNoPower".Translate());
            else if (MannedNow) lines.Add("JLX_InspectManned".Translate(operatorPawn.LabelShort));
            return string.Join("\n", lines);
        }
    }

    /// <summary>Draws the console (with the gravship look on substructure) and offers the "Man laser console" order.</summary>
    public class Building_LaserConsole : Building
    {
        public CompLaserConsole Console => GetComp<CompLaserConsole>();

#if RW14
        public override void DrawAt(Vector3 drawLoc, bool flip = false)
#else
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
#endif
        {
            Vector3 scale = new Vector3(def.graphicData.drawSize.x, 1f, def.graphicData.drawSize.y);
            Material mat = LaserArt.OnGravship(this) ? LaserArt.ShipMat("LaserConsole", "Base") : LaserArt.Mat("LaserConsole", "Base");
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawLoc, Rotation.AsQuat, scale), mat, 0);
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption o in base.GetFloatMenuOptions(selPawn)) yield return o;
            if (Faction != Faction.OfPlayer || selPawn.Faction != Faction.OfPlayer) yield break;
            if (selPawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                yield return new FloatMenuOption("JLX_CannotManViolent".Translate(selPawn.LabelShort), null);
                yield break;
            }
            if (Console != null && !Console.Powered)
            {
                yield return new FloatMenuOption("JLX_CannotManNoPower".Translate(), null);
                yield break;
            }
            if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly) || !selPawn.CanReserve(this))
            {
                yield return new FloatMenuOption("JLX_CannotManNoPath".Translate(), null);
                yield break;
            }
            yield return new FloatMenuOption("JLX_Man".Translate(LabelShort), () =>
            {
                Job job = JobMaker.MakeJob(LasersDefOf.JLX_ManLaserConsole, this);
                selPawn.jobs.TryTakeOrderedJob(job);
            });
        }
    }

    /// <summary>A colonist works the console, manning every linked laser while there.</summary>
    public class JobDriver_ManLaserConsole : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil man = ToilMaker.MakeToil("ManLaserConsole");
            man.tickAction = () =>
            {
                job.targetA.Thing.TryGetComp<CompLaserConsole>()?.ManForATick(pawn);
                pawn.rotationTracker.FaceTarget(job.targetA);
            };
            man.handlingFacing = true;
            man.defaultCompleteMode = ToilCompleteMode.Never;
            yield return man;
        }
    }
}
