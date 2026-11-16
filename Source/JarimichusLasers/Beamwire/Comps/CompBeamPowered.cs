using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace Beamwire
{
    public class CompProperties_BeamPowered : CompProperties
    {
        public CompProperties_BeamPowered()
        {
            compClass = typeof(CompBeamPowered);
        }
    }

    /// <summary>
    /// <b>Beam-powered devices API.</b> Lets a building from <i>another mod</i> take power from Beamwire beams,
    /// with no compile-time reference between the mods.
    /// <para>
    /// Add this comp and a <see cref="CompProperties_BeamNode"/> (kind Receiver, rotatable false) to the building,
    /// for example from a patch loaded only when Jarimichus Lasers is active. Then give one of the building's own comps
    /// these public members; they are found by name:
    /// </para>
    /// <list type="bullet">
    /// <item><c>float BeamWantedWatts { get; }</c>: watts it wants from beams right now (0 = nothing to do).</item>
    /// <item><c>void SetBeamWatts(float watts)</c>: called after every solve with the watts beams deliver.</item>
    /// <item><c>IEnumerable&lt;IntVec3&gt; BeamGuardLine { get; }</c> (optional): cells of a dangerous line the
    /// device fires along, so safety shutters on it guard the stretch beyond them.</item>
    /// </list>
    /// <para>
    /// Going the other way, Beamwire nodes expose <c>bool BlocksLasers</c> (true while a safety shutter is closed),
    /// and beam windows carry <c>Beamwire.BeamTransparentExtension</c>, so other mods' lasers can respect both.
    /// </para>
    /// </summary>
    public class CompBeamPowered : ThingComp
    {
        private sealed class Binding
        {
            public PropertyInfo Wanted;
            public MethodInfo Set;
            public PropertyInfo Guard;
        }

        private static readonly Dictionary<Type, Binding> Bindings = new Dictionary<Type, Binding>();

        private ThingComp host;
        private Binding binding;
        private bool lastWanted;

        public bool Bound => host != null;

        public float WantedWatts
        {
            get
            {
                if (host == null) return 0f;
                try { return (float)binding.Wanted.GetValue(host, null); }
                catch (Exception) { return 0f; }
            }
        }

        public void SetBeamWatts(float watts)
        {
            if (host == null) return;
            try { binding.Set.Invoke(host, new object[] { watts }); }
            catch (Exception) { /* the other mod failed; ignore so the network keeps running */ }
        }

        public IEnumerable<IntVec3> GuardLine
        {
            get
            {
                if (host == null || binding.Guard == null) return Enumerable.Empty<IntVec3>();
                try { return binding.Guard.GetValue(host, null) as IEnumerable<IntVec3> ?? Enumerable.Empty<IntVec3>(); }
                catch (Exception) { return Enumerable.Empty<IntVec3>(); }
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            foreach (ThingComp comp in parent.AllComps)
            {
                if (comp == this) continue;
                Binding b = BindingFor(comp.GetType());
                if (b != null)
                {
                    host = comp;
                    binding = b;
                    break;
                }
            }
            if (host == null)
            {
                Log.WarningOnce($"[Beamwire] {parent.def.defName} has CompBeamPowered but no comp with BeamWantedWatts and SetBeamWatts(float).",
                    parent.def.shortHash ^ 0x5BEA);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            // When the device starts or stops wanting power, re-plan so auto-aim routes beams to it (or away).
            if (host != null && parent.IsHashIntervalTick(30))
            {
                bool wanted = WantedWatts > 0f;
                if (wanted != lastWanted)
                {
                    lastWanted = wanted;
                    parent.Map?.GetComponent<MapComponent_BeamNetwork>()?.MarkPlanDirty();
                }
            }
        }

        private static Binding BindingFor(Type t)
        {
            if (Bindings.TryGetValue(t, out Binding cached)) return cached;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            PropertyInfo wanted = t.GetProperty("BeamWantedWatts", flags);
            MethodInfo set = t.GetMethod("SetBeamWatts", flags, null, new[] { typeof(float) }, null);
            Binding b = null;
            if (wanted != null && wanted.PropertyType == typeof(float) && set != null)
            {
                PropertyInfo guard = t.GetProperty("BeamGuardLine", flags);
                if (guard != null && !typeof(IEnumerable<IntVec3>).IsAssignableFrom(guard.PropertyType)) guard = null;
                b = new Binding { Wanted = wanted, Set = set, Guard = guard };
            }
            Bindings[t] = b;
            return b;
        }
    }
}
