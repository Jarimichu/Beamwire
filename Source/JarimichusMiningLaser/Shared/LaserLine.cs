using System;
using System.Collections.Generic;
using System.Reflection;
using JarimichusMiningLaser.Core;
using RimWorld;
using Verse;

namespace JarimichusMiningLaser
{
    /// <summary>
    /// Line-of-sight rules for the laser. Impassable buildings block it, except Jarimichus Beamwire's beam windows.
    /// A closed Beamwire safety shutter pauses it. Both are recognised by name, so Beamwire is optional.
    /// </summary>
    public static class LaserLine
    {
        private static readonly Dictionary<ThingDef, bool> Transparent = new Dictionary<ThingDef, bool>();
        private static readonly Dictionary<Type, PropertyInfo> BlocksLasersProps = new Dictionary<Type, PropertyInfo>();
        private static readonly List<IntVec3> Tmp = new List<IntVec3>();

        public static void CellsBetween(IntVec3 a, IntVec3 b, List<IntVec3> into)
        {
            into.Clear();
            foreach (var c in LaserMath.CellsBetween(a.x, a.z, b.x, b.z)) into.Add(new IntVec3(c.x, 0, c.z));
        }

        /// <summary>True when something solid (not counting shutters) stops the laser in this cell.</summary>
        public static bool Blocks(IntVec3 c, Map map)
        {
            if (!c.InBounds(map)) return true;
            Building e = c.GetEdifice(map);
            return e != null && e.def.passability == Traversability.Impassable && !IsBeamTransparent(e.def);
        }

        /// <summary>
        /// True when the laser can see from a to b: nothing solid strictly between them, and a diagonal step can't
        /// squeeze between two corner-touching blockers.
        /// </summary>
        public static bool LineClear(IntVec3 a, IntVec3 b, Map map)
        {
            CellsBetween(a, b, Tmp);
            Tmp.Add(b);
            IntVec3 prev = a;
            foreach (IntVec3 c in Tmp)
            {
                if (c.x != prev.x && c.z != prev.z &&
                    Blocks(new IntVec3(c.x, 0, prev.z), map) && Blocks(new IntVec3(prev.x, 0, c.z), map))
                {
                    return false;
                }
                if (c != b && Blocks(c, map)) return false;
                prev = c;
            }
            return true;
        }

        /// <summary>True while a closed Beamwire safety shutter stands on any of these cells.</summary>
        public static bool AnyClosedShutter(List<IntVec3> cells, Map map)
        {
            foreach (IntVec3 c in cells)
            {
                if (!c.InBounds(map)) continue;
                foreach (Thing t in c.GetThingList(map))
                {
                    if (t is ThingWithComps twc && BlocksLasers(twc)) return true;
                }
            }
            return false;
        }

        private static bool BlocksLasers(ThingWithComps t)
        {
            foreach (ThingComp comp in t.AllComps)
            {
                Type type = comp.GetType();
                if (!BlocksLasersProps.TryGetValue(type, out PropertyInfo prop))
                {
                    prop = type.GetProperty("BlocksLasers", BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null && prop.PropertyType != typeof(bool)) prop = null;
                    BlocksLasersProps[type] = prop;
                }
                if (prop != null && (bool)prop.GetValue(comp, null)) return true;
            }
            return false;
        }

        private static bool IsBeamTransparent(ThingDef def)
        {
            if (!Transparent.TryGetValue(def, out bool yes))
            {
                yes = false;
                if (def.modExtensions != null)
                {
                    foreach (DefModExtension ext in def.modExtensions)
                    {
                        if (ext.GetType().FullName == "Beamwire.BeamTransparentExtension") yes = true;
                    }
                }
                Transparent[def] = yes;
            }
            return yes;
        }
    }
}
