using UnityEngine;
using Verse;

namespace Beamwire
{
    /// <summary>
    /// A beam node building. Its base plate is the ordinary graphic; on top it draws the moving part
    /// (lens, mirror pane, splitter pane) at the node's 22.5° orientation, which RimWorld's 4-way
    /// rotation can't show.
    /// </summary>
    public class Building_BeamNode : Building
    {
        private CompBeamNode node;
        private Material topMat;

        public CompBeamNode Node => node ?? (node = GetComp<CompBeamNode>());

        private Material TopMat
        {
            get
            {
                if (topMat == null)
                {
                    var ext = def.GetModExtension<BeamNodeGraphicExtension>();
                    if (ext?.topTexPath != null)
                    {
                        topMat = MaterialPool.MatFrom(ext.topTexPath, ShaderDatabase.Cutout);
                    }
                }
                return topMat;
            }
        }

#if RW14
        public override void DrawAt(Vector3 drawLoc, bool flip = false)
#else
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
#endif
        {
            base.DrawAt(drawLoc, flip);
            Material mat = TopMat;
            if (mat == null || Node == null)
            {
                return;
            }
            Vector3 pos = drawLoc;
            pos.y += Altitudes.AltInc;
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(Node.AngleDegrees, Vector3.up), new Vector3(def.graphicData.drawSize.x, 1f, def.graphicData.drawSize.y));
            Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);
        }
    }

    /// <summary>XML: the texture for the rotating top part of a beam node.</summary>
    public class BeamNodeGraphicExtension : DefModExtension
    {
        public string topTexPath;
    }
}
