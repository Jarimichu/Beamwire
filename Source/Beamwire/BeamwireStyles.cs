using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Beamwire
{
    /// <summary>One selectable art style: its texture folder and beam colour.</summary>
    public class BeamwireStyle
    {
        public string Folder;
        public string LabelKey;
        public Color BeamColor;

        public string Label => LabelKey.Translate();
    }

    /// <summary>
    /// The art styles players can pick in the mod settings. Switching style changes node art, architect
    /// icons, build ghosts and beam colour at once, with no restart.
    /// Textures live in Textures/Beamwire/Styles/&lt;Folder&gt;/Things/&lt;Node&gt;_{Base,Top,Icon}.png.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BeamwireStyles
    {
        public static readonly List<BeamwireStyle> All = new List<BeamwireStyle>
        {
            new BeamwireStyle { Folder = "Standard", LabelKey = "Beamwire_Style_Standard", BeamColor = new Color32(255, 214, 120, 255) },
            new BeamwireStyle { Folder = "Industrial", LabelKey = "Beamwire_Style_Industrial", BeamColor = new Color32(255, 150, 40, 255) },
            new BeamwireStyle { Folder = "Spacer", LabelKey = "Beamwire_Style_Spacer", BeamColor = new Color32(90, 240, 220, 255) },
        };

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly FieldInfo CachedGraphicField =
            typeof(GraphicData).GetField("cachedGraphic", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        static BeamwireStyles()
        {
            Apply();
        }

        public static BeamwireStyle Current
        {
            get
            {
                string folder = BeamwireMod.Settings?.artStyle;
                foreach (BeamwireStyle s in All)
                {
                    if (s.Folder == folder) return s;
                }
                return All[0];
            }
        }

        public static string TexPath(string node, string part) => TexPath(Current, node, part);

        public static string TexPath(BeamwireStyle style, string node, string part) =>
            $"Beamwire/Styles/{style.Folder}/Things/{node}_{part}";

        /// <summary>Cached material for a node part in the current style.</summary>
        public static Material Mat(string node, string part)
        {
            string path = TexPath(node, part);
            if (!Materials.TryGetValue(path, out Material mat))
            {
                mat = MaterialPool.MatFrom(path, ShaderDatabase.Cutout);
                Materials[path] = mat;
            }
            return mat;
        }

        /// <summary>
        /// Points every Beamwire building's architect icon and build-ghost graphic at the current style.
        /// Placed buildings draw themselves from <see cref="Mat"/>, so they switch automatically.
        /// </summary>
        public static void Apply()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                var ext = def.GetModExtension<BeamNodeGraphicExtension>();
                if (ext?.node == null) continue;

                def.uiIconPath = TexPath(ext.node, "Icon");
                Texture2D icon = ContentFinder<Texture2D>.Get(def.uiIconPath, false);
                if (icon != null)
                {
                    def.uiIcon = icon;
                }

                if (def.graphicData != null)
                {
                    def.graphicData.texPath = TexPath(ext.node, "Base");
                    // Drop the cached graphic so ghosts and minified items pick up the new texture.
                    CachedGraphicField?.SetValue(def.graphicData, null);
                }
            }

            // Beam windows are baked into the map mesh, so redraw it.
            if (Verse.Current.ProgramState == ProgramState.Playing && Find.Maps != null)
            {
                foreach (Map map in Find.Maps)
                {
                    map.mapDrawer.RegenerateEverythingNow();
                }
            }
        }
    }
}
