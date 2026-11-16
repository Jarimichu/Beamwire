using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace JarimichusLaserCannon
{
    /// <summary>
    /// The three Jarimichus art styles. The laser uses its own setting, or follows Jarimichus Beamwire's chosen
    /// style when Beamwire is installed and the "follow Beamwire" setting is on (read by name; no dependency).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class LaserStyles
    {
        public static readonly string[] Folders = { "Standard", "Industrial", "Spacer" };
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly FieldInfo CachedGraphicField =
            typeof(GraphicData).GetField("cachedGraphic", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static FieldInfo beamwireSettings;
        private static FieldInfo beamwireStyle;
        private static bool lookedUp;
        private static int cachedFrame = -1;
        private static string cachedStyle;

        static LaserStyles()
        {
            Apply();
        }

        public static string Current
        {
            get
            {
                if (Time.frameCount == cachedFrame && cachedStyle != null) return cachedStyle;
                cachedFrame = Time.frameCount;
                string style = null;
                if (LaserCannonMod.Settings.followBeamwire) style = BeamwireStyle();
                if (Array.IndexOf(Folders, style) < 0) style = LaserCannonMod.Settings.artStyle;
                if (Array.IndexOf(Folders, style) < 0) style = Folders[0];
                cachedStyle = style;
                return style;
            }
        }

        public static bool BeamwireInstalled
        {
            get { LookUp(); return beamwireStyle != null; }
        }

        private static void LookUp()
        {
            if (lookedUp) return;
            lookedUp = true;
            Type mod = GenTypes.GetTypeInAnyAssembly("Beamwire.BeamwireMod");
            beamwireSettings = mod?.GetField("Settings", BindingFlags.Public | BindingFlags.Static);
            beamwireStyle = beamwireSettings?.FieldType.GetField("artStyle", BindingFlags.Public | BindingFlags.Instance);
        }

        private static string BeamwireStyle()
        {
            LookUp();
            if (beamwireStyle == null) return null;
            object settings = beamwireSettings.GetValue(null);
            return settings == null ? null : beamwireStyle.GetValue(settings) as string;
        }

        public static string TexPath(string part) => $"JarimichusLaserCannon/Styles/{Current}/Things/LaserCannon_{part}";

        public static Material Mat(string part)
        {
            string path = TexPath(part);
            if (!Materials.TryGetValue(path, out Material mat))
            {
                mat = MaterialPool.MatFrom(path, ShaderDatabase.Cutout);
                Materials[path] = mat;
            }
            return mat;
        }

        /// <summary>Points the architect icon and build ghost at the current style.</summary>
        public static void Apply()
        {
            cachedFrame = -1;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("JLC_LaserCannon");
            if (def == null) return;
            def.uiIconPath = TexPath("Icon");
            Texture2D icon = ContentFinder<Texture2D>.Get(def.uiIconPath, false);
            if (icon != null) def.uiIcon = icon;
            if (def.graphicData != null)
            {
                def.graphicData.texPath = TexPath("Base");
                CachedGraphicField?.SetValue(def.graphicData, null);
            }
        }
    }
}
