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
    /// Mining laser power comp. It uses beam power first and draws whatever is still missing from its own power
    /// grid, plus a small idle draw.
    /// </summary>
    public class CompPowerTrader_MiningLaser : CompPowerTrader
    {
        private float gridDraw;

        /// <summary>Watts the laser is asking its grid for right now (not counting the idle draw).</summary>
        public float GridDraw => gridDraw;

        /// <summary>Watts actually arriving from the grid: nothing if it isn't connected or the grid is out of power.</summary>
        public float GridSupply => PowerNet != null && PowerOn ? gridDraw : 0f;

        public void SetGridDraw(float watts)
        {
            gridDraw = watts;
            ApplyOutput();
        }

        public override void SetUpPowerVars()
        {
            base.SetUpPowerVars();
            ApplyOutput();
        }

        private void ApplyOutput()
        {
            PowerOutput = -(Props.PowerConsumption + gridDraw);
        }
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
