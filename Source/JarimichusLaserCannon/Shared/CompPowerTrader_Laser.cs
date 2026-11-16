using Verse;
using RimWorld;

namespace JarimichusLaserCannon
{
    /// <summary>
    /// The laser's own grid connection. It asks its grid for whatever beam power (from Jarimichus Beamwire, if
    /// installed) doesn't cover, plus a small idle draw.
    /// </summary>
    public class CompPowerTrader_Laser : CompPowerTrader
    {
        private float gridDraw;

        public float GridDraw => gridDraw;

        /// <summary>Watts actually arriving from the grid: nothing if not connected or the grid is out of power.</summary>
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
}
