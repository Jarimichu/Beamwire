using System.Collections.Generic;
using JarimichusLasers.Core;
using Xunit;

namespace JarimichusLasers.Tests
{
    public class FriendlyFireTests
    {
        private static bool Check(int bx, int bz, params (int, int)[] friendlies)
        {
            var set = new HashSet<(int, int)>(friendlies);
            return LaserMath.FriendlyOnLine(0, 0, bx, bz, (x, z) => set.Contains((x, z)));
        }

        [Fact]
        public void FriendlyBetween_HoldsFire() => Assert.True(Check(6, 0, (3, 0)));

        [Fact]
        public void FriendlyOnAnOddAngleLine_HoldsFire()
        {
            var cells = LaserMath.CellsBetween(0, 0, 7, 3);
            Assert.True(Check(7, 3, cells[2]));
        }

        [Fact]
        public void FriendlyOffTheLine_DoesNotHoldFire() => Assert.False(Check(6, 0, (3, 1), (7, 0), (-1, 0)));

        [Fact]
        public void TurretAndTargetCellsDontCount() => Assert.False(Check(6, 0, (0, 0), (6, 0)));
    }
}
