using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace JarimichusLasers
{
    /// <summary>
    /// Art for the laser buildings (mining laser, laser cannon, laser control console) in the mod-wide art style,
    /// plus the gravship look used on Odyssey substructure.
    /// Textures: JarimichusLasers/Styles/&lt;Style&gt;/Things/&lt;Building&gt;_{Base,Top,Icon} and
    /// JarimichusLasers/Ship/&lt;Building&gt;_Ship{Base,Top}.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class LaserStyles
    {
        public static readonly string[] Folders = { "Standard", "Industrial", "Spacer" };

        /// <summary>The laser buildings and their defs.</summary>
        private static readonly Dictionary<string, string> Buildings = new Dictionary<string, string>
        {
            { "JML_MiningLaser", "MiningLaser" },
            { "JLC_LaserCannon", "LaserCannon" },
            { "JLX_LaserConsole", "LaserConsole" },
        };

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly FieldInfo CachedGraphicField =
            typeof(GraphicData).GetField("cachedGraphic", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        static LaserStyles()
        {
            Apply();
        }

        public static string Current
        {
            get
            {
                string style = LasersMod.Settings?.artStyle;
                return System.Array.IndexOf(Folders, style) >= 0 ? style : Folders[0];
            }
        }

        public static string TexPath(string building, string part) => $"JarimichusLasers/Styles/{Current}/Things/{building}_{part}";

        /// <summary>A part of a laser building in the current art style.</summary>
        public static Material StyleMat(string building, string part) => CachedMat(TexPath(building, part));

        /// <summary>The gravship hull-mount look (Odyssey): one look in every art style, used on substructure.</summary>
        public static Material ShipMatFor(string building, string part) => CachedMat($"JarimichusLasers/Ship/{building}_Ship{part}");

        private static Material CachedMat(string path)
        {
            if (!Materials.TryGetValue(path, out Material mat))
            {
                mat = MaterialPool.MatFrom(path, ShaderDatabase.Cutout);
                Materials[path] = mat;
            }
            return mat;
        }

        /// <summary>True when the thing stands on gravship substructure (Odyssey, RimWorld 1.6).</summary>
        public static bool OnGravship(Thing t)
        {
#if RW16
            if (!ModsConfig.OdysseyActive || !t.Spawned) return false;
            TerrainDef terrain = t.Position.GetTerrain(t.Map);
            return terrain != null && terrain.IsSubstructure;
#else
            return false;
#endif
        }

        /// <summary>Points each laser building's architect icon and build ghost at the current style.</summary>
        public static void Apply()
        {
            foreach (KeyValuePair<string, string> b in Buildings)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(b.Key);
                if (def == null) continue; // that part is switched off
                def.uiIconPath = TexPath(b.Value, "Icon");
                Texture2D icon = ContentFinder<Texture2D>.Get(def.uiIconPath, false);
                if (icon != null) def.uiIcon = icon;
                if (def.graphicData != null)
                {
                    def.graphicData.texPath = TexPath(b.Value, "Base");
                    CachedGraphicField?.SetValue(def.graphicData, null);
                }
            }
        }
    }
}
