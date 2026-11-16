using System;
using System.Collections.Generic;

namespace JarimichusLasers.Core
{
    /// <summary>Pure maths shared by the Jarimichus lasers (kept in sync by hand between the mods).</summary>
    public static class LaserMath
    {
        private static readonly float[] PulseFreqs = { 9.1f, 13.3f, 17.2f };

        /// <summary>Bresenham line: the cells strictly between (ax, az) and (bx, bz).</summary>
        public static List<(int x, int z)> CellsBetween(int ax, int az, int bx, int bz)
        {
            var cells = new List<(int, int)>();
            if (ax == bx && az == bz) return cells;
            int x = ax, z = az;
            int dx = Math.Abs(bx - ax), dz = Math.Abs(bz - az);
            int sx = ax < bx ? 1 : -1, sz = az < bz ? 1 : -1;
            int err = dx - dz;
            while (true)
            {
                int e2 = 2 * err;
                if (e2 > -dz) { err -= dz; x += sx; }
                if (e2 < dx) { err += dx; z += sz; }
                if (x == bx && z == bz) break;
                cells.Add((x, z));
            }
            return cells;
        }

        /// <summary>
        /// Brightness of one barrel's beam at a moment: each barrel swells and dims on its own rhythm but never
        /// fades out (always between 0.4 and 1).
        /// </summary>
        public static float Pulse(double time, int barrel, int seed)
        {
            double phase = (seed * 0.618 + barrel * 2.1) % (Math.PI * 2);
            double s = 0.5 + 0.5 * Math.Sin(time * PulseFreqs[barrel % 3] + phase);
            return (float)(0.4 + 0.6 * s);
        }

        /// <summary>Signed shortest difference from angle a to angle b, in degrees (-180..180].</summary>
        public static float DeltaAngle(float a, float b)
        {
            float d = ((b - a) % 360f + 540f) % 360f - 180f;
            return d == -180f ? 180f : d;
        }

        /// <summary>Turns <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxStep"/> degrees, the short way round.</summary>
        public static float TurnTowards(float current, float target, float maxStep)
        {
            float d = DeltaAngle(current, target);
            if (Math.Abs(d) <= maxStep) return target;
            return current + Math.Sign(d) * maxStep;
        }

        /// <summary>
        /// True when anyone the turret must not hurt stands strictly between the turret at (ax, az) and its target at
        /// (bx, bz). <paramref name="friendlyAt"/> says whether a cell holds such a pawn.
        /// </summary>
        public static bool FriendlyOnLine(int ax, int az, int bx, int bz, Func<int, int, bool> friendlyAt)
        {
            foreach (var c in CellsBetween(ax, az, bx, bz))
            {
                if (friendlyAt(c.x, c.z)) return true;
            }
            return false;
        }

        /// <summary>Angle in degrees clockwise from North for a step of (dx, dz).</summary>
        public static float AngleTo(float dx, float dz) => (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
    }
}
