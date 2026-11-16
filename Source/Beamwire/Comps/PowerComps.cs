using Verse;
using RimWorld;

namespace Beamwire
{
    /// <summary>
    /// Emitter power comp. It consumes its idle draw from the XML plus whatever the beam is currently carrying.
    /// </summary>
    public class CompPowerTrader_BeamEmitter : CompPowerTrader
    {
        private float beamDraw;

        public float BeamDraw => beamDraw;

        public void SetBeamDraw(float watts)
        {
            beamDraw = watts;
            ApplyOutput();
        }

        public override void SetUpPowerVars()
        {
            base.SetUpPowerVars();
            ApplyOutput();
        }

        private void ApplyOutput()
        {
            PowerOutput = -(IdlePowerDraw + beamDraw);
        }

        private float IdlePowerDraw => Props.PowerConsumption;
    }

    /// <summary>
    /// Receiver power comp. It works like a generator whose output is whatever the beams deliver.
    /// </summary>
    public class CompBeamReceiver : CompPowerPlant
    {
        private float delivered;

        public float Delivered => delivered;

        public void SetDelivered(float watts)
        {
            delivered = watts;
        }

        protected override float DesiredPowerOutput => delivered;

        public override void PostExposeData()
        {
            base.PostExposeData();
            // Saved so power stays steady for the first moments after loading, before the network solves.
            Scribe_Values.Look(ref delivered, "beamwireDelivered", 0f);
        }
    }
}
