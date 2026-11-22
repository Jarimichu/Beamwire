using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Beamwire
{
    /// <summary>
    /// Hides the parts of a beam that pass through the solid wall block of a beam window, so a beam shows only
    /// where it crosses the window's glass channel, as if it really went through a slit in a wall.
    /// The channel's shape follows the window's links exactly as its art does (Tools/art_beamwire.py,
    /// window_tile): a band 34/256 of a tile either side of the centre line, running to every linked side.
    /// Works for any straight beam, grid-aligned or not, so the lasers' beams use it too.
    /// </summary>
    public static class WindowOcclusion
    {
        private const float Half = 34f / 256f;          // half-width of the glass channel
        private const float OpenEnd = 0.5f - 28f / 256f; // where a channel ends on a side with no link
        private const float SampleStep = 0.1f;

        private static readonly List<Vector2> Hidden = new List<Vector2>();
        private static readonly List<Vector2> Spans = new List<Vector2>();
        private static readonly List<Vector2> Visible = new List<Vector2>();
        private static readonly HashSet<IntVec3> Seen = new HashSet<IntVec3>();

        /// <summary>
        /// The visible stretches of the line from <paramref name="a"/> to <paramref name="b"/>, as (start, end)
        /// distances along it. Without beam windows on the line that is the whole line. The returned list is
        /// reused: use it before calling again.
        /// </summary>
        /// <param name="cells">The cells the line crosses, if known (Beamwire segments); otherwise it samples.</param>
        public static List<Vector2> VisibleSpans(Map map, Vector3 a, Vector3 b, IEnumerable<IntVec3> cells = null)
        {
            Spans.Clear();
            Hidden.Clear();
            Seen.Clear();
            Vector3 d = b - a;
            d.y = 0f;
            float len = d.magnitude;
            if (len < 1e-4f) return Spans;
            Vector3 u = d / len;

            if (map != null)
            {
                if (cells != null)
                {
                    foreach (IntVec3 c in cells) Consider(map, c, a, u, len);
                }
                else
                {
                    int steps = Mathf.CeilToInt(len / SampleStep);
                    for (int i = 0; i <= steps; i++)
                    {
                        Consider(map, (a + u * (len * i / steps)).ToIntVec3(), a, u, len);
                    }
                }
            }

            Hidden.Sort((x, y) => x.x.CompareTo(y.x));
            float cur = 0f;
            foreach (Vector2 h in Hidden)
            {
                if (h.x > cur) Spans.Add(new Vector2(cur, Mathf.Min(h.x, len)));
                cur = Mathf.Max(cur, h.y);
                if (cur >= len) break;
            }
            if (cur < len) Spans.Add(new Vector2(cur, len));
            return Spans;
        }

        private static void Consider(Map map, IntVec3 c, Vector3 a, Vector3 u, float len)
        {
            if (!Seen.Add(c) || !c.InBounds(map)) return;
            if (!(c.GetEdifice(map) is Building_BeamWindow window)) return;

            // The line in the cell's own frame: origin at the cell centre, x east, z north.
            float ox = a.x - (c.x + 0.5f), oz = a.z - (c.z + 0.5f);
            if (!Slab(ox, oz, u.x, u.z, -0.5f, 0.5f, -0.5f, 0.5f, out float t0, out float t1)) return;
            t0 = Mathf.Max(t0, 0f);
            t1 = Mathf.Min(t1, len);
            if (t1 <= t0) return;

            LinkFlags flags = window.def.graphicData.linkFlags;
            bool up = Linked(map, c + IntVec3.North, flags), right = Linked(map, c + IntVec3.East, flags);
            bool down = Linked(map, c + IntVec3.South, flags), left = Linked(map, c + IntVec3.West, flags);
            bool hasH = left || right, hasV = up || down;

            Visible.Clear();
            if (hasH || !hasV)
            {
                float x0 = left ? -0.5f : (hasV ? -Half : -OpenEnd);
                float x1 = right ? 0.5f : (hasV ? Half : OpenEnd);
                AddVisible(ox, oz, u, x0, x1, -Half, Half, t0, t1);
            }
            if (hasV)
            {
                float z0 = down ? -0.5f : (hasH ? -Half : -OpenEnd);
                float z1 = up ? 0.5f : (hasH ? Half : OpenEnd);
                AddVisible(ox, oz, u, -Half, Half, z0, z1, t0, t1);
            }

            // Hidden = the cell's stretch minus the glass.
            Visible.Sort((x, y) => x.x.CompareTo(y.x));
            float cur = t0;
            foreach (Vector2 v in Visible)
            {
                if (v.x > cur) Hidden.Add(new Vector2(cur, v.x));
                cur = Mathf.Max(cur, v.y);
            }
            if (cur < t1) Hidden.Add(new Vector2(cur, t1));
        }

        private static void AddVisible(float ox, float oz, Vector3 u, float x0, float x1, float z0, float z1, float t0, float t1)
        {
            if (!Slab(ox, oz, u.x, u.z, x0, x1, z0, z1, out float s0, out float s1)) return;
            s0 = Mathf.Max(s0, t0);
            s1 = Mathf.Min(s1, t1);
            if (s1 > s0) Visible.Add(new Vector2(s0, s1));
        }

        private static bool Linked(Map map, IntVec3 c, LinkFlags flags) =>
            c.InBounds(map) && (map.linkGrid.LinkFlagsAt(c) & flags) != LinkFlags.None;

        /// <summary>Where the line o + t·u is inside the box, as t from <paramref name="tMin"/> to <paramref name="tMax"/>.</summary>
        private static bool Slab(float ox, float oz, float ux, float uz, float x0, float x1, float z0, float z1, out float tMin, out float tMax)
        {
            tMin = float.NegativeInfinity;
            tMax = float.PositiveInfinity;
            if (!Axis(ox, ux, x0, x1, ref tMin, ref tMax)) return false;
            if (!Axis(oz, uz, z0, z1, ref tMin, ref tMax)) return false;
            return tMax > tMin;
        }

        private static bool Axis(float o, float u, float lo, float hi, ref float tMin, ref float tMax)
        {
            if (Mathf.Abs(u) < 1e-6f) return o >= lo && o <= hi;
            float a = (lo - o) / u, b = (hi - o) / u;
            tMin = Mathf.Max(tMin, Mathf.Min(a, b));
            tMax = Mathf.Min(tMax, Mathf.Max(a, b));
            return true;
        }
    }
}
