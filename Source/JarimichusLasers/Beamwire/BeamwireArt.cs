using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Beamwire
{
    /// <summary>
    /// Beamwire's art: the high-tech look (white ceramic, teal light strips) and its teal power beams.
    /// Textures live in Textures/Beamwire/Things/&lt;Node&gt;_{Base,Top,Icon}.png.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BeamwireArt
    {
        /// <summary>The colour of power beams and of the beam preview while placing a node.</summary>
        public static readonly Color BeamColor = new Color32(90, 240, 220, 255);

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        public static string TexPath(string node, string part) => $"Beamwire/Things/{node}_{part}";

        /// <summary>Cached material for a node part.</summary>
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
    }
}
