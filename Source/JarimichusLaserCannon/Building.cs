using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace JarimichusLaserCannon
{
    /// <summary>Draws the cannon and its beams, and offers the "Man laser cannon" order.</summary>
    public class Building_LaserCannon : Building
    {
        private CompLaserCannon cannon;

        public CompLaserCannon Cannon => cannon ?? (cannon = GetComp<CompLaserCannon>());

#if RW14
        public override void DrawAt(Vector3 drawLoc, bool flip = false)
#else
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
#endif
        {
            Vector3 scale = new Vector3(def.graphicData.drawSize.x, 1f, def.graphicData.drawSize.y);
            // On a gravship's deck it uses the flush hull-mount look instead of the free-standing one.
            bool ship = LaserStyles.OnGravship(this);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawLoc, Quaternion.identity, scale), ship ? LaserStyles.ShipMat("Base") : LaserStyles.Mat("Base"), 0);
            Vector3 top = drawLoc;
            top.y += Altitudes.AltInc;
            float angle = Cannon != null ? Cannon.HeadAngle : Rotation.AsAngle;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(top, Quaternion.AngleAxis(angle, Vector3.up), scale), ship ? LaserStyles.ShipMat("Top") : LaserStyles.Mat("Top"), 0);

            if (Cannon != null && Cannon.Firing && Cannon.Target != null)
            {
                TwinBeamRenderer.Draw(Position.ToVector3Shifted(), Cannon.Target.DrawPos, Cannon.TotalWatts, thingIDNumber, 0.15f, 0.48f);
            }
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption o in base.GetFloatMenuOptions(selPawn))
            {
                yield return o;
            }
            if (Faction != Faction.OfPlayer || selPawn.Faction != Faction.OfPlayer) yield break;
            if (selPawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                yield return new FloatMenuOption("JLC_CannotManViolent".Translate(selPawn.LabelShort), null);
                yield break;
            }
            if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
            {
                yield return new FloatMenuOption("JLC_CannotManNoPath".Translate(), null);
                yield break;
            }
            if (!selPawn.CanReserve(this))
            {
                yield return new FloatMenuOption("JLC_CannotManReserved".Translate(), null);
                yield break;
            }
            yield return new FloatMenuOption("JLC_Man".Translate(LabelShort), () =>
            {
                Job job = JobMaker.MakeJob(LaserCannonDefOf.JLC_ManLaserCannon, this);
                selPawn.jobs.TryTakeOrderedJob(job);
            });
        }
    }

    /// <summary>A colonist stands at the cannon's controls, boosting its range and damage while there.</summary>
    public class JobDriver_ManLaserCannon : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            Toil man = ToilMaker.MakeToil("ManLaserCannon");
            man.tickAction = () =>
            {
                CompLaserCannon c = job.targetA.Thing.TryGetComp<CompLaserCannon>();
                c?.ManForATick(pawn);
                pawn.rotationTracker.FaceTarget(job.targetA);
            };
            man.handlingFacing = true;
            man.defaultCompleteMode = ToilCompleteMode.Never;
            yield return man;
        }
    }
}
