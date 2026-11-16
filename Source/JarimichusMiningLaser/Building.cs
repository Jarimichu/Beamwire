using UnityEngine;
using Verse;

namespace JarimichusMiningLaser
{
    /// <summary>Draws the base plate, the turning trigun head and, while cutting, the three red beams.</summary>
    public class Building_MiningLaser : Building
    {
        private CompMiningLaser laser;

        private CompMiningLaser Laser => laser ?? (laser = GetComp<CompMiningLaser>());

#if RW14
        public override void DrawAt(Vector3 drawLoc, bool flip = false)
#else
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
#endif
        {
            Vector3 scale = new Vector3(def.graphicData.drawSize.x, 1f, def.graphicData.drawSize.y);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawLoc, Quaternion.identity, scale), LaserStyles.Mat("Base"), 0);
            Vector3 top = drawLoc;
            top.y += Altitudes.AltInc;
            float angle = Laser != null ? Laser.HeadAngle : Rotation.AsAngle;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(top, Quaternion.AngleAxis(angle, Vector3.up), scale), LaserStyles.Mat("Top"), 0);

            if (Laser != null && Laser.Cutting)
            {
                TrigunRenderer.Draw(Position.ToVector3Shifted(), Laser.Target.ToVector3Shifted(), Laser.TotalWatts, thingIDNumber, 0.45f);
            }
        }
    }
}
