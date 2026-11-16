using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace JarimichusLasers
{
    /// <summary>
    /// Art for the laser buildings (mining laser, laser cannon, laser control console): the high-tech look, plus
    /// the gravship look used on Odyssey substructure.
    /// Textures: JarimichusLasers/Things/&lt;Building&gt;_{Base,Top,Icon} and JarimichusLasers/Ship/&lt;Building&gt;_Ship{Base,Top}.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class LaserArt
    {
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        /// <summary>A part of a laser building.</summary>
        public static Material Mat(string building, string part) => CachedMat($"JarimichusLasers/Things/{building}_{part}");

        /// <summary>The gravship hull-mount look (Odyssey), used while the building stands on substructure.</summary>
        public static Material ShipMat(string building, string part) => CachedMat($"JarimichusLasers/Ship/{building}_Ship{part}");

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
    }
}
