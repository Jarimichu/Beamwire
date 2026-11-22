using JarimichusLasers.Core;
using UnityEngine;
using Verse;

namespace JarimichusLasers
{
    /// <summary>
    /// Draws the trigun's three thin orange beams from its three barrels, converging on a point. Each pulses on its own
    /// rhythm but never fades out. Always orange (an industrial laser).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TrigunRenderer
    {
        private static readonly Material Core = MaterialPool.MatFrom("JarimichusLasers/Effects/BeamCore", ShaderDatabase.Transparent);
        private static readonly Material Glow = MaterialPool.MatFrom("JarimichusLasers/Effects/BeamGlow", ShaderDatabase.MoteGlow);
        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color CoreColor = new Color(1f, 0.82f, 0.66f);
        private static readonly Color GlowColor = new Color(1f, 0.48f, 0.08f);
        private static readonly float[] BarrelOffsets = { -0.17f, 0f, 0.17f };
        private static readonly float[] EndOffsets = { -0.06f, 0f, 0.06f };

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
            float frac = Mathf.Clamp01(watts / 1500f);
            double time = Time.realtimeSinceStartup;

            for (int i = 0; i < 3; i++)
            {
                float pulse = LaserMath.Pulse(time, i, seed);
                Vector3 a = from + dir * muzzle + side * BarrelOffsets[i];
                Vector3 b = to - dir * endInset + side * EndOffsets[i];
                a.y = b.y = altitude + i * 0.0005f;
                float length = (b - a).magnitude;
                if (length < 0.05f) continue;
                Quaternion rot = Quaternion.LookRotation((b - a).normalized);
                float core = Mathf.Lerp(0.035f, 0.05f, frac) * (0.85f + 0.3f * pulse);
                Vector3 unit = (b - a) / length;
                Color glow = new Color(GlowColor.r, GlowColor.g, GlowColor.b, Mathf.Lerp(0.3f, 0.6f, frac) * pulse);
                Color coreColor = new Color(CoreColor.r, CoreColor.g, CoreColor.b, pulse);

                // Only the stretches not hidden inside a beam window's wall block (see Beamwire.WindowOcclusion).
                foreach (Vector2 span in Beamwire.WindowOcclusion.VisibleSpans(Find.CurrentMap, a, b))
                {
                    float spanLength = span.y - span.x;
                    if (spanLength < 0.01f) continue;
                    Vector3 mid = a + unit * ((span.x + span.y) * 0.5f);
                    bool cut = span.x > 0.001f || span.y < length - 0.001f;
                    Block.SetColor(ColorId, glow);
                    Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(mid, rot, new Vector3(core * 3f, 1f, spanLength + (cut ? 0f : core))), Glow, 0, null, 0, Block);
                    Block.SetColor(ColorId, coreColor);
                    Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(mid + new Vector3(0f, 0.001f, 0f), rot, new Vector3(core, 1f, spanLength)), Core, 0, null, 0, Block);
                }
            }
        }
    }
}
