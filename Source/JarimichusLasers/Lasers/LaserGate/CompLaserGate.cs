using System.Collections.Generic;
using Beamwire;
using Beamwire.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace JarimichusLasers
{
    public enum LaserGateMode
    {
        /// <summary>The fence is a wall of light nobody can pass or damage, until it's opened with the gate button.</summary>
        Barrier,

        /// <summary>The fence can be walked through, but burns hostiles. It switches off while a non-hostile passes.</summary>
        Burn,
    }

    public class CompProperties_LaserGate : CompProperties
    {
        /// <summary>The most open tiles (curtain) in one straight stretch of fence, between posts or mirrors. Beam windows don't count.</summary>
        public int maxSpan = 5;

        /// <summary>How many Beamwire mirrors one stretch of fence may bend off.</summary>
        public int maxMirrors = 4;

        /// <summary>Burn damage per burn in Burn mode at the lowest power setting; it grows with the square root of power.</summary>
        public float burnDamage = 12f;

        /// <summary>The power settings a post can run at, in watts. The first is the default.</summary>
        public List<float> powerLevels = new List<float> { 150f, 300f, 600f, 1200f };

        /// <summary>Share of the burn left after each mirror the fence bends off (as Beamwire's mirror loss).</summary>
        public float mirrorKeep = 0.95f;

        /// <summary>Ticks between burns, in Burn mode.</summary>
        public int burnInterval = 60;

        public CompProperties_LaserGate()
        {
            compClass = typeof(CompLaserGate);
        }
    }

    /// <summary>One straight leg of a fence: from a post or mirror centre to the next, and the cells it crosses.</summary>
    public class LaserGateLeg
    {
        public Vector3 From;
        public Vector3 To;
        public readonly List<IntVec3> Cells = new List<IntVec3>();
    }

    /// <summary>
    /// A stretch of fence between two posts. It may bend off Beamwire mirrors and pass through beam windows; the
    /// curtain itself fills only the open cells (<see cref="Cells"/>).
    /// </summary>
    public class LaserGateLink
    {
        public CompLaserGate Other;
        public readonly List<IntVec3> Cells = new List<IntVec3>();
        public readonly List<LaserGateLeg> Legs = new List<LaserGateLeg>();
        public int Mirrors;
        public bool Active;
        public bool Lit;
    }

    /// <summary>
    /// A laser fence post. Posts link with the nearest other post north, east, south and west, across up to
    /// <see cref="CompProperties_LaserGate.maxSpan"/> tiles per straight leg, and fill the gap with a laser curtain
    /// while both have power. The path may bend off Beamwire mirrors and pass through beam windows, so posts can sit
    /// inside the base. Of the two posts a stretch joins, the one with the lower ID owns it.
    /// Barrier mode fills the gap with <see cref="Building_LaserCurtain"/>s, a wall of light nobody can pass or damage
    /// until the gate is opened. Burn mode leaves the gap open and burns hostiles in it.
    /// </summary>
    public class CompLaserGate : ThingComp
    {
        private static readonly IntVec3[] AllDirs = { IntVec3.North, IntVec3.East, IntVec3.South, IntVec3.West };
        private static readonly List<Dir8> TmpOut = new List<Dir8>();

        public LaserGateMode mode = LaserGateMode.Barrier;
        public int powerLevel;

        /// <summary>Barrier mode only: the gate is held open (the fence is off) so anyone can pass.</summary>
        public bool open;
        private readonly LaserGateLink[] owned = new LaserGateLink[4];
        private CompPowerTrader power;
        private CompBreakdownable breakdown;

        public CompProperties_LaserGate Props => (CompProperties_LaserGate)props;

        /// <summary>Watts this post draws at its power setting.</summary>
        public float Watts => Props.powerLevels[Mathf.Clamp(powerLevel, 0, Props.powerLevels.Count - 1)];

        /// <summary>Burn per burn at this post's power setting, before mirror losses: grows with the square root of power.</summary>
        public float BurnDamage => Props.burnDamage * Mathf.Sqrt(Watts / Props.powerLevels[0]);

        /// <summary>The stretches of fence this post owns.</summary>
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
            Scribe_Values.Look(ref powerLevel, "jlgPowerLevel", 0);
            Scribe_Values.Look(ref open, "jlgOpen", false);
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
            if (power != null && power.PowerOn) power.PowerOutput = -Watts;
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
            for (int d = 0; d < AllDirs.Length; d++)
            {
                LaserGateLink found = FindLink(AllDirs[d]);
                if (found != null && found.Other.parent.thingIDNumber < parent.thingIDNumber) found = null; // the other post owns it
                if (found != null) ClaimCurtains(found, map);
                LaserGateLink old = owned[d];
                if (old != null && (found == null || found.Other != old.Other || !SameCells(found, old)))
                {
                    ClearCurtains(old, map);
                }
                owned[d] = found;
                if (found == null) continue;

                found.Active = Working && found.Other.Working;
                // The owner's mode rules the stretch; both posts normally share it (the mode gizmo sets the whole fence).
                if (found.Active && mode == LaserGateMode.Barrier)
                {
                    // Held open: no curtain, no light. Otherwise the curtain closes once nobody is standing in the line.
                    bool closed = !open && EnsureCurtains(found, map);
                    if (!closed) ClearCurtains(found, map);
                    found.Lit = closed;
                }
                else
                {
                    ClearCurtains(found, map);
                    found.Lit = found.Active && !NonHostileIn(found);
                }
            }
        }

        private static bool SameCells(LaserGateLink a, LaserGateLink b)
        {
            if (a.Cells.Count != b.Cells.Count) return false;
            for (int i = 0; i < a.Cells.Count; i++)
            {
                if (a.Cells[i] != b.Cells[i]) return false;
            }
            return true;
        }

        /// <summary>
        /// Follows a fence from this post in direction <paramref name="dir"/> to the next post: across open cells (the
        /// curtain), through beam windows, and around Beamwire mirrors that turn it 90°. Each straight leg may have up to
        /// maxSpan open tiles; beam windows don't count. Null if it runs into anything else, off the map, or too far. Cells holding laser
        /// curtains count as open.
        /// </summary>
        public LaserGateLink FindLink(IntVec3 dir)
        {
            Map map = parent.Map;
            var link = new LaserGateLink();
            var leg = new LaserGateLeg { From = parent.Position.ToVector3Shifted() };
            IntVec3 c = parent.Position;
            int legLength = 0;
            for (int guard = 0; guard < 64; guard++)
            {
                c += dir;
                if (!c.InBounds(map)) return null;
                Building e = c.GetEdifice(map);
                if (e == null || e is Building_LaserCurtain)
                {
                    // Only open tiles (the curtain itself) count toward a leg's span; beam windows don't.
                    if (++legLength > Props.maxSpan) return null;
                    link.Cells.Add(c);
                    leg.Cells.Add(c);
                    continue;
                }
                CompLaserGate other = e.TryGetComp<CompLaserGate>();
                if (other != null)
                {
                    if (other == this || link.Cells.Count == 0) return null;
                    leg.To = c.ToVector3Shifted();
                    link.Legs.Add(leg);
                    link.Other = other;
                    return link;
                }
                if (e.def.HasModExtension<BeamTransparentExtension>())
                {
                    leg.Cells.Add(c); // through a beam window: no curtain here, the window is the wall
                    continue;
                }
                CompBeamNode node = e.TryGetComp<CompBeamNode>();
                if (node != null && node.Kind == NodeKind.Mirror && link.Mirrors < Props.maxMirrors)
                {
                    IntVec3 turned = Reflect(node.Facing, dir);
                    if (!turned.IsValid) return null; // the back of the mirror, or straight back the way it came
                    leg.To = c.ToVector3Shifted();
                    link.Legs.Add(leg);
                    leg = new LaserGateLeg { From = leg.To };
                    link.Mirrors++;
                    dir = turned;
                    legLength = 0;
                    continue;
                }
                return null;
            }
            return null;
        }

        /// <summary>Where a Beamwire mirror facing <paramref name="facing"/> sends a fence travelling in <paramref name="dir"/>, using Beamwire's own optics.</summary>
        private static IntVec3 Reflect(Dir8 facing, IntVec3 dir)
        {
            Dir8 travel = dir == IntVec3.North ? Dir8.North : dir == IntVec3.East ? Dir8.East : dir == IntVec3.South ? Dir8.South : Dir8.West;
            BeamOptics.Outputs(new BeamNode(0, NodeKind.Mirror, default(GridPos), facing), travel, TmpOut);
            if (TmpOut.Count != 1) return IntVec3.Invalid;
            switch (TmpOut[0])
            {
                case Dir8.North: return travel == Dir8.South ? IntVec3.Invalid : IntVec3.North;
                case Dir8.East: return travel == Dir8.West ? IntVec3.Invalid : IntVec3.East;
                case Dir8.South: return travel == Dir8.North ? IntVec3.Invalid : IntVec3.South;
                case Dir8.West: return travel == Dir8.East ? IntVec3.Invalid : IntVec3.West;
                default: return IntVec3.Invalid;
            }
        }

        /// <summary>Takes ownership of the curtains already standing in a stretch this post owns (e.g. after loading).</summary>
        private void ClaimCurtains(LaserGateLink link, Map map)
        {
            foreach (IntVec3 c in link.Cells)
            {
                if (c.GetEdifice(map) is Building_LaserCurtain curtain && (curtain.ownerPost == null || curtain.ownerPost.Destroyed || !curtain.ownerPost.Spawned))
                {
                    curtain.ownerPost = parent;
                }
            }
        }

        /// <summary>
        /// Fills the stretch with curtains. False, with nothing spawned, while anyone stands in the line (the
        /// curtain can't close on them) or something else has been built in it.
        /// </summary>
        private bool EnsureCurtains(LaserGateLink link, Map map)
        {
            ThingDef def = LasersDefOf.JLG_LaserCurtain;
            if (def == null) return false;
            foreach (IntVec3 c in link.Cells)
            {
                Building e = c.GetEdifice(map);
                if (e is Building_LaserCurtain) continue;
                if (e != null || c.GetFirstPawn(map) != null) return false;
            }
            foreach (IntVec3 c in link.Cells)
            {
                if (c.GetEdifice(map) is Building_LaserCurtain) continue;
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
                    curtain.Remove();
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
                float amount = BurnDamage * Mathf.Pow(Props.mirrorKeep, link.Mirrors);
                p.TakeDamage(new DamageInfo(def, amount, def.defaultArmorPenetration * Mathf.Sqrt(Watts / Props.powerLevels[0]), -1f, parent));
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
                    LaserGateLink l = g.FindLink(d);
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
                if (FindLink(d) != null) n++;
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
            if (barrier)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "JLG_GateOpen".Translate(),
                    defaultDesc = "JLG_GateOpenDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/HoldOpen", false) ?? BaseContent.BadTex,
                    isActive = () => open,
                    toggleAction = () =>
                    {
                        bool next = !open;
                        foreach (CompLaserGate g in Fence())
                        {
                            g.open = next;
                        }
                        foreach (CompLaserGate g in Fence())
                        {
                            g.Refresh();
                        }
                    },
                };
            }
            yield return new Command_Action
            {
                defaultLabel = "JLG_Power".Translate(Watts.ToString("0")),
                defaultDesc = "JLG_PowerDesc".Translate(PowerTable()),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower", false) ?? BaseContent.BadTex,
                action = () =>
                {
                    int next = (powerLevel + 1) % Props.powerLevels.Count;
                    foreach (CompLaserGate g in Fence())
                    {
                        g.powerLevel = next;
                    }
                },
            };
        }

        private string PowerTable()
        {
            var parts = new List<string>();
            foreach (float w in Props.powerLevels)
            {
                parts.Add("JLG_PowerRow".Translate(w.ToString("0"), (Props.burnDamage * Mathf.Sqrt(w / Props.powerLevels[0])).ToString("0")));
            }
            return string.Join(", ", parts);
        }


        public override string CompInspectStringExtra()
        {
            var lines = new List<string> { (mode == LaserGateMode.Burn ? "JLG_InspectBurn" : open ? "JLG_InspectOpen" : "JLG_InspectBarrier").Translate() };
            int links = LinkCount();
            lines.Add(links == 0 ? "JLG_InspectNoLinks".Translate(Props.maxSpan) : "JLG_InspectLinks".Translate(links));
            lines.Add("JLG_InspectPower".Translate(Watts.ToString("0"), BurnDamage.ToString("0")));
            if (!Working) lines.Add("JLG_InspectOff".Translate());
            return string.Join("\n", lines);
        }
    }
}
