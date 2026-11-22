using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace JarimichusLasers
{
    /// <summary>A laser fence post: draws itself, and the laser curtain of every stretch of fence it owns.</summary>
    [StaticConstructorOnStartup]
    public class Building_LaserGatePost : Building
    {
        private static readonly Material Core = MaterialPool.MatFrom("JarimichusLasers/Effects/BeamCore", ShaderDatabase.Transparent);
        private static readonly Material Glow = MaterialPool.MatFrom("JarimichusLasers/Effects/BeamGlow", ShaderDatabase.MoteGlow);
        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color CurtainColor = new Color(1f, 0.16f, 0.12f);
        private static readonly Color CoreColor = new Color(1f, 0.72f, 0.66f);
        private static readonly float[] Strands = { -0.2f, -0.07f, 0.07f, 0.2f };

        public CompLaserGate Gate => GetComp<CompLaserGate>();

#if RW14
        public override void DrawAt(Vector3 drawLoc, bool flip = false)
#else
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
#endif
        {
            Vector3 scale = new Vector3(def.graphicData.drawSize.x, 1f, def.graphicData.drawSize.y);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawLoc, Quaternion.identity, scale), LaserArt.Mat("LaserGatePost", "Base"), 0);
            CompLaserGate gate = Gate;
            if (gate == null) return;
            foreach (LaserGateLink link in gate.OwnedLinks)
            {
                if (link.Lit) DrawCurtain(link, gate.mode);
            }
        }

        /// <summary>
        /// Four thin strands of laser light with a soft red haze between them, from this post to the other.
        /// Cells whose curtain is open (a colonist passing) are left dark.
        /// </summary>
        private void DrawCurtain(LaserGateLink link, LaserGateMode mode)
        {
            Vector3 from = Position.ToVector3Shifted(), to = link.Other.parent.Position.ToVector3Shifted();
            Vector3 dir = (to - from).normalized;
            Vector3 side = new Vector3(dir.z, 0f, -dir.x);
            float altitude = AltitudeLayer.Projectile.AltitudeFor();
            float time = Time.realtimeSinceStartup;
            Quaternion rot = Quaternion.LookRotation(dir);
            bool burn = mode == LaserGateMode.Burn;

            // Lit stretches: runs of cells with no open curtain, extended to the posts at the ends.
            var runs = new List<Vector2>();
            float start = 0.3f, total = (to - from).magnitude - 0.3f;
            for (int i = 0; i < link.Cells.Count; i++)
            {
                bool open = link.Cells[i].GetEdifice(Map) is Building_LaserCurtain c && c.Open;
                if (!open) continue;
                float cellStart = i + 0.5f, cellEnd = i + 1.5f;
                if (cellStart > start) runs.Add(new Vector2(start, cellStart));
                start = cellEnd;
            }
            if (total > start) runs.Add(new Vector2(start, total));

            foreach (Vector2 run in runs)
            {
                float len = run.y - run.x;
                Vector3 mid = from + dir * ((run.x + run.y) * 0.5f);
                mid.y = altitude;
                // haze
                Block.SetColor(ColorId, new Color(CurtainColor.r, CurtainColor.g, CurtainColor.b, burn ? 0.18f : 0.32f));
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(mid, rot, new Vector3(0.62f, 1f, len)), Glow, 0, null, 0, Block);
                // strands, each flickering on its own rhythm
                for (int s = 0; s < Strands.Length; s++)
                {
                    float pulse = 0.7f + 0.3f * Mathf.Sin(time * (burn ? 19f : 7f) + s * 1.9f + thingIDNumber);
                    Vector3 p = mid + side * Strands[s] + new Vector3(0f, 0.001f * (s + 1), 0f);
                    Block.SetColor(ColorId, new Color(CurtainColor.r, CurtainColor.g, CurtainColor.b, 0.55f * pulse));
                    Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(p, rot, new Vector3(0.12f, 1f, len)), Glow, 0, null, 0, Block);
                    Block.SetColor(ColorId, new Color(CoreColor.r, CoreColor.g, CoreColor.b, pulse));
                    Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(p + new Vector3(0f, 0.0005f, 0f), rot, new Vector3(0.035f, 1f, len)), Core, 0, null, 0, Block);
                }
            }
        }
    }

    /// <summary>
    /// One cell of a laser fence in Barrier mode: a door that only colonists and allies can open, so hostiles have to
    /// break through. It can't be damaged itself; whatever hits it lands on the nearer of its two posts.
    /// It is spawned and removed by the posts (see <see cref="CompLaserGate"/>), never built by hand.
    /// </summary>
    public class Building_LaserCurtain : Building_Door
    {
        public Thing ownerPost;

        public override bool PawnCanOpen(Pawn p)
        {
            if (p.HostileTo(Faction.OfPlayer)) return false;
            return base.PawnCanOpen(p);
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = true;
            Thing post = NearestPost(dinfo.Instigator);
            if (post != null && !post.Destroyed) post.TakeDamage(dinfo);
        }

        private Thing NearestPost(Thing from)
        {
            if (ownerPost == null || ownerPost.Destroyed) return null;
            CompLaserGate gate = ownerPost.TryGetComp<CompLaserGate>();
            Thing best = ownerPost;
            if (gate != null && from != null)
            {
                foreach (LaserGateLink l in gate.OwnedLinks)
                {
                    if (!l.Cells.Contains(Position)) continue;
                    Thing other = l.Other.parent;
                    if ((other.Position - from.Position).LengthHorizontalSquared < (best.Position - from.Position).LengthHorizontalSquared) best = other;
                }
            }
            return best;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ownerPost, "jlgOwnerPost");
        }

        // The curtain is drawn by its owning post, as one continuous stretch of fence.
#if RW14
        public override void DrawAt(Vector3 drawLoc, bool flip = false) { }
#else
        protected override void DrawAt(Vector3 drawLoc, bool flip = false) { }
#endif
    }

    /// <summary>While placing a post, outlines the gaps it would fence off to posts already in line.</summary>
    public class PlaceWorker_LaserGate : PlaceWorker
    {
        private static readonly IntVec3[] Dirs = { IntVec3.North, IntVec3.East, IntVec3.South, IntVec3.West };

        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            int span = def.GetCompProperties<CompProperties_LaserGate>()?.maxSpan ?? 5;
            var cells = new List<IntVec3>();
            foreach (IntVec3 d in Dirs)
            {
                var run = new List<IntVec3>();
                for (int i = 1; i <= span + 1; i++)
                {
                    IntVec3 c = center + d * i;
                    if (!c.InBounds(map)) break;
                    Thing e = c.GetEdifice(map);
                    ThingDef there = e?.def ?? (c.GetFirstThing(map, def.blueprintDef) != null ? def : null);
                    if (there == def)
                    {
                        if (run.Count > 0) cells.AddRange(run);
                        break;
                    }
                    if (e != null && !(e is Building_LaserCurtain)) break;
                    run.Add(c);
                }
            }
            if (cells.Count > 0) GenDraw.DrawFieldEdges(cells, new Color(1f, 0.3f, 0.25f));
        }
    }
}
