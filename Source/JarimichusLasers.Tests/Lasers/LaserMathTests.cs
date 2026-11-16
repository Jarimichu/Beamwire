using System.Linq;
using JarimichusLasers.Core;
using Xunit;

namespace JarimichusLasers.Tests
{
    public class LaserMathTests
    {
        [Fact]
        public void CellsBetween_StraightAndDiagonal()
        {
            Assert.Equal(new[] { (1, 0), (2, 0) }, LaserMath.CellsBetween(0, 0, 3, 0).ToArray());
            Assert.Equal(new[] { (1, 1), (2, 2) }, LaserMath.CellsBetween(0, 0, 3, 3).ToArray());
            Assert.Empty(LaserMath.CellsBetween(0, 0, 1, 1));
            Assert.Empty(LaserMath.CellsBetween(2, 2, 2, 2));
        }

        [Fact]
        public void CellsBetween_OddAngle_IsContinuousAndExcludesEnds()
        {
            var cells = LaserMath.CellsBetween(0, 0, 5, 2);
            Assert.DoesNotContain((0, 0), cells);
            Assert.DoesNotContain((5, 2), cells);
            var path = new[] { (0, 0) }.Concat(cells).Concat(new[] { (5, 2) }).ToList();
            for (int i = 1; i < path.Count; i++)
            {
                Assert.True(System.Math.Abs(path[i].Item1 - path[i - 1].Item1) <= 1);
                Assert.True(System.Math.Abs(path[i].Item2 - path[i - 1].Item2) <= 1);
            }
        }

        [Fact]
        public void Pulse_NeverFadesOut()
        {
            for (int seed = 0; seed < 5; seed++)
                for (int b = 0; b < 3; b++)
                    for (double t = 0; t < 10; t += 0.013)
                    {
                        float p = LaserMath.Pulse(t, b, seed);
                        Assert.InRange(p, 0.399f, 1.001f);
                    }
        }

        [Fact]
        public void Pulse_BarrelsAreIndependent()
        {
            Assert.NotEqual(LaserMath.Pulse(1.0, 0, 7), LaserMath.Pulse(1.0, 1, 7));
            Assert.NotEqual(LaserMath.Pulse(1.0, 1, 7), LaserMath.Pulse(1.0, 2, 7));
        }

        [Fact]
        public void TurnTowards_TakesTheShortWay_AndConverges()
        {
            Assert.Equal(-6f, LaserMath.TurnTowards(0f, 350f, 6f), 3); // 0 -> 350 turns left, not right
            float a = 10f;
            for (int i = 0; i < 100; i++) a = LaserMath.TurnTowards(a, 200f, 6f);
            Assert.Equal(200f, a, 3);
            Assert.Equal(180f, System.Math.Abs(LaserMath.DeltaAngle(0f, 180f)), 3);
        }

        [Fact]
        public void AngleTo_IsClockwiseFromNorth()
        {
            Assert.Equal(0f, LaserMath.AngleTo(0, 1), 3);
            Assert.Equal(90f, LaserMath.AngleTo(1, 0), 3);
            Assert.Equal(45f, LaserMath.AngleTo(1, 1), 3);
        }
    }
}
