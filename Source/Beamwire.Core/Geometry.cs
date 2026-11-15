using System;

namespace Beamwire.Core
{
    /// <summary>
    /// The eight beam directions, clockwise from North in 45° steps. North, East, South and West
    /// (0, 2, 4, 6) line up with RimWorld's Rot4 when multiplied by 2.
    /// </summary>
    public enum Dir8
    {
        North = 0,
        NorthEast = 1,
        East = 2,
        SouthEast = 3,
        South = 4,
        SouthWest = 5,
        West = 6,
        NorthWest = 7,
    }

    public static class Dir8Ext
    {
        public const float Sqrt2 = 1.41421356f;

        public static Dir8 Rotate(this Dir8 d, int steps) => (Dir8)Mod((int)d + steps, 8);

        public static Dir8 Opposite(this Dir8 d) => d.Rotate(4);

        public static bool IsDiagonal(this Dir8 d) => ((int)d & 1) == 1;

        /// <summary>Length of one step in this direction, in tiles.</summary>
        public static float StepLength(this Dir8 d) => d.IsDiagonal() ? Sqrt2 : 1f;

        /// <summary>The direction as a multiple of 22.5° (0..15).</summary>
        public static int To16(this Dir8 d) => (int)d * 2;

        public static GridPos Offset(this Dir8 d)
        {
            switch (d)
            {
                case Dir8.North: return new GridPos(0, 1);
                case Dir8.NorthEast: return new GridPos(1, 1);
                case Dir8.East: return new GridPos(1, 0);
                case Dir8.SouthEast: return new GridPos(1, -1);
                case Dir8.South: return new GridPos(0, -1);
                case Dir8.SouthWest: return new GridPos(-1, -1);
                case Dir8.West: return new GridPos(-1, 0);
                case Dir8.NorthWest: return new GridPos(-1, 1);
                default: throw new ArgumentOutOfRangeException(nameof(d));
            }
        }

        public static int Mod(int x, int m)
        {
            int r = x % m;
            return r < 0 ? r + m : r;
        }

        /// <summary>Wraps an angle in 22.5° units into the range -7..8.</summary>
        public static int Signed16(int x)
        {
            int r = Mod(x, 16);
            return r > 8 ? r - 16 : r;
        }
    }

    /// <summary>Integer map cell. X runs east, Z runs north (RimWorld convention).</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Z;

        public GridPos(int x, int z)
        {
            X = x;
            Z = z;
        }

        public static GridPos operator +(GridPos a, GridPos b) => new GridPos(a.X + b.X, a.Z + b.Z);

        public static bool operator ==(GridPos a, GridPos b) => a.X == b.X && a.Z == b.Z;

        public static bool operator !=(GridPos a, GridPos b) => !(a == b);

        public GridPos Step(Dir8 d) => this + d.Offset();

        public bool Equals(GridPos other) => this == other;

        public override bool Equals(object obj) => obj is GridPos other && this == other;

        public override int GetHashCode() => unchecked((X * 397) ^ Z);

        public override string ToString() => $"({X}, {Z})";
    }
}
