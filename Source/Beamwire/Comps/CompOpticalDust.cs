using UnityEngine;
using Verse;

namespace Beamwire
{
    /// <summary>Optics get dusty over time, faster outdoors. Colonists clean them using the Cleaning work type.</summary>
    public class CompOpticalDust : ThingComp
    {
        private const int Interval = 250;

        private float dust;

        public CompProperties_OpticalDust Props => (CompProperties_OpticalDust)props;

        public float Dust => dust;

        public float Efficiency => 1f - Props.maxEfficiencyLoss * dust;

        public bool NeedsCleaning => BeamwireMod.Settings.dustEnabled && dust >= Props.cleanThreshold;

        public override void CompTick()
        {
            base.CompTick();
            if (!BeamwireMod.Settings.dustEnabled || !parent.IsHashIntervalTick(Interval))
            {
                return;
            }
#if RW16
            // In vacuum there is nothing to settle on the optics (Odyssey).
            if (ModsConfig.OdysseyActive && parent.Spawned && VacuumUtility.GetVacuum(parent.Position, parent.Map) >= 0.5f)
            {
                return;
            }
#endif
            float rate = Props.dustPerDay * Interval / 60000f;
            if (parent.Map != null && parent.Position.Roofed(parent.Map))
            {
                rate *= Props.roofedFactor;
            }
            dust = Mathf.Min(1f, dust + rate);
        }

        public void Clean()
        {
            dust = 0f;
            parent.Map?.GetComponent<MapComponent_BeamNetwork>()?.MarkDirty();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref dust, "beamwireDust", 0f);
        }

        public override string CompInspectStringExtra()
        {
            if (!BeamwireMod.Settings.dustEnabled)
            {
                return null;
            }
            return "Beamwire_InspectDust".Translate(dust.ToStringPercent(), Efficiency.ToStringPercent());
        }
    }
}
