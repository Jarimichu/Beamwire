using UnityEngine;
using Verse;

namespace Beamwire
{
    /// <summary>
    /// A beam node building. It draws its base plate in the chosen art style, then the moving part
    /// (lens, mirror pane, splitter cube) at the node's 45° facing, which RimWorld's 4-way
    /// rotation can't show.
    /// </summary>
    public class Building_BeamNode : Building
    {
        private CompBeamNode node;
        private BeamNodeGraphicExtension ext;

        public CompBeamNode Node => node ?? (node = GetComp<CompBeamNode>());

        private BeamNodeGraphicExtension Ext => ext ?? (ext = def.GetModExtension<BeamNodeGraphicExtension>());

#if RW14
        public override void DrawAt(Vector3 drawLoc, bool flip = false)
#else
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
#endif
        {
            if (Ext?.node == null || Node == null)
            {
                base.DrawAt(drawLoc, flip);
                return;
            }
            Vector3 scale = new Vector3(def.graphicData.drawSize.x, 1f, def.graphicData.drawSize.y);

            // Fixed base plate, then the rotating top at the node's 45° facing.
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawLoc, Quaternion.identity, scale),
                BeamwireStyles.Mat(Ext.node, "Base"), 0);

            Vector3 topPos = drawLoc;
            topPos.y += Altitudes.AltInc;
            Graphics.DrawMesh(MeshPool.plane10,
                Matrix4x4.TRS(topPos, Quaternion.AngleAxis(Node.AngleDegrees, Vector3.up), scale),
                BeamwireStyles.Mat(Ext.node, "Top"), 0);
        }
    }

    /// <summary>XML: which node art set this building uses (Emitter, Receiver, Mirror, Splitter or Relay).</summary>
    public class BeamNodeGraphicExtension : DefModExtension
    {
        public string node;
    }
}
