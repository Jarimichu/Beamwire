using JarimichusLaserCannon.Core;
using UnityEngine;
using Verse;

namespace JarimichusLaserCannon
{
    /// <summary>
    /// Draws the cannon's two thin green beams from its two barrels, converging on a point. Each pulses on its own
    /// rhythm but never fades out. Green in every art style.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TwinBeamRenderer
    {
        private static readonly Material Core = MaterialPool.MatFrom("JarimichusLaserCannon/Effects/BeamCore", ShaderDatabase.Transparent);
        private static readonly Material Glow = MaterialPool.MatFrom("JarimichusLaserCannon/Effects/BeamGlow", ShaderDatabase.MoteGlow);
        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color CoreColor = new Color(0.62f, 1f, 0.62f);
        private static readonly Color GlowColor = new Color(0.15f, 1f, 0.28f);
        private static readonly float[] BarrelOffsets = { -0.12f, 0.12f };
        private static readonly float[] EndOffsets = { -0.04f, 0.04f };

        /// <param name="from">The turret's centre.</param>
        /// <param name="to">The point the beams converge on.</param>
        /// <param name="endInset">How far short of <paramref name="to"/> the beams stop (e.g. a rock face).</param>
        public static void Draw(Vector3 from, Vector3 to, float watts, int seed, float endInset, float muzzle = 0.42f)
        {
            Vector3 flat = to - from;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return;
            Vector3 dir = flat.normalized;
            Vector3 side = new Vector3(dir.z, 0f, -dir.x);
            float altitude = AltitudeLayer.Projectile.AltitudeFor();
            float frac = Mathf.Clamp01(watts / 2000f);
            double time = Time.realtimeSinceStartup;

            for (int i = 0; i < BarrelOffsets.Length; i++)
            {
                float pulse = LaserMath.Pulse(time, i, seed);
                Vector3 a = from + dir * muzzle + side * BarrelOffsets[i];
                Vector3 b = to - dir * endInset + side * EndOffsets[i];
                a.y = b.y = altitude + i * 0.0005f;
                float length = (b - a).magnitude;
                if (length < 0.05f) continue;
                Vector3 mid = (a + b) * 0.5f;
                Quaternion rot = Quaternion.LookRotation((b - a).normalized);
                float core = Mathf.Lerp(0.035f, 0.05f, frac) * (0.85f + 0.3f * pulse);

                Block.SetColor(ColorId, new Color(GlowColor.r, GlowColor.g, GlowColor.b, Mathf.Lerp(0.3f, 0.6f, frac) * pulse));
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(mid, rot, new Vector3(core * 3f, 1f, length + core)), Glow, 0, null, 0, Block);
                Block.SetColor(ColorId, new Color(CoreColor.r, CoreColor.g, CoreColor.b, pulse));
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(mid + new Vector3(0f, 0.001f, 0f), rot, new Vector3(core, 1f, length)), Core, 0, null, 0, Block);
            }
        }
    }
}
