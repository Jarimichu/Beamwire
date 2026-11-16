using Beamwire.Core;
using UnityEngine;
using Verse;

namespace Beamwire
{
    /// <summary>
    /// Draws beams as thin, Portal-style lasers: a narrow white-hot core over a faint additive glow.
    /// As in MiningCo DrillTurret, each beam is a plane mesh stretched along its path at projectile altitude.
    /// More power makes a beam brighter; its width hardly changes.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BeamRenderer
    {
        private const float CoreWidthMin = 0.05f;
        private const float CoreWidthMax = 0.10f;
        private const float GlowWidthFactor = 4f;
        private const float FullBrightnessWatts = 2000f;
        private const float NodeLensInset = 0.3f;

        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public static void Draw(BeamGraph graph)
        {
            float altitude = AltitudeLayer.Projectile.AltitudeFor();
            Color beam = BeamwireArt.BeamColor;
            // The core is mostly white with a hint of the style's colour; the halo carries the colour.
            Color coreTint = Color.Lerp(Color.white, beam, 0.25f);
            float time = Time.realtimeSinceStartup;

            foreach (BeamSegment s in graph.Segments)
            {
                Vector3 a = StartPoint(s);
                Vector3 b = EndPoint(s);
                a.y = b.y = altitude;
                Vector3 delta = b - a;
                float length = delta.magnitude;
                if (length < 0.05f)
                {
                    continue;
                }

                // Unpowered beams show as a dim pilot beam, so players can still line up mirrors.
                bool powered = s.Watts >= 1f;
                float frac = powered ? Mathf.Clamp01(s.Watts / FullBrightnessWatts) : 0f;
                float flicker = 1f + 0.06f * Mathf.Sin(time * 23f + s.From.Id * 1.7f);

                float coreWidth = Mathf.Lerp(CoreWidthMin, CoreWidthMax, frac) * flicker;
                float coreAlpha = powered ? Mathf.Lerp(0.75f, 1f, frac) : 0.35f;
                float glowAlpha = powered ? Mathf.Lerp(0.25f, 0.6f, frac) * flicker : 0.08f;

                Vector3 mid = (a + b) * 0.5f;
                Quaternion rot = Quaternion.AngleAxis((int)s.Dir * 45f, Vector3.up);

                // Glow first, then the core on top.
                Block.SetColor(ColorId, new Color(beam.r, beam.g, beam.b, glowAlpha));
                Graphics.DrawMesh(MeshPool.plane10,
                    Matrix4x4.TRS(mid, rot, new Vector3(coreWidth * GlowWidthFactor, 1f, length + coreWidth)),
                    BeamwireTex.BeamGlow, 0, null, 0, Block);

                Block.SetColor(ColorId, new Color(coreTint.r, coreTint.g, coreTint.b, coreAlpha));
                Graphics.DrawMesh(MeshPool.plane10,
                    Matrix4x4.TRS(mid + new Vector3(0f, 0.001f, 0f), rot, new Vector3(coreWidth, 1f, length)),
                    BeamwireTex.BeamCore, 0, null, 0, Block);
            }
        }

        public static Vector3 StartPoint(BeamSegment s)
        {
            return MapBeamGrid.ToCell(s.From.Pos).ToVector3Shifted() + DirVector(s.Dir) * NodeLensInset;
        }

        public static Vector3 EndPoint(BeamSegment s)
        {
            Vector3 dir = DirVector(s.Dir);
            switch (s.End)
            {
                case SegmentEnd.Node:
                    // Stop at the node's lens, or at its centre for mirrors and splitters so the bend looks clean.
                    Vector3 target = MapBeamGrid.ToCell(s.To.Pos).ToVector3Shifted();
                    return s.To.Kind == NodeKind.Mirror || s.To.Kind == NodeKind.Splitter
                        ? target
                        : target - dir * NodeLensInset;
                case SegmentEnd.Blocked:
                    // Touch the face (or corner, for diagonal beams) of whatever blocked it.
                    return MapBeamGrid.ToCell(s.EndPos).ToVector3Shifted() + dir * (s.Dir.IsDiagonal() ? 0.7f : 0.5f);
                default:
                    return MapBeamGrid.ToCell(s.EndPos).ToVector3Shifted() + dir * 0.5f;
            }
        }

        /// <summary>Unit vector for a beam direction.</summary>
        private static Vector3 DirVector(Dir8 d)
        {
            GridPos o = d.Offset();
            return new Vector3(o.X, 0f, o.Z).normalized;
        }
    }
}
