using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Beamwire
{
    /// <summary>
    /// A beam node building. It draws its base plate, then the moving part
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
                BeamwireArt.Mat(Ext.node, "Base"), 0);

            Vector3 topPos = drawLoc;
            topPos.y += Altitudes.AltInc;
            string topPart = Node.ShutterClosed ? "TopClosed" : "Top";
            Graphics.DrawMesh(MeshPool.plane10,
                Matrix4x4.TRS(topPos, Quaternion.AngleAxis(Node.AngleDegrees, Vector3.up), scale),
                BeamwireArt.Mat(Ext.node, topPart), 0);

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

    /// <summary>The look of a beam window.</summary>
    public enum BeamWindowLook
    {
        /// <summary>Stone look when built from stone, high-tech look otherwise.</summary>
        Auto,
        HighTech,
        Stone,
    }

    /// <summary>
    /// A beam window: a wall that beams pass through. It links with walls, natural and smoothed rock, and other
    /// windows, like a vanilla wall. Two looks: the high-tech one (white ceramic), and a stone one whose block takes
    /// the colour of the stone it's built from, so it blends into stone walls and smoothed rock; the glass stays teal.
    /// </summary>
    [StaticConstructorOnStartup]
    public class Building_BeamWindow : Building
    {
        private static Graphic linked;
        private static Material glass;
        private static readonly Dictionary<Color, Material> StoneMats = new Dictionary<Color, Material>();
        private static readonly IntVec3[] Cardinals = { IntVec3.North, IntVec3.East, IntVec3.South, IntVec3.West };

        public BeamWindowLook look = BeamWindowLook.Auto;

        /// <summary>
        /// The high-tech window's linked graphic, kept white: the stone or metal it is built from doesn't tint it.
        /// It links like a wall (see the def's linkFlags), picking its atlas tile from its neighbours.
        /// </summary>
        public override Graphic Graphic
        {
            get
            {
                if (linked == null)
                {
                    Graphic single = GraphicDatabase.Get<Graphic_Single>(BeamwireArt.TexPath("Window", "Atlas"), ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white);
                    linked = GraphicUtility.WrapLinked(single, LinkDrawerType.Basic);
                }
                return linked;
            }
        }

        /// <summary>True when this window shows the stone look.</summary>
        public bool StoneLook
        {
            get
            {
                if (look != BeamWindowLook.Auto) return look == BeamWindowLook.Stone;
                return Stuff?.stuffProps?.categories != null && Stuff.stuffProps.categories.Contains(StuffCategoryDefOf.Stony);
            }
        }

        public override void Print(SectionLayer layer)
        {
            if (!StoneLook)
            {
                base.Print(layer);
                return;
            }
            // Stone look: the block tinted with the stone's colour, then the glass on top, untinted.
            var links = (LinkDirections)LinkMask();
            Vector3 center = this.TrueCenter();
            center.y = def.Altitude;
            Printer_Plane.PrintPlane(layer, center, Vector2.one, MaterialAtlasPool.SubMaterialFromAtlas(StoneMat(DrawColor), links));
            center.y += 0.01f;
            Printer_Plane.PrintPlane(layer, center, Vector2.one, MaterialAtlasPool.SubMaterialFromAtlas(GlassMat, links));
        }

        /// <summary>Which neighbours this window links with, as RimWorld's linked graphics work it out.</summary>
        private int LinkMask()
        {
            int mask = 0, bit = 1;
            foreach (IntVec3 d in Cardinals)
            {
                IntVec3 c = Position + d;
                if (c.InBounds(Map) && (Map.linkGrid.LinkFlagsAt(c) & def.graphicData.linkFlags) != LinkFlags.None) mask |= bit;
                bit <<= 1;
            }
            return mask;
        }

        private static Material StoneMat(Color color)
        {
            if (!StoneMats.TryGetValue(color, out Material mat))
            {
                mat = MaterialPool.MatFrom(BeamwireArt.TexPath("Window", "StoneAtlas"), ShaderDatabase.Cutout, color);
                StoneMats[color] = mat;
            }
            return mat;
        }

        private static Material GlassMat => glass ?? (glass = MaterialPool.MatFrom(BeamwireArt.TexPath("Window", "GlassAtlas"), ShaderDatabase.Transparent));

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;
            if (Faction != Faction.OfPlayer) yield break;
            yield return new Command_Action
            {
                defaultLabel = ("Beamwire_WindowLook_" + look).Translate(),
                defaultDesc = "Beamwire_WindowLookDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get(BeamwireArt.TexPath("Window", "Icon"), false) ?? BaseContent.BadTex,
                action = () =>
                {
                    look = (BeamWindowLook)(((int)look + 1) % 3);
                    Map.mapDrawer.MapMeshDirty(Position,
#if RW14
                        MapMeshFlag.Things
#else
                        MapMeshFlagDefOf.Things
#endif
                        );
                },
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref look, "beamWindowLook", BeamWindowLook.Auto);
        }
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
