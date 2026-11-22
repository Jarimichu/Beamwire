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
        /// Four thin strands of laser light with a soft red haze between them, along every leg of the stretch, from this
        /// post round any mirrors to the other. Hidden inside beam windows' wall blocks, and dark across any cell whose
        /// curtain is open (a colonist passing).
        /// </summary>
        private void DrawCurtain(LaserGateLink link, LaserGateMode mode)
        {
            float altitude = AltitudeLayer.Projectile.AltitudeFor();
            float time = Time.realtimeSinceStartup;
            bool burn = mode == LaserGateMode.Burn;
            for (int li = 0; li < link.Legs.Count; li++)
            {
                LaserGateLeg leg = link.Legs[li];
                Vector3 delta = leg.To - leg.From;
                delta.y = 0f;
                float length = delta.magnitude;
                if (length < 0.05f) continue;
                Vector3 dir = delta / length;
                float start = li == 0 ? 0.3f : 0f, end = li == link.Legs.Count - 1 ? length - 0.3f : length;

                // Open curtains (a colonist passing) leave their cell dark.
                var dark = new List<Vector2>();
                foreach (IntVec3 c in leg.Cells)
                {
                    if (c.GetEdifice(Map) is Building_LaserCurtain curtain && curtain.Open)
                    {
                        float t = Vector3.Dot(c.ToVector3Shifted() - leg.From, dir);
                        dark.Add(new Vector2(t - 0.5f, t + 0.5f));
                    }
                }
                List<Vector2> visible = Beamwire.WindowOcclusion.VisibleSpans(Map, leg.From, leg.To, leg.Cells);
                var runs = new List<Vector2>();
                foreach (Vector2 v in visible)
                {
                    float a0 = Mathf.Max(v.x, start), a1 = Mathf.Min(v.y, end);
                    if (a1 <= a0) continue;
                    float cur = a0;
                    foreach (Vector2 d in dark)
                    {
                        if (d.y <= cur || d.x >= a1) continue;
                        if (d.x > cur) runs.Add(new Vector2(cur, d.x));
                        cur = Mathf.Max(cur, d.y);
                    }
                    if (cur < a1) runs.Add(new Vector2(cur, a1));
                }

                Quaternion rot = Quaternion.LookRotation(dir);
                Vector3 side = new Vector3(dir.z, 0f, -dir.x);
                foreach (Vector2 run in runs)
                {
                    float len = run.y - run.x;
                    if (len < 0.01f) continue;
                    Vector3 mid = leg.From + dir * ((run.x + run.y) * 0.5f);
                    mid.y = altitude;
                    Block.SetColor(ColorId, new Color(CurtainColor.r, CurtainColor.g, CurtainColor.b, burn ? 0.18f : 0.32f));
                    Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(mid, rot, new Vector3(0.62f, 1f, len)), Glow, 0, null, 0, Block);
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
    }

    /// <summary>
    /// One cell of a laser fence in Barrier mode: a door that only colonists and allies can open, so hostiles have to
    /// break through. It can't be damaged itself; whatever hits it lands on the nearest post or mirror of its stretch.
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

        /// <summary>
        /// The post or Beamwire mirror of this curtain's stretch nearest to the attacker. With the posts tucked away
        /// behind mirrors, the mirrors in the wall line take the hits.
        /// </summary>
        private Thing NearestPost(Thing from)
        {
            if (ownerPost == null || ownerPost.Destroyed) return null;
            CompLaserGate gate = ownerPost.TryGetComp<CompLaserGate>();
            Thing best = ownerPost;
            if (gate == null || from == null) return best;
            foreach (LaserGateLink l in gate.OwnedLinks)
            {
                if (!l.Cells.Contains(Position)) continue;
                var candidates = new List<Thing> { ownerPost, l.Other.parent };
                for (int i = 1; i < l.Legs.Count; i++)
                {
                    Building mirror = l.Legs[i].From.ToIntVec3().GetEdifice(Map);
                    if (mirror != null) candidates.Add(mirror);
                }
                foreach (Thing t in candidates)
                {
                    if ((t.Position - from.Position).LengthHorizontalSquared < (best.Position - from.Position).LengthHorizontalSquared) best = t;
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
