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
            string topPart = Node.ShutterClosed ? "TopClosed" : "Top";
            Graphics.DrawMesh(MeshPool.plane10,
                Matrix4x4.TRS(topPos, Quaternion.AngleAxis(Node.AngleDegrees, Vector3.up), scale),
                BeamwireStyles.Mat(Ext.node, topPart), 0);

            if (Node.Kind == Core.NodeKind.Capacitor)
            {
                GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest
                {
                    center = drawLoc + new Vector3(0f, Altitudes.AltInc * 2f, -0.36f),
                    size = new Vector2(0.7f, 0.1f),
                    fillPercent = Node.StoredFraction,
                    filledMat = ChargeFilled,
                    unfilledMat = ChargeEmpty,
                    margin = 0.02f,
                    rotation = Rot4.North,
                });
            }
        }

        private static readonly Material ChargeFilled = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.35f, 0.85f, 0.45f));
        private static readonly Material ChargeEmpty = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.12f, 0.12f, 0.12f));
    }

    /// <summary>
    /// A beam window: a wall that beams pass through. It looks up its graphic from the chosen art style, so
    /// switching style re-skins windows that are already built.
    /// </summary>
    public class Building_BeamWindow : Building
    {
        public override Graphic Graphic =>
            GraphicDatabase.Get<Graphic_Single>(BeamwireStyles.TexPath("Window", "Base"), ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white);
    }

    /// <summary>XML: marks a building that beams pass straight through, even though it is impassable.</summary>
    public class BeamTransparentExtension : DefModExtension
    {
    }

    /// <summary>XML: which node art set this building uses (Emitter, Receiver, Mirror, Splitter or Relay).</summary>
    public class BeamNodeGraphicExtension : DefModExtension
    {
        public string node;
    }
}
