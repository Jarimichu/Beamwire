using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Beamwire
{
    public class WorkGiver_CleanOptics : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            MapComponent_BeamNetwork net = pawn.Map?.GetComponent<MapComponent_BeamNetwork>();
            if (net == null) yield break;
            foreach (Thing t in net.DustyNodes())
            {
                yield return t;
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!BeamwireMod.Settings.dustEnabled) return true;
            MapComponent_BeamNetwork net = pawn.Map?.GetComponent<MapComponent_BeamNetwork>();
            return net == null || !net.AnyDustyNodes();
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompOpticalDust dust = t.TryGetComp<CompOpticalDust>();
            if (dust == null || !dust.NeedsCleaning) return false;
            if (t.Faction != pawn.Faction) return false;
            if (t.IsForbidden(pawn) || t.IsBurning()) return false;
            if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null) return false;
            return pawn.CanReserve(t, 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(BeamwireDefOf.Beamwire_CleanOptics, t);
        }
    }

    public class JobDriver_CleanOptics : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            int ticks = job.targetA.Thing.TryGetComp<CompOpticalDust>()?.Props.cleanTicks ?? 240;
            yield return Toils_General.Wait(ticks)
                .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
                .WithProgressBarToilDelay(TargetIndex.A);

            Toil finish = ToilMaker.MakeToil("CleanOptics");
            finish.initAction = () => job.targetA.Thing.TryGetComp<CompOpticalDust>()?.Clean();
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
