using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace JarimichusLasers
{
    public enum LaserGateMode
    {
        /// <summary>The fence blocks the way like a door that opens only for colonists and allies.</summary>
        Barrier,

        /// <summary>The fence can be walked through, but burns hostiles. It switches off while a non-hostile passes.</summary>
        Burn,
    }

    public class CompProperties_LaserGate : CompProperties
    {
        /// <summary>The widest gap two posts can span, in empty tiles.</summary>
        public int maxSpan = 5;

        /// <summary>Burn damage per burn, in Burn mode.</summary>
        public float burnDamage = 12f;

        /// <summary>Ticks between burns, in Burn mode.</summary>
        public int burnInterval = 60;

        public CompProperties_LaserGate()
        {
            compClass = typeof(CompLaserGate);
        }
    }

    /// <summary>A stretch of fence between two posts: the post that owns it, the one it reaches, and the cells between.</summary>
    public class LaserGateLink
    {
        public CompLaserGate Other;
        public readonly List<IntVec3> Cells = new List<IntVec3>();
        public bool Active;
        public bool Lit;
    }

    /// <summary>
    /// A laser fence post. Posts link with the nearest other post in line north, east, south and west, across up to
    /// <see cref="CompProperties_LaserGate.maxSpan"/> empty tiles, and fill the gap with a laser curtain while both
    /// have power. Each post owns its links to the north and east, so every stretch has exactly one owner.
    /// Barrier mode fills the gap with <see cref="Building_LaserCurtain"/>s, door-like blocks that only colonists and
    /// allies can pass; attacks on them land on the posts. Burn mode leaves the gap open and burns hostiles in it.
    /// </summary>
    public class CompLaserGate : ThingComp
    {
        private static readonly IntVec3[] OwnedDirs = { IntVec3.North, IntVec3.East };
        private static readonly IntVec3[] AllDirs = { IntVec3.North, IntVec3.East, IntVec3.South, IntVec3.West };

        public LaserGateMode mode = LaserGateMode.Barrier;
        private readonly LaserGateLink[] owned = new LaserGateLink[2];
        private CompPowerTrader power;
        private CompBreakdownable breakdown;

        public CompProperties_LaserGate Props => (CompProperties_LaserGate)props;

        /// <summary>The stretches of fence this post owns (north and east).</summary>
        public IEnumerable<LaserGateLink> OwnedLinks
        {
            get
            {
                foreach (LaserGateLink l in owned)
                {
                    if (l != null) yield return l;
                }
            }
        }

        /// <summary>Powered, switched on and not broken down.</summary>
        public bool Working => parent.Spawned && (power == null || power.PowerOn) && (breakdown == null || !breakdown.BrokenDown);

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            power = parent.GetComp<CompPowerTrader>();
            breakdown = parent.GetComp<CompBreakdownable>();
            Refresh();
        }

        public override void PostDeSpawn(Map map
#if RW16
            , DestroyMode mode = DestroyMode.Vanish
#endif
            )
        {
            for (int i = 0; i < owned.Length; i++)
            {
                if (owned[i] != null) ClearCurtains(owned[i], map);
                owned[i] = null;
            }
            base.PostDeSpawn(map
#if RW16
                , mode
#endif
                );
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref mode, "jlgMode", LaserGateMode.Barrier);
        }

        public override void ReceiveCompSignal(string signal)
        {
            base.ReceiveCompSignal(signal);
            if (parent.Spawned) Refresh();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned) return;
            if (parent.IsHashIntervalTick(15)) Refresh();
            // Burn mode reacts every tick: the fence switches off the moment a non-hostile steps into it.
            foreach (LaserGateLink l in OwnedLinks)
            {
                if (!l.Active || mode != LaserGateMode.Burn) continue;
                l.Lit = !NonHostileIn(l);
                if (l.Lit && parent.IsHashIntervalTick(Props.burnInterval)) BurnHostiles(l);
            }
        }

        /// <summary>Finds this post's links, then keeps each owned stretch's curtains in step with its state.</summary>
        public void Refresh()
        {
            Map map = parent.Map;
            for (int d = 0; d < OwnedDirs.Length; d++)
            {
                LaserGateLink found = FindLink(OwnedDirs[d], true);
                LaserGateLink old = owned[d];
                if (old != null && (found == null || found.Other != old.Other || found.Cells.Count != old.Cells.Count))
                {
                    ClearCurtains(old, map);
                }
                owned[d] = found;
                if (found == null) continue;

                found.Active = Working && found.Other.Working;
                // The owner's mode rules the stretch; both posts normally share it (the mode gizmo sets the whole fence).
                if (found.Active && mode == LaserGateMode.Barrier)
                {
                    found.Active = EnsureCurtains(found, map);
                    found.Lit = found.Active;
                }
                else
                {
                    ClearCurtains(found, map);
                    found.Lit = found.Active && !NonHostileIn(found);
                }
            }
        }

        /// <summary>
        /// The nearest post in line in direction <paramref name="dir"/>, with at least one empty tile between, up to
        /// maxSpan tiles away. Cells with this post's own curtains count as empty.
        /// </summary>
        public LaserGateLink FindLink(IntVec3 dir, bool claimCurtains)
        {
            Map map = parent.Map;
            var link = new LaserGateLink();
            for (int i = 1; i <= Props.maxSpan + 1; i++)
            {
                IntVec3 c = parent.Position + dir * i;
                if (!c.InBounds(map)) return null;
                Building e = c.GetEdifice(map);
                if (e is Building_LaserCurtain curtain)
                {
                    if (curtain.ownerPost != null && curtain.ownerPost != parent && !curtain.ownerPost.Destroyed) return null;
                    if (claimCurtains) curtain.ownerPost = parent;
                    link.Cells.Add(c);
                    continue;
                }
                if (e != null)
                {
                    CompLaserGate other = e.TryGetComp<CompLaserGate>();
                    if (other == null || link.Cells.Count == 0) return null;
                    link.Other = other;
                    return link;
                }
                link.Cells.Add(c);
            }
            return null;
        }

        private bool EnsureCurtains(LaserGateLink link, Map map)
        {
            ThingDef def = LasersDefOf.JLG_LaserCurtain;
            if (def == null) return false;
            foreach (IntVec3 c in link.Cells)
            {
                Building e = c.GetEdifice(map);
                if (e is Building_LaserCurtain) continue;
                if (e != null) return false; // something was built in the gap
                var curtain = (Building_LaserCurtain)ThingMaker.MakeThing(def);
                curtain.ownerPost = parent;
                curtain.SetFaction(parent.Faction);
                GenSpawn.Spawn(curtain, c, map, WipeMode.VanishOrMoveAside);
            }
            return true;
        }

        private void ClearCurtains(LaserGateLink link, Map map)
        {
            if (map == null) return;
            foreach (IntVec3 c in link.Cells)
            {
                if (c.InBounds(map) && c.GetEdifice(map) is Building_LaserCurtain curtain && (curtain.ownerPost == parent || curtain.ownerPost == null))
                {
                    curtain.Destroy(DestroyMode.Vanish);
                }
            }
        }

        private bool NonHostileIn(LaserGateLink link)
        {
            foreach (Pawn p in parent.Map.mapPawns.AllPawnsSpawned)
            {
                if (p.Dead || p.HostileTo(Faction.OfPlayer)) continue;
                if (link.Cells.Contains(p.Position)) return true;
                if (p.pather != null && p.pather.Moving && link.Cells.Contains(p.pather.nextCell)) return true;
            }
            return false;
        }

        private void BurnHostiles(LaserGateLink link)
        {
            DamageDef def = LasersDefOf.JLG_GateBurn;
            if (def == null) return;
            var victims = new List<Pawn>();
            foreach (IntVec3 c in link.Cells)
            {
                foreach (Thing t in c.GetThingList(parent.Map))
                {
                    if (t is Pawn p && !p.Dead && p.HostileTo(Faction.OfPlayer)) victims.Add(p);
                }
            }
            foreach (Pawn p in victims)
            {
                p.TakeDamage(new DamageInfo(def, Props.burnDamage, def.defaultArmorPenetration, -1f, parent));
                FleckMaker.ThrowMicroSparks(p.DrawPos, parent.Map);
            }
        }

        /// <summary>Every post connected to this one through fence links, this one included.</summary>
        public List<CompLaserGate> Fence()
        {
            var seen = new HashSet<CompLaserGate> { this };
            var queue = new Queue<CompLaserGate>();
            queue.Enqueue(this);
            while (queue.Count > 0)
            {
                CompLaserGate g = queue.Dequeue();
                if (!g.parent.Spawned) continue;
                foreach (IntVec3 d in AllDirs)
                {
                    LaserGateLink l = g.FindLink(d, false);
                    if (l != null && seen.Add(l.Other)) queue.Enqueue(l.Other);
                }
            }
            return new List<CompLaserGate>(seen);
        }

        private int LinkCount()
        {
            int n = 0;
            foreach (IntVec3 d in AllDirs)
            {
                if (FindLink(d, false) != null) n++;
            }
            return n;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;
            if (parent.Faction != Faction.OfPlayer) yield break;
            bool barrier = mode == LaserGateMode.Barrier;
            yield return new Command_Action
            {
                defaultLabel = (barrier ? "JLG_ModeBarrier" : "JLG_ModeBurn").Translate(),
                defaultDesc = "JLG_ModeDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get(barrier ? "UI/Commands/HoldFire" : "UI/Commands/Attack", false) ?? BaseContent.BadTex,
                action = () =>
                {
                    LaserGateMode next = barrier ? LaserGateMode.Burn : LaserGateMode.Barrier;
                    foreach (CompLaserGate g in Fence())
                    {
                        g.mode = next;
                    }
                    foreach (CompLaserGate g in Fence())
                    {
                        g.Refresh();
                    }
                },
            };
        }

        public override string CompInspectStringExtra()
        {
            var lines = new List<string> { (mode == LaserGateMode.Barrier ? "JLG_InspectBarrier" : "JLG_InspectBurn").Translate() };
            int links = LinkCount();
            lines.Add(links == 0 ? "JLG_InspectNoLinks".Translate(Props.maxSpan) : "JLG_InspectLinks".Translate(links));
            if (!Working) lines.Add("JLG_InspectOff".Translate());
            return string.Join("\n", lines);
        }
    }
}
